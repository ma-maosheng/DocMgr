using DocMgr.Infrastructure.AgentDebugLogging;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
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

            bool applied = false;
            await RunOnUiAsync(() =>
            {
                if (myGeneration != Volatile.Read(ref _selectedRecordsChangedGeneration))
                {
                    return;
                }

                _selectedRecords = refreshedRecords;
                OnPropertyChanged(nameof(SelectedRecords));
                OnPropertyChanged(nameof(ElectronicApplicationFormNosText));
                OnPropertyChanged(nameof(ArchivePurposeText));
                applied = true;
            }).ConfigureAwait(true);

            return applied;
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
                OnPropertyChanged(nameof(ArchivePurposeText));
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
                OnPropertyChanged(nameof(ArchivePurposeText));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedDisposition));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedMediumCode));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingStatus));
                OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingProgress));
                RaiseElectronicStepFourPresentationChanged();
                return;
            }

            var first = SelectedRecords.First();
            string implementYear = await ResolveSelectedRecordImplementYearAsync(first).ConfigureAwait(true);
            if (myGeneration != Volatile.Read(ref _selectedRecordsChangedGeneration))
            {
                return;
            }

            TargetProject = ArchiveFilingBusinessRules.ResolveElectronicArchiveProjectName(first);
            TargetYear = implementYear;

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
                await LoadExistingBoxesAsync(TargetProject, DateTime.Now.Year.ToString()).ConfigureAwait(true);
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
            OnPropertyChanged(nameof(ArchivePurposeText));
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
            string materialProgress = BuildVisibleMaterialProgressText();
            SummaryText = $"已选择 {SelectedRecords.Count} 份{trackText}资料{itemText}，准备{modeText}";
            if (materialProgress.Length > 0)
            {
                SummaryText += "。" + materialProgress;
            }
        }

        /// <summary>一介质含多份资料名称时，标出每份资料已立档的子项数。</summary>
        private static string BuildMaterialFilingProgressText(YearlyArchiveRegisterMedia media)
        {
            var groups = (media.Items ?? [])
                .Select(item => new
                {
                    Name = ArchiveRegisterMaterialSummarySupport.ResolveItemMaterialName(item),
                    Archived = item.ElectronicArchiveUnitMediaItemLinks.Any()
                })
                .Where(item => item.Name.Length > 0)
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (groups.Count <= 1)
            {
                return string.Empty;
            }

            return string.Join("，", groups.Select(group => $"{group.Key} {group.Count(item => item.Archived)}/{group.Count()}"));
        }

        private string BuildVisibleMaterialProgressText()
        {
            IEnumerable<(string Name, bool Archived)> rows = IsSimulatedTrack
                ? SimulatedRecordItems.Select(item => (item.MaterialName?.Trim() ?? string.Empty, !item.CanSelect))
                : ElectronicRecordItems.Select(item => (item.MaterialName?.Trim() ?? string.Empty, !item.CanSelect));
            var groups = rows
                .Where(row => row.Name.Length > 0)
                .GroupBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (groups.Count <= 1)
            {
                return string.Empty;
            }

            return string.Join("，", groups.Select(group =>
                $"{group.Key} 未立档 {group.Count(row => !row.Archived)}/{group.Count()}"));
        }

        private static string BuildElectronicContentSummary(IReadOnlyList<SelectableElectronicArchiveMediaViewModel> items)
        {
            var named = items
                .Select(item => (
                    Material: item.MaterialName?.Trim() ?? string.Empty,
                    Item: item.ItemName?.Trim() ?? string.Empty))
                .Where(row => row.Item.Length > 0)
                .ToList();
            if (named.Count == 0)
            {
                return string.Empty;
            }

            var groups = named
                .GroupBy(row => row.Material, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (groups.Count <= 1)
            {
                return string.Join("；", named.Select(row => row.Item).Distinct(StringComparer.Ordinal));
            }

            return string.Join("；", groups.Select(group =>
            {
                string material = string.IsNullOrWhiteSpace(group.Key) ? "未命名资料" : group.Key;
                string itemNames = string.Join("、", group.Select(row => row.Item).Distinct(StringComparer.Ordinal));
                return $"{material}：{itemNames}";
            }));
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
            ApplyAppendHardDiskFilter();
            _pageDb.RunInBackground(PrefillElectronicFieldsFromSelectedRecordsAsync);
            _pageDb.RunInBackground(RebuildElectronicFilingDetailRowsAsync);
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

        private readonly List<HardDiskAppendCandidate> _appendHardDiskCandidates = new();

        private int _appendMatchProjectId;

        private string _appendMatchProjectName = string.Empty;

        private string _appendMatchImplementYear = string.Empty;

        private Task LoadExistingElectronicUnitsAsync(string projectName, string year)
        {
            return LoadExistingElectronicUnitItemsAsync(projectName, year);
        }

        private async Task LoadExistingElectronicUnitItemsAsync(string projectName, string year)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IArchiveFilingService filing = scope.ServiceProvider.GetRequiredService<IArchiveFilingService>();
                int projectId = SelectedRecords.FirstOrDefault()?.ProjectId ?? 0;
                List<HardDiskAppendCandidate> candidates = await filing.GetHardDiskAppendCandidatesAsync(
                    projectId > 0 ? projectId : null,
                    projectName,
                    null).ConfigureAwait(false);
                List<ArchiveContainerSummary> summaries = await filing.GetExistingContainerSummariesForProjectAsync(projectName, year, ArchiveContainerKind.ElectronicBag).ConfigureAwait(false);
                string implementYear = await ResolveAppendMatchImplementYearAsync(scope.ServiceProvider, projectId, year).ConfigureAwait(false);
                await RunOnUiAsync(() =>
                {
                    _appendHardDiskCandidates.Clear();
                    _appendHardDiskCandidates.AddRange(candidates);
                    _appendMatchProjectId = projectId;
                    _appendMatchProjectName = projectName?.Trim() ?? string.Empty;
                    _appendMatchImplementYear = implementYear;
                    _existingContainerSummaries = summaries;
                    ApplyAppendHardDiskFilter();
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await RunOnUiAsync(() => MessageBox.Show("查询已有电子立档单元失败: " + ex.GetBaseException().Message)).ConfigureAwait(false);
            }
        }

        private async Task<string> ResolveSelectedRecordImplementYearAsync(YearlyArchiveRegisterRecord record)
        {
            int projectId = record.ProjectId ?? 0;
            string fallback = record.CreatedDate.Year.ToString();
            if (projectId <= 0)
            {
                return fallback;
            }

            using IServiceScope scope = _scopeFactory.CreateScope();
            return await ResolveAppendMatchImplementYearAsync(scope.ServiceProvider, projectId, fallback).ConfigureAwait(true);
        }

        private static async Task<string> ResolveAppendMatchImplementYearAsync(IServiceProvider services, int projectId, string fallbackYear)
        {
            if (projectId <= 0)
            {
                return fallbackYear?.Trim() ?? string.Empty;
            }

            IArchiveFilingFactRepository facts = services.GetRequiredService<IArchiveFilingFactRepository>();
            IReadOnlyDictionary<int, string> years = await facts.GetProjectImplementYearsByIdsAsync([projectId]).ConfigureAwait(false);
            if (years.TryGetValue(projectId, out string? implementYear) && !string.IsNullOrWhiteSpace(implementYear))
            {
                return implementYear.Trim();
            }

            return fallbackYear?.Trim() ?? string.Empty;
        }

        private void ClearAppendHardDiskCandidates()
        {
            _appendHardDiskCandidates.Clear();
            _appendMatchProjectId = 0;
            _appendMatchProjectName = string.Empty;
            _appendMatchImplementYear = string.Empty;
        }

        /// <summary>
        /// 按同一项目名称、同一实施年度收窄可并入硬盘，并在当前选中项落出范围时改选可用容量足够的硬盘。
        /// </summary>
        private void ApplyAppendHardDiskFilter()
        {
            if (_appendHardDiskCandidates.Count == 0)
            {
                return;
            }

            decimal requiredMb = EnumeratePendingElectronicMediaItemRows().Sum(row => row.DataSizeMb);
            int? selectedId = _selectedExistingElectronicUnitItem?.Unit.Id;
            int? predeterminedId = GetPredeterminedAppendUnitId();

            var items = _appendHardDiskCandidates
                .Where(candidate => IncludeAppendCandidate(candidate, predeterminedId))
                .Select(candidate => ExistingElectronicArchiveUnitListItem.From(
                    candidate.Unit,
                    candidate.BelongsToCurrentProject,
                    CandidateMatchesYear(candidate),
                    requiredMb))
                .OrderByDescending(item => item.CanHoldPendingBatch)
                .ThenByDescending(item => item.BelongsToCurrentProject)
                .ThenByDescending(item => item.BelongsToCurrentYear)
                .ThenByDescending(item => item.AvailableCapacityMb)
                .ThenBy(item => item.ElectronicArchiveNo, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (predeterminedId is int keepId && items.All(item => item.Unit.Id != keepId))
            {
                ExistingElectronicArchiveUnitListItem? forced = ExistingElectronicUnits.FirstOrDefault(item => item.Unit.Id == keepId);
                if (forced != null)
                {
                    items.Add(forced);
                }
            }

            ReplaceItems(ExistingElectronicUnits, items);

            if (_deferConsoleAutoSelection)
            {
                SelectedExistingElectronicUnitItem = null;
                return;
            }

            if (predeterminedId is int forcedId)
            {
                ExistingElectronicArchiveUnitListItem? forced = items.FirstOrDefault(item => item.Unit.Id == forcedId && item.CanSelectForAppend);
                if (forced != null)
                {
                    SelectedExistingElectronicUnitItem = forced;
                    return;
                }
            }

            ExistingElectronicArchiveUnitListItem? kept = selectedId is int id
                ? items.FirstOrDefault(item => item.Unit.Id == id && item.CanSelectForAppend)
                : null;
            if (kept != null)
            {
                SelectedExistingElectronicUnitItem = kept;
                return;
            }

            SelectedExistingElectronicUnitItem = items.FirstOrDefault(item => item.CanSelectForAppend && item.CanHoldPendingBatch);
        }

        private int? GetPredeterminedAppendUnitId()
        {
            return EnumeratePendingElectronicMediaItemRows()
                .Where(item => item.IsOutboundArchiveFiling
                    && !item.UseInStockBlankHardDisk
                    && item.AppendTargetElectronicUnitId is > 0)
                .Select(item => item.AppendTargetElectronicUnitId)
                .FirstOrDefault();
        }

        private List<AppendHardDiskPickerRow> BuildAppendHardDiskPickerRows()
        {
            decimal requiredMb = EnumeratePendingElectronicMediaItemRows().Sum(row => row.DataSizeMb);
            int? predeterminedId = GetPredeterminedAppendUnitId();
            return _appendHardDiskCandidates
                .Select(candidate =>
                {
                    ExistingElectronicArchiveUnitListItem item = ExistingElectronicArchiveUnitListItem.From(
                        candidate.Unit,
                        candidate.BelongsToCurrentProject,
                        CandidateMatchesYear(candidate),
                        requiredMb);
                    return new AppendHardDiskPickerRow
                    {
                        UnitId = candidate.Unit.Id,
                        GroupLabel = item.GroupLabel,
                        ElectronicArchiveNo = item.ElectronicArchiveNo,
                        DiskCode = item.LinkedMediumCodesDisplay,
                        YearProjectText = FormatYearProjectText(candidate),
                        AvailableCapacityText = item.AvailableCapacityText,
                        AvailableCapacityMb = item.AvailableCapacityMb,
                        StorageLocation = item.StorageLocation,
                        Remarks = item.Remarks,
                        CanSelect = item.CanSelectForAppend,
                        CanHoldPendingBatch = item.CanHoldPendingBatch,
                        BelongsToCurrentProject = item.BelongsToCurrentProject,
                        BelongsToCurrentYear = item.BelongsToCurrentYear,
                        MatchesProjectFilter = CandidateMatchesProjectName(candidate),
                        MatchesYearFilter = CandidateMatchesImplementYearOnly(candidate),
                        MatchesProjectYear = CandidateMatchesProjectName(candidate) && CandidateMatchesImplementYearOnly(candidate),
                        IsPredetermined = predeterminedId == candidate.Unit.Id
                    };
                })
                .ToList();
        }

        private bool IncludeAppendCandidate(HardDiskAppendCandidate candidate, int? predeterminedId)
        {
            if (predeterminedId is int keepId && candidate.Unit.Id == keepId)
            {
                return true;
            }

            if (AppendSameProjectOnly && !CandidateMatchesProjectName(candidate))
            {
                return false;
            }

            if (AppendSameYearOnly && !CandidateMatchesImplementYearOnly(candidate))
            {
                return false;
            }

            return true;
        }

        private static string FormatYearProjectText(HardDiskAppendCandidate candidate)
        {
            List<string> labels = candidate.Projects
                .Select(project => FormatYearProjectLabel(project.ImplementYear, project.ProjectName))
                .Where(label => label.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToList();
            return labels.Count == 0 ? "—" : string.Join("、", labels);
        }

        private static string FormatYearProjectLabel(string? implementYear, string? projectName)
        {
            string year = implementYear?.Trim() ?? string.Empty;
            string name = projectName?.Trim() ?? string.Empty;
            if (year.Length == 0)
            {
                return name;
            }

            return name.Length == 0 ? year : $"{year}-{name}";
        }

        /// <summary>硬盘上任一项目的名称与当前申请相同。不看实施年度，也不按项目编号比对。</summary>
        private bool CandidateMatchesProjectName(HardDiskAppendCandidate candidate)
        {
            if (string.IsNullOrWhiteSpace(_appendMatchProjectName))
            {
                return false;
            }

            return candidate.Projects.Any(project => ProjectNameMatches(project.ProjectName, _appendMatchProjectName));
        }

        /// <summary>硬盘上任一项目的实施年度与当前申请相同。不看项目名称。</summary>
        private bool CandidateMatchesImplementYearOnly(HardDiskAppendCandidate candidate)
        {
            if (string.IsNullOrWhiteSpace(_appendMatchImplementYear))
            {
                return false;
            }

            return candidate.Projects.Any(project => YearMatches(project.ImplementYear, _appendMatchImplementYear));
        }

        private bool CandidateMatchesProject(HardDiskAppendCandidate candidate)
        {
            if (_appendMatchProjectId > 0)
            {
                return candidate.Projects.Any(project => project.ProjectId == _appendMatchProjectId);
            }

            return candidate.Projects.Any(project => ProjectNameMatches(project.ProjectName, _appendMatchProjectName));
        }

        private bool CandidateMatchesYear(HardDiskAppendCandidate candidate)
        {
            if (string.IsNullOrWhiteSpace(_appendMatchImplementYear))
            {
                return true;
            }

            return candidate.Projects.Any(project => YearMatches(project.ImplementYear, _appendMatchImplementYear));
        }

        private bool CandidateMatchesProjectYear(HardDiskAppendCandidate candidate)
        {
            if (string.IsNullOrWhiteSpace(_appendMatchImplementYear))
            {
                return candidate.BelongsToCurrentProject || CandidateMatchesProject(candidate);
            }

            if (_appendMatchProjectId > 0)
            {
                return candidate.Projects.Any(project =>
                    project.ProjectId == _appendMatchProjectId
                    && YearMatches(project.ImplementYear, _appendMatchImplementYear));
            }

            return candidate.Projects.Any(project =>
                ProjectNameMatches(project.ProjectName, _appendMatchProjectName)
                && YearMatches(project.ImplementYear, _appendMatchImplementYear));
        }

        private static bool ProjectNameMatches(string? left, string? right)
            => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static bool YearMatches(string? diskYear, string targetYear)
        {
            string left = diskYear?.Trim() ?? string.Empty;
            string right = targetYear.Trim();
            if (left.Length == 0 || right.Length == 0)
            {
                return false;
            }

            return string.Equals(left, right, StringComparison.Ordinal)
                || left.StartsWith(right, StringComparison.Ordinal)
                || right.StartsWith(left, StringComparison.Ordinal);
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
                        MaterialName = ArchiveRegisterMaterialSummarySupport.ResolveItemMaterialName(entry.Item),
                        MediaType = entry.Media.MediaType ?? string.Empty,
                        MaterialCategory = SimulatedMediaItemClassificationSupport.ResolveMaterialCategory(entry.Item),
                        SubCategory = SimulatedMediaItemClassificationSupport.ResolveSubCategory(entry.Item),
                        DataOrganizationForm = SimulatedMediaItemClassificationSupport.ResolveOrganizationFormDisplay(entry.Item),
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
                .ThenBy(item => item.MaterialName, StringComparer.OrdinalIgnoreCase)
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
                    string materialNames = ArchiveRegisterMaterialSummarySupport.Build([x.Media]);
                    if (string.IsNullOrWhiteSpace(materialNames))
                    {
                        materialNames = x.Record.MaterialName ?? string.Empty;
                    }

                    string materialProgress = BuildMaterialFilingProgressText(x.Media);
                    string label = $"{x.Record.FormNo} | {x.Media.MediaType} | {materialNames} | {filingStatus}({archivedCount}/{totalCount})";
                    if (materialProgress.Length > 0)
                    {
                        label += $"（{materialProgress}）";
                    }

                    return new ElectronicMediaFormListItem
                    {
                        MediaEntryId = x.Media.Id,
                        FormNo = x.Record.FormNo,
                        MaterialName = materialNames,
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
                        .OrderBy(item => item.ContentDesc, StringComparer.OrdinalIgnoreCase)
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
                    // 子项优先按自身立档链接判断；无子项条目按整体投影（全部子项已入袋才视为已立档，无子项视为未立档）。
                    var archiveLink = entry.Item?.ElectronicArchiveUnitMediaItemLinks
                        ?.Where(link => ElectronicMediumCatalog.FindCurrentBag(link) != null)
                        .OrderByDescending(link => link.CreatedAt)
                        .FirstOrDefault();
                    bool isArchived = entry.Item != null
                        ? archiveLink != null
                        : entry.Media.Items.Count > 0
                            && entry.Media.Items.All(item => item.ElectronicArchiveUnitMediaItemLinks.Any());
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
                        ProjectId = entry.Record.ProjectId ?? 0,
                        ProjectName = entry.Record.ProjectName?.Trim() ?? string.Empty,
                        MaterialName = entry.Item == null
                            ? ArchiveRegisterMaterialSummarySupport.Build([entry.Media])
                            : ArchiveRegisterMaterialSummarySupport.ResolveItemMaterialName(entry.Item),
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
                        ElectronicArchiveNo = (archiveLink == null ? null : ElectronicMediumCatalog.FindCurrentBag(archiveLink)?.ElectronicArchiveNo) ?? string.Empty,
                        LinkedMediumCodes = (archiveLink == null ? null : ElectronicMediumCatalog.FindCurrentBag(archiveLink)?.LinkedMediumCodes) ?? string.Empty,
                        IsBorrowedHardDisk = entry.Media.IsBorrowedHardDisk,
                        BorrowedHardDiskCode = entry.Media.BorrowedHardDiskCode ?? string.Empty,
                        IsOutboundArchiveFiling = entry.Record.SourceNetworkOutboundRecordId is > 0
                            && string.Equals(
                                entry.Media.Disposition?.Trim(),
                                ArchiveRegisterDomainValues.ElectronicDispositionNone,
                                StringComparison.Ordinal),
                        UseInStockBlankHardDisk = entry.Media.UseInStockBlankHardDisk,
                        RequisitionedHardDiskCode = entry.Media.RequisitionedHardDiskCode ?? string.Empty,
                        AppendTargetElectronicUnitId = entry.Media.AppendTargetElectronicUnitId,
                        AppendTargetHardDiskCode = entry.Media.AppendTargetHardDiskCode ?? string.Empty,
                        IsSelected = !isArchived
                    };
                    return viewModel;
                })
                .OrderBy(item => item.MaterialName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.MaterialCategory, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.SubCategory, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DataOrganizationForm, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.StoragePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ReplaceItems(ElectronicRecordItems, items);
            UpdateSummaryText();
            var formOptions = BuildElectronicMediaFormOptionsList();
            ReplaceItems(ElectronicMediaFormOptions, formOptions);
            FinalizeElectronicMediaFormSelectionAfterRebuild(formOptions);
            _pageDb.RunInBackground(RebuildElectronicFilingDetailRowsAsync);
        }
    }
}
