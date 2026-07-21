using DocMgr.Infrastructure.AgentDebugLogging;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Services.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 选中记录的视图投影、摘要同步与容器列表装配逻辑。
    /// </summary>
    public partial class ArchiveFilingViewModel
    {
        private async Task HandleSelectedRecordsChangedAsync()
        {
            // #region agent log
            AgentDebugSessionLog.Write("E", "HandleSelectedRecordsChangedAsync", "enter", new
            {
                selectedCount = SelectedRecords.Count,
                isSimulated = IsSimulatedTrack,
                threadId = Environment.CurrentManagedThreadId
            });
            // #endregion
            await _selectedRecordsChangedGate.WaitAsync().ConfigureAwait(true);
            try
            {
                int myGeneration = Interlocked.Increment(ref _selectedRecordsChangedGeneration);
                // #region agent log
                AgentDebugSessionLog.Write("E", "HandleSelectedRecordsChangedAsync", "gate acquired", new { myGeneration });
                // #endregion

                try
                {
                    await HandleSelectedRecordsChangedCoreAsync(myGeneration).ConfigureAwait(true);
                    // #region agent log
                    AgentDebugSessionLog.Write("E", "HandleSelectedRecordsChangedAsync", "core completed", new
                    {
                        myGeneration,
                        itemCount = SimulatedRecordItems.Count,
                        project = TargetProject
                    });
                    // #endregion
                }
                catch (Exception ex)
                {
                    // #region agent log
                    AgentDebugSessionLog.WriteException("A", "HandleSelectedRecordsChangedAsync.catch", "core threw", ex);
                    // #endregion
                    string detail = ex.GetBaseException().Message;
                    await RunOnUiAsync(() =>
                        MessageBox.Show(
                            "加载所选资料立档信息失败: " + detail + "\n\n" + ex.GetType().Name,
                            "错误",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error)
                    ).ConfigureAwait(true);
                }
            }
            finally
            {
                _selectedRecordsChangedGate.Release();
            }
        }

        /// <summary>
        /// 按当前轨道与年度，从数据库重新加载所选登记单的完整导航图，避免列表项导航属性未展开导致投影失败。
        /// </summary>
        private async Task<bool> TryRefreshSelectedPendingRecordsAsync(int myGeneration)
        {
            var selectedIds = SelectedRecords
                .Select(record => record.Id)
                .Distinct()
                .ToList();
            if (selectedIds.Count == 0)
            {
                return false;
            }

            List<YearlyArchiveRegisterRecord> refreshedRecords;
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IArchiveFilingService filing = scope.ServiceProvider.GetRequiredService<IArchiveFilingService>();
                List<YearlyArchiveRegisterRecord> pending = IsSimulatedTrack
                    ? await filing.GetPendingSimulatedRecordsAsync(SelectedPendingYear).ConfigureAwait(false)
                    : await filing.GetPendingElectronicRecordsAsync(SelectedPendingYear).ConfigureAwait(false);
                refreshedRecords = pending
                    .Where(record => selectedIds.Contains(record.Id))
                    .ToList();
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugSessionLog.WriteException("D", "TryRefreshSelectedPendingRecordsAsync", "refresh failed", ex);
                // #endregion
                await RunOnUiAsync(() =>
                    MessageBox.Show(
                        "刷新所选待立档登记单失败: " + ex.GetBaseException().Message,
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error)
                ).ConfigureAwait(false);
                return false;
            }

            if (myGeneration != Volatile.Read(ref _selectedRecordsChangedGeneration))
            {
                return false;
            }

            await RunOnUiAsync(() =>
            {
                _selectedRecords = refreshedRecords;
                OnPropertyChanged(nameof(SelectedRecords));
                OnPropertyChanged(nameof(ElectronicApplicationFormNosText));
            }).ConfigureAwait(true);

            return true;
        }

        private async Task HandleSelectedRecordsChangedCoreAsync(int myGeneration)
        {
            UpdateSummaryText();

            if (SelectedRecords.Count == 0)
            {
                ReplaceItems(ExistingBoxes, Array.Empty<YearlyArchiveBox>());
                ReplaceItems(ExistingElectronicUnits, Array.Empty<ExistingElectronicArchiveUnitListItem>());
                ReplaceItems(SimulatedRecordItems, Array.Empty<SelectableSimulatedArchiveItemViewModel>());
                RefreshSimulatedRecordItemsPanel();
                ReplaceItems(ElectronicRecordItems, Array.Empty<SelectableElectronicArchiveMediaViewModel>());
                ReplaceItems(ElectronicRecordItemsStepTwo, Array.Empty<SelectableElectronicArchiveMediaViewModel>());
                RefreshElectronicRecordItemsStepTwoPanel();
                ReplaceItems(ElectronicMediaFormOptions, Array.Empty<ElectronicMediaFormListItem>());
                _selectedElectronicMediaForm = null;
                OnPropertyChanged(nameof(SelectedElectronicMediaForm));
                TargetProject = string.Empty;
                TargetYear = string.Empty;
                ResetElectronicFields();
                ResetElectronicLocationSelection();
                Remarks = string.Empty;
                OnPropertyChanged(nameof(ElectronicApplicationFormNosText));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedDisposition));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedMediumCode));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingStatus));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingProgress));
                RaiseElectronicStepFourPresentationChanged();
                return;
            }

            if (!await TryRefreshSelectedPendingRecordsAsync(myGeneration).ConfigureAwait(true)
                || myGeneration != Volatile.Read(ref _selectedRecordsChangedGeneration))
            {
                return;
            }

            if (SelectedRecords.Count == 0)
            {
                ReplaceItems(ExistingBoxes, Array.Empty<YearlyArchiveBox>());
                ReplaceItems(ExistingElectronicUnits, Array.Empty<ExistingElectronicArchiveUnitListItem>());
                ReplaceItems(SimulatedRecordItems, Array.Empty<SelectableSimulatedArchiveItemViewModel>());
                RefreshSimulatedRecordItemsPanel();
                ReplaceItems(ElectronicRecordItems, Array.Empty<SelectableElectronicArchiveMediaViewModel>());
                ReplaceItems(ElectronicRecordItemsStepTwo, Array.Empty<SelectableElectronicArchiveMediaViewModel>());
                RefreshElectronicRecordItemsStepTwoPanel();
                ReplaceItems(ElectronicMediaFormOptions, Array.Empty<ElectronicMediaFormListItem>());
                _selectedElectronicMediaForm = null;
                OnPropertyChanged(nameof(SelectedElectronicMediaForm));
                TargetProject = string.Empty;
                TargetYear = string.Empty;
                ResetElectronicFields();
                ResetElectronicLocationSelection();
                Remarks = string.Empty;
                OnPropertyChanged(nameof(ElectronicApplicationFormNosText));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedDisposition));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedMediumCode));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingStatus));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingProgress));
                RaiseElectronicStepFourPresentationChanged();
                return;
            }

            var first = SelectedRecords.First();
            TargetProject = ArchiveFilingBusinessRules.ResolveElectronicArchiveProjectName(first);
            TargetYear = first.CreatedDate.Year.ToString();

            if (IsSimulatedTrack)
            {
                // #region agent log
                AgentDebugSessionLog.Write("D", "HandleSelectedRecordsChangedCoreAsync", "before LoadExistingBoxes", new
                {
                    TargetProject,
                    TargetYear,
                    mediaEntries = first.MediaEntries?.Count ?? -1
                });
                // #endregion
                await LoadExistingBoxesAsync(TargetProject, TargetYear).ConfigureAwait(true);
                if (myGeneration != Volatile.Read(ref _selectedRecordsChangedGeneration))
                {
                    return;
                }

                // #region agent log
                AgentDebugSessionLog.Write("A", "HandleSelectedRecordsChangedCoreAsync", "before RebuildSimulatedRecordItems", new
                {
                    selectedCount = SelectedRecords.Count,
                    mediaEntries = SelectedRecords.Sum(r => r.MediaEntries?.Count ?? 0)
                });
                // #endregion
                try
                {
                    RebuildSimulatedRecordItems();
                }
                catch (Exception ex)
                {
                    // #region agent log
                    AgentDebugSessionLog.WriteException("A", "RebuildSimulatedRecordItems", "rebuild threw", ex);
                    // #endregion
                    throw;
                }

                // #region agent log
                AgentDebugSessionLog.Write("B", "HandleSelectedRecordsChangedCoreAsync", "after RebuildSimulatedRecordItems", new
                {
                    itemCount = SimulatedRecordItems.Count,
                    threadId = Environment.CurrentManagedThreadId
                });
                // #endregion
                ReplaceItems(ElectronicRecordItems, Array.Empty<SelectableElectronicArchiveMediaViewModel>());
                ReplaceItems(ElectronicRecordItemsStepTwo, Array.Empty<SelectableElectronicArchiveMediaViewModel>());
                RefreshElectronicRecordItemsStepTwoPanel();
                ReplaceItems(ElectronicMediaFormOptions, Array.Empty<ElectronicMediaFormListItem>());
                _selectedElectronicMediaForm = null;
                OnPropertyChanged(nameof(SelectedElectronicMediaForm));
                ResetElectronicFields();
                if (IsNewBoxMode)
                {
                    await LoadSimulatedTargetLocationOptionsAsync().ConfigureAwait(true);
                }
            }
            else
            {
                ReplaceItems(SimulatedRecordItems, Array.Empty<SelectableSimulatedArchiveItemViewModel>());
                RefreshSimulatedRecordItemsPanel();
                await LoadExistingElectronicUnitsAsync(TargetProject, TargetYear).ConfigureAwait(true);
                if (myGeneration != Volatile.Read(ref _selectedRecordsChangedGeneration))
                {
                    return;
                }

                RebuildElectronicRecordItems();
                await PrefillElectronicFieldsFromSelectedRecordsAsync().ConfigureAwait(true);
                if (myGeneration != Volatile.Read(ref _selectedRecordsChangedGeneration))
                {
                    return;
                }
            }

            OnPropertyChanged(nameof(ElectronicApplicationFormNosText));
            UpdateSummaryText();
            RaiseElectronicStepFourPresentationChanged();
        }

        private void UpdateSummaryText()
        {
            string trackText = IsSimulatedTrack ? "模拟介质" : "电子介质";
            string modeText = BuildModeText();
            string itemText = IsSimulatedTrack
                ? $"，其中 {SimulatedRecordItems.Count(item => item.IsSelected)} 个资料子项待本次入盒"
                : $"，其中 {CountPendingElectronicMediaItemsForSubmit()} 个资料明细待本次入袋（整介质一并立档）";
            SummaryText = $"已选择 {SelectedRecords.Count} 份{trackText}资料{itemText}，准备{modeText}";
        }

        /// <summary>
        /// 提交/预览时取当前第一步所选电子介质表单（单条介质条目）。
        /// </summary>
        private IReadOnlyList<int> GetSelectedMediaEntryIdsForElectronicSubmit()
        {
            if (SelectedElectronicMediaForm == null || SelectedElectronicMediaForm.MediaEntryId <= 0)
            {
                return Array.Empty<int>();
            }

            return [SelectedElectronicMediaForm.MediaEntryId];
        }

        private int CountPendingElectronicMediaItemsForSubmit()
            => EnumeratePendingElectronicMediaItemRows().Count();

        /// <summary>
        /// 当前第一步所选电子介质下、尚未入袋的全部资料子项（第二步不可再拆选）。
        /// </summary>
        private IEnumerable<SelectableElectronicArchiveMediaViewModel> EnumeratePendingElectronicMediaItemRows()
        {
            if (SelectedElectronicMediaForm == null)
            {
                return Enumerable.Empty<SelectableElectronicArchiveMediaViewModel>();
            }

            int currentMediaEntryId = SelectedElectronicMediaForm.MediaEntryId;
            return ElectronicRecordItems
                .Where(item => item.MediaEntryId == currentMediaEntryId && item.CanSelect);
        }

        /// <summary>
        /// 兼容旧调用点：与 <see cref="EnumeratePendingElectronicMediaItemRows"/> 同义。
        /// </summary>
        private IEnumerable<SelectableElectronicArchiveMediaViewModel> EnumerateSelectedElectronicMediaEntryRows()
            => EnumeratePendingElectronicMediaItemRows();

        private void OnCurrentElectronicMediaFormChanged()
        {
            UpdateSummaryText();
            _ = PrefillElectronicFieldsFromSelectedRecordsAsync();
            _ = RebuildElectronicFilingDetailRowsAsync();
        }

        private string BuildModeText()
        {
            if (IsNewBoxMode)
            {
                return "新建立档容器";
            }

            var selectedContainer = GetSelectedContainer();
            var summary = selectedContainer?.ToSummary()
                ?? _existingContainerSummaries.FirstOrDefault();
            return summary.ToAppendModeText();
        }

        private IArchiveContainer? GetSelectedContainer()
        {
            return IsSimulatedTrack
                ? SelectedExistingBox
                : SelectedExistingElectronicUnit;
        }

        private Task LoadExistingBoxesAsync(string projectName, string year)
        {
            return LoadExistingContainersAsync(
                projectName,
                year,
                static (f, p, y) => f.GetExistingBoxesForProjectAsync(p, y),
                ExistingBoxes,
                item => SelectedExistingBox = item,
                ArchiveContainerKind.ArchiveBox,
                "查询已有案卷失败: ");
        }

        private Task LoadExistingElectronicUnitsAsync(string projectName, string year)
        {
            return LoadExistingElectronicUnitItemsAsync(projectName, year);
        }

        private async Task LoadExistingElectronicUnitItemsAsync(string projectName, string year)
        {
            List<YearlyElectronicArchiveUnit> units;
            List<ArchiveContainerSummary> summaries;

            try
            {
                using (IServiceScope scope = _scopeFactory.CreateScope())
                {
                    IArchiveFilingService filing = scope.ServiceProvider.GetRequiredService<IArchiveFilingService>();
                    units = await filing.GetExistingElectronicUnitsForProjectAsync(projectName, year).ConfigureAwait(false);
                    summaries = await filing.GetExistingContainerSummariesForProjectAsync(projectName, year, ArchiveContainerKind.ElectronicBag).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await RunOnUiAsync(() => MessageBox.Show("查询已有电子立档单元失败: " + ex.GetBaseException().Message)).ConfigureAwait(false);
                return;
            }

            await RunOnUiAsync(() =>
            {
                var items = units.Select(ExistingElectronicArchiveUnitListItem.From).ToList();
                ReplaceItems(ExistingElectronicUnits, items);
                SelectedExistingElectronicUnitItem = items.FirstOrDefault(item => item.CanSelectForAppend);
                _existingContainerSummaries = summaries;
            }).ConfigureAwait(false);
        }

        private async Task LoadExistingContainersAsync<TContainer>(
            string projectName,
            string year,
            Func<IArchiveFilingService, string, string, Task<List<TContainer>>> loadFromFiling,
            ObservableCollection<TContainer> target,
            Action<TContainer?> setSelected,
            ArchiveContainerKind containerKind,
            string errorPrefix)
            where TContainer : class, IArchiveContainer
        {
            List<TContainer> containers;
            List<ArchiveContainerSummary> summaries;

            try
            {
                using (IServiceScope scope = _scopeFactory.CreateScope())
                {
                    IArchiveFilingService filing = scope.ServiceProvider.GetRequiredService<IArchiveFilingService>();
                    containers = await loadFromFiling(filing, projectName, year).ConfigureAwait(false);
                    summaries = await filing.GetExistingContainerSummariesForProjectAsync(projectName, year, containerKind).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugSessionLog.WriteException("D", "LoadExistingContainersAsync.query", errorPrefix + "query failed", ex);
                // #endregion
                await RunOnUiAsync(() => MessageBox.Show(errorPrefix + ex.GetBaseException().Message)).ConfigureAwait(false);
                return;
            }

            try
            {
                await RunOnUiAsync(() =>
                {
                    ReplaceItems(target, containers);
                    // 新建模式下仅刷新可选盒列表，不改写当前选中，避免连带触发柜位重算与 DbContext 并发。
                    if (!IsNewBoxMode)
                    {
                        setSelected(target.FirstOrDefault());
                    }
                    else
                    {
                        setSelected(null);
                    }

                    _existingContainerSummaries = summaries;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugSessionLog.WriteException("B", "LoadExistingContainersAsync.ui", errorPrefix + "ui apply failed", ex);
                // #endregion
                await RunOnUiAsync(() => MessageBox.Show(errorPrefix + ex.GetBaseException().Message)).ConfigureAwait(false);
            }
        }

        private void RebuildSimulatedRecordItems()
        {
            var items = SelectedRecords
                .SelectMany(record => (record.MediaEntries ?? Enumerable.Empty<YearlyArchiveRegisterMedia>())
                    .Where(media => string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(media => (media.Items ?? Enumerable.Empty<YearlyArchiveRegisterMediaItem>())
                        .Select(item => new { Record = record, Media = media, Item = item })))
                .Select(entry =>
                {
                    var archiveLink = (entry.Item.ArchiveBoxLinks ?? Enumerable.Empty<YearlyArchiveBoxMediaItemLink>())
                        .Where(link => link.ArchiveBox != null)
                        .OrderByDescending(link => link.CreatedAt)
                        .FirstOrDefault();
                    bool isArchived = archiveLink?.ArchiveBox != null;
                    var viewModel = new SelectableSimulatedArchiveItemViewModel
                    {
                        MediaItemId = entry.Item.Id,
                        RecordId = entry.Record.Id,
                        FormNo = entry.Record.FormNo ?? string.Empty,
                        MaterialName = entry.Record.MaterialName ?? string.Empty,
                        MediaType = entry.Media.MediaType ?? string.Empty,
                        ItemType = entry.Item.ItemType ?? string.Empty,
                        ContentDesc = entry.Item.ContentDesc ?? string.Empty,
                        ContentCount = entry.Item.ContentCount,
                        Note = entry.Item.Note ?? string.Empty,
                        CanSelect = !isArchived,
                        ArchiveStatusText = isArchived ? "已入盒" : "未入盒",
                        ArchiveSequenceNo = archiveLink?.ArchiveBox?.ArchiveSequenceNo ?? string.Empty,
                        ArchiveLocationCode = archiveLink?.ArchiveBox?.BoxLocationCode ?? string.Empty,
                        IsSelected = !isArchived
                    };
                    viewModel.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName == nameof(SelectableSimulatedArchiveItemViewModel.IsSelected))
                        {
                            UpdateSummaryText();
                        }
                    };
                    return viewModel;
                })
                .OrderBy(item => item.FormNo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.MediaType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ContentDesc, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ReplaceItems(SimulatedRecordItems, items);
            RefreshSimulatedRecordItemsPanel();
            UpdateSummaryText();
        }

        private IEnumerable<(YearlyArchiveRegisterRecord Record, YearlyArchiveRegisterMedia Media)> EnumerateSelectedElectronicMediaEntries()
        {
            foreach (var record in SelectedRecords)
            {
                foreach (var media in record.MediaEntries ?? Enumerable.Empty<YearlyArchiveRegisterMedia>())
                {
                    if (string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return (record, media);
                    }
                }
            }
        }

        private List<ElectronicMediaFormListItem> BuildElectronicMediaFormOptionsList()
        {
            return EnumerateSelectedElectronicMediaEntries()
                .OrderBy(x => x.Record.FormNo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Media.MediaType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => ElectronicMediaItemSupport.BuildStoragePathSummary(x.Media), StringComparer.OrdinalIgnoreCase)
                .Select(x =>
                {
                    string code = x.Media.IsBorrowedHardDisk && !string.IsNullOrWhiteSpace(x.Media.BorrowedHardDiskCode)
                        ? x.Media.BorrowedHardDiskCode.Trim()
                        : "—";
                    int totalCount = x.Media.Items.Count > 0 ? x.Media.Items.Count : Math.Max(1, x.Media.MediaCount);
                    int archivedCount = x.Media.Items.Count(item => item.ElectronicArchiveUnitMediaItemLinks.Any());
                    if (archivedCount < 0)
                    {
                        archivedCount = 0;
                    }

                    if (archivedCount > totalCount)
                    {
                        archivedCount = totalCount;
                    }

                    string filingStatus = archivedCount == 0
                        ? "未启动立档"
                        : archivedCount >= totalCount
                            ? "已全部立档"
                            : "已部分立档";
                    string label = $"{x.Record.FormNo} | {x.Media.MediaType} | {x.Record.MaterialName} | {filingStatus}({archivedCount}/{totalCount})";
                    return new ElectronicMediaFormListItem
                    {
                        MediaEntryId = x.Media.Id,
                        FormNo = x.Record.FormNo,
                        MaterialName = x.Record.MaterialName ?? string.Empty,
                        MediaType = x.Media.MediaType,
                        Disposition = x.Media.Disposition ?? string.Empty,
                        MediumCode = code,
                        DisplayLabel = label,
                        ArchivedCount = archivedCount,
                        TotalCount = totalCount,
                        FilingStatus = filingStatus
                    };
                })
                .ToList();
        }

        private void SyncElectronicStepTwoRows()
        {
            if (_selectedElectronicMediaForm == null)
            {
                ReplaceItems(ElectronicRecordItemsStepTwo, Array.Empty<SelectableElectronicArchiveMediaViewModel>());
                RefreshElectronicRecordItemsStepTwoPanel();
                return;
            }

            int id = _selectedElectronicMediaForm.MediaEntryId;
            var rows = ElectronicRecordItems.Where(i => i.MediaEntryId == id).ToList();
            ReplaceItems(ElectronicRecordItemsStepTwo, rows);
            RefreshElectronicRecordItemsStepTwoPanel();
        }

        private void FinalizeElectronicMediaFormSelectionAfterRebuild(IReadOnlyList<ElectronicMediaFormListItem> options)
        {
            if (options.Count == 0)
            {
                if (_selectedElectronicMediaForm != null)
                {
                    _selectedElectronicMediaForm = null;
                    OnPropertyChanged(nameof(SelectedElectronicMediaForm));
                }

                SyncElectronicStepTwoRows();
                OnPropertyChanged(nameof(ElectronicStepOneSelectedDisposition));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedMediumCode));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingStatus));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingProgress));
                OnCurrentElectronicMediaFormChanged();
                return;
            }

            int? prevId = _selectedElectronicMediaForm?.MediaEntryId;
            var pick = prevId.HasValue ? options.FirstOrDefault(o => o.MediaEntryId == prevId.Value && o.CanSelectAsCurrent) : null;
            pick ??= options.FirstOrDefault(o => o.CanSelectAsCurrent) ?? options[0];

            _selectedElectronicMediaForm = pick;
            OnPropertyChanged(nameof(SelectedElectronicMediaForm));
            SyncElectronicStepTwoRows();
            OnPropertyChanged(nameof(ElectronicStepOneSelectedDisposition));
            OnPropertyChanged(nameof(ElectronicStepOneSelectedMediumCode));
            OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingStatus));
            OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingProgress));
            RefreshElectronicScenario();
            OnCurrentElectronicMediaFormChanged();
        }

        private bool TryGetRepresentativeElectronicRecordRow(out SelectableElectronicArchiveMediaViewModel? row)
        {
            row = EnumeratePendingElectronicMediaItemRows().FirstOrDefault();
            return row != null;
        }

        private bool ComputeElectronicStepFourSummaryOnly()
        {
            if (!IsElectronicTrack
                || !IsNewBoxMode
                || SelectedElectronicSubmissionMode != ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew
                || !IsElectronicHardDiskRetainedScenario)
            {
                return false;
            }

            if (!TryGetRepresentativeElectronicRecordRow(out var row) || row == null)
            {
                return false;
            }

            if (!row.IsBorrowedHardDisk || string.IsNullOrWhiteSpace(row.BorrowedHardDiskCode))
            {
                return false;
            }

            if (!string.Equals(SelectedRetainedHardDiskSource?.Trim(), ArchiveFilingBusinessRules.BorrowedHardDiskSourceOption, StringComparison.Ordinal))
            {
                return false;
            }

            if (_borrowedHardDiskReturnCandidate == null)
            {
                return false;
            }

            return string.Equals(_borrowedHardDiskReturnCandidate.DiskCode.Trim(), row.BorrowedHardDiskCode.Trim(), StringComparison.Ordinal);
        }

        private void RebuildElectronicRecordItems()
        {
            var items = EnumerateSelectedElectronicMediaEntries()
                .SelectMany(entry =>
                {
                    var orderedItems = (entry.Media.Items ?? Enumerable.Empty<YearlyArchiveRegisterMediaItem>())
                        .OrderBy(item => item.ItemType, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(item => item.ContentDesc, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(item => item.Id)
                        .ToList();

                    if (orderedItems.Count == 0)
                    {
                        return new[] { new { entry.Record, entry.Media, Item = (YearlyArchiveRegisterMediaItem?)null } };
                    }

                    return orderedItems.Select(item => new { entry.Record, entry.Media, Item = (YearlyArchiveRegisterMediaItem?)item });
                })
                .Select(entry =>
                {
                    var itemArchiveLink = entry.Item?.ElectronicArchiveUnitMediaItemLinks
                        ?.Where(link => link.ElectronicArchiveUnit != null)
                        .OrderByDescending(link => link.CreatedAt)
                        .FirstOrDefault();
                    var mediaArchiveLink = entry.Media.ElectronicArchiveUnitLinks
                        .Where(link => link.ElectronicArchiveUnit != null)
                        .OrderByDescending(link => link.CreatedAt)
                        .FirstOrDefault();
                    bool isArchived = itemArchiveLink?.ElectronicArchiveUnit != null
                        || (entry.Item == null && mediaArchiveLink?.ElectronicArchiveUnit != null);
                    ElectronicMediaContentPathLine displayLine = entry.Item != null
                        ? ElectronicMediaItemSupport.ResolveMediaItemDisplayContentPathLine(entry.Item)
                        : ElectronicMediaItemSupport.CollectMediaContentPathLines(entry.Media).FirstOrDefault();
                    int mediaItemId = entry.Item?.Id ?? 0;

                    var viewModel = new SelectableElectronicArchiveMediaViewModel
                    {
                        MediaEntryId = entry.Media.Id,
                        MediaItemId = mediaItemId,
                        RecordId = entry.Record.Id,
                        FormNo = entry.Record.FormNo,
                        MaterialName = entry.Record.MaterialName ?? string.Empty,
                        MediaType = entry.Media.MediaType,
                        MaterialCategory = entry.Item?.ElectronicDetail?.MaterialCategory?.Trim() ?? string.Empty,
                        SubCategory = entry.Item?.ElectronicDetail?.SubCategory?.Trim() ?? string.Empty,
                        DataOrganizationForm = entry.Item?.ElectronicDetail?.DataOrganizationForm?.Trim() ?? string.Empty,
                        MediaCount = entry.Item != null
                            ? Math.Max(1, entry.Item.ContentCount)
                            : Math.Max(1, entry.Media.MediaCount),
                        DataSizeMb = entry.Item != null
                            ? ElectronicMediaItemSupport.ResolveMediaItemDataSizeMb(entry.Item)
                            : ElectronicMediaItemSupport.ResolveMediaDataSizeMb(entry.Media),
                        StoragePath = displayLine.StoragePath,
                        Disposition = entry.Media.Disposition,
                        ItemName = displayLine.ItemName,
                        CanSelect = !isArchived,
                        ArchiveStatusText = isArchived ? "已入袋" : "未入袋",
                        ElectronicArchiveNo = itemArchiveLink?.ElectronicArchiveUnit?.ElectronicArchiveNo
                            ?? mediaArchiveLink?.ElectronicArchiveUnit?.ElectronicArchiveNo
                            ?? string.Empty,
                        LinkedMediumCodes = itemArchiveLink?.ElectronicArchiveUnit?.LinkedMediumCodes
                            ?? mediaArchiveLink?.ElectronicArchiveUnit?.LinkedMediumCodes
                            ?? string.Empty,
                        IsBorrowedHardDisk = entry.Media.IsBorrowedHardDisk,
                        BorrowedHardDiskCode = entry.Media.BorrowedHardDiskCode ?? string.Empty,
                        IsSelected = !isArchived
                    };
                    return viewModel;
                })
                .OrderBy(item => item.FormNo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.MediaType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.StoragePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ReplaceItems(ElectronicRecordItems, items);
            UpdateSummaryText();
            var formOptions = BuildElectronicMediaFormOptionsList();
            ReplaceItems(ElectronicMediaFormOptions, formOptions);
            FinalizeElectronicMediaFormSelectionAfterRebuild(formOptions);
            _ = RebuildElectronicFilingDetailRowsAsync();
        }
    }
}
