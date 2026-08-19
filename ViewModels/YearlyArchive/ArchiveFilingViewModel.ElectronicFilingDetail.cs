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
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                IArchiveFilingRepository repository = scope.ServiceProvider.GetRequiredService<IArchiveFilingRepository>();
                if (IsAppendMode && SelectedExistingElectronicUnit != null)
                {
                    var links = await repository.GetElectronicArchiveUnitMediaItemLinksByUnitIdAsync(SelectedExistingElectronicUnit.Id)
                        .ConfigureAwait(false);
                    existingRows = links.Select(MapExistingFilingDetailRow).ToList();
                }
                else if (!IsPendingMediumCode(mediumCode))
                {
                    var links = await repository.GetElectronicArchiveUnitMediaItemLinksByMediumCodeAsync(mediumCode)
                        .ConfigureAwait(false);
                    existingRows = links.Select(MapExistingFilingDetailRow).ToList();
                }

                await UpdateElectronicFilingMediumCapacityAsync(repository, mediumCode, existingRows).ConfigureAwait(false);
            }

            bool pathEditable = IsElectronicFilingStoragePathEditable;
            var pendingSources = EnumeratePendingElectronicMediaItemRows().ToList();
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
                .Select(row => CreatePendingFilingDetailRow(row, mediumCode, pathEditable, folderNameByMediaItemId))
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
                    : "待第八步确定";
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

            string mode = IsAppendMode ? "并入既有袋" : "新建袋";
            return $"拟使用介质：{mediumCode} ｜ 载体：{carrier} ｜ 方式：{mode}";
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
                return;
            }

            decimal totalMb = await ResolveTargetMediumTotalCapacityMbAsync(repository, mediumCode).ConfigureAwait(false);
            decimal usedMb = existingRows.Sum(row => row.DataSizeMb)
                + EnumeratePendingElectronicMediaItemRows().Sum(row => row.DataSizeMb);

            ElectronicFilingMediumTotalCapacityText = ElectronicMediaCapacitySupport.FormatCapacityMb(totalMb);
            ElectronicFilingMediumAvailableCapacityText = totalMb > 0
                ? ElectronicMediaCapacitySupport.FormatCapacityMb(Math.Max(0, totalMb - usedMb))
                : "—";
        }

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

        private static ElectronicFilingDetailRowViewModel MapExistingFilingDetailRow(YearlyElectronicArchiveUnitMediaItemLink link)
        {
            return new ElectronicFilingDetailRowViewModel
            {
                MediaItemId = link.YearlyArchiveRegisterMediaItemId,
                MediaEntryId = link.MediaItem?.YearlyArchiveRegisterMediaId ?? 0,
                FormNo = link.FormNo,
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
                ElectronicArchiveNo = link.ElectronicArchiveUnit?.ElectronicArchiveNo ?? string.Empty,
                MediumCode = link.MediumCode,
                IsStoragePathEditable = false,
                ArchivedAt = link.CreatedAt
            };
        }

        private ElectronicFilingDetailRowViewModel CreatePendingFilingDetailRow(
            SelectableElectronicArchiveMediaViewModel source,
            string mediumCode,
            bool pathEditable,
            IReadOnlyDictionary<int, string> folderNameByMediaItemId)
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
        }
    }
}
