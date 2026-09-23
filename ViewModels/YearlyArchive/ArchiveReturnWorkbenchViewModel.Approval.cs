using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Shared;
using DocMgr.Services.SystemSettings;
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
        private string _archiveRoomHeadName = string.Empty;
        private DateTime? _archiveRoomHeadDateValue;
        private string _productionHeadName = string.Empty;
        private DateTime? _productionHeadDate;
        private string _archiveDeputyPresidentName = string.Empty;
        private DateTime? _archiveDeputyPresidentDate;
        private string _vicePresidentName = string.Empty;
        private DateTime? _vicePresidentDate;
        private string _approvalOpinion = string.Empty;
        private string _handoverApplicant = string.Empty;
        private string _handoverAdmin = string.Empty;
        private DateTime? _handoverDate;
        private SystemAttachment? _selectedSignedAttachment;

        public string DeptHead
        {
            get => _reviewerName;
            set => SetProperty(ref _reviewerName, value ?? string.Empty);
        }

        public DateTime? DeptHeadDate
        {
            get => _reviewerDate;
            set => SetProperty(ref _reviewerDate, value);
        }

        public string ArchiveRoomHead
        {
            get => _archiveRoomHeadName;
            set => SetProperty(ref _archiveRoomHeadName, value ?? string.Empty);
        }

        public DateTime? ArchiveRoomHeadDate
        {
            get => _archiveRoomHeadDateValue;
            set => SetProperty(ref _archiveRoomHeadDateValue, value);
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

        /// <summary>分管资料院长签字（灭失时按签批链启用）。</summary>
        public string ArchiveDeputyPresidentName
        {
            get => _archiveDeputyPresidentName;
            set => SetProperty(ref _archiveDeputyPresidentName, value ?? string.Empty);
        }

        public DateTime? ArchiveDeputyPresidentDate
        {
            get => _archiveDeputyPresidentDate;
            set => SetProperty(ref _archiveDeputyPresidentDate, value);
        }

        /// <summary>分管生产院长签字（灭失时按签批链启用）。</summary>
        public string ProductionVicePresidentName
        {
            get => _vicePresidentName;
            set => SetProperty(ref _vicePresidentName, value ?? string.Empty);
        }

        public DateTime? ProductionVicePresidentDate
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

            _approvalChain = await _returnService.ResolveApprovalChainAsync(record);
            ApprovalChainApplySupport.ApplyToReturn(record, _approvalChain, outbound, today);

            DeptHead = record.DeptHead ?? string.Empty;
            DeptHeadDate = record.DeptHeadDate ?? (_approvalChain.DeptHead.IsEnabled ? today : null);
            ArchiveRoomHead = record.ArchiveRoomHead ?? string.Empty;
            ArchiveRoomHeadDate = record.ArchiveRoomHeadDate ?? record.ApprovedAt;
            ProductionHeadName = record.ProductionHead ?? string.Empty;
            ProductionHeadDate = record.ProductionHeadDate;
            ArchiveDeputyPresidentName = record.ArchiveDeputyPresident ?? string.Empty;
            ArchiveDeputyPresidentDate = record.ArchiveDeputyPresidentDate;
            ProductionVicePresidentName = record.ProductionVicePresident ?? string.Empty;
            ProductionVicePresidentDate = record.ProductionVicePresidentDate;

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
        /// 完好/灭失切换时按签批链同步审批区（不覆盖用户已录入姓名）。
        /// </summary>
        private async Task SyncApprovalSignersForLossStateAsync(YearlyArchiveReturnRecord record)
        {
            YearlyArchiveOutboundRecord? outbound = null;
            if (record.SourceOutboundRecordId > 0)
            {
                outbound = await _outboundService.GetRecordAsync(record.SourceOutboundRecordId);
            }

            DateTime today = DateTime.Today;
            _approvalChain = await _returnService.ResolveApprovalChainAsync(record);

            record.DeptHead = DeptHead;
            record.ArchiveRoomHead = ArchiveRoomHead;
            record.ProductionHead = ProductionHeadName;
            record.ArchiveDeputyPresident = ArchiveDeputyPresidentName;
            record.ProductionVicePresident = ProductionVicePresidentName;
            ApprovalChainApplySupport.ApplyToReturn(record, _approvalChain, outbound, today);

            DeptHead = record.DeptHead;
            DeptHeadDate ??= record.DeptHeadDate ?? (_approvalChain.DeptHead.IsEnabled ? today : null);
            ArchiveRoomHead = _approvalChain.ArchiveRoomHead.IsEnabled ? record.ArchiveRoomHead : string.Empty;
            ArchiveRoomHeadDate = _approvalChain.ArchiveRoomHead.IsEnabled
                ? (ArchiveRoomHeadDate ?? record.ArchiveRoomHeadDate ?? record.ApprovedAt ?? today)
                : null;
            ProductionHeadName = _approvalChain.ProductionHead.IsEnabled ? record.ProductionHead : string.Empty;
            ProductionHeadDate = _approvalChain.ProductionHead.IsEnabled
                ? (ProductionHeadDate ?? record.ProductionHeadDate ?? today)
                : null;
            ArchiveDeputyPresidentName = _approvalChain.ArchiveDeputyPresident.IsEnabled
                ? record.ArchiveDeputyPresident
                : string.Empty;
            ArchiveDeputyPresidentDate = _approvalChain.ArchiveDeputyPresident.IsEnabled
                ? (ArchiveDeputyPresidentDate ?? record.ArchiveDeputyPresidentDate ?? today)
                : null;
            ProductionVicePresidentName = _approvalChain.ProductionVicePresident.IsEnabled
                ? record.ProductionVicePresident
                : string.Empty;
            ProductionVicePresidentDate = _approvalChain.ProductionVicePresident.IsEnabled
                ? (ProductionVicePresidentDate ?? record.ProductionVicePresidentDate ?? today)
                : null;

            OnPropertyChanged(nameof(ShowIntactApprovalSigner));
            OnPropertyChanged(nameof(ShowLossApprovalSigners));
            OnPropertyChanged(nameof(ReviewerFieldLabel));
            OnPropertyChanged(nameof(ApproverFieldLabel));
        }

        private ArchiveReturnApprovalInput BuildApprovalInput() =>
            new()
            {
                DeptHead = DeptHead,
                DeptHeadDate = DeptHeadDate,
                ArchiveRoomHead = ArchiveRoomHead,
                ArchiveRoomHeadDate = ArchiveRoomHeadDate,
                ProductionHeadName = ProductionHeadName,
                ProductionHeadDate = ProductionHeadDate,
                ArchiveDeputyPresidentName = ArchiveDeputyPresidentName,
                ArchiveDeputyPresidentDate = ArchiveDeputyPresidentDate,
                ProductionVicePresidentName = ProductionVicePresidentName,
                ProductionVicePresidentDate = ProductionVicePresidentDate,
                ApprovalOpinion = ApprovalOpinion
            };

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
                YearlyArchiveReturnRecord.Completed => CanSupplementOtherAttachments
                    ? "本单已办结入库；资料管理员可增补「其他附件」。"
                    : "本单已办结入库。",
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

        private async Task SupplementOtherAttachmentAsync()
        {
            if (EditingRecord is not { Id: > 0 } record || !CanSupplementOtherAttachments)
            {
                _dialogService.ShowMessage("办结后仅资料管理员可增补「其他附件」。");
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
                Title = "选择其他附件"
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
                var result = await _returnService.UploadOtherAttachmentFlowAsync(record.Id, attachment, user);
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

        private async Task CaptureOtherAttachmentAsync()
        {
            if (EditingRecord is not { Id: > 0 } record || !CanSupplementOtherAttachments)
            {
                _dialogService.ShowMessage("办结后仅资料管理员可增补「其他附件」。");
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
                    ArchiveReturnDomainValues.AttachmentKindOther,
                    "资归还");
                var attachment = new SystemAttachment
                {
                    FileName = fileName,
                    Extension = ".jpg",
                    FileSize = captured.JpegContent.LongLength,
                    FileContent = captured.JpegContent
                };
                var result = await _returnService.UploadOtherAttachmentFlowAsync(record.Id, attachment, user);
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
