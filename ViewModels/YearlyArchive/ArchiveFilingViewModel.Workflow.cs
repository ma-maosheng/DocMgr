using DocMgr.Models.Cabinets;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 立档页面初始化、待办刷新与提交编排流程。
    /// </summary>
    public partial class ArchiveFilingViewModel
    {
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await LoadCabinetsAsync();
            await RefreshPendingList();
            _isInitialized = true;
        }

        public Task RefreshPendingList()
            => RefreshPendingList(null, null);

        private async Task RefreshPendingList(
            IReadOnlyCollection<int>? simulatedRecordIdsToRetain,
            IReadOnlyCollection<int>? electronicRecordIdsToRetain = null)
        {
            string year = SelectedPendingYear;
            List<YearlyArchiveRegisterRecord> simulatedRecords;
            List<YearlyArchiveRegisterRecord> electronicRecords;
            int simulatedFiledCount;
            int electronicFiledCount;

            try
            {
                using (IServiceScope scope = _scopeFactory.CreateScope())
                {
                    var filing = scope.ServiceProvider.GetRequiredService<IArchiveFilingService>();
                    // 在独立 scope + 线程池延续上读库，避免 SQLite/EF 长时间占满 UI 调度线程导致整窗无法响应。
                    Task<List<YearlyArchiveRegisterRecord>> simulatedTask = filing.GetPendingSimulatedRecordsAsync(year);
                    Task<List<YearlyArchiveRegisterRecord>> electronicTask = filing.GetPendingElectronicRecordsAsync(year);
                    Task<int> simulatedFiledTask = filing.GetFiledSimulatedRecordCountAsync(year);
                    Task<int> electronicFiledTask = filing.GetFiledElectronicRecordCountAsync(year);
                    await Task.WhenAll(simulatedTask, electronicTask, simulatedFiledTask, electronicFiledTask).ConfigureAwait(false);
                    simulatedRecords = await simulatedTask.ConfigureAwait(false);
                    electronicRecords = await electronicTask.ConfigureAwait(false);
                    simulatedFiledCount = await simulatedFiledTask.ConfigureAwait(false);
                    electronicFiledCount = await electronicFiledTask.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await RunOnUiAsync(() => MessageBox.Show("加载待立档数据出错: " + ex.Message)).ConfigureAwait(false);
                return;
            }

            await RunOnUiAsync(async () =>
            {
                SuppressPendingListSelectionSync = true;
                try
                {
                    // 大批量逐项 Add 会长时间占用 UI 线程；分片并让出调度，避免整窗（含左侧菜单）假死。
                    await ReplaceItemsAllowingPumpAsync(SimulatedPendingRecords, simulatedRecords);
                    await ReplaceItemsAllowingPumpAsync(ElectronicPendingRecords, electronicRecords);
                    UpdateFilingTrackCounts(
                        simulatedRecords.Count,
                        simulatedFiledCount,
                        electronicRecords.Count,
                        electronicFiledCount);

                    if (await TryRestoreSimulatedSelectionAsync(simulatedRecordIdsToRetain).ConfigureAwait(true))
                    {
                        return;
                    }

                    if (await TryRestoreElectronicSelectionAsync(electronicRecordIdsToRetain).ConfigureAwait(true))
                    {
                        return;
                    }

                    RequestClearPendingListSelections?.Invoke();
                    ResetSelection();
                    ResetPanelState();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("加载待立档数据出错: " + ex.Message);
                }
                finally
                {
                    SuppressPendingListSelectionSync = false;
                }
            }).ConfigureAwait(false);
        }

        private async Task<bool> TryRestoreSimulatedSelectionAsync(IReadOnlyCollection<int>? simulatedRecordIdsToRetain)
        {
            if (!IsSimulatedTrack || simulatedRecordIdsToRetain == null || simulatedRecordIdsToRetain.Count == 0)
            {
                return false;
            }

            var retainedRecords = SimulatedPendingRecords
                .Where(record => simulatedRecordIdsToRetain.Contains(record.Id))
                .ToList();
            if (retainedRecords.Count == 0)
            {
                return false;
            }

            ResetPanelState();
            _selectedRecords = retainedRecords;
            OnPropertyChanged(nameof(SelectedRecords));
            OnPropertyChanged(nameof(ElectronicApplicationFormNosText));
            SuppressPendingListSelectionSync = true;
            try
            {
                SimulatedPendingSelectionRestoreRequested?.Invoke(retainedRecords.Select(record => record.Id).ToList());
                await HandleSelectedRecordsChangedAsync().ConfigureAwait(true);
            }
            finally
            {
                SuppressPendingListSelectionSync = false;
            }

            return true;
        }

        private async Task<bool> TryRestoreElectronicSelectionAsync(IReadOnlyCollection<int>? electronicRecordIdsToRetain)
        {
            if (!IsElectronicTrack || electronicRecordIdsToRetain == null || electronicRecordIdsToRetain.Count == 0)
            {
                return false;
            }

            var retainedRecords = ElectronicPendingRecords
                .Where(record => electronicRecordIdsToRetain.Contains(record.Id))
                .ToList();
            if (retainedRecords.Count == 0)
            {
                return false;
            }

            ResetPanelState();
            _selectedRecords = retainedRecords;
            OnPropertyChanged(nameof(SelectedRecords));
            OnPropertyChanged(nameof(ElectronicApplicationFormNosText));
            await HandleSelectedRecordsChangedAsync().ConfigureAwait(true);
            return true;
        }

        private void InitializePendingYears()
        {
            int currentYear = DateTime.Now.Year;
            PendingYears.Clear();
            for (int i = 0; i < 5; i++)
            {
                PendingYears.Add((currentYear - i).ToString());
            }

            if (PendingYears.Count > 0)
            {
                _selectedPendingYear = PendingYears[0];
                OnPropertyChanged(nameof(SelectedPendingYear));
            }
        }

        private void UpdateFilingTrackCounts(
            int simulatedPending,
            int simulatedFiled,
            int electronicPending,
            int electronicFiled)
        {
            SimulatedPendingCount = simulatedPending;
            SimulatedFiledCount = simulatedFiled;
            ElectronicPendingCount = electronicPending;
            ElectronicFiledCount = electronicFiled;
        }

        private async Task LoadCabinetsAsync()
        {
            List<Cabinet> list;

            try
            {
                using (IServiceScope scope = _scopeFactory.CreateScope())
                {
                    var cabinetService = scope.ServiceProvider.GetRequiredService<ICabinetService>();
                    list = await cabinetService.GetAllCabinetsAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await RunOnUiAsync(() => MessageBox.Show("加载档案柜信息失败: " + ex.Message)).ConfigureAwait(false);
                return;
            }

            await RunOnUiAsync(() =>
            {
                ReplaceItems(Cabinets, CabinetSelectionSupport.BuildSimulatedArchiveCabinetItems(list));
                ReplaceItems(ElectronicCabinets, CabinetSelectionSupport.BuildElectronicMagneticCabinetItems(list));
                ApplyDefaultCabinetSelectionIfNeeded();
            }).ConfigureAwait(false);
        }

        private void ApplyDefaultCabinetSelectionIfNeeded()
        {
            if (SelectedCabinet == null && Cabinets.Count > 0)
            {
                SelectedCabinet = Cabinets[0];
            }

            if (SelectedElectronicCabinet == null && ElectronicCabinets.Count > 0)
            {
                SelectedElectronicCabinet = ElectronicCabinets[0];
            }
        }

        private static Task RunOnUiAsync(Action action)
        {
            return RunOnUiAsync(() =>
            {
                action();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// 提交前刷新所选登记单的模拟子项 ID，但不重置档案盒编号、柜位等已填信息。
        /// </summary>
        private async Task RefreshSelectedSimulatedMediaItemsForSubmitAsync()
        {
            if (!IsSimulatedTrack || SelectedRecords.Count == 0)
            {
                return;
            }

            var selectionKeys = SimulatedRecordItems
                .Where(item => item.IsSelected)
                .Select(item => (item.FormNo, item.MediaType, item.ItemType, item.ContentDesc))
                .ToHashSet();

            var recordIds = SelectedRecords
                .Select(record => record.Id)
                .Distinct()
                .ToHashSet();

            List<YearlyArchiveRegisterRecord> freshRecords;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var filing = scope.ServiceProvider.GetRequiredService<IArchiveFilingService>();
                var pending = await filing.GetPendingSimulatedRecordsAsync(SelectedPendingYear).ConfigureAwait(false);
                freshRecords = pending
                    .Where(record => recordIds.Contains(record.Id))
                    .ToList();
            }
            catch
            {
                return;
            }

            if (freshRecords.Count == 0)
            {
                return;
            }

            await RunOnUiAsync(() =>
            {
                _selectedRecords = freshRecords;
                OnPropertyChanged(nameof(SelectedRecords));
                RebuildSimulatedRecordItems();
                foreach (var item in SimulatedRecordItems)
                {
                    item.IsSelected = selectionKeys.Contains((item.FormNo, item.MediaType, item.ItemType, item.ContentDesc));
                }

                UpdateSummaryText();
            }).ConfigureAwait(true);
        }

        private static Task RunOnUiAsync(Func<Task> asyncAction)
        {
            Application? app = Application.Current;
            if (app == null)
            {
                return asyncAction();
            }

            Dispatcher dispatcher = app.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                return asyncAction();
            }

            // 必须可靠地等到异步委托全部完成。仅用 InvokeAsync(Func<Task>).Task 在部分运行时上等价于过早完成。
            var tcs = new TaskCompletionSource();
            _ = dispatcher.BeginInvoke(
                priority: DispatcherPriority.Normal,
                method: new Action(async () =>
                {
                    try
                    {
                        await asyncAction().ConfigureAwait(true);
                        tcs.TrySetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }));
            return tcs.Task;
        }

        private static async Task ReplaceItemsAllowingPumpAsync<T>(ObservableCollection<T> target, List<T> source)
        {
            target.Clear();
            int n = source.Count;
            if (n == 0)
            {
                return;
            }

            const int chunk = 40;
            for (int i = 0; i < n; i++)
            {
                target.Add(source[i]);
                if (i > 0 && (i % chunk) == 0 && i < n - 1)
                {
                    await Task.Yield();
                }
            }
        }

        private async Task GenerateSequence()
        {
            if (string.IsNullOrWhiteSpace(TargetYear))
            {
                MessageBox.Show("请先选择资料以确定归属年度。");
                return;
            }

            try
            {
                if (IsSimulatedTrack)
                {
                    ArchiveSequenceNo = await _filingService.GenerateNextArchiveSequenceNoAsync(TargetYear);
                    return;
                }

                ElectronicArchiveNo = await _filingService.GenerateNextElectronicArchiveNoAsync(TargetYear);
            }
            catch (Exception ex)
            {
                MessageBox.Show("生成编号失败: " + ex.Message);
            }
        }

        private async Task Submit()
        {
            if (SelectedRecords.Count == 0)
            {
                MessageBox.Show("请先在左侧选择要归档的资料。");
                return;
            }

            if (IsSimulatedTrack)
            {
                await SubmitSimulatedAsync();
                return;
            }

            await SubmitElectronicAsync();
        }

        private async Task SubmitSimulatedAsync()
        {
            var selectedRecordIds = SelectedRecords
                .Select(item => item.Id)
                .Distinct()
                .ToList();

            await RefreshSelectedSimulatedMediaItemsForSubmitAsync();

            var selectedMediaItemIds = SimulatedRecordItems
                .Where(item => item.IsSelected)
                .Select(item => item.MediaItemId)
                .Distinct()
                .ToList();

            if (selectedMediaItemIds.Count == 0)
            {
                MessageBox.Show("请先勾选本次需要入盒的资料子项。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (IsNewBoxMode)
            {
                if (string.IsNullOrWhiteSpace(ArchiveSequenceNo))
                {
                    MessageBox.Show("档案盒编号不能为空。");
                    return;
                }

                if (IsPhysicalCodeWarning || string.IsNullOrWhiteSpace(PhysicalCodeResult))
                {
                    MessageBox.Show("物理位置未正确生成。");
                    return;
                }

                bool exists = await _filingService.IsArchiveSequenceExistsAsync(ArchiveSequenceNo.Trim());
                if (exists)
                {
                    MessageBox.Show($"编号 {ArchiveSequenceNo} 已存在，请重新生成或手动修改。");
                    return;
                }

                if (!int.TryParse(SelectedRow, out int row) || !int.TryParse(SelectedColumn, out int col))
                {
                    MessageBox.Show("请选择有效的柜位信息。");
                    return;
                }

                var newBox = new YearlyArchiveBox
                {
                    ArchiveSequenceNo = ArchiveSequenceNo.Trim(),
                    BoxLocationCode = PhysicalCodeResult,
                    CabinetName = SelectedCabinet?.Name ?? string.Empty,
                    Side = SelectedSide,
                    Row = row,
                    Column = col,
                    BoxIndex = _currentCellBoxCount + 1,
                    ProjectName = TargetProject,
                    Year = TargetYear,
                    Specs = SelectedSpec,
                    ArchivedBy = _userContextService.CurrentUser?.RealName ?? "Unknown",
                    ArchivedDate = DateTime.Now,
                    Remarks = Remarks
                };

                try
                {
                    await _filingService.CreateArchiveBoxAsync(newBox, selectedMediaItemIds);
                    MessageBox.Show($"模拟介质立档成功。\n档案盒编号：{newBox.ArchiveSequenceNo}\n本次已入盒 {selectedMediaItemIds.Count} 个资料子项。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshPendingList(selectedRecordIds);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("模拟介质立档失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return;
            }

            if (SelectedExistingBox == null)
            {
                MessageBox.Show("请先选择要并入的档案盒。");
                return;
            }

            try
            {
                await _filingService.AppendToArchiveBoxAsync(SelectedExistingBox.Id, selectedMediaItemIds);
                MessageBox.Show($"模拟介质已并入档案盒：{SelectedExistingBox.ArchiveSequenceNo}\n本次已入盒 {selectedMediaItemIds.Count} 个资料子项。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                await RefreshPendingList(selectedRecordIds);
            }
            catch (Exception ex)
            {
                MessageBox.Show("并入档案盒失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SubmitElectronicAsync()
        {
            EnsureElectronicBagDefaults();

            if (!EnsureExternalHardDiskRegisteredForRetainedScenario(showDialogs: true))
            {
                return;
            }

            var selectedRecordIds = SelectedRecords
                .Select(item => item.Id)
                .Distinct()
                .ToList();

            var selectedMediaItemIds = GetSelectedMediaItemIdsForElectronicSubmit();

            if (selectedMediaItemIds.Count == 0)
            {
                ShowNoPendingElectronicItemsMessage();
                return;
            }

            try
            {
                await EnsureOpticalDiscAppendTargetCompatibleAsync();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (IsNewBoxMode)
            {
                try
                {
                    var request = BuildElectronicSubmissionRequest(selectedMediaItemIds, null);

                    var preview = await _filingService.PreviewNewElectronicArchiveUnitAsync(request, _userContextService.CurrentUser);
                    ShowElectronicArchivePreviewDialog(preview);
                    MessageBoxResult confirm = MessageBox.Show(
                        "系统已生成拟执行逻辑。是否确认执行电子介质立档？",
                        "确认执行",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    var result = await _filingService.SubmitNewElectronicArchiveUnitAsync(request, _userContextService.CurrentUser);
                    ShowElectronicArchiveResultDialog(result);
                    await RefreshPendingList(null, selectedRecordIds);
                    ResetElectronicStepThreeSummaryAfterSuccessfulFiling();
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show("电子介质立档失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show("电子介质立档失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return;
            }

            try
            {
                var request = BuildElectronicSubmissionRequest(selectedMediaItemIds, SelectedExistingElectronicUnit?.Id);

                var preview = await _filingService.PreviewAppendElectronicArchiveUnitAsync(request, _userContextService.CurrentUser);
                ShowElectronicArchivePreviewDialog(preview);
                MessageBoxResult confirm = MessageBox.Show(
                    "系统已生成拟执行逻辑。是否确认执行并入电子立档？",
                    "确认执行",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }

                var result = await _filingService.SubmitAppendElectronicArchiveUnitAsync(request, _userContextService.CurrentUser);
                ShowElectronicArchiveResultDialog(result);
                await RefreshPendingList(null, selectedRecordIds);
                ResetElectronicStepThreeSummaryAfterSuccessfulFiling();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show("并入电子立档单元失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show("并入电子立档单元失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PreviewElectronicSubmissionAsync()
        {
            if (SelectedRecords.Count == 0)
            {
                MessageBox.Show("请先在左侧选择要归档的资料。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            EnsureElectronicBagDefaults();

            if (!EnsureExternalHardDiskRegisteredForRetainedScenario(showDialogs: true))
            {
                return;
            }

            var selectedMediaItemIds = GetSelectedMediaItemIdsForElectronicSubmit();

            if (selectedMediaItemIds.Count == 0)
            {
                ShowNoPendingElectronicItemsMessage();
                return;
            }

            try
            {
                await EnsureOpticalDiscAppendTargetCompatibleAsync();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (IsNewBoxMode)
            {
                try
                {
                    var request = BuildElectronicSubmissionRequest(selectedMediaItemIds, null);
                    var result = await _filingService.PreviewNewElectronicArchiveUnitAsync(request, _userContextService.CurrentUser);
                    ShowElectronicArchivePreviewDialog(result);
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show("无法生成拟执行逻辑预览: " + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show("无法生成拟执行逻辑预览: " + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return;
            }

            if (SelectedExistingElectronicUnit == null)
            {
                MessageBox.Show("请先选择要并入的电子介质袋。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var request = BuildElectronicSubmissionRequest(selectedMediaItemIds, SelectedExistingElectronicUnit.Id);
                var result = await _filingService.PreviewAppendElectronicArchiveUnitAsync(request, _userContextService.CurrentUser);
                ShowElectronicArchivePreviewDialog(result);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show("无法生成拟执行逻辑预览: " + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show("无法生成拟执行逻辑预览: " + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private ElectronicArchiveSubmissionRequest BuildElectronicSubmissionRequest(IReadOnlyList<int> mediaItemIds, int? existingUnitId)
        {
            EnsureBorrowedHardDiskLinkedMediumCodesBeforeSubmit();

            ElectronicArchiveSubmissionMode submissionMode = SelectedElectronicSubmissionMode ?? ElectronicArchiveSubmissionMode.CopyNewHardDisk;
            string linkedMediumCodes = ElectronicLinkedMediumCodes.Trim();
            if (submissionMode is ElectronicArchiveSubmissionMode.CopyNewOpticalDisc
                or ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew
                or ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc)
            {
                linkedMediumCodes = string.Empty;
            }

            var filingPaths = BuildFilingStoragePathByMediaItemId();
            return new ElectronicArchiveSubmissionRequest
            {
                ArchiveUnit = new YearlyElectronicArchiveUnit
                {
                    Id = existingUnitId ?? 0,
                    ElectronicArchiveNo = IsNewBoxMode
                        ? ElectronicArchiveNo.Trim()
                        : SelectedExistingElectronicUnit?.ElectronicArchiveNo?.Trim() ?? ElectronicArchiveNo.Trim(),
                    ProjectName = TargetProject,
                    Year = TargetYear,
                    StorageCarrierType = ElectronicStorageCarrierType.Trim(),
                    StoragePath = ElectronicStoragePath.Trim(),
                    StorageLocation = ElectronicStorageLocation.Trim(),
                    LinkedMediumCodes = linkedMediumCodes,
                    Disposition = ElectronicDisposition.Trim(),
                    MediaCount = ResolveElectronicMediaCount(linkedMediumCodes, ElectronicMediaCount),
                    ContentSummary = ElectronicContentSummary.Trim(),
                    Remarks = Remarks.Trim()
                },
                MediaItemIds = mediaItemIds.ToList(),
                FilingStoragePathByMediaItemId = filingPaths,
                MediaEntryIds = ElectronicRecordItems
                    .Where(item => mediaItemIds.Contains(item.MediaItemId))
                    .Select(item => item.MediaEntryId)
                    .Distinct()
                    .ToList(),
                SubmissionMode = submissionMode,
                ExistingElectronicArchiveUnitId = existingUnitId,
                PendingExternalHardDisk = CreatePendingExternalHardDiskSnapshot(),
                BorrowedHardDiskCandidate = CreateBorrowedHardDiskCandidateSnapshot(),
                IsRetainedHardDiskScenario = IsElectronicHardDiskRetainedScenario,
                IsOpticalDiscArchiveScenario = IsOpticalDiscArchiveScenario,
                FilingMode = SelectedHardDiskCopyTargetMode.Trim(),
                AppendTargetStorageLocation = SelectedExistingElectronicUnit?.StorageLocation?.Trim() ?? string.Empty,
                RequiresFormatRetainedHardDisk = submissionMode is ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc or ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk
                    || (IsElectronicHardDiskRetainedScenario && submissionMode == ElectronicArchiveSubmissionMode.CopyNewHardDisk)
            };
        }

        private void ShowNoPendingElectronicItemsMessage()
        {
            string message = SelectedElectronicMediaForm == null
                ? "请先在第一步选择待立档的电子介质表单。"
                : "当前电子介质下没有待入袋的资料明细，请更换第一步所选介质或刷新待立档列表。";
            MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
