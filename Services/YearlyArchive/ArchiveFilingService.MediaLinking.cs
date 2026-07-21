using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 模拟/电子介质链接与并档约束逻辑。
    /// </summary>
    public partial class ArchiveFilingService
    {
        private List<YearlyArchiveBoxMediaItemLink> AddMediaItemLinks(int boxId, IEnumerable<int> mediaItemIds, DateTime createdAt)
        {
            ArgumentNullException.ThrowIfNull(mediaItemIds);

            var targetMediaItemIds = mediaItemIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (targetMediaItemIds.Count == 0)
            {
                return [];
            }

            var existingMediaItemIds = _archiveFilingRepository.GetArchiveBoxLinkedMediaItemIds(boxId).ToHashSet();
            var createdLinks = new List<YearlyArchiveBoxMediaItemLink>();

            foreach (var mediaItemId in targetMediaItemIds.Where(id => !existingMediaItemIds.Contains(id)))
            {
                var link = new YearlyArchiveBoxMediaItemLink
                {
                    YearlyArchiveBoxId = boxId,
                    YearlyArchiveRegisterMediaItemId = mediaItemId,
                    CreatedAt = createdAt
                };
                _archiveFilingRepository.AddArchiveBoxMediaItemLink(link);
                createdLinks.Add(link);
            }

            return createdLinks;
        }

        private async Task UpdateSimulatedArchiveStatusesAsync(IEnumerable<int> recordIds, DateTime archivedAt)
        {
            ArgumentNullException.ThrowIfNull(recordIds);

            var targetRecordIds = recordIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (targetRecordIds.Count == 0)
            {
                return;
            }

            var records = await _archiveFilingRepository.GetRecordsForSimulatedStatusUpdateAsync(targetRecordIds);

            UpdateSimulatedArchiveStatuses(records, archivedAt);
        }

        private async Task UpdateElectronicArchiveStatusesAsync(IEnumerable<int> recordIds, DateTime archivedAt)
        {
            ArgumentNullException.ThrowIfNull(recordIds);

            var targetRecordIds = recordIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (targetRecordIds.Count == 0)
            {
                return;
            }

            var records = await _archiveFilingRepository.GetRecordsForElectronicStatusUpdateAsync(targetRecordIds);

            UpdateElectronicArchiveStatuses(records, archivedAt);
        }

        private async Task<List<HardDiskMedium>> PrepareElectronicArchiveUnitAsync(
            YearlyElectronicArchiveUnit unit,
            DateTime archivedAt,
            HardDiskMediaReturnCandidate? borrowedHardDiskCandidate = null,
            PendingExternalHardDiskRegistration? pendingExternalHardDisk = null)
        {
            ArgumentNullException.ThrowIfNull(unit);

            unit.ElectronicArchiveNo = unit.ElectronicArchiveNo.Trim();
            unit.ProjectName = unit.ProjectName.Trim();
            unit.Year = unit.Year.Trim();
            unit.StorageCarrierType = unit.StorageCarrierType.Trim();
            unit.StoragePath = unit.StoragePath.Trim();
            unit.StorageLocation = unit.StorageLocation.Trim();
            unit.LinkedMediumCodes = NormalizeMediumCodes(unit.LinkedMediumCodes);
            unit.Disposition = unit.Disposition.Trim();
            unit.ContentSummary = unit.ContentSummary.Trim();
            unit.ArchivedBy = unit.ArchivedBy.Trim();
            unit.SourceType = unit.SourceType.Trim();
            unit.SourceRecordKey = unit.SourceRecordKey.Trim();
            unit.Remarks = unit.Remarks.Trim();
            unit.ArchivedDate = archivedAt;

            ValidateElectronicArchiveUnit(unit);

            var linkedMedia = await LoadLinkedMediaAsync(unit.LinkedMediumCodes, pendingExternalHardDisk);
            await ValidateElectronicStorageLocationSlotCategoryAsync(unit, linkedMedia);
            await EnsureElectronicStorageLocationAvailableAsync(unit);
            await ValidateMediumLinkConflictsAsync(unit.Id, unit.ElectronicArchiveNo, linkedMedia);

            if (RequiresHardDiskLink(unit))
            {
                if (linkedMedia.Count != 1)
                {
                    throw new InvalidOperationException("电子介质袋需要且仅能关联一块入袋硬盘。");
                }

                unit.MediaCount = 1;
            }

            var ledgerSnapshots = linkedMedia.ToDictionary(
                medium => medium.Id,
                medium => new
                {
                    medium.DiskCode,
                    Status = medium.Ledger?.MediaStatus ?? string.Empty,
                    Location = medium.Ledger?.StorageLocation ?? string.Empty,
                    Nature = medium.Ledger?.MediaNature ?? string.Empty
                });

            SyncLinkedMedia(unit, linkedMedia, archivedAt, borrowedHardDiskCandidate);

            string operatorName = unit.ArchivedBy?.Trim() ?? "资料室管理员";
            string relatedBatch = unit.ElectronicArchiveNo.Trim();
            string relatedArchiveTitle = string.IsNullOrWhiteSpace(unit.ContentSummary)
                ? relatedBatch
                : unit.ContentSummary.Trim();
            bool borrowedReturnWillHandleLedgerSync = ShouldBorrowedReturnHandleLedgerSync(
                borrowedHardDiskCandidate,
                linkedMedia);

            foreach (var medium in linkedMedia)
            {
                if (!ledgerSnapshots.TryGetValue(medium.Id, out var snapshot) || medium.Ledger == null)
                {
                    continue;
                }

                if (borrowedReturnWillHandleLedgerSync
                    && borrowedHardDiskCandidate != null
                    && medium.Id == borrowedHardDiskCandidate.MediumId)
                {
                    continue;
                }

                var before = new HardDiskLedgerSyncSupport.LedgerSnapshot(
                    snapshot.Status,
                    snapshot.Location,
                    snapshot.Nature);
                if (!HardDiskLedgerSyncSupport.HasLedgerMaterialChange(before, medium.Ledger))
                {
                    continue;
                }

                _archiveFilingRepository.AddHardDiskMediaTransaction(
                    HardDiskLedgerSyncSupport.BuildSyncTransaction(
                        medium,
                        before,
                        operatorName,
                        archivedAt,
                        $"电子立档 [{relatedBatch}] 完成后同步硬盘台账。",
                        "立档入袋关联后同步 HardDiskLedger 与流转记录",
                        relatedBatch,
                        relatedArchiveTitle));
            }

            if (_submissionChangeTracker != null)
            {
                foreach (var medium in linkedMedia)
                {
                    if (!ledgerSnapshots.TryGetValue(medium.Id, out var snapshot) || medium.Ledger == null)
                    {
                        continue;
                    }

                    _submissionChangeTracker.AddLedgerChange(
                        medium.DiskCode,
                        snapshot.Status,
                        medium.Ledger.MediaStatus,
                        snapshot.Location,
                        medium.Ledger.StorageLocation,
                        snapshot.Nature,
                        medium.Ledger.MediaNature,
                        "入袋关联后同步 HardDiskLedger");
                }
            }

            return linkedMedia;
        }

        private static void AssignElectronicArchiveMediaLinks(YearlyElectronicArchiveUnit unit, IEnumerable<YearlyArchiveRegisterMedia> mediaEntries, DateTime createdAt)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(mediaEntries);

            unit.MediaEntryLinks.Clear();
            foreach (var mediaEntry in mediaEntries)
            {
                unit.MediaEntryLinks.Add(new YearlyElectronicArchiveUnitMediaLink
                {
                    ElectronicArchiveUnit = unit,
                    MediaEntry = mediaEntry,
                    CreatedAt = createdAt
                });
            }
        }

        private List<YearlyElectronicArchiveUnitMediaItemLink> AddElectronicMediaItemLinks(
            YearlyElectronicArchiveUnit unit,
            IEnumerable<YearlyArchiveRegisterMediaItem> mediaItems,
            IReadOnlyDictionary<int, string> filingStoragePathByMediaItemId,
            string mediumCode,
            DateTime createdAt)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(mediaItems);

            var existingMediaItemIds = unit.MediaItemLinks
                .Select(item => item.YearlyArchiveRegisterMediaItemId)
                .ToHashSet();

            string normalizedMediumCode = mediumCode.Trim();
            var createdLinks = new List<YearlyElectronicArchiveUnitMediaItemLink>();

            foreach (var mediaItem in mediaItems)
            {
                if (existingMediaItemIds.Contains(mediaItem.Id))
                {
                    continue;
                }

                string filingPath = filingStoragePathByMediaItemId.TryGetValue(mediaItem.Id, out string? editedPath)
                    && !string.IsNullOrWhiteSpace(editedPath)
                    ? editedPath.Trim()
                    : mediaItem.StoragePath?.Trim() ?? string.Empty;

                var record = mediaItem.MediaEntry?.RegisterRecord;
                var link = new YearlyElectronicArchiveUnitMediaItemLink
                {
                    ElectronicArchiveUnit = unit,
                    MediaItem = mediaItem,
                    FilingStoragePath = filingPath,
                    MediumCode = normalizedMediumCode,
                    FormNo = record?.FormNo?.Trim() ?? string.Empty,
                    MaterialName = record?.MaterialName?.Trim() ?? string.Empty,
                    ItemName = mediaItem.ContentDesc?.Trim() ?? string.Empty,
                    DataSizeMb = ElectronicMediaItemSupport.ResolveMediaItemDataSizeMb(mediaItem),
                    CreatedAt = createdAt
                };
                unit.MediaItemLinks.Add(link);
                createdLinks.Add(link);
            }

            return createdLinks;
        }

        private void SyncElectronicMediaEntryLinksAfterItemFiling(
            YearlyElectronicArchiveUnit unit,
            IEnumerable<YearlyArchiveRegisterMediaItem> filedMediaItems,
            DateTime createdAt)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(filedMediaItems);

            var mediaEntries = filedMediaItems
                .Select(item => item.MediaEntry)
                .Where(entry => entry != null)
                .DistinctBy(entry => entry!.Id)
                .Cast<YearlyArchiveRegisterMedia>()
                .ToList();

            var fullyArchivedEntries = mediaEntries
                .Where(entry => entry.Items.Count > 0 && entry.Items.All(IsMediaItemLinkedForEntrySync))
                .ToList();

            AddElectronicMediaLinks(unit, fullyArchivedEntries, createdAt);

            bool IsMediaItemLinkedForEntrySync(YearlyArchiveRegisterMediaItem item)
                => item.ElectronicArchiveUnitMediaItemLinks.Any()
                   || unit.MediaItemLinks.Any(link => link.YearlyArchiveRegisterMediaItemId == item.Id);
        }

        private void AddElectronicMediaLinks(YearlyElectronicArchiveUnit unit, IEnumerable<YearlyArchiveRegisterMedia> mediaEntries, DateTime createdAt)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(mediaEntries);

            var existingMediaEntryIds = unit.MediaEntryLinks
                .Select(item => item.YearlyArchiveRegisterMediaId)
                .ToHashSet();

            foreach (var mediaEntry in mediaEntries)
            {
                if (existingMediaEntryIds.Contains(mediaEntry.Id))
                {
                    continue;
                }

                unit.MediaEntryLinks.Add(new YearlyElectronicArchiveUnitMediaLink
                {
                    YearlyElectronicArchiveUnitId = unit.Id,
                    YearlyArchiveRegisterMediaId = mediaEntry.Id,
                    ElectronicArchiveUnit = unit,
                    MediaEntry = mediaEntry,
                    CreatedAt = createdAt
                });
            }
        }

        private async Task ValidateMediumLinkConflictsAsync(int currentUnitId, string electronicArchiveNo, IEnumerable<HardDiskMedium> linkedMedia)
        {
            ArgumentNullException.ThrowIfNull(linkedMedia);

            var mediaIds = linkedMedia.Select(item => item.Id).Distinct().ToList();
            if (mediaIds.Count == 0)
            {
                return;
            }

            var conflicts = await GetElectronicArchiveLinkInfosAsync(mediaIds);
            var invalidLinks = conflicts
                .Where(link => link.ElectronicArchiveUnitId != currentUnitId)
                .GroupBy(link => link.DiskCode)
                .Select(group => $"{group.Key} -> {string.Join("、", group.Select(item => item.ElectronicArchiveNo).Distinct(StringComparer.Ordinal))}")
                .ToList();

            if (invalidLinks.Count > 0)
            {
                throw new InvalidOperationException($"硬盘已关联到其他电子立档单元，当前[{electronicArchiveNo}]不能重复关联：{string.Join("；", invalidLinks)}");
            }
        }

        private static void ValidateElectronicAppendConstraints(
            YearlyElectronicArchiveUnit existingUnit,
            YearlyElectronicArchiveUnit updatedUnit,
            IEnumerable<YearlyArchiveRegisterRecord> records)
        {
            ArgumentNullException.ThrowIfNull(existingUnit);
            ArgumentNullException.ThrowIfNull(updatedUnit);
            ArgumentNullException.ThrowIfNull(records);

            string existingProject = existingUnit.ProjectName.Trim();
            string existingYear = existingUnit.Year.Trim();
            string updatedProject = updatedUnit.ProjectName.Trim();
            string updatedYear = updatedUnit.Year.Trim();

            if (!string.IsNullOrWhiteSpace(updatedProject) && !string.Equals(existingProject, updatedProject, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"电子立档单元 [{existingUnit.ElectronicArchiveNo}] 与当前资料项目不一致，禁止跨项目并入。");
            }

            if (!string.IsNullOrWhiteSpace(updatedYear) && !string.Equals(existingYear, updatedYear, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"电子立档单元 [{existingUnit.ElectronicArchiveNo}] 与当前资料年度不一致，禁止跨年度并入。");
            }

            if (!string.IsNullOrWhiteSpace(existingUnit.StorageCarrierType)
                && !string.IsNullOrWhiteSpace(updatedUnit.StorageCarrierType)
                && !string.Equals(existingUnit.StorageCarrierType.Trim(), updatedUnit.StorageCarrierType.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"电子立档单元 [{existingUnit.ElectronicArchiveNo}] 的载体类型为 [{existingUnit.StorageCarrierType}]，不能并入不同载体类型的资料。");
            }

            if (!ArchiveFilingBusinessRules.IsHardDiskArchiveCarrierType(existingUnit.StorageCarrierType))
            {
                throw new InvalidOperationException($"电子立档单元 [{existingUnit.ElectronicArchiveNo}] 不是硬盘档，当前业务仅允许并入本项目已立档硬盘。");
            }

            var existingLinkedCodes = ParseMediumCodes(existingUnit.LinkedMediumCodes);
            var updatedLinkedCodes = ParseMediumCodes(updatedUnit.LinkedMediumCodes);

            if (updatedLinkedCodes.Count > 1)
            {
                throw new InvalidOperationException($"电子立档单元 [{existingUnit.ElectronicArchiveNo}] 仅允许关联一块入袋硬盘。");
            }

            if (existingLinkedCodes.Count > 0
                && updatedLinkedCodes.Count > 0
                && !string.Equals(existingLinkedCodes[0], updatedLinkedCodes[0], StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"电子立档单元 [{existingUnit.ElectronicArchiveNo}] 已绑定硬盘 [{existingLinkedCodes[0]}]，并入时不能改绑其他硬盘。\n请继续使用原硬盘袋，或改为新建电子介质袋。");
            }

            foreach (var record in records)
            {
                string recordProject = record.ProjectName?.Trim() ?? string.Empty;
                string recordYear = record.CreatedDate.Year.ToString();

                if (!string.Equals(existingProject, recordProject, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"登记单 [{record.FormNo}] 与电子立档单元 [{existingUnit.ElectronicArchiveNo}] 项目不一致，禁止跨项目并入。");
                }

                if (!string.Equals(existingYear, recordYear, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"登记单 [{record.FormNo}] 与电子立档单元 [{existingUnit.ElectronicArchiveNo}] 年度不一致，禁止跨年度并入。");
                }
            }
        }

        private static YearlyElectronicArchiveUnit MergeElectronicArchiveUnit(YearlyElectronicArchiveUnit existingUnit, YearlyElectronicArchiveUnit updatedUnit, DateTime archivedAt)
        {
            ArgumentNullException.ThrowIfNull(existingUnit);
            ArgumentNullException.ThrowIfNull(updatedUnit);

            return new YearlyElectronicArchiveUnit
            {
                Id = existingUnit.Id,
                ElectronicArchiveNo = existingUnit.ElectronicArchiveNo,
                ProjectName = string.IsNullOrWhiteSpace(updatedUnit.ProjectName) ? existingUnit.ProjectName : updatedUnit.ProjectName,
                Year = string.IsNullOrWhiteSpace(updatedUnit.Year) ? existingUnit.Year : updatedUnit.Year,
                StorageCarrierType = string.IsNullOrWhiteSpace(updatedUnit.StorageCarrierType) ? existingUnit.StorageCarrierType : updatedUnit.StorageCarrierType,
                StoragePath = MergeDelimitedText(existingUnit.StoragePath, updatedUnit.StoragePath),
                StorageLocation = string.IsNullOrWhiteSpace(updatedUnit.StorageLocation) ? existingUnit.StorageLocation : updatedUnit.StorageLocation,
                LinkedMediumCodes = MergeMediumCodes(existingUnit.LinkedMediumCodes, updatedUnit.LinkedMediumCodes),
                Disposition = string.IsNullOrWhiteSpace(updatedUnit.Disposition) ? existingUnit.Disposition : updatedUnit.Disposition,
                MediaCount = updatedUnit.MediaCount > 0 ? updatedUnit.MediaCount : existingUnit.MediaCount,
                ContentSummary = MergeDelimitedText(existingUnit.ContentSummary, updatedUnit.ContentSummary),
                ArchivedBy = string.IsNullOrWhiteSpace(updatedUnit.ArchivedBy) ? existingUnit.ArchivedBy : updatedUnit.ArchivedBy,
                ArchivedDate = archivedAt,
                SourceType = string.IsNullOrWhiteSpace(updatedUnit.SourceType) ? existingUnit.SourceType : updatedUnit.SourceType,
                SourceRecordKey = string.IsNullOrWhiteSpace(updatedUnit.SourceRecordKey) ? existingUnit.SourceRecordKey : updatedUnit.SourceRecordKey,
                Remarks = MergeDelimitedText(existingUnit.Remarks, updatedUnit.Remarks),
                RegisterRecords = existingUnit.RegisterRecords
            };
        }

        private static void ApplyElectronicArchiveUnitUpdates(YearlyElectronicArchiveUnit target, YearlyElectronicArchiveUnit source)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(source);

            target.ProjectName = source.ProjectName;
            target.Year = source.Year;
            target.StorageCarrierType = source.StorageCarrierType;
            target.StoragePath = source.StoragePath;
            target.StorageLocation = source.StorageLocation;
            target.LinkedMediumCodes = source.LinkedMediumCodes;
            target.Disposition = source.Disposition;
            target.MediaCount = source.MediaCount;
            target.ContentSummary = source.ContentSummary;
            target.ArchivedBy = source.ArchivedBy;
            target.ArchivedDate = source.ArchivedDate;
            target.SourceType = source.SourceType;
            target.SourceRecordKey = source.SourceRecordKey;
            target.Remarks = source.Remarks;
        }

        private static void AssignElectronicArchiveMediumLinks(YearlyElectronicArchiveUnit unit, IEnumerable<HardDiskMedium> linkedMedia)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(linkedMedia);

            unit.MediumLinks.Clear();
            foreach (var medium in linkedMedia)
            {
                unit.MediumLinks.Add(new YearlyElectronicArchiveUnitMediumLink
                {
                    ElectronicArchiveUnit = unit,
                    HardDiskMedium = medium
                });
            }
        }

        private static void MergeElectronicArchiveMediumLinks(YearlyElectronicArchiveUnit unit, IEnumerable<HardDiskMedium> linkedMedia)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(linkedMedia);

            var existingMediumIds = unit.MediumLinks
                .Select(item => item.HardDiskMediumId)
                .ToHashSet();

            foreach (var medium in linkedMedia)
            {
                if (existingMediumIds.Contains(medium.Id))
                {
                    continue;
                }

                unit.MediumLinks.Add(new YearlyElectronicArchiveUnitMediumLink
                {
                    YearlyElectronicArchiveUnitId = unit.Id,
                    HardDiskMediumId = medium.Id,
                    ElectronicArchiveUnit = unit,
                    HardDiskMedium = medium
                });
            }
        }

        private static bool ShouldBorrowedReturnHandleLedgerSync(
            HardDiskMediaReturnCandidate? borrowedHardDiskCandidate,
            IReadOnlyList<HardDiskMedium> linkedMedia)
        {
            return borrowedHardDiskCandidate != null
                && linkedMedia.Count == 1
                && linkedMedia[0].Id == borrowedHardDiskCandidate.MediumId
                && string.Equals(linkedMedia[0].DiskCode, borrowedHardDiskCandidate.DiskCode, StringComparison.Ordinal);
        }
    }
}
