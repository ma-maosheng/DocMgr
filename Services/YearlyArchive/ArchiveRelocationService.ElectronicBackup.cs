using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        private async Task<ArchiveRelocationExecutionContext> ExecuteElectronicBackupToEmptyAsync(
            YearlyElectronicArchiveUnit source,
            ElectronicRelocationRequest request,
            DateTime operatedAt)
        {
            int mediumId = request.TargetBlankHardDiskMediumId
                ?? throw new InvalidOperationException("未指定拟迁入空白硬盘。");

            var targetMedium = await _filingRepository.GetHardDiskMediumByIdWithLedgerAsync(mediumId)
                ?? throw new InvalidOperationException("未找到所选空白硬盘。");

            if (!string.Equals(targetMedium.Ledger?.MediaStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"硬盘 [{targetMedium.DiskCode}] 当前不是在库空盘状态。");
            }

            if (source.MediumLinks.Any(link => link.HardDiskMediumId == targetMedium.Id))
            {
                throw new InvalidOperationException("拟迁入空白硬盘不能与源袋当前关联硬盘相同。");
            }

            string? targetUnitBlockReason = await ValidateBlankHardDiskTargetUnitAsync(source, targetMedium);
            if (!string.IsNullOrWhiteSpace(targetUnitBlockReason))
            {
                throw new InvalidOperationException(targetUnitBlockReason);
            }

            string finalLocation = await ResolveMoveToEmptyFinalStorageLocationAsync(
                source,
                string.IsNullOrWhiteSpace(request.NewStorageLocation)
                    ? source.StorageLocation
                    : request.NewStorageLocation);
            string newDiskCode = targetMedium.DiskCode.Trim();
            string operatorName = ResolveOperatorName();
            string backupUnitNo = await GenerateNextElectronicArchiveNoAsync(source.Year);
            string remark = $"资料备份：由电子介质袋 [{source.ElectronicArchiveNo}] 备份至新建袋 [{backupUnitNo}] / 硬盘 [{newDiskCode}]，存放于 [{finalLocation}]；原件保留于 [{source.StorageLocation}]。";

            await ValidateBackupMediumCapacityAsync(newDiskCode, source.MediaItemLinks);

            var backupUnit = new YearlyElectronicArchiveUnit
            {
                ElectronicArchiveNo = backupUnitNo,
                ProjectName = source.ProjectName.Trim(),
                Year = source.Year.Trim(),
                StorageCarrierType = ResolveHardDiskBagCarrierType(source),
                StoragePath = source.StoragePath?.Trim() ?? string.Empty,
                StorageLocation = finalLocation,
                LinkedMediumCodes = newDiskCode,
                Disposition = source.Disposition?.Trim() ?? string.Empty,
                ContentSummary = BuildBackupContentSummary(source),
                ArchivedBy = operatorName,
                ArchivedDate = operatedAt,
                SourceType = source.SourceType?.Trim() ?? string.Empty,
                SourceRecordKey = $"BackupFrom:{source.Id}",
                Remarks = request.Remarks?.Trim() ?? string.Empty,
                UnitLifecycleStatus = ArchiveContainerLifecycleStatus.InUse
            };

            await DetachMediumFromOtherElectronicUnitsAsync(source.Id, targetMedium);

            _filingRepository.AddElectronicArchiveUnit(backupUnit);
            backupUnit.MediumLinks.Add(new YearlyElectronicArchiveUnitMediumLink
            {
                ElectronicArchiveUnit = backupUnit,
                HardDiskMedium = targetMedium,
                HardDiskMediumId = targetMedium.Id
            });

            var cloneResult = await CloneSourceLinksToTargetUnitAsync(source, backupUnit, newDiskCode, operatedAt);
            backupUnit.MediaCount = backupUnit.MediaItemLinks.Count;

            var blankDiskLedgerBefore = HardDiskLedgerSyncSupport.CaptureSnapshot(targetMedium);
            ConvertBlankHardDiskToDataCarrier(targetMedium, finalLocation, operatedAt);
            RecordBlankHardDiskToDataCarrierSync(
                targetMedium,
                blankDiskLedgerBefore,
                finalLocation,
                operatedAt,
                operatorName,
                remark,
                backupUnitNo,
                backupUnit.ContentSummary);

            await _relocationRepository.SaveChangesAsync();

            await _filingFactWriter.WriteBackupElectronicLinksAsync(
                backupUnit,
                cloneResult.WriteItems,
                cloneResult.PrimaryFilingFactIdByOriginalLinkId,
                operatedAt,
                operatorName,
                remark);

            var context = new ArchiveRelocationExecutionContext
            {
                TargetContainerId = backupUnit.Id,
                TargetContainerCode = backupUnit.ElectronicArchiveNo,
                TargetStorageLocation = finalLocation,
                SourceMediumDisposition = ArchiveRelocationSourceDisposition.OriginalRetained
            };

            AppendBackupRelocationItems(context, source, cloneResult.WriteItems, cloneResult.PrimaryFilingFactIdByOriginalLinkId);
            request.TargetBlankHardDiskCode = newDiskCode;
            request.NewStorageLocation = finalLocation;
            return context;
        }

        private async Task<ArchiveRelocationExecutionContext> ExecuteElectronicBackupMergeAsync(
            YearlyElectronicArchiveUnit source,
            ElectronicRelocationRequest request,
            DateTime operatedAt)
        {
            int targetUnitId = request.TargetUnitId ?? throw new InvalidOperationException("未指定目标电子介质袋。");
            var target = await _relocationRepository.GetElectronicUnitForRelocationAsync(targetUnitId)
                ?? throw new InvalidOperationException("未找到目标电子介质袋。");

            if (target.MediaItemLinks.Count == 0)
            {
                throw new InvalidOperationException("并档目标电子介质袋不能为空。");
            }

            EnsureHardDiskMergeTargetUnit(target);

            if (!string.Equals(target.ProjectName, source.ProjectName, StringComparison.Ordinal)
                || !string.Equals(target.Year, source.Year, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("目标电子介质袋必须与源袋属于同一项目、同一年度。");
            }

            string targetMediumCode = ResolveTargetMediumCode(target);
            if (string.IsNullOrWhiteSpace(targetMediumCode))
            {
                throw new InvalidOperationException("目标电子介质袋未关联硬盘编号，无法执行备份并入。");
            }

            string operatorName = ResolveOperatorName();
            string remark = $"资料备份：由电子介质袋 [{source.ElectronicArchiveNo}] 备份 {source.MediaItemLinks.Count} 条资料子项至 [{target.ElectronicArchiveNo}]（{target.StorageLocation}）；原件保留于 [{source.StorageLocation}]。";

            await ValidateBackupMediumCapacityAsync(targetMediumCode, source.MediaItemLinks);

            var cloneResult = await CloneSourceLinksToTargetUnitAsync(source, target, targetMediumCode, operatedAt);
            target.MediaCount = target.MediaItemLinks.Count;
            target.UnitLifecycleStatus = ArchiveContainerLifecycleStatus.InUse;

            await _relocationRepository.SaveChangesAsync();

            await _filingFactWriter.WriteBackupElectronicLinksAsync(
                target,
                cloneResult.WriteItems,
                cloneResult.PrimaryFilingFactIdByOriginalLinkId,
                operatedAt,
                operatorName,
                remark);

            var context = new ArchiveRelocationExecutionContext
            {
                TargetContainerId = target.Id,
                TargetContainerCode = target.ElectronicArchiveNo,
                TargetStorageLocation = target.StorageLocation,
                SourceMediumDisposition = ArchiveRelocationSourceDisposition.OriginalRetained
            };

            AppendBackupRelocationItems(context, source, cloneResult.WriteItems, cloneResult.PrimaryFilingFactIdByOriginalLinkId);
            return context;
        }

        private async Task<ElectronicBackupCloneResult> CloneSourceLinksToTargetUnitAsync(
            YearlyElectronicArchiveUnit source,
            YearlyElectronicArchiveUnit targetUnit,
            string mediumCode,
            DateTime operatedAt)
        {
            var sourceLinks = source.MediaItemLinks.ToList();
            if (sourceLinks.Count == 0)
            {
                throw new InvalidOperationException("源电子介质袋内无资料子项，无法备份。");
            }

            var sourceLinkIds = sourceLinks.Select(link => link.Id).ToList();
            var sourceFacts = await _relocationRepository.GetFilingFactsBySourceLinksAsync(
                FilingFactSourceLinkType.ElectronicMediaItemLink,
                sourceLinkIds);
            if (sourceFacts.Count != sourceLinks.Count)
            {
                throw new InvalidOperationException("部分源资料子项缺少立档事实，无法生成可检索备份。");
            }

            var primaryFilingFactIdByOriginalLinkId = sourceFacts.ToDictionary(
                fact => fact.SourceLinkId,
                fact => fact.Id);

            var mediaItemIds = sourceLinks
                .Select(link => link.YearlyArchiveRegisterMediaItemId)
                .Distinct()
                .ToList();
            var mediaItems = await _filingRepository.GetElectronicMediaItemsForArchivingAsync(mediaItemIds);
            var mediaItemById = mediaItems.ToDictionary(item => item.Id);

            var writeItems = new List<BackupElectronicLinkWriteItem>();
            foreach (var sourceLink in sourceLinks)
            {
                if (!mediaItemById.TryGetValue(sourceLink.YearlyArchiveRegisterMediaItemId, out var mediaItem))
                {
                    throw new InvalidOperationException($"未找到资料子项 [{sourceLink.YearlyArchiveRegisterMediaItemId}]。");
                }

                var clone = ElectronicArchiveMediaItemCloneSupport.CloneForBackup(mediaItem);
                _filingRepository.AddRegisterMediaItem(clone);
                await _filingRepository.SaveChangesAsync();

                string filingPath = string.IsNullOrWhiteSpace(sourceLink.FilingStoragePath)
                    ? clone.StoragePath?.Trim() ?? string.Empty
                    : sourceLink.FilingStoragePath.Trim();

                var newLink = new YearlyElectronicArchiveUnitMediaItemLink
                {
                    ElectronicArchiveUnit = targetUnit,
                    MediaItem = clone,
                    YearlyArchiveRegisterMediaItemId = clone.Id,
                    FilingStoragePath = filingPath,
                    MediumCode = mediumCode.Trim(),
                    FormNo = sourceLink.FormNo?.Trim() ?? string.Empty,
                    MaterialName = sourceLink.MaterialName?.Trim() ?? string.Empty,
                    ItemName = sourceLink.ItemName?.Trim() ?? clone.ContentDesc?.Trim() ?? string.Empty,
                    DataSizeMb = sourceLink.DataSizeMb > 0
                        ? sourceLink.DataSizeMb
                        : ElectronicMediaItemSupport.ResolveMediaItemDataSizeMb(clone),
                    CreatedAt = operatedAt
                };

                targetUnit.MediaItemLinks.Add(newLink);
                _filingRepository.AddElectronicArchiveUnitMediaItemLink(newLink);
                await _filingRepository.SaveChangesAsync();

                writeItems.Add(new BackupElectronicLinkWriteItem
                {
                    Link = newLink,
                    OriginalSourceLinkId = sourceLink.Id
                });
            }

            return new ElectronicBackupCloneResult
            {
                WriteItems = writeItems,
                PrimaryFilingFactIdByOriginalLinkId = primaryFilingFactIdByOriginalLinkId
            };
        }

        private async Task ValidateBackupMediumCapacityAsync(
            string mediumCode,
            IReadOnlyCollection<YearlyElectronicArchiveUnitMediaItemLink> sourceLinks)
        {
            decimal pendingMb = sourceLinks.Sum(link =>
                link.DataSizeMb > 0 ? link.DataSizeMb : 0m);
            if (pendingMb <= 0)
            {
                return;
            }

            var hardDisk = await _filingRepository.GetHardDiskMediumByDiskCodeWithLedgerAsync(mediumCode.Trim());
            decimal totalMb = hardDisk == null ? 0 : ElectronicMediaCapacitySupport.ParseCapacityTextToMb(hardDisk.Capacity);
            if (totalMb <= 0)
            {
                throw new InvalidOperationException(
                    $"无法获取目标介质 [{mediumCode}] 的容量信息，请先在台账中登记容量后再执行备份。");
            }

            var existingLinks = await _filingRepository.GetElectronicArchiveUnitMediaItemLinksByMediumCodeAsync(mediumCode.Trim());
            decimal usedMb = existingLinks.Sum(link => link.DataSizeMb);
            decimal availableMb = totalMb - usedMb;
            if (availableMb < pendingMb)
            {
                throw new InvalidOperationException(
                    $"目标介质可用容量不足：可用 {ElectronicMediaCapacitySupport.FormatCapacityMb(Math.Max(0, availableMb))}，"
                    + $"本次备份数据量 {ElectronicMediaCapacitySupport.FormatCapacityMb(pendingMb)}。");
            }
        }

        private async Task<string> GenerateNextElectronicArchiveNoAsync(string year)
        {
            if (string.IsNullOrWhiteSpace(year))
            {
                year = DateTime.Now.Year.ToString();
            }

            string prefix = $"年度电子-{year}-";
            var lastUnit = await _filingRepository.GetLastElectronicUnitByPrefixAsync(prefix);
            int nextSeq = 1;
            if (lastUnit != null)
            {
                string parts = lastUnit.ElectronicArchiveNo.Substring(prefix.Length);
                if (int.TryParse(parts, out int current))
                {
                    nextSeq = current + 1;
                }
            }

            return $"{prefix}{nextSeq:D3}";
        }

        private static string ResolveHardDiskBagCarrierType(YearlyElectronicArchiveUnit source)
        {
            if (IsHardDiskCarrier(source.StorageCarrierType))
            {
                return source.StorageCarrierType.Trim();
            }

            return "硬盘";
        }

        private static string BuildBackupContentSummary(YearlyElectronicArchiveUnit source)
        {
            string summary = source.ContentSummary?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(summary)
                ? $"备份自 {source.ElectronicArchiveNo}"
                : $"{summary}（备份自 {source.ElectronicArchiveNo}）";
        }

        private void AppendBackupRelocationItems(
            ArchiveRelocationExecutionContext context,
            YearlyElectronicArchiveUnit source,
            IReadOnlyList<BackupElectronicLinkWriteItem> writeItems,
            IReadOnlyDictionary<int, int> primaryFilingFactIdByOriginalLinkId)
        {
            foreach (var item in writeItems)
            {
                if (!primaryFilingFactIdByOriginalLinkId.TryGetValue(item.OriginalSourceLinkId, out int primaryFactId))
                {
                    continue;
                }

                context.RelocationItems.Add(new YearlyArchiveRelocationItem
                {
                    FilingFactId = primaryFactId,
                    SourceLinkId = item.OriginalSourceLinkId,
                    SourceLinkType = FilingFactSourceLinkType.ElectronicMediaItemLink,
                    BeforeContainerCode = source.ElectronicArchiveNo.Trim(),
                    BeforeStorageLocation = source.StorageLocation?.Trim() ?? string.Empty,
                    AfterContainerCode = context.TargetContainerCode,
                    AfterStorageLocation = context.TargetStorageLocation
                });
            }
        }

        private sealed class ElectronicBackupCloneResult
        {
            public List<BackupElectronicLinkWriteItem> WriteItems { get; init; } = [];

            public Dictionary<int, int> PrimaryFilingFactIdByOriginalLinkId { get; init; } = [];
        }
    }
}
