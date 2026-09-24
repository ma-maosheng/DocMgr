using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    public partial class ArchiveRegisterViewModel
    {
        private void MarkCommitted()
        {
            HasCommittedChanges = true;
        }

        #region Record CRUD & Logic

        public void ResetRecord()
        {
            var newRecord = _archiveRegisterService.CreateDraftRecord(_userContextService.CurrentUser);
            CurrentRecord = newRecord;
            SelectedProject = null;
            SelectedArchivePurpose = GetDefaultArchivePurpose();
            HasProofMaterial = false;

            MediaEntries.Clear();
            Attachments.Clear();
        }

        private async Task LoadRecordDetailAsync(int id)
        {
            try
            {
                IsLoadingRecord = true;
                var record = await _archiveRegisterService.GetByIdAsync(id);
                if (record != null)
                {
                    CurrentRecord = record;
                    return;
                }

                _dialogService.ShowMessage($"未找到编号为 {id} 的登记记录。", "提示");
            }
            catch (Exception ex) { _dialogService.ShowError("加载记录详情失败: " + ex.Message); }
            finally { IsLoadingRecord = false; }
        }

        private async void OnCurrentRecordChanged()
        {
            if (CurrentRecord == null) return;
            UpdateUIState();
            SyncCollectionsFromRecord();
            ApplyArchivePurposeSelection();
            if (CurrentRecord.ProjectId.HasValue)
            {
                var proj = Projects.FirstOrDefault(p => p.Id == CurrentRecord.ProjectId.Value);
                if (proj != null) SelectedProject = proj;
            }
            await LoadAttachments();
            await RefreshAttachmentRequirementsAsync();
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(IsExternalSource));
            NotifyNetworkOutboundTransferUiState();
            if (_userContextService.CurrentUser != null)
            {
                bool shouldAutoFillDefaultApproval =
                    _workspaceMode == ArchiveRegisterWorkspaceMode.Approval
                    && CurrentRecord.IsSubmitted
                    && _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser);

                if (shouldAutoFillDefaultApproval)
                {
                    try
                    {
                        await _archiveRegisterService.ApplyDefaultApprovalInfoAsync(CurrentRecord, _userContextService.CurrentUser);
                    }
                    catch
                    {
                        // 自动回填失败不阻断页面打开，用户仍可手工录入审批信息。
                    }
                    OnPropertyChanged(nameof(CurrentRecord));
                }
            }

            await ApplyApprovalChainEnableFlagsAsync();
            OnPropertyChanged(nameof(IsArchivePurposeOtherSelected));
        }

        private async Task ResetRecordWithAutoFormNoAsync()
        {
            try
            {
                CurrentRecord = await _archiveRegisterService.CreateDraftRecordWithNextFormNoAsync(_userContextService.CurrentUser);
                SelectedProject = null;
                SelectedArchivePurpose = GetDefaultArchivePurpose();
                HasProofMaterial = false;
                MediaEntries.Clear();
                Attachments.Clear();
                OnPropertyChanged(nameof(CurrentRecord));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("生成编号失败: " + ex.Message);
            }
            OnPropertyChanged(nameof(WindowTitle));
        }

        private async Task<bool> SaveDraftAsync()
        {
            try
            {
                if (CurrentRecord != null)
                {
                    CurrentRecord.ArchivePurpose = SelectedArchivePurpose ?? string.Empty;
                    ApplyProofMaterialNoteToRecord();
                }

                if (!ValidateProofMaterialInput())
                {
                    return false;
                }

                var result = await _archiveRegisterService.SaveDraftFlowAsync(
                    CurrentRecord,
                    BuildMediaEntries(),
                    _userContextService.CurrentUser);
                _dialogService.ShowMessage(result.Message);
                if (!result.Success) return false;
                MarkCommitted();
                RequestClose?.Invoke(true);
                return true;
            }
            catch (Exception ex) { _dialogService.ShowError("保存草稿失败: " + ex.Message); return false; }
        }

        private async Task<bool> SaveApprovalAsync()
        {
            if (!_dialogService.ShowConfirm(
                    "请确认已根据线下审批结果核实并登记各资料子项密级。\n\n【确定】已核实，继续审批通过\n【取消】尚未核实，返回修改",
                    "核实资料密级"))
            {
                return false;
            }

            try
            {
                var result = await _archiveRegisterService.SaveApprovalFlowAsync(
                    CurrentRecord,
                    BuildMediaEntries(),
                    Attachments.ToList(),
                    _userContextService.CurrentUser);

                _dialogService.ShowMessage(result.Message);
                if (!result.Success) return false;

                MarkCommitted();
                CurrentRecord?.MarkAsApprovedReceived();
                UpdateUIState();
                OnPropertyChanged(nameof(CurrentRecord));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(CanEditItemConfidentialLevel));
                return true;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("保存失败: " + ex.Message);
                return false;
            }
        }

        private async Task ConfirmPhysicalHandoverAsync()
        {
            if (CurrentRecord == null)
            {
                _dialogService.ShowMessage("当前记录为空，无法确认实物交接。");
                return;
            }

            if (!CanConfirmPhysicalHandover)
            {
                _dialogService.ShowMessage("请先审批通过后再确认实物交接。");
                return;
            }

            try
            {
                var result = await _archiveRegisterService.ConfirmPhysicalHandoverFlowAsync(CurrentRecord, _userContextService.CurrentUser);
                _dialogService.ShowMessage(result.Message);
                if (!result.Success)
                {
                    return;
                }

                MarkCommitted();
                CurrentRecord.MarkAsSignedUploaded();
                UpdateUIState();
                OnPropertyChanged(nameof(CurrentRecord));
                OnPropertyChanged(nameof(WindowTitle));
                await RefreshAttachmentRequirementsAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("确认实物交接失败: " + ex.Message);
            }
        }

        private async Task CompleteAsync()
        {
            if (CurrentRecord == null)
            {
                _dialogService.ShowMessage("当前记录为空，无法确认办结。");
                return;
            }

            if (!CanCompleteApproval)
            {
                _dialogService.ShowMessage("请先上传签批交接单和资料照片后再确认办结。");
                return;
            }

            try
            {
                var result = await _archiveRegisterService.CompleteRegisterFlowAsync(CurrentRecord, Attachments.ToList(), _userContextService.CurrentUser);
                _dialogService.ShowMessage(result.Message);
                if (!result.Success)
                {
                    return;
                }

                MarkCommitted();
                CurrentRecord.MarkAsCompleted();
                UpdateUIState();
                OnPropertyChanged(nameof(CurrentRecord));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("确认办结失败: " + ex.Message);
            }
        }

        private ApprovalWorkflowButtonSupport.Phase ResolveApprovalPhase()
        {
            if (CurrentRecord == null)
            {
                return ApprovalWorkflowButtonSupport.Phase.PendingApproval;
            }

            if (CurrentRecord.IsArchived)
            {
                return ApprovalWorkflowButtonSupport.Phase.Completed;
            }

            if (CurrentRecord.IsSignedUploaded)
            {
                return AttachmentsMeetMandatoryRequirements
                    ? ApprovalWorkflowButtonSupport.Phase.PendingComplete
                    : ApprovalWorkflowButtonSupport.Phase.PendingSignedUpload;
            }

            if (CurrentRecord.IsApprovedReceived)
            {
                return ApprovalWorkflowButtonSupport.Phase.PendingPhysicalHandover;
            }

            if (CurrentRecord.IsSubmitted)
            {
                return ApprovalWorkflowButtonSupport.Phase.PendingApproval;
            }

            return ApprovalWorkflowButtonSupport.Phase.PendingApproval;
        }

        private ApprovalWorkflowButtonSupport.ButtonState ResolveApprovalButtonState()
        {
            if (_workspaceMode != ArchiveRegisterWorkspaceMode.Approval || CurrentRecord == null)
            {
                return new ApprovalWorkflowButtonSupport.ButtonState(false, false, false, false, false);
            }

            bool isOperatorAllowed = _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser);
            bool canExecuteApprovePass = CanApproveProd && CurrentRecord.IsSubmitted;

            return ApprovalWorkflowButtonSupport.Resolve(
                ResolveApprovalPhase(),
                isOperatorAllowed,
                canExecuteApprovePass);
        }

        private async Task SubmitApplication()
        {
            if (CurrentRecord == null)
            {
                _dialogService.ShowMessage("当前记录为空，无法提交。");
                return;
            }

            CurrentRecord.ArchivePurpose = SelectedArchivePurpose ?? string.Empty;
            ApplyProofMaterialNoteToRecord();
            if (!ValidateProofMaterialInput())
            {
                return;
            }

            var mediaEntries = BuildMediaEntries();
            if (!_dialogService.ShowConfirm("确认提交申请吗？\n\n提交后所有审批信息将被重置，状态流转为“已提交”。")) return;

            try
            {
                var result = await _archiveRegisterService.SubmitApplicationFlowAsync(
                    CurrentRecord,
                    mediaEntries,
                    IsExternalSource,
                    _userContextService.CurrentUser);
                _dialogService.ShowMessage(result.Message);
                if (!result.Success) return;

                MarkCommitted();
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("提交保存失败: " + ex.Message);
            }
        }

        private bool ValidateProofMaterialInput()
        {
            if (HasProofMaterial && string.IsNullOrWhiteSpace(ProofMaterialName))
            {
                _dialogService.ShowError("已选择附有证明材料，请填写证明材料名称。");
                return false;
            }

            return true;
        }

        private void UpdateUIState()
        {
            var state = _archiveRegisterService.ResolveUiPermissionState(_userContextService.CurrentUser, CurrentRecord);

            CanEditForm = state.CanEditForm;
            CanEditItemConfidentialLevel = state.CanEditItemConfidentialLevel;
            CanApproveProd = CanApproveRnd = CanApproveDeputy = state.CanApprove;
            CanUpload = state.CanUpload;

            OnPropertyChanged(nameof(CanApprovePass));
            OnPropertyChanged(nameof(CanConfirmPhysicalHandover));
            OnPropertyChanged(nameof(CanUploadSignedAttachment));
            OnPropertyChanged(nameof(CanCompleteApproval));
            OnPropertyChanged(nameof(CanPrintHandoverSheet));
            OnPropertyChanged(nameof(AttachmentsMeetMandatoryRequirements));
            OnPropertyChanged(nameof(AttachmentRequirementHint));
            OnPropertyChanged(nameof(ApproveHintText));
            OnPropertyChanged(nameof(ConfirmHandoverHintText));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            OnPropertyChanged(nameof(PrintHintText));
            OnPropertyChanged(nameof(RequiresProofMaterialScanUpload));
            OnPropertyChanged(nameof(ProofMaterialAttachmentHint));
            OnPropertyChanged(nameof(CanUploadProofMaterialAttachment));
            NotifyNetworkOutboundTransferUiState();
            NotifyShellAliasPropertiesChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        private void NotifyNetworkOutboundTransferUiState()
        {
            OnPropertyChanged(nameof(IsNetworkOutboundTransferRegister));
            OnPropertyChanged(nameof(IsElectronicMediaTypeEditable));
            OnPropertyChanged(nameof(IsElectronicDispositionEditable));
            OnPropertyChanged(nameof(NetworkOutboundTransferHint));
        }

        private async Task RefreshAttachmentRequirementsAsync()
        {
            if (CurrentRecord == null || _workspaceMode != ArchiveRegisterWorkspaceMode.Approval)
            {
                AttachmentsMeetMandatoryRequirements = true;
                AttachmentRequirementHint = string.Empty;
                return;
            }

            var validation = await _archiveRegisterService.ValidateMandatoryAttachmentsAsync(
                CurrentRecord,
                Attachments.ToList());
            AttachmentsMeetMandatoryRequirements = validation.IsValid;
            AttachmentRequirementHint = validation.IsValid
                ? (RequiresProofMaterialScanUpload
                    ? "必备附件已齐全：签批交接单、资料照片、证明材料。"
                    : "必备附件已齐全：签批交接单、资料照片。")
                : "必备附件未齐全：\n" + validation.ErrorMessage;

            OnPropertyChanged(nameof(CanConfirmPhysicalHandover));
            OnPropertyChanged(nameof(CanUploadSignedAttachment));
            OnPropertyChanged(nameof(CanCompleteApproval));
            OnPropertyChanged(nameof(ConfirmHandoverHintText));
            OnPropertyChanged(nameof(UploadHintText));
            OnPropertyChanged(nameof(CompleteHintText));
            OnPropertyChanged(nameof(RequiresProofMaterialScanUpload));
            OnPropertyChanged(nameof(ProofMaterialAttachmentHint));
            OnPropertyChanged(nameof(CanUploadProofMaterialAttachment));
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task FillDefaultApprovalInfoAsync()
        {
            if (CurrentRecord == null || _userContextService.CurrentUser == null)
            {
                _dialogService.ShowMessage("当前记录或用户信息为空，无法填写默认审批信息。");
                return;
            }

            try
            {
                await _archiveRegisterService.ApplyDefaultApprovalInfoAsync(CurrentRecord, _userContextService.CurrentUser);
                OnPropertyChanged(nameof(CurrentRecord));
                await ApplyApprovalChainEnableFlagsAsync();
                CommandManager.InvalidateRequerySuggested();
                _dialogService.ShowMessage("默认审批信息已填写。");
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowMessage(ex.Message);
            }
        }

        /// <summary>按签批链规则刷新签字卡 Enable*/Show*。</summary>
        private async Task ApplyApprovalChainEnableFlagsAsync()
        {
            if (_workspaceMode != ArchiveRegisterWorkspaceMode.Approval || CurrentRecord == null)
            {
                return;
            }

            try
            {
                var chain = await _archiveRegisterService.ResolveApprovalChainAsync(CurrentRecord);
                EnableDeptHead = chain.DeptHead.IsEnabled;
                EnableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled;
                EnableProductionHead = chain.ProductionHead.IsEnabled;
                EnableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled;
                EnableProductionVicePresident = chain.ProductionVicePresident.IsEnabled;
                NotifyShellAliasPropertiesChanged();
            }
            catch
            {
                // 解析失败时保持当前 Enable*，不阻断打开。
            }
        }

        /// <summary>是否启用部门审核签字栏。</summary>
        public bool EnableDeptHead
        {
            get => _enableDeptHead;
            private set => SetProperty(ref _enableDeptHead, value);
        }

        /// <summary>是否启用资料室签字栏。</summary>
        public bool EnableArchiveRoomHead
        {
            get => _enableArchiveRoomHead;
            private set => SetProperty(ref _enableArchiveRoomHead, value);
        }

        /// <summary>是否启用生产科签字栏。</summary>
        public bool EnableProductionHead
        {
            get => _enableProductionHead;
            private set => SetProperty(ref _enableProductionHead, value);
        }

        /// <summary>是否启用分管资料院长签字栏。</summary>
        public bool EnableArchiveDeputyPresident
        {
            get => _enableArchiveDeputyPresident;
            private set => SetProperty(ref _enableArchiveDeputyPresident, value);
        }

        /// <summary>是否启用分管生产院长签字栏。</summary>
        public bool EnableProductionVicePresident
        {
            get => _enableProductionVicePresident;
            private set => SetProperty(ref _enableProductionVicePresident, value);
        }

        // Helpers
        private void LoadDepartments()
        {
            try { var depts = _userService.GetAllDepartments(); Departments.Clear(); foreach (var d in depts) Departments.Add(d); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }
        private void LoadProjectYears()
        {
            int currentYear = DateTime.Now.Year;
            ProjectYears.Clear(); ProjectYears.Add("全部");
            for (int i = 0; i < 10; i++) ProjectYears.Add((currentYear - i).ToString());
            SelectedProjectYear = currentYear.ToString();
        }
        private void LoadProjects()
        {
            try
            {
                if (string.IsNullOrEmpty(SelectedProjectYear)) return;
                string searchYear = (SelectedProjectYear == "全部") ? "" : SelectedProjectYear;
                var list = _projectService.SearchProjects(searchYear, "");
                Projects.Clear(); foreach (var p in list) Projects.Add(p);
            }
            catch (Exception ex) { _dialogService.ShowError("加载项目异常: " + ex.Message); }
        }
        private async Task RefreshUserBorrowedHardDiskCodesAsync()
        {
            if (_borrowedHardDiskCodesRefreshInProgress)
            {
                return;
            }

            _borrowedHardDiskCodesRefreshInProgress = true;
            try
            {
                // ComboBox ItemsSource 被 Clear 时，WPF 常会把 SelectedItem/TwoWay 绑定的编号写成空并写回介质行，须在清空前快照并在刷新后恢复。
                var preservedSnapshots = new Dictionary<MediaEntryViewModel, string>();
                foreach (var m in MediaEntries.Where(IsDataElectronic))
                {
                    if (!m.IsRetainedHardDiskScenario || !m.IsBorrowedHardDisk)
                    {
                        continue;
                    }

                    string c = m.BorrowedHardDiskCode?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(c))
                    {
                        preservedSnapshots[m] = c;
                    }
                }

                UserBorrowedHardDiskCodes.Clear();
                var user = _userContextService.CurrentUser;
                if (user == null)
                {
                    RestoreBorrowedHardDiskUiAfterCodesReload(preservedSnapshots);
                    return;
                }

                try
                {
                    var codes = await _hardDiskMediaService.GetCurrentUserBorrowedHardDiskCodesAsync(user);
                    foreach (var code in codes)
                    {
                        if (!string.IsNullOrWhiteSpace(code))
                            UserBorrowedHardDiskCodes.Add(code);
                    }

                    RestoreBorrowedHardDiskUiAfterCodesReload(preservedSnapshots);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError("加载借出硬盘列表失败: " + ex.Message);
                    RestoreBorrowedHardDiskUiAfterCodesReload(preservedSnapshots);
                }
            }
            finally
            {
                _borrowedHardDiskCodesRefreshInProgress = false;
            }
        }

        /// <summary>
        /// 在借出硬盘编号列表重新加载后，恢复因清空 ItemsSource 而丢失的各行选中编号。
        /// </summary>
        private void RestoreBorrowedHardDiskUiAfterCodesReload(Dictionary<MediaEntryViewModel, string> preservedSnapshots)
        {
            foreach (var code in preservedSnapshots.Values.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!UserBorrowedHardDiskCodes.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase)))
                    UserBorrowedHardDiskCodes.Insert(0, code);
            }

            foreach (var kv in preservedSnapshots)
            {
                MediaEntryViewModel vm = kv.Key;
                string preservedCode = kv.Value;
                string current = vm.BorrowedHardDiskCode?.Trim() ?? string.Empty;
                if (!string.Equals(current, preservedCode, StringComparison.OrdinalIgnoreCase))
                    vm.BorrowedHardDiskCode = preservedCode;
            }

            EnsureUserBorrowedHardDiskListIncludesSelected();
        }

        private async Task EnsureApprovalInfoForPrintAsync()
        {
            if (CurrentRecord == null || _userContextService.CurrentUser == null) return;
            var changed = await _archiveRegisterService.TryAutoFillApprovalForArchiveAdminAsync(CurrentRecord, _userContextService.CurrentUser);
            if (changed)
            {
                OnPropertyChanged(nameof(CurrentRecord));
            }
        }

        #endregion
    }
}