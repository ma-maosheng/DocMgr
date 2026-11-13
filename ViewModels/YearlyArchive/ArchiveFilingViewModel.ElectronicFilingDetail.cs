using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.ViewModels.YearlyArchive
{
    public partial class ArchiveFilingViewModel
    {
        private readonly Dictionary<int, string> _pendingFilingStoragePaths = new();

        public ObservableCollection<ElectronicFilingDetailRowViewModel> ElectronicFilingExistingDetailRows { get; } = new();

        public ObservableCollection<ElectronicFilingDetailRowViewModel> ElectronicFilingPendingDetailRows { get; } = new();

        public string ElectronicFilingTargetMediumSummary { get; private set; } = string.Empty;

        public string ElectronicFilingMediumTotalCapacityText { get; private set; } = "—";

        public string ElectronicFilingMediumAvailableCapacityText { get; private set; } = "—";

        /// <summary>第四步介质容量说明：可用容量（总容量，已占用）。已占用含本批拟立档数据量。</summary>
        public string ElectronicFilingMediumCapacityDescription { get; private set; } = "—";

        private string _copyTargetHardDiskCapacityDescription = string.Empty;
        private string _selectedExistingHardDiskCapacityDescription = string.Empty;
        private int _copyTargetHardDiskCapacityVersion;
        private int _selectedExistingHardDiskCapacityVersion;

        /// <summary>新建硬盘立档时所选库内空盘的可用容量说明。</summary>
        public string CopyTargetHardDiskCapacityDescription
        {
            get => _copyTargetHardDiskCapacityDescription;
            private set => SetProperty(ref _copyTargetHardDiskCapacityDescription, value);
        }

        /// <summary>所选已立档硬盘袋的可用容量说明。</summary>
        public string SelectedExistingHardDiskCapacityDescription
        {
            get => _selectedExistingHardDiskCapacityDescription;
            private set => SetProperty(ref _selectedExistingHardDiskCapacityDescription, value);
        }

        /// <summary>
        /// 原光盘直接留袋时不可编辑立档路径；拷贝型立档与原硬盘直接立档可编辑。
        /// </summary>
        public bool IsElectronicFilingStoragePathEditable =>
            SelectedElectronicSubmissionMode != ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew;

        private IReadOnlyList<int> GetSelectedMediaItemIdsForElectronicSubmit()
        {
            return EnumerateSelectedElectronicMediaEntryRows()
                .Select(item => item.MediaItemId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        private IReadOnlyDictionary<int, string> BuildFilingStoragePathByMediaItemId()
        {
            var map = new Dictionary<int, string>();
            foreach (var row in ElectronicFilingPendingDetailRows)
            {
                if (row.MediaItemId <= 0)
                {
                    continue;
                }

                map[row.MediaItemId] = row.FilingStoragePath?.Trim() ?? string.Empty;
            }

            return map;
        }

        private async Task RebuildElectronicFilingDetailRowsAsync()
        {
            if (!IsElectronicTrack)
            {
                ReplaceItems(ElectronicFilingExistingDetailRows, Array.Empty<ElectronicFilingDetailRowViewModel>());
                ReplaceItems(ElectronicFilingPendingDetailRows, Array.Empty<ElectronicFilingDetailRowViewModel>());
                RefreshElectronicFilingExistingDetailRowsPanel();
                RefreshElectronicFilingPendingDetailRowsPanel();
                return;
            }

            string mediumCode = ResolveElectronicFilingTargetMediumCode();
            ElectronicFilingTargetMediumSummary = BuildElectronicFilingTargetMediumSummary(mediumCode);

            List<ElectronicFilingDetailRowViewModel> existingRows = new();
            IReadOnlyDictionary<int, string> implementYears = new Dictionary<int, string>();
            bool pathEditable = IsElectronicFilingStoragePathEditable;
            var pendingSources = EnumeratePendingElectronicMediaItemRows().ToList();
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                IArchiveFilingRepository repository = scope.ServiceProvider.GetRequiredService<IArchiveFilingRepository>();
                List<YearlyElectronicArchiveUnitMediaItemLink> links = new();
                if (IsAppendMode && SelectedExistingElectronicUnit != null)
                {
                    links = await repository.GetElectronicArchiveUnitMediaItemLinksByUnitIdAsync(SelectedExistingElectronicUnit.Id)
                        .ConfigureAwait(false);
                }
                else if (!IsPendingMediumCode(mediumCode))
                {
                    links = await repository.GetElectronicArchiveUnitMediaItemLinksByMediumCodeAsync(mediumCode)
                        .ConfigureAwait(false);
                }

                var projectIds = links
                    .Select(link => link.MediaItem?.MediaEntry?.RegisterRecord?.ProjectId ?? 0)
                    .Concat(pendingSources.Select(source => source.ProjectId))
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();
                IArchiveFilingFactRepository factRepository = scope.ServiceProvider.GetRequiredService<IArchiveFilingFactRepository>();
                implementYears = await factRepository.GetProjectImplementYearsByIdsAsync(projectIds).ConfigureAwait(false);
                existingRows = links.Select(link => MapExistingFilingDetailRow(link, implementYears)).ToList();

                await UpdateElectronicFilingMediumCapacityAsync(repository, mediumCode, existingRows).ConfigureAwait(false);
            }
            var occupiedFolderNamesByMaterial = existingRows
                .GroupBy(row => row.MaterialName?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyCollection<string>)group
                        .Select(row => ElectronicFilingStoragePathSupport.ExtractItemFolderName(row.FilingStoragePath))
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);
            var folderNameByMediaItemId = ElectronicFilingStoragePathSupport.BuildItemFolderNameByMediaItemId(
                pendingSources.Select(source => new ElectronicFilingStoragePathSupport.PendingFilingStoragePathItem(
                    source.MediaItemId,
                    source.FormNo,
                    source.MaterialName,
                    source.ItemName)),
                occupiedFolderNamesByMaterial);

            var pendingRows = pendingSources
                .Select(row => CreatePendingFilingDetailRow(row, mediumCode, pathEditable, folderNameByMediaItemId, implementYears))
                .OrderBy(row => row.MaterialName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.ItemName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.MaterialCategory, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.SubCategory, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.DataOrganizationForm, StringComparer.OrdinalIgnoreCase)
                .ToList();
            existingRows = existingRows
                .OrderBy(row => row.MaterialName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.ItemName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.MaterialCategory, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.SubCategory, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.DataOrganizationForm, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await RunOnUiAsync(() =>
            {
                ReplaceItems(ElectronicFilingExistingDetailRows, existingRows);
                ReplaceItems(ElectronicFilingPendingDetailRows, pendingRows);
                RefreshElectronicFilingExistingDetailRowsPanel();
                RefreshElectronicFilingPendingDetailRowsPanel();
                OnPropertyChanged(nameof(ElectronicFilingTargetMediumSummary));
                OnPropertyChanged(nameof(ElectronicFilingMediumTotalCapacityText));
                OnPropertyChanged(nameof(ElectronicFilingMediumAvailableCapacityText));
                OnPropertyChanged(nameof(ElectronicFilingMediumCapacityDescription));
                OnPropertyChanged(nameof(IsElectronicFilingStoragePathEditable));
                SyncElectronicStoragePathFromPendingFilingRows();
            }).ConfigureAwait(false);
        }

        private static bool IsPendingMediumCode(string mediumCode)
            => string.IsNullOrWhiteSpace(mediumCode)
               || mediumCode.StartsWith("待", StringComparison.Ordinal);

        private string ResolveElectronicFilingTargetMediumCode()
        {
            if (IsAppendMode && SelectedExistingElectronicUnit != null)
            {
                if (!string.IsNullOrWhiteSpace(SelectedExistingElectronicUnit.LinkedMediumCodes))
                {
                    return SelectedExistingElectronicUnit.LinkedMediumCodes.Trim();
                }

                return SelectedExistingElectronicUnit.ElectronicArchiveNo?.Trim() ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(ElectronicLinkedMediumCodes))
            {
                return ElectronicLinkedMediumCodes.Trim();
            }

            if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew)
            {
                return !string.IsNullOrWhiteSpace(ElectronicArchiveNo)
                    ? ElectronicArchiveNo.Trim()
                    : "待步骤8确定";
            }

            if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew
                && !string.IsNullOrWhiteSpace(SelectedElectronicMediaForm?.MediumCode)
                && !string.Equals(SelectedElectronicMediaForm.MediumCode.Trim(), "—", StringComparison.Ordinal))
            {
                return SelectedElectronicMediaForm.MediumCode.Trim();
            }

            return "待后续步骤确定";
        }

        private string BuildElectronicFilingTargetMediumSummary(string mediumCode)
        {
            string carrier = ElectronicStorageCarrierType;
            if (string.IsNullOrWhiteSpace(carrier))
            {
                carrier = SelectedElectronicSubmissionMode?.ToString() ?? "—";
            }

            string mode = IsAppendMode ? DescribeAppendBagRelation() : "新建袋";
            if (!TryDescribeSelectedHardDiskCabinet(out string location, out string bag))
            {
                return $"拟使用介质：{mediumCode} ｜ 载体：{carrier} ｜ 方式：{mode}";
            }

            return $"拟使用介质：{mediumCode} ｜ 位置：{location} ｜ 介质袋：{bag} ｜ 载体：{carrier} ｜ 方式：{mode}";
        }

        private void RefreshElectronicFilingTargetMediumSummary()
        {
            if (!IsElectronicTrack)
            {
                return;
            }

            ElectronicFilingTargetMediumSummary = BuildElectronicFilingTargetMediumSummary(ResolveElectronicFilingTargetMediumCode());
            OnPropertyChanged(nameof(ElectronicFilingTargetMediumSummary));
        }

        private void NotifyHardDiskCabinetLocateText()
        {
            OnPropertyChanged(nameof(BlankHardDiskCabinetLocateText));
            OnPropertyChanged(nameof(SelectedAppendHardDiskCabinetLocateText));
            RefreshElectronicFilingTargetMediumSummary();
        }

        /// <summary>
        /// 硬盘编号、档口和介质袋，供操作者到磁盘柜取盘。
        /// </summary>
        private static string BuildHardDiskCabinetLocateText(string? mediumCode, string? storageLocation, string bag)
        {
            if (string.IsNullOrWhiteSpace(mediumCode))
            {
                return string.Empty;
            }

            return $"{mediumCode.Trim()} ｜ 位置：{DisplayCabinetLocation(storageLocation)} ｜ 介质袋：{bag}";
        }

        private bool TryDescribeSelectedHardDiskCabinet(out string location, out string bag)
        {
            location = string.Empty;
            bag = string.Empty;
            if (SelectedElectronicSubmissionMode is ElectronicArchiveSubmissionMode.CopyAppendExistingHardDisk
                    or ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk
                && SelectedExistingElectronicUnitItem is { CanSelectForAppend: true } item)
            {
                location = DisplayCabinetLocation(item.StorageLocation);
                bag = DescribeElectronicBag(item.Unit.StorageCarrierType, item.ElectronicArchiveNo);
                return true;
            }

            if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.CopyNewHardDisk
                && !string.IsNullOrWhiteSpace(ElectronicLinkedMediumCodes))
            {
                location = DisplayCabinetLocation(ElectronicOriginalStorageLocation);
                bag = "尚无";
                return true;
            }

            return false;
        }

        private static string DescribeElectronicBag(string? carrierType, string? archiveNo)
        {
            string carrier = string.IsNullOrWhiteSpace(carrierType) ? "硬盘袋" : carrierType.Trim();
            string number = archiveNo?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(number) ? carrier : $"{carrier} {number}";
        }

        private static string DisplayCabinetLocation(string? storageLocation)
            => string.IsNullOrWhiteSpace(storageLocation) ? "未登记" : storageLocation.Trim();

        /// <summary>
        /// 按项目名称、实施年度分别对照本次拟立档资料，并写出硬盘上对应的年度-项目。
        /// </summary>
        private string DescribeAppendBagRelation()
        {
            if (SelectedExistingElectronicUnitItem == null)
            {
                return "并入既有袋";
            }

            HardDiskAppendCandidate? candidate = _appendHardDiskCandidates
                .FirstOrDefault(item => item.Unit.Id == SelectedExistingElectronicUnitItem.Unit.Id);
            if (candidate == null)
            {
                return "并入既有袋";
            }

            string current = FormatYearProjectLabel(_appendMatchImplementYear, _appendMatchProjectName);
            List<string> exact = new();
            List<string> nameOnly = new();
            List<string> yearOnly = new();
            List<string> neither = new();
            foreach (HardDiskAppendProjectRef project in candidate.Projects)
            {
                string label = FormatYearProjectLabel(project.ImplementYear, project.ProjectName);
                if (label.Length == 0)
                {
                    continue;
                }

                bool sameName = !string.IsNullOrWhiteSpace(_appendMatchProjectName)
                    && ProjectNameMatches(project.ProjectName, _appendMatchProjectName);
                bool sameYear = !string.IsNullOrWhiteSpace(_appendMatchImplementYear)
                    && YearMatches(project.ImplementYear, _appendMatchImplementYear);
                if (sameName && sameYear)
                {
                    exact.Add(label);
                }
                else if (sameName)
                {
                    nameOnly.Add(label);
                }
                else if (sameYear)
                {
                    yearOnly.Add(label);
                }
                else
                {
                    neither.Add(label);
                }
            }

            exact = DistinctYearProjectLabels(exact);
            nameOnly = DistinctYearProjectLabels(nameOnly);
            yearOnly = DistinctYearProjectLabels(yearOnly);
            neither = DistinctYearProjectLabels(neither);
            if (exact.Count == 0 && nameOnly.Count == 0 && yearOnly.Count == 0 && neither.Count == 0)
            {
                return string.IsNullOrEmpty(current)
                    ? "并入既有袋"
                    : $"并入既有袋（硬盘上尚未登记年度-项目，本次为 {current}）";
            }

            if (exact.Count > 0 && nameOnly.Count == 0 && yearOnly.Count == 0 && neither.Count == 0)
            {
                return $"并入既有袋（与本次相同：{JoinYearProjectLabels(exact)}）";
            }

            if (exact.Count == 0 && nameOnly.Count > 0 && yearOnly.Count == 0 && neither.Count == 0)
            {
                return DescribeSingleAppendRelation("项目名称相同、实施年度不同", nameOnly, current);
            }

            if (exact.Count == 0 && nameOnly.Count == 0 && yearOnly.Count > 0 && neither.Count == 0)
            {
                return DescribeSingleAppendRelation("实施年度相同、项目名称不同", yearOnly, current);
            }

            if (exact.Count == 0 && nameOnly.Count == 0 && yearOnly.Count == 0)
            {
                return DescribeSingleAppendRelation("项目名称不同、实施年度不同", neither, current);
            }

            List<string> parts = new();
            if (exact.Count > 0)
            {
                parts.Add($"与本次相同：{JoinYearProjectLabels(exact)}");
            }

            if (nameOnly.Count > 0)
            {
                parts.Add($"项目名称相同、实施年度不同：{JoinYearProjectLabels(nameOnly)}");
            }

            if (yearOnly.Count > 0)
            {
                parts.Add($"实施年度相同、项目名称不同：{JoinYearProjectLabels(yearOnly)}");
            }

            if (neither.Count > 0)
            {
                parts.Add($"项目名称不同、实施年度不同：{JoinYearProjectLabels(neither)}");
            }

            if (!string.IsNullOrEmpty(current))
            {
                parts.Add($"本次为 {current}");
            }

            return $"并入既有袋（{string.Join("；", parts)}）";
        }

        private static string DescribeSingleAppendRelation(string relation, IReadOnlyList<string> diskLabels, string current)
        {
            string disk = JoinYearProjectLabels(diskLabels);
            return string.IsNullOrEmpty(current)
                ? $"并入既有袋（{relation}：硬盘为 {disk}）"
                : $"并入既有袋（{relation}：硬盘为 {disk}，本次为 {current}）";
        }

        private static List<string> DistinctYearProjectLabels(List<string> labels)
        {
            return labels
                .Distinct(StringComparer.Ordinal)
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToList();
        }

        private static string JoinYearProjectLabels(IReadOnlyList<string> labels)
            => string.Join("、", labels);

        /// <summary>
        /// 同年度同项目的数据盘上，同一资料下的子项名称已存在时不允许再次写入。
        /// </summary>
        private async Task<bool> EnsureSameProjectYearDiskHasNoDuplicateSubItemsAsync(IReadOnlyCollection<int> mediaItemIds)
        {
            if (IsNewBoxMode || SelectedExistingElectronicUnitItem is not { CanSelectForAppend: true } selected)
            {
                return true;
            }

            HardDiskAppendCandidate? candidate = _appendHardDiskCandidates
                .FirstOrDefault(item => item.Unit.Id == selected.Unit.Id);
            if (candidate == null
                || string.IsNullOrWhiteSpace(_appendMatchImplementYear)
                || !CandidateMatchesProjectYear(candidate))
            {
                return true;
            }

            HashSet<int> pendingIds = mediaItemIds.Where(id => id > 0).ToHashSet();
            List<SelectableElectronicArchiveMediaViewModel> pending = EnumeratePendingElectronicMediaItemRows()
                .Where(item => pendingIds.Contains(item.MediaItemId))
                .ToList();
            List<YearlyElectronicArchiveUnitMediaItemLink> existingLinks;
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                IArchiveFilingRepository repository = scope.ServiceProvider.GetRequiredService<IArchiveFilingRepository>();
                existingLinks = await repository
                    .GetElectronicArchiveUnitMediaItemLinksByUnitIdAsync(selected.Unit.Id)
                    .ConfigureAwait(false);
            }

            var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (YearlyElectronicArchiveUnitMediaItemLink link in existingLinks)
            {
                string? key = BuildMaterialSubItemKey(link.MaterialName, link.ItemName);
                if (key != null)
                {
                    existingKeys.Add(key);
                }
            }

            List<string> conflicts = pending
                .Select(item => (Key: BuildMaterialSubItemKey(item.MaterialName, item.ItemName), item.MaterialName, item.ItemName))
                .Where(item => item.Key != null && existingKeys.Contains(item.Key))
                .Select(item => FormatMaterialSubItemLabel(item.MaterialName, item.ItemName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (conflicts.Count == 0)
            {
                return true;
            }

            string sample = string.Join("、", conflicts.Take(5));
            if (conflicts.Count > 5)
            {
                sample += $" 等共 {conflicts.Count} 项";
            }

            await RunOnUiAsync(() => MessageBox.Show(
                $"该同年度同项目硬盘中已有重名资料子项，不能重复写入：{sample}。",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information)).ConfigureAwait(false);
            return false;
        }

        private static string? BuildMaterialSubItemKey(string? materialName, string? itemName)
        {
            string material = materialName?.Trim() ?? string.Empty;
            string item = itemName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(material) && string.IsNullOrWhiteSpace(item))
            {
                return null;
            }

            return $"{material}\u001f{item}";
        }

        private static string FormatMaterialSubItemLabel(string? materialName, string? itemName)
        {
            string material = materialName?.Trim() ?? string.Empty;
            string item = itemName?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(material) && !string.IsNullOrWhiteSpace(item))
            {
                return $"{material} / {item}";
            }

            return string.IsNullOrWhiteSpace(item) ? material : item;
        }

        private int _blankHardDiskLocationLookupVersion;

        /// <summary>
        /// 出网预定的库内空盘只有编号时，补读台账档口，供文本框和卡片 4 定位。
        /// </summary>
        private void RequestBlankHardDiskCabinetLocationLookup()
        {
            if (SelectedElectronicSubmissionMode != ElectronicArchiveSubmissionMode.CopyNewHardDisk)
            {
                return;
            }

            string diskCode = FirstHardDiskCode(ElectronicLinkedMediumCodes);
            if (string.IsNullOrWhiteSpace(diskCode) || !string.IsNullOrWhiteSpace(ElectronicOriginalStorageLocation))
            {
                return;
            }

            int version = ++_blankHardDiskLocationLookupVersion;
            _pageDb.RunInBackground(() => LoadBlankHardDiskCabinetLocationAsync(version, diskCode));
        }

        private async Task LoadBlankHardDiskCabinetLocationAsync(int version, string diskCode)
        {
            string location;
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                IArchiveFilingRepository repository = scope.ServiceProvider.GetRequiredService<IArchiveFilingRepository>();
                var media = await repository.GetHardDiskMediaByCodesWithLedgerAsync(new[] { diskCode }).ConfigureAwait(false);
                location = media.FirstOrDefault()?.Ledger?.StorageLocation?.Trim() ?? string.Empty;
            }

            if (version != _blankHardDiskLocationLookupVersion)
            {
                return;
            }

            await RunOnUiAsync(() =>
            {
                if (version != _blankHardDiskLocationLookupVersion
                    || !string.Equals(FirstHardDiskCode(ElectronicLinkedMediumCodes), diskCode, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                ElectronicOriginalStorageLocation = location;
            }).ConfigureAwait(false);
        }

        private async Task UpdateElectronicFilingMediumCapacityAsync(
            IArchiveFilingRepository repository,
            string mediumCode,
            IReadOnlyCollection<ElectronicFilingDetailRowViewModel> existingRows)
        {
            if (IsPendingMediumCode(mediumCode))
            {
                ElectronicFilingMediumTotalCapacityText = "—";
                ElectronicFilingMediumAvailableCapacityText = "—";
                ElectronicFilingMediumCapacityDescription = "—";
                return;
            }

            decimal totalMb = await ResolveTargetMediumTotalCapacityMbAsync(repository, mediumCode).ConfigureAwait(false);
            decimal usedMb = existingRows.Sum(row => row.DataSizeMb)
                + EnumeratePendingElectronicMediaItemRows().Sum(row => row.DataSizeMb);
            decimal availableMb = Math.Max(0, totalMb - usedMb);

            ElectronicFilingMediumTotalCapacityText = ElectronicMediaCapacitySupport.FormatCapacityMb(totalMb);
            ElectronicFilingMediumAvailableCapacityText = totalMb > 0
                ? ElectronicMediaCapacitySupport.FormatCapacityMb(availableMb)
                : "—";
            ElectronicFilingMediumCapacityDescription = totalMb > 0
                ? $"可用容量 {FormatCapacityMbOrZero(availableMb)}（总容量 {ElectronicMediaCapacitySupport.FormatCapacityMb(totalMb)}，已占用 {FormatCapacityMbOrZero(usedMb)}）"
                : "未登记容量";
        }

        private void RequestCopyTargetHardDiskCapacityRefresh()
        {
            int version = ++_copyTargetHardDiskCapacityVersion;
            string diskCode = SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.CopyNewHardDisk
                ? ElectronicLinkedMediumCodes
                : string.Empty;
            if (IsPendingMediumCode(diskCode))
            {
                diskCode = string.Empty;
            }

            _pageDb.RunInBackground(() => ApplySelectionHardDiskCapacityAsync(
                version,
                () => _copyTargetHardDiskCapacityVersion,
                FirstHardDiskCode(diskCode),
                value => CopyTargetHardDiskCapacityDescription = value));
        }

        private void RequestSelectedExistingHardDiskCapacityRefresh()
        {
            int version = ++_selectedExistingHardDiskCapacityVersion;
            string diskCode = SelectedExistingElectronicUnitItem is { IsOpticalDiscBag: false }
                ? SelectedExistingElectronicUnitItem.LinkedMediumCodesDisplay
                : string.Empty;
            _pageDb.RunInBackground(() => ApplySelectionHardDiskCapacityAsync(
                version,
                () => _selectedExistingHardDiskCapacityVersion,
                FirstHardDiskCode(diskCode),
                value => SelectedExistingHardDiskCapacityDescription = value));
        }

        private async Task ApplySelectionHardDiskCapacityAsync(
            int version,
            Func<int> currentVersion,
            string diskCode,
            Action<string> assign)
        {
            string description = await HardDiskCapacityDescriptionLoader.LoadAsync(_filingService, diskCode);
            if (version != currentVersion())
            {
                return;
            }

            assign(description);
        }

        private static string FirstHardDiskCode(string? linkedMediumCodes)
        {
            if (string.IsNullOrWhiteSpace(linkedMediumCodes))
            {
                return string.Empty;
            }

            return linkedMediumCodes
                .Split([',', '，', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? string.Empty;
        }

        private static string FormatCapacityMbOrZero(decimal capacityMb) =>
            capacityMb <= 0 ? "0 MB" : ElectronicMediaCapacitySupport.FormatCapacityMb(capacityMb);

        private static async Task<decimal> ResolveTargetMediumTotalCapacityMbAsync(IArchiveFilingRepository repository, string mediumCode)
        {
            var hardDisk = await repository.GetHardDiskMediumByDiskCodeWithLedgerAsync(mediumCode).ConfigureAwait(false);
            if (hardDisk != null)
            {
                return ElectronicMediaCapacitySupport.ParseCapacityTextToMb(hardDisk.Capacity);
            }

            var disc = await repository.GetOpticalDiscMediumByCodeAsync(mediumCode).ConfigureAwait(false);
            if (disc != null)
            {
                return ElectronicMediaCapacitySupport.ParseCapacityTextToMb(disc.Capacity);
            }

            return 0;
        }

        private static ElectronicFilingDetailRowViewModel MapExistingFilingDetailRow(
            YearlyElectronicArchiveUnitMediaItemLink link,
            IReadOnlyDictionary<int, string> implementYears)
        {
            YearlyArchiveRegisterRecord? record = link.MediaItem?.MediaEntry?.RegisterRecord;
            return new ElectronicFilingDetailRowViewModel
            {
                MediaItemId = link.YearlyArchiveRegisterMediaItemId,
                MediaEntryId = link.MediaItem?.YearlyArchiveRegisterMediaId ?? 0,
                FormNo = link.FormNo,
                ImplementYear = ResolveImplementYear(record?.ProjectId, implementYears),
                ProjectName = record?.ProjectName?.Trim() ?? string.Empty,
                MaterialName = link.MaterialName,
                MediaType = link.MediaItem?.MediaEntry?.MediaType ?? string.Empty,
                MaterialCategory = link.MediaItem?.ElectronicDetail?.MaterialCategory?.Trim() ?? string.Empty,
                SubCategory = link.MediaItem?.ElectronicDetail?.SubCategory?.Trim() ?? string.Empty,
                DataOrganizationForm = link.MediaItem?.ElectronicDetail?.DataOrganizationForm?.Trim() ?? string.Empty,
                ContentCount = link.MediaItem != null ? Math.Max(1, link.MediaItem.ContentCount) : 1,
                DataSizeMb = link.DataSizeMb,
                SourceStoragePath = link.MediaItem?.StoragePath ?? string.Empty,
                FilingStoragePath = link.FilingStoragePath,
                ItemName = link.ItemName,
                ElectronicArchiveNo = ElectronicMediumCatalog.FindCurrentBag(link)?.ElectronicArchiveNo ?? string.Empty,
                MediumCode = link.MediumCode,
                IsStoragePathEditable = false,
                ArchivedAt = link.CreatedAt
            };
        }

        private static string ResolveImplementYear(int? projectId, IReadOnlyDictionary<int, string> implementYears)
        {
            if (projectId is not int id || id <= 0)
            {
                return string.Empty;
            }

            return implementYears.TryGetValue(id, out string? year) ? year : string.Empty;
        }

        private ElectronicFilingDetailRowViewModel CreatePendingFilingDetailRow(
            SelectableElectronicArchiveMediaViewModel source,
            string mediumCode,
            bool pathEditable,
            IReadOnlyDictionary<int, string> folderNameByMediaItemId,
            IReadOnlyDictionary<int, string> implementYears)
        {
            string sourceStoragePath = source.StoragePath?.Trim() ?? string.Empty;
            string defaultFilingPath = sourceStoragePath;
            if (pathEditable
                && SelectedElectronicSubmissionMode != ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew)
            {
                string itemFolderName = folderNameByMediaItemId.TryGetValue(source.MediaItemId, out string? resolvedFolder)
                    ? resolvedFolder
                    : ElectronicFilingStoragePathSupport.SanitizePathSegment(source.ItemName, "未命名子项");
                defaultFilingPath = ElectronicFilingStoragePathSupport.BuildDefaultFilingStoragePath(
                    TargetYear,
                    TargetProject,
                    source.MaterialName,
                    itemFolderName);
            }

            if (!_pendingFilingStoragePaths.TryGetValue(source.MediaItemId, out string? storedPath)
                || string.IsNullOrWhiteSpace(storedPath))
            {
                storedPath = defaultFilingPath;
                _pendingFilingStoragePaths[source.MediaItemId] = storedPath;
            }

            var row = new ElectronicFilingDetailRowViewModel
            {
                MediaItemId = source.MediaItemId,
                MediaEntryId = source.MediaEntryId,
                FormNo = source.FormNo,
                ImplementYear = ResolveImplementYear(source.ProjectId, implementYears),
                ProjectName = source.ProjectName,
                MaterialName = source.MaterialName,
                MediaType = source.MediaType,
                MaterialCategory = source.MaterialCategory,
                SubCategory = source.SubCategory,
                DataOrganizationForm = source.DataOrganizationForm,
                ContentCount = source.MediaCount,
                DataSizeMb = source.DataSizeMb,
                SourceStoragePath = sourceStoragePath,
                FilingStoragePath = storedPath,
                ItemName = source.ItemName,
                ElectronicArchiveNo = IsAppendMode
                    ? SelectedExistingElectronicUnit?.ElectronicArchiveNo ?? "待并入"
                    : "待生成",
                MediumCode = mediumCode,
                IsStoragePathEditable = pathEditable
            };

            row.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ElectronicFilingDetailRowViewModel.FilingStoragePath))
                {
                    _pendingFilingStoragePaths[row.MediaItemId] = row.FilingStoragePath?.Trim() ?? string.Empty;
                    SyncElectronicStoragePathFromPendingFilingRows();
                }
            };

            return row;
        }

        private void SyncElectronicStoragePathFromPendingFilingRows()
        {
            if (!IsElectronicFilingStoragePathEditable)
            {
                return;
            }

            string merged = string.Join("\n", ElectronicFilingPendingDetailRows
                .Select(row => row.FilingStoragePath?.Trim())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase));

            if (IsNewBoxMode)
            {
                ElectronicStoragePath = merged;
            }
        }

        /// <summary>
        /// 提交前单独确认用户已核对第四步「立档存储路径」。
        /// </summary>
        private bool ConfirmElectronicFilingStoragePaths()
        {
            MessageBoxResult result = MessageBox.Show(
                "是否已对立档存储路径进行了确认？",
                "立档存储路径确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        private void ClearElectronicFilingDetailState()
        {
            _pendingFilingStoragePaths.Clear();
            ReplaceItems(ElectronicFilingExistingDetailRows, Array.Empty<ElectronicFilingDetailRowViewModel>());
            ReplaceItems(ElectronicFilingPendingDetailRows, Array.Empty<ElectronicFilingDetailRowViewModel>());
            RefreshElectronicFilingExistingDetailRowsPanel();
            RefreshElectronicFilingPendingDetailRowsPanel();
            ElectronicFilingTargetMediumSummary = string.Empty;
            ElectronicFilingMediumTotalCapacityText = "—";
            ElectronicFilingMediumAvailableCapacityText = "—";
            ElectronicFilingMediumCapacityDescription = "—";
        }
    }
}
