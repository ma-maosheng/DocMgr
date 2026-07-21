using DocMgr.Infrastructure.AgentDebugLogging;
using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 立档模式切换与模拟介质位置计算/建议逻辑。
    /// </summary>
    public partial class ArchiveFilingViewModel
    {
        private async void OnModeChanged()
        {
            OnPropertyChanged(nameof(SelectedSimulatedAppendTargetHintText));

            if (IsSimulatedTrack)
            {
                if (!IsNewBoxMode)
                {
                    PhysicalCodeResult = "请从上方列表选择要并入的档案盒";
                    ArchiveSequenceNo = string.Empty;
                    IsPhysicalCodeWarning = true;
                    OnSelectedExistingBoxChanged();
                }
                else
                {
                    ArchiveSequenceNo = _draftNewArchiveSequenceNo;
                    if (string.IsNullOrWhiteSpace(SelectedSpec))
                    {
                        SelectedSpec = Specs.FirstOrDefault() ?? "标准(5cm)";
                    }

                    await LoadSimulatedTargetLocationOptionsAsync();
                }
            }
            else if (!IsNewBoxMode)
            {
                if (!CanUseElectronicAppendMode)
                {
                    _isNewBoxMode = true;
                    OnPropertyChanged(nameof(IsNewBoxMode));
                    OnPropertyChanged(nameof(IsAppendMode));
                    string reason = string.IsNullOrWhiteSpace(_electronicDecision.AppendRestrictionReason)
                        ? "当前场景不允许并档，请使用新建立档。"
                        : _electronicDecision.AppendRestrictionReason;
                    _dialogService.ShowMessage(reason, "提示");
                    return;
                }

                PrepareElectronicAppendModeState();
                OnSelectedExistingElectronicUnitChanged();
            }
            else
            {
                await PrepareElectronicNewModeStateAsync();
            }

            UpdateSummaryText();
            RaiseElectronicStepFourPresentationChanged();
        }

        private void PrepareElectronicAppendModeState()
        {
            _suppressElectronicScenarioRefresh = true;
            try
            {
                ResetElectronicLocationSelection(reloadOptions: false);
                ResetElectronicRetainedHardDiskState();
                ClearElectronicAppendTargetFields();
            }
            finally
            {
                _suppressElectronicScenarioRefresh = false;
            }
        }

        private async Task PrepareElectronicNewModeStateAsync()
        {
            var preservedExternalHardDisk = _registeredExternalHardDisk;
            _suppressElectronicScenarioRefresh = true;
            try
            {
                ResetElectronicLocationSelection(reloadOptions: false);
                ResetElectronicRetainedHardDiskState();
                ClearElectronicNewModeEditableFields();
                ElectronicArchiveNo = _draftNewElectronicArchiveNo;
                RestorePendingExternalHardDiskRegistration(preservedExternalHardDisk);
            }
            finally
            {
                _suppressElectronicScenarioRefresh = false;
            }

            await PrefillElectronicFieldsFromSelectedRecordsAsync();
            EnsureElectronicBagDefaults();
            await LoadElectronicTargetLocationOptionsAsync();
        }

        /// <summary>
        /// 切换第三步「可选立档方式」时清空第四至第六步中沿用上一立档方式的界面数据（再由 <see cref="OnModeChanged"/> / Prefill 按新方式回填）。
        /// </summary>
        private void RestorePendingExternalHardDiskRegistration(PendingExternalHardDiskRegistration? preserved)
        {
            if (!IsElectronicHardDiskRetainedScenario || preserved == null)
            {
                return;
            }

            _registeredExternalHardDisk = preserved;
            SelectedRetainedHardDiskSource = ArchiveFilingBusinessRules.ExternalHardDiskSourceOption;
            OnPropertyChanged(nameof(RegisteredExternalHardDiskCodeDisplay));
            OnPropertyChanged(nameof(ExternalHardDiskRegistrationTooltip));
        }

        private void ResetElectronicFilingStepsFourThroughSixForModeChange()
        {
            var preservedExternalHardDisk = _registeredExternalHardDisk;
            ResetElectronicLocationSelection(reloadOptions: false);

            ClearSelectedHardDisk();
            ElectronicStorageCarrierType = string.Empty;
            ElectronicStoragePath = string.Empty;
            ElectronicStorageLocation = string.Empty;
            ElectronicOriginalStorageLocation = string.Empty;
            ElectronicSelectedMediumStatus = string.Empty;
            ElectronicLinkedMediumCodes = string.Empty;
            ElectronicMediaCount = 0;
            ElectronicContentSummary = string.Empty;
            ElectronicCellCountText = "-";
            Remarks = string.Empty;
            ElectronicArchiveNo = IsNewBoxMode ? _draftNewElectronicArchiveNo : string.Empty;

            ResetElectronicRetainedHardDiskState();
            RestorePendingExternalHardDiskRegistration(preservedExternalHardDisk);
            RaiseElectronicStepFourPresentationChanged();
        }

        private void ApplyHardDiskCopyTargetSelection()
        {
            if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew)
            {
                EnsureElectronicBagDefaults();
                return;
            }

            if (SelectedElectronicSubmissionMode is ElectronicArchiveSubmissionMode.CopyNewOpticalDisc
                or ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew
                or ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc)
            {
                ClearSelectedHardDisk();
                ElectronicStorageCarrierType = ArchiveFilingBusinessRules.DefaultOpticalDiscBagCarrierType;
                ElectronicMediaCount = 1;
                return;
            }

            if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.CopyNewHardDisk)
            {
                ClearSelectedHardDisk();
                EnsureElectronicBagDefaults();
                ElectronicSelectedMediumStatus = HardDiskMedium.StatusInStockBlank;
            }
        }

        private void ClearElectronicAppendTargetFields()
        {
            ElectronicArchiveNo = string.Empty;
            ElectronicStorageCarrierType = string.Empty;
            ElectronicStoragePath = string.Empty;
            ElectronicStorageLocation = string.Empty;
            ElectronicOriginalStorageLocation = string.Empty;
            ElectronicSelectedMediumStatus = string.Empty;
            ElectronicLinkedMediumCodes = string.Empty;
            ElectronicDisposition = string.Empty;
            ElectronicMediaCount = 0;
            ElectronicContentSummary = string.Empty;
            ElectronicCellCountText = "-";
            Remarks = string.Empty;
        }

        private void ClearElectronicNewModeEditableFields()
        {
            ElectronicStorageCarrierType = string.Empty;
            ElectronicStoragePath = string.Empty;
            ElectronicStorageLocation = string.Empty;
            ElectronicOriginalStorageLocation = string.Empty;
            ElectronicSelectedMediumStatus = string.Empty;
            ElectronicLinkedMediumCodes = string.Empty;
            ElectronicDisposition = string.Empty;
            ElectronicMediaCount = 0;
            ElectronicContentSummary = string.Empty;
            ElectronicCellCountText = "-";
            Remarks = string.Empty;
        }

        private void ResetElectronicRetainedHardDiskState()
        {
            _borrowedHardDiskReturnCandidate = null;
            SelectedRetainedHardDiskSource = string.Empty;
            _registeredExternalHardDisk = null;
            ExternalHardDiskFormattedBlankTargetLocation = string.Empty;
            ElectronicLocationSuggestionHint = string.Empty;
            OnPropertyChanged(nameof(ExternalHardDiskRegistrationTooltip));
        }

        private void ResetSimulatedLocationSelection(bool reloadOptions = true)
        {
            ClearSimulatedLocationSelectionCore();
            Interlocked.Increment(ref _simulatedTargetLocationOptionsGeneration);

            if (reloadOptions
                && IsSimulatedTrack
                && IsNewBoxMode)
            {
                _ = LoadSimulatedTargetLocationOptionsAsync();
                return;
            }

            ReplaceSimulatedTargetLocationOptions(Array.Empty<ArchiveBoxTargetLocationOption>());
        }

        private void ClearSimulatedLocationSelectionCore()
        {
            _suppressSimulatedLocationRecalc = true;
            _suppressSimulatedLocationOptionSync = true;
            try
            {
                _selectedCabinet = null;
                OnPropertyChanged(nameof(SelectedCabinet));
                _selectedSide = string.Empty;
                OnPropertyChanged(nameof(SelectedSide));
                _selectedRow = string.Empty;
                OnPropertyChanged(nameof(SelectedRow));
                _selectedColumn = string.Empty;
                OnPropertyChanged(nameof(SelectedColumn));
                ReplaceItems(Sides, Array.Empty<string>());
                ReplaceItems(Rows, Array.Empty<string>());
                ReplaceItems(Columns, Array.Empty<string>());
                _selectedSimulatedTargetLocationOption = null;
                OnPropertyChanged(nameof(SelectedSimulatedTargetLocationOption));
                PhysicalCodeResult = "请先选择位置";
                IsPhysicalCodeWarning = true;
                CellCountText = "-";
                _currentCellBoxCount = 0;
                _resolvedBoxSequenceIndex = 1;
            }
            finally
            {
                _suppressSimulatedLocationRecalc = false;
                _suppressSimulatedLocationOptionSync = false;
            }

            RaiseSlotSnapshotAvailabilityChanged();
        }

        private void ReplaceSimulatedTargetLocationOptions(IReadOnlyList<ArchiveBoxTargetLocationOption> options)
        {
            var ordered = options
                .OrderBy(item => item.Priority)
                .ThenBy(item => item.ExistingBoxCount)
                .ThenBy(item => item.Location, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? selectedLocation = _selectedSimulatedTargetLocationOption?.Location;

            _suppressSimulatedLocationOptionSync = true;
            try
            {
                SimulatedTargetLocationOptions.Clear();
                foreach (var option in ordered)
                {
                    SimulatedTargetLocationOptions.Add(option);
                }

                _selectedSimulatedTargetLocationOption = FindSimulatedTargetLocationOption(
                    SimulatedTargetLocationOptions,
                    selectedLocation);
                OnPropertyChanged(nameof(SelectedSimulatedTargetLocationOption));
            }
            finally
            {
                _suppressSimulatedLocationOptionSync = false;
            }
        }

        private void UpdateSides()
        {
            ReplaceItems(Sides, Array.Empty<string>());
            if (SelectedCabinet == null)
            {
                return;
            }

            Sides.Add("A");
            if (SelectedCabinet.FaceCount > 1)
            {
                Sides.Add("B");
            }

            if (Sides.Count > 0 && string.IsNullOrWhiteSpace(SelectedSide))
            {
                SelectedSide = Sides[0];
            }
        }

        private void UpdateRowsAndCols()
        {
            ReplaceItems(Rows, Array.Empty<string>());
            ReplaceItems(Columns, Array.Empty<string>());

            if (SelectedCabinet == null)
            {
                return;
            }

            for (int i = 1; i <= SelectedCabinet.LayerCount; i++)
            {
                Rows.Add(i.ToString());
            }

            for (int i = 1; i <= SelectedCabinet.ColumnCount; i++)
            {
                Columns.Add(i.ToString());
            }

            if (Rows.Count > 0 && string.IsNullOrWhiteSpace(SelectedRow))
            {
                SelectedRow = Rows[0];
            }

            if (Columns.Count > 0 && string.IsNullOrWhiteSpace(SelectedColumn))
            {
                SelectedColumn = Columns[0];
            }
        }

        /// <summary>
        /// 根据当前柜位计算盒内序号与物理位置编码。
        /// 使用独立 scope 读库，并用代数丢弃过期的并发计算结果。
        /// </summary>
        private async void CalculateBoxIndex()
        {
            if (_suppressSimulatedLocationRecalc || !IsSimulatedTrack || !IsNewBoxMode)
            {
                return;
            }

            if (SelectedCabinet == null
                || string.IsNullOrEmpty(SelectedSide)
                || string.IsNullOrEmpty(SelectedRow)
                || string.IsNullOrEmpty(SelectedColumn)
                || !int.TryParse(SelectedRow, out int row)
                || !int.TryParse(SelectedColumn, out int col))
            {
                PhysicalCodeResult = "位置信息不全";
                IsPhysicalCodeWarning = true;
                RaiseSlotSnapshotAvailabilityChanged();
                return;
            }

            string cabinetName = SelectedCabinet.Name;
            string side = SelectedSide;
            int myGeneration = Interlocked.Increment(ref _simulatedBoxIndexCalculationGeneration);
            // #region agent log
            AgentDebugSessionLog.Write("C", "CalculateBoxIndex", "start", new
            {
                cabinetName,
                side,
                row,
                col,
                myGeneration,
                threadId = Environment.CurrentManagedThreadId
            });
            // #endregion

            try
            {
                int count;
                int nextSequence;
                using (IServiceScope scope = _scopeFactory.CreateScope())
                {
                    var filing = scope.ServiceProvider.GetRequiredService<IArchiveFilingService>();
                    // 回到 UI 同步上下文后再写绑定属性，避免跨线程异常。
                    count = await filing.GetBoxCountInCellAsync(cabinetName, side, row, col).ConfigureAwait(true);
                    nextSequence = await filing.GetMinimumAvailableBoxSequenceInCellAsync(cabinetName, side, row, col)
                        .ConfigureAwait(true);
                }

                if (myGeneration != Volatile.Read(ref _simulatedBoxIndexCalculationGeneration))
                {
                    return;
                }

                _currentCellBoxCount = count;
                _resolvedBoxSequenceIndex = nextSequence;
                CellCountText = $"{count} 盒";
                PhysicalCodeResult = $"{cabinetName}{side}-{row}-{col}-{nextSequence:D2}";
                IsPhysicalCodeWarning = false;
                RaiseSlotSnapshotAvailabilityChanged();
                // #region agent log
                AgentDebugSessionLog.Write("C", "CalculateBoxIndex", "success", new { count, PhysicalCodeResult });
                // #endregion
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugSessionLog.WriteException("C", "CalculateBoxIndex", "failed", ex);
                // #endregion
                if (myGeneration != Volatile.Read(ref _simulatedBoxIndexCalculationGeneration))
                {
                    return;
                }

                PhysicalCodeResult = "位置计算失败";
                IsPhysicalCodeWarning = true;
                RaiseSlotSnapshotAvailabilityChanged();
                MessageBox.Show("计算档案盒位置失败: " + ex.GetBaseException().Message);
            }
        }

        private void UpdatePhysicalCodePreview() => CalculateBoxIndex();

        private async Task LoadSimulatedTargetLocationOptionsAsync(
            string? preferredLocation = null,
            bool preferSuggestedSelection = false)
        {
            if (!IsSimulatedTrack || !IsNewBoxMode)
            {
                ReplaceSimulatedTargetLocationOptions(Array.Empty<ArchiveBoxTargetLocationOption>());
                SyncSelectedSimulatedTargetLocationOption(null);
                return;
            }

            int generation = Interlocked.Increment(ref _simulatedTargetLocationOptionsGeneration);
            string? locationToPrefer = preferredLocation;
            if (string.IsNullOrWhiteSpace(locationToPrefer) && !preferSuggestedSelection)
            {
                locationToPrefer = SelectedSimulatedTargetLocationOption?.Location
                    ?? ArchiveSlotLocationSupport.BuildSlotKey(PhysicalCodeResult);
            }

            IReadOnlyList<ArchiveBoxTargetLocationOption> options;
            try
            {
                options = await _filingService.GetArchiveBoxTargetLocationOptionsAsync(
                    TargetProject,
                    TargetYear,
                    SelectedSpec);
            }
            catch (Exception ex)
            {
                if (generation != Volatile.Read(ref _simulatedTargetLocationOptionsGeneration))
                {
                    return;
                }

                MessageBox.Show("加载可选档口失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (generation != Volatile.Read(ref _simulatedTargetLocationOptionsGeneration))
            {
                return;
            }

            ReplaceSimulatedTargetLocationOptions(options);

            ArchiveBoxTargetLocationOption? selected = null;
            if (preferSuggestedSelection)
            {
                selected = SimulatedTargetLocationOptions
                    .OrderBy(item => item.FitsCapacity ? 0 : 1)
                    .ThenBy(item => item.Priority)
                    .ThenBy(item => item.ExistingBoxCount)
                    .ThenBy(item => item.Location, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
            }
            else if (!string.IsNullOrWhiteSpace(locationToPrefer))
            {
                selected = FindSimulatedTargetLocationOption(SimulatedTargetLocationOptions, locationToPrefer);
            }

            selected ??= SimulatedTargetLocationOptions.FirstOrDefault();

            if (selected == null)
            {
                SyncSelectedSimulatedTargetLocationOption(null);
                ClearSimulatedLocationSelectionCore();
                return;
            }

            if (!TryApplySimulatedSlotOption(selected))
            {
                SyncSelectedSimulatedTargetLocationOption(null);
                ClearSimulatedLocationSelectionCore();
                return;
            }

            SyncSelectedSimulatedTargetLocationOption(selected.Location);
        }

        private async Task SuggestSimulatedLocationAsync()
        {
            if (!IsSimulatedTrack || !IsNewBoxMode)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(TargetYear))
            {
                MessageBox.Show("请先选择待立档资料。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 建议档口：刷新占用数并更新 items，而不是先清空再重载。
                await LoadSimulatedTargetLocationOptionsAsync(preferSuggestedSelection: true);

                var suggestedOption = SimulatedTargetLocationOptions
                    .OrderBy(item => item.FitsCapacity ? 0 : 1)
                    .ThenBy(item => item.Priority)
                    .ThenBy(item => item.ExistingBoxCount)
                    .ThenBy(item => item.Location, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (suggestedOption == null)
                {
                    MessageBox.Show("未找到符合当前规格的建议档口，请手动选择位置。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string summary = suggestedOption.FitsCapacity
                    ? $"建议使用档口 {suggestedOption.Location}。"
                    : $"建议使用档口 {suggestedOption.Location}。当前未找到严格满足容量规则的档口，已回退为占用较少的可用档口建议，请人工确认。";
                MessageBox.Show(summary, "建议档口位置", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("获取建议档口位置失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryApplySimulatedSlotOption(ArchiveBoxTargetLocationOption option)
        {
            ArgumentNullException.ThrowIfNull(option);

            var matchedCabinet = Cabinets.FirstOrDefault(item =>
                string.Equals(item.Name, option.CabinetName, StringComparison.OrdinalIgnoreCase));
            if (matchedCabinet == null)
            {
                return false;
            }

            _suppressSimulatedLocationRecalc = true;
            try
            {
                SelectedCabinet = matchedCabinet;
                SelectedSide = option.Side;
                SelectedRow = option.Row.ToString();
                SelectedColumn = option.Column.ToString();
            }
            finally
            {
                _suppressSimulatedLocationRecalc = false;
            }

            CalculateBoxIndex();
            SyncSelectedSimulatedTargetLocationOption(option.Location);
            return true;
        }

        private static ArchiveBoxTargetLocationOption? FindSimulatedTargetLocationOption(
            IEnumerable<ArchiveBoxTargetLocationOption> options,
            string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            return options.FirstOrDefault(item => ArchiveSlotLocationSupport.IsSameSlot(item.Location, location));
        }

        private void SyncSelectedSimulatedTargetLocationOption(string? location)
        {
            var matched = FindSimulatedTargetLocationOption(SimulatedTargetLocationOptions, location);
            if (ReferenceEquals(_selectedSimulatedTargetLocationOption, matched))
            {
                return;
            }

            _suppressSimulatedLocationOptionSync = true;
            try
            {
                _selectedSimulatedTargetLocationOption = matched;
                OnPropertyChanged(nameof(SelectedSimulatedTargetLocationOption));
            }
            finally
            {
                _suppressSimulatedLocationOptionSync = false;
            }
        }

        private void ShowSimulatedSlotSnapshot()
        {
            if (!IsSimulatedTrack)
            {
                return;
            }

            if (TryResolveSimulatedSlotSnapshotContext(out Cabinet cabinet, out string side, out string row, out string column))
            {
                ShowSlotSnapshot(cabinet, side, row, column);
                return;
            }

            _dialogService.ShowMessage("当前暂无有效物理存放位置，无法查看档口占用快照。", "提示");
        }
    }
}
