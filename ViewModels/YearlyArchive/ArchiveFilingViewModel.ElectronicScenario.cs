using System;
using System.Linq;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 电子介质立档场景决策与提交模式同步逻辑。
    /// </summary>
    public partial class ArchiveFilingViewModel
    {
        private void RaiseElectronicStepFourPresentationChanged()
        {
            OnPropertyChanged(nameof(ElectronicStepFourBlankHardDiskSectionVisibility));
            OnPropertyChanged(nameof(ExternalHardDiskRegistrationVisibility));
            OnPropertyChanged(nameof(ExternalHardDiskFormattedBlankLocationVisibility));
            OnPropertyChanged(nameof(ElectronicStepFourBorrowedDirectCompleteVisibility));
            OnPropertyChanged(nameof(ElectronicStepFourGenericIdleVisibility));
            OnPropertyChanged(nameof(ElectronicStepFourAppendModeNoticeVisibility));
            OnPropertyChanged(nameof(ElectronicStepFourNewBoxSectionVisibility));
            OnPropertyChanged(nameof(IsElectronicMediumSelectionButtonVisible));
            OnPropertyChanged(nameof(ElectronicStoragePathLabel));
            OnPropertyChanged(nameof(ElectronicMediaCountLabel));
        }

        /// <summary>
        /// 与第一步介质编号一致：有有效编号为资料室借出硬盘，否则为外来。在刷新场景决策前写回字段，避免经 <see cref="SelectedRetainedHardDiskSource"/> setter 递归进入本方法。
        /// </summary>
        private void ApplyInferredRetainedHardDiskSourceForRefresh()
        {
            if (!IsElectronicHardDiskRetainedScenario)
            {
                return;
            }

            string inferred = ArchiveFilingBusinessRules.ResolveRetainedHardDiskSourceFromStepOneMediumCode(SelectedElectronicMediaForm?.MediumCode);
            if (string.Equals(_selectedRetainedHardDiskSource, inferred, StringComparison.Ordinal))
            {
                return;
            }

            _selectedRetainedHardDiskSource = inferred;
            OnPropertyChanged(nameof(SelectedRetainedHardDiskSource));
        }

        private void RefreshElectronicScenario()
        {
            if (_suppressElectronicScenarioRefresh)
            {
                return;
            }

            if (_isRefreshingElectronicScenario)
            {
                return;
            }

            try
            {
                _isRefreshingElectronicScenario = true;
                _suppressElectronicSubmissionModeChange = true;

                ApplyInferredRetainedHardDiskSourceForRefresh();

                _electronicDecision = _filingService.ResolveElectronicArchiveUiDecision(new ElectronicArchiveScenarioInput
                {
                    ProjectName = TargetProject,
                    Year = TargetYear,
                    SelectedMediaTypes = SelectedElectronicMediaTypes,
                    Disposition = ElectronicDisposition,
                    SelectedMediaEntryIds = GetSelectedMediaEntryIdsForElectronicSubmit().ToList(),
                    ExistingElectronicUnits = ExistingElectronicUnits.Select(item => item.Unit).ToList(),
                    SelectedArchiveAction = IsNewBoxMode ? ElectronicArchiveArchiveAction.New : ElectronicArchiveArchiveAction.Append,
                    SelectedExistingElectronicUnitId = SelectedExistingElectronicUnit?.Id,
                    StepOneMediumCode = SelectedElectronicMediaForm?.MediumCode,
                    SelectedRetainedHardDiskSource = SelectedRetainedHardDiskSource,
                    SelectedSubmissionMode = _selectedElectronicSubmissionMode
                });

                ReplaceItems(AvailableElectronicSubmissionModes, _electronicDecision.AvailableModes);

                if (_electronicDecision.SelectedMode != _selectedElectronicSubmissionMode)
                {
                    _selectedElectronicSubmissionMode = _electronicDecision.SelectedMode;
                    OnPropertyChanged(nameof(SelectedElectronicSubmissionMode));
                }

                string selectedModeLabel = AvailableElectronicSubmissionModes.FirstOrDefault(item => item.Mode == _selectedElectronicSubmissionMode)?.DisplayName ?? string.Empty;
                if (!string.Equals(_selectedHardDiskCopyTargetMode, selectedModeLabel, StringComparison.Ordinal))
                {
                    _selectedHardDiskCopyTargetMode = selectedModeLabel;
                    OnPropertyChanged(nameof(SelectedHardDiskCopyTargetMode));
                }

                OnPropertyChanged(nameof(CanUseElectronicAppendMode));
                OnPropertyChanged(nameof(ElectronicStepFourLayout));
                OnPropertyChanged(nameof(ElectronicStepSevenTitle));
                OnPropertyChanged(nameof(ElectronicStepEightTitle));
                OnPropertyChanged(nameof(ElectronicLocationActionHintText));
                ApplyHardDiskCopyTargetSelection();
                RaiseElectronicStepFourPresentationChanged();
                _ = RebuildElectronicFilingDetailRowsAsync();
            }
            finally
            {
                _suppressElectronicSubmissionModeChange = false;
                _isRefreshingElectronicScenario = false;
            }
        }

        private void ApplySelectedSubmissionMode()
        {
            if (SelectedElectronicSubmissionMode == null)
            {
                return;
            }

            ElectronicStorageCarrierType = _electronicDecision.StorageCarrierType;
            EnsureElectronicBagDefaults();
        }
    }
}
