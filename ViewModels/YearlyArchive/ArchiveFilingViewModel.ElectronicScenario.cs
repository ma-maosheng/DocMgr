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
                _outboundArchiveFilingMode = TryResolveOutboundArchiveFilingMode();

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
                    SelectedSubmissionMode = _outboundArchiveFilingMode ?? _selectedElectronicSubmissionMode,
                    PredeterminedSubmissionMode = _outboundArchiveFilingMode
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
                ApplyOutboundArchiveFilingPreset();
                RaiseElectronicStepFourPresentationChanged();
                _pageDb.RunInBackground(RebuildElectronicFilingDetailRowsAsync);
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

        private ElectronicArchiveSubmissionMode? _outboundArchiveFilingMode;

        public bool IsElectronicSubmissionModeLocked => _outboundArchiveFilingMode != null;

        public bool IsElectronicSubmissionModeEditable => !IsElectronicSubmissionModeLocked;

        public bool IsOutboundArchiveBlankDiskCodeReadOnly =>
            _outboundArchiveFilingMode == ElectronicArchiveSubmissionMode.CopyNewHardDisk;

        public string OutboundArchiveFilingModeHint => _outboundArchiveFilingMode switch
        {
            ElectronicArchiveSubmissionMode.CopyNewOpticalDisc => "出网申请已预定新光盘立档，本步只选择光盘档口并赋码。",
            ElectronicArchiveSubmissionMode.CopyNewHardDisk => "出网申请已预定库内空盘，介质编号不可更改，本步选择数据硬盘档口。",
            ElectronicArchiveSubmissionMode.CopyAppendExistingHardDisk => "出网申请已预定并入下方硬盘袋。",
            _ => string.Empty
        };

        private ElectronicArchiveSubmissionMode? TryResolveOutboundArchiveFilingMode()
        {
            List<SelectableElectronicArchiveMediaViewModel> rows = EnumerateSelectedElectronicMediaEntryRows()
                .Where(row => row.IsOutboundArchiveFiling)
                .ToList();
            if (rows.Count == 0)
            {
                return null;
            }

            ElectronicArchiveSubmissionMode? mode = null;
            foreach (SelectableElectronicArchiveMediaViewModel row in rows)
            {
                ElectronicArchiveSubmissionMode rowMode = row.UseInStockBlankHardDisk
                    ? ElectronicArchiveSubmissionMode.CopyNewHardDisk
                    : row.AppendTargetElectronicUnitId is > 0
                        ? ElectronicArchiveSubmissionMode.CopyAppendExistingHardDisk
                        : string.Equals(row.MediaType?.Trim(), ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc, StringComparison.Ordinal)
                            ? ElectronicArchiveSubmissionMode.CopyNewOpticalDisc
                            : throw new InvalidOperationException("出网转入建档的硬盘必须已预定库内空盘或并档目标。");
                if (mode == null)
                {
                    mode = rowMode;
                }
                else if (mode != rowMode)
                {
                    throw new InvalidOperationException("同一出网转入建档单只能使用一种预定归档载体。");
                }
            }

            return mode;
        }

        private void ApplyOutboundArchiveFilingPreset()
        {
            OnPropertyChanged(nameof(IsElectronicSubmissionModeLocked));
            OnPropertyChanged(nameof(IsElectronicSubmissionModeEditable));
            OnPropertyChanged(nameof(OutboundArchiveFilingModeHint));
            OnPropertyChanged(nameof(IsOutboundArchiveBlankDiskCodeReadOnly));
            OnPropertyChanged(nameof(IsElectronicMediumSelectionButtonVisible));

            if (_outboundArchiveFilingMode != ElectronicArchiveSubmissionMode.CopyAppendExistingHardDisk)
            {
                return;
            }

            int? unitId = EnumerateSelectedElectronicMediaEntryRows()
                .Select(row => row.AppendTargetElectronicUnitId)
                .FirstOrDefault(id => id is > 0);
            if (unitId is not > 0)
            {
                return;
            }

            IsNewBoxMode = false;
            ExistingElectronicArchiveUnitListItem? item = ExistingElectronicUnits
                .FirstOrDefault(candidate => candidate.Unit.Id == unitId.Value);
            if (item == null)
            {
                return;
            }

            if (!ReferenceEquals(SelectedExistingElectronicUnitItem, item))
            {
                SelectedExistingElectronicUnitItem = item;
            }
            else
            {
                OnSelectedExistingElectronicUnitChanged();
            }
        }
    }
}
