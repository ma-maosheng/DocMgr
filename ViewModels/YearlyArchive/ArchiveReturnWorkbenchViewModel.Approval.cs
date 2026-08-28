using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Shared;
using DocMgr.Services.YearlyArchive;
using DocMgr.Views.Shared;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料归还工作台：审批/交接表单字段与签批交接单附件操作。
    /// </summary>
    public sealed partial class ArchiveReturnWorkbenchViewModel
    {
        private string _reviewerName = string.Empty;
        private DateTime? _reviewerDate;
        private string _approverName = string.Empty;
        private DateTime? _approverDate;
        private string _productionHeadName = string.Empty;
        private DateTime? _productionHeadDate;
        private string _vicePresidentName = string.Empty;
        private DateTime? _vicePresidentDate;
        private string _approvalOpinion = string.Empty;
        private string _handoverApplicant = string.Empty;
        private string _handoverAdmin = string.Empty;
        private DateTime? _handoverDate;
        private SystemAttachment? _selectedSignedAttachment;

        public string ReviewerName
        {
            get => _reviewerName;
            set => SetProperty(ref _reviewerName, value ?? string.Empty);
        }

        public DateTime? ReviewerDate
        {
            get => _reviewerDate;
            set => SetProperty(ref _reviewerDate, value);
        }

        public string ApproverName
        {
            get => _approverName;
            set => SetProperty(ref _approverName, value ?? string.Empty);
        }

        public DateTime? ApproverDate
        {
            get => _approverDate;
            set => SetProperty(ref _approverDate, value);
        }

        /// <summary>生产科负责人（灭失时必填，默认可取自借出审批）。</summary>
        public string ProductionHeadName
        {
            get => _productionHeadName;
            set => SetProperty(ref _productionHeadName, value ?? string.Empty);
        }

        public DateTime? ProductionHeadDate
        {
            get => _productionHeadDate;
            set => SetProperty(ref _productionHeadDate, value);
        }

        /// <summary>生产副院长（灭失时必填，默认可取自借出审批）。</summary>
        public string VicePresidentName
        {
            get => _vicePresidentName;
            set => SetProperty(ref _vicePresidentName, value ?? string.Empty);
        }

        public DateTime? VicePresidentDate
        {
            get => _vicePresidentDate;
            set => SetProperty(ref _vicePresidentDate, value);
        }

        public string ApprovalOpinion
        {
            get => _approvalOpinion;
            set => SetProperty(ref _approvalOpinion, value ?? string.Empty);
        }

        public string HandoverApplicant
        {
            get => _handoverApplicant;
            set => SetProperty(ref _handoverApplicant, value ?? string.Empty);
        }

        public string HandoverAdmin
        {
            get => _handoverAdmin;
            set => SetProperty(ref _handoverAdmin, value ?? string.Empty);
        }

        public DateTime? HandoverDate
        {
            get => _handoverDate;
            set => SetProperty(ref _handoverDate, value);
        }

        public SystemAttachment? SelectedSignedAttachment
        {
            get => _selectedSignedAttachment;
            set => SetProperty(ref _selectedSignedAttachment, value);
        }

        private async Task LoadApprovalFormFieldsAsync(YearlyArchiveReturnRecord record)
        {
            var user = _userContextService.CurrentUser;
            string currentName = user == null
                ? string.Empty
                : (string.IsNullOrWhiteSpace(user.RealName) ? user.LoginName : user.RealName.Trim());
            DateTime today = DateTime.Today;

            YearlyArchiveOutboundRecord? outbound = null;
            if (record.SourceOutboundRecordId > 0)
            {
                outbound = await _outboundService.GetRecordAsync(record.SourceOutboundRecordId);
            }

            var users = _userService.GetAllUsers();

            // 审核审批人取借出时签字人，禁止默认成交接双方（归还人/资料管理员）。
            // 完好：仅部门负责人；灭失：四级审核审批人均可编辑。
            ReviewerName = string.IsNullOrWhiteSpace(record.ReviewerName)
                ? ResolveDefaultReviewerName(record, outbound, users)
                : record.ReviewerName;
            ReviewerDate = record.ReviewerDate ?? today;

            if (ArchiveReturnDomainValues.HasAbnormalReturnItems(record.Items))
            {
                ApproverName = string.IsNullOrWhiteSpace(record.ApprovedBy)
                    ? ResolveDefaultApproverName(outbound, users)
                    : record.ApprovedBy;
                ApproverDate = record.ApprovedAt ?? outbound?.ArchiveRoomHeadDate ?? today;
                ProductionHeadName = string.IsNullOrWhiteSpace(record.ProductionHead)
                    ? FirstNonEmpty(outbound?.ProductionHead)
                    : record.ProductionHead;
                ProductionHeadDate = record.ProductionHeadDate ?? outbound?.ProductionHeadDate ?? today;
                VicePresidentName = string.IsNullOrWhiteSpace(record.VicePresident)
                    ? FirstNonEmpty(outbound?.VicePresident)
                    : record.VicePresident;
                VicePresidentDate = record.VicePresidentDate ?? outbound?.VicePresidentDate ?? today;
            }
            else
            {
                // 完好归还不需要资料室负责人及其他审批人。
                ApproverName = string.Empty;
                ApproverDate = null;
                ProductionHeadName = string.Empty;
                ProductionHeadDate = null;
                VicePresidentName = string.Empty;
                VicePresidentDate = null;
            }

            ApprovalOpinion = string.IsNullOrWhiteSpace(record.ApprovalOpinion) ? "同意" : record.ApprovalOpinion;
            HandoverApplicant = string.IsNullOrWhiteSpace(record.HandoverApplicant)
                ? (record.BorrowerName ?? record.RegisteredByName ?? string.Empty)
                : record.HandoverApplicant;
            HandoverAdmin = string.IsNullOrWhiteSpace(record.HandoverAdmin) ? currentName : record.HandoverAdmin;
            HandoverDate = record.HandoverDate ?? today;

            OnPropertyChanged(nameof(ApproveHintText));
            OnPropertyChanged(nameof(ReviewerFieldLabel));
            OnPropertyChanged(nameof(ApproverFieldLabel));
            OnPropertyChanged(nameof(ShowIntactApprovalSigner));
            OnPropertyChanged(nameof(ShowLossApprovalSigners));
        }

        /// <summary>
        /// 完好/灭失切换时同步审批区：完好清空资料室负责人；灭失补齐四级签字人（不覆盖已录入）。
        /// 不覆盖用户已改的部门负责人。
        /// </summary>
        private async Task SyncApprovalSignersForLossStateAsync(YearlyArchiveReturnRecord record)
        {
            YearlyArchiveOutboundRecord? outbound = null;
            if (record.SourceOutboundRecordId > 0)
            {
                outbound = await _outboundService.GetRecordAsync(record.SourceOutboundRecordId);
            }

            var users = _userService.GetAllUsers();
            DateTime today = DateTime.Today;

            if (HasAbnormalReturnItems)
            {
                if (string.IsNullOrWhiteSpace(ApproverName))
                {
                    ApproverName = string.IsNullOrWhiteSpace(record.ApprovedBy)
                        ? ResolveDefaultApproverName(outbound, users)
                        : record.ApprovedBy;
                }

                ApproverDate ??= record.ApprovedAt ?? outbound?.ArchiveRoomHeadDate ?? today;

                if (string.IsNullOrWhiteSpace(ProductionHeadName))
                {
                    ProductionHeadName = string.IsNullOrWhiteSpace(record.ProductionHead)
                        ? FirstNonEmpty(outbound?.ProductionHead)
                        : record.ProductionHead;
                }

                ProductionHeadDate ??= record.ProductionHeadDate ?? outbound?.ProductionHeadDate ?? today;

                if (string.IsNullOrWhiteSpace(VicePresidentName))
                {
                    VicePresidentName = string.IsNullOrWhiteSpace(record.VicePresident)
                        ? FirstNonEmpty(outbound?.VicePresident)
                        : record.VicePresident;
                }

                VicePresidentDate ??= record.VicePresidentDate ?? outbound?.VicePresidentDate ?? today;
            }
            else
            {
                ApproverName = string.Empty;
                ApproverDate = null;
                ProductionHeadName = string.Empty;
                ProductionHeadDate = null;
                VicePresidentName = string.Empty;
                VicePresidentDate = null;
            }

            OnPropertyChanged(nameof(ShowIntactApprovalSigner));
            OnPropertyChanged(nameof(ShowLossApprovalSigners));
        }

        /// <summary>
        /// 默认审核人：源出库单部门审核人；否则借出部门「部门负责人」。
        /// </summary>
        private static string ResolveDefaultReviewerName(
            YearlyArchiveReturnRecord record,
            YearlyArchiveOutboundRecord? outbound,
            IReadOnlyList<User> users)
        {
            if (!string.IsNullOrWhiteSpace(outbound?.DeptAuditor))
            {
                return outbound.DeptAuditor.Trim();
            }

            string borrowerDept = FirstNonEmpty(record.BorrowerDept, outbound?.ApplicantDept);
            if (!string.IsNullOrWhiteSpace(borrowerDept))
            {
                string reviewer = users
                    .FirstOrDefault(user =>
                        string.Equals(user.Department, borrowerDept, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(user.RealName)
                        && (user.Role?.Contains("部门负责人", StringComparison.OrdinalIgnoreCase) ?? false))
                    ?.RealName
                    ?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(reviewer))
                {
                    return reviewer;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 默认审批人：源出库单资料室负责人；否则资料室「负责人」。
        /// </summary>
        private static string ResolveDefaultApproverName(
            YearlyArchiveOutboundRecord? outbound,
            IReadOnlyList<User> users)
        {
            if (!string.IsNullOrWhiteSpace(outbound?.ArchiveRoomHead))
            {
                return outbound.ArchiveRoomHead.Trim();
            }

            string approver = users
                .FirstOrDefault(user =>
                    string.Equals(user.Department, "资料室", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(user.RealName)
                    && (user.Role?.Contains("负责人", StringComparison.OrdinalIgnoreCase) ?? false))
                ?.RealName
                ?.Trim() ?? string.Empty;

            return approver;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private ArchiveReturnApprovalInput BuildApprovalInput()
        {
            // 完好归还仅录入部门负责人；灭失时录入借出时全部四级审核审批人。
            bool hasLoss = HasAbnormalReturnItems;
            return new ArchiveReturnApprovalInput
            {
                ReviewerName = ReviewerName,
                ReviewerDate = ReviewerDate,
                ApproverName = hasLoss ? ApproverName : string.Empty,
                ApproverDate = hasLoss ? ApproverDate : null,
                ProductionHeadName = hasLoss ? ProductionHeadName : string.Empty,
                ProductionHeadDate = hasLoss ? ProductionHeadDate : null,
                VicePresidentName = hasLoss ? VicePresidentName : string.Empty,
                VicePresidentDate = hasLoss ? VicePresidentDate : null,
                ApprovalOpinion = ApprovalOpinion
            };
        }

        private ArchiveReturnApprovalInput BuildHandoverInput() => new()
        {
            HandoverApplicant = HandoverApplicant,
            HandoverAdmin = HandoverAdmin,
            HandoverDate = HandoverDate
        };

        private void RefreshWorkflowHint()
        {
            if (!IsEditing || EditingRecord == null)
            {
                WorkflowHintText = string.Empty;
                return;
            }

            if (ShowApplicationActions)
            {
                if (IsEditable)
                {
                    WorkflowHintText = HasAbnormalReturnItems
                        ? "下一步：填写灭失说明（写入签批交接单），打印并完成线下签字后提交（签批交接单扫描件由资料室上传）。"
                        : "下一步：填写归还信息，打印签批交接单并完成线下签字后保存草稿或提交申请（扫描件由资料室上传）。";
                    return;
                }

                WorkflowHintText = CanPrintSignedHandoverOnApplication
                    ? "申请已提交，可继续打印签批交接单供线下签字；扫描件由资料室资料管理员上传。"
                    : "当前状态不允许重新编辑，请等待资料室审批办理。";
                return;
            }

            WorkflowHintText = EditingRecord.Status switch
            {
                YearlyArchiveReturnRecord.Submitted => ApproveHintText,
                YearlyArchiveReturnRecord.Approved => ConfirmHandoverHintText,
                YearlyArchiveReturnRecord.SignedUploaded when !EditingRecord.SignedAttachmentUploaded => UploadHintText,
                YearlyArchiveReturnRecord.SignedUploaded => CompleteHintText,
                YearlyArchiveReturnRecord.Completed => "本单已办结入库。",
                _ => string.Empty
            };
        }

        private async Task UploadSignedAttachmentAsync()
        {
            if (EditingRecord is not { Id: > 0 } record || !CanUploadSignedAttachment)
            {
                _dialogService.ShowMessage("请先确认实物交接，再上传签批交接单。");
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = SystemAttachmentUploadSupport.OpenFileDialogFilter,
                Title = "选择签批交接单扫描件"
            };
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return;
            }

            IsBusy = true;
            try
            {
                var fileInfo = new FileInfo(dialog.FileName);
                var fileContent = await File.ReadAllBytesAsync(dialog.FileName);
                var attachment = new SystemAttachment
                {
                    FileName = fileInfo.Name,
                    Extension = fileInfo.Extension,
                    FileSize = fileInfo.Length,
                    FileContent = fileContent
                };
                var result = await _returnService.UploadSignedHandoverAttachmentFlowAsync(record.Id, attachment, user);
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowMessage(result.Message);
                await ReloadSavedRecordAsync(record.Id);
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"读取附件失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
                await TryReloadListsAfterOperationAsync();
            }
        }

        private async Task CaptureSignedAttachmentAsync()
        {
            if (EditingRecord is not { Id: > 0 } record || !CanUploadSignedAttachment)
            {
                _dialogService.ShowMessage("请先确认实物交接，再上传签批交接单。");
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            DocumentCameraCaptureResult? captured = DocumentCameraAttachmentCaptureSupport.Capture(_dialogService);
            if (captured == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                string fileName = DocumentCameraAttachmentCaptureSupport.BuildFileName(
                    record.ReturnNo,
                    ArchiveReturnDomainValues.AttachmentKindSignedHandover,
                    "资归还");
                var attachment = new SystemAttachment
                {
                    FileName = fileName,
                    Extension = ".jpg",
                    FileSize = captured.JpegContent.LongLength,
                    FileContent = captured.JpegContent
                };
                var result = await _returnService.UploadSignedHandoverAttachmentFlowAsync(record.Id, attachment, user);
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowMessage(result.Message);
                await ReloadSavedRecordAsync(record.Id);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"上传失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
                await TryReloadListsAfterOperationAsync();
            }
        }

        private async Task ViewSignedAttachmentAsync()
        {
            if (SelectedSignedAttachment == null)
            {
                return;
            }

            try
            {
                var result = await _returnService.PrepareAttachmentViewFlowAsync(SelectedSignedAttachment);
                if (!result.Success || result.Attachment?.FileContent == null)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowSystemAttachmentView(result.Attachment);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("打开附件失败：" + ex.Message);
            }
        }

        private async Task DeleteSignedAttachmentAsync()
        {
            if (EditingRecord is not { Id: > 0 } record || SelectedSignedAttachment == null || !CanDeleteSignedAttachment)
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                return;
            }

            if (!_dialogService.ShowConfirm("确认删除所选签批交接单附件？", "删除确认"))
            {
                return;
            }

            IsBusy = true;
            try
            {
                var result = await _returnService.DeleteSignedHandoverAttachmentFlowAsync(
                    record.Id,
                    SelectedSignedAttachment,
                    user);
                if (!result.Success)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowMessage(result.Message);
                await ReloadSavedRecordAsync(record.Id);
            }
            finally
            {
                IsBusy = false;
                await TryReloadListsAfterOperationAsync();
            }
        }

        private async Task PrintHandoverDocumentAsync()
        {
            if (EditingRecord is not { Id: > 0 } record)
            {
                return;
            }

            IsBusy = true;
            try
            {
                bool blankHandoverSignatures = !record.IsCompleted;
                var data = await _returnService.BuildReceiptPrintDataAsync(record.Id, blankHandoverSignatures);
                var document = ArchiveReturnPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document)
                {
                    Owner = Application.Current.MainWindow
                };

                await _returnService.RecordPrintAsync(record.Id);
                previewWindow.ShowDialog();

                var reloaded = await _returnService.GetReturnAsync(record.Id);
                if (reloaded != null)
                {
                    LoadEditing(reloaded);
                }
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("交接单打印生成失败：" + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
