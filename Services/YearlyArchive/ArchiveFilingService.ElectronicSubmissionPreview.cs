using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 电子介质立档拟执行逻辑预览：复用提交校验，仅生成变更报告，不写入数据库。
    /// </summary>
    public partial class ArchiveFilingService
    {
        private const string PreviewApplicationNoPlaceholder = "(拟生成)";

        /// <inheritdoc/>
        public async Task<ElectronicArchiveSubmissionResult> PreviewNewElectronicArchiveUnitAsync(
            ElectronicArchiveSubmissionRequest request,
            User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.ArchiveUnit);

            var (mediaItemIds, mediaItems, mediaEntries) = await ResolveElectronicSubmissionAsync(request);
            EnrichBorrowedHardDiskSubmissionAsync(request, mediaEntries);
            ValidateElectronicSubmissionRequest(request, mediaItemIds, mediaEntries, requireExistingUnitId: false);
            await ValidateCopySubmissionMediumCapacityAsync(request, mediaItems);

            var archiveUnit = CreateSubmissionArchiveUnit(request.ArchiveUnit, currentUser);
            ApplyOpticalDiscSingleArchiveRules(request, archiveUnit, mediaEntries);
            var records = mediaEntries
                .Select(item => item.RegisterRecord!)
                .DistinctBy(item => item.Id)
                .ToList();

            ValidateNewElectronicArchiveConstraints(archiveUnit, records);

            bool exists = await IsElectronicArchiveNoExistsAsync(archiveUnit.ElectronicArchiveNo);
            if (exists)
            {
                throw new InvalidOperationException($"编号 {archiveUnit.ElectronicArchiveNo} 已存在，请重新生成或手动修改。");
            }

            var changeTracker = new ElectronicArchiveSubmissionChangeTracker();
            changeTracker.BeginSection("提交概要");
            changeTracker.AddLine($"立档方式：新建电子介质袋 / 模式 [{request.SubmissionMode}]");
            changeTracker.AddLine($"目标电子袋编号：{archiveUnit.ElectronicArchiveNo}");
            changeTracker.AddLine($"本次入袋明细数：{mediaItemIds.Count}");
            AppendRetainedHardDiskUsageSummary(changeTracker, request, archiveUnit);
            changeTracker.AddLine("说明：以下为拟执行结果预览，尚未写入数据库。");

            PreviewPersistPendingExternalHardDisk(request.PendingExternalHardDisk, changeTracker);

            var borrowedHardDiskCandidate = await ResolveBorrowedHardDiskCandidateForSubmissionAsync(request, mediaEntries);
            await PreviewCreateElectronicArchiveUnitAsync(
                archiveUnit,
                mediaEntries,
                mediaItemIds,
                borrowedHardDiskCandidate,
                currentUser,
                changeTracker,
                request.PendingExternalHardDisk);

            var filingMediaEntryIds = mediaEntries.Select(entry => entry.Id).ToList();
            await PreviewFinalizeRetainedHardDiskAfterSubmissionAsync(
                request with { BorrowedHardDiskCandidate = borrowedHardDiskCandidate },
                archiveUnit,
                filingMediaEntryIds,
                currentUser,
                changeTracker);

            return new ElectronicArchiveSubmissionResult(
                archiveUnit.ElectronicArchiveNo,
                mediaItemIds.Count,
                false,
                changeTracker.BuildReport());
        }

        /// <inheritdoc/>
        public async Task<ElectronicArchiveSubmissionResult> PreviewAppendElectronicArchiveUnitAsync(
            ElectronicArchiveSubmissionRequest request,
            User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.ArchiveUnit);

            var (mediaItemIds, mediaItems, mediaEntries) = await ResolveElectronicSubmissionAsync(request);
            EnrichBorrowedHardDiskSubmissionAsync(request, mediaEntries);
            ValidateElectronicSubmissionRequest(request, mediaItemIds, mediaEntries, requireExistingUnitId: true);

            var archiveUnit = CreateSubmissionArchiveUnit(request.ArchiveUnit, currentUser);
            ApplyOpticalDiscSingleArchiveRules(request, archiveUnit, mediaEntries);

            int unitId = request.ExistingElectronicArchiveUnitId!.Value;
            var existingUnit = await _archiveFilingRepository.GetElectronicArchiveUnitWithDetailsAsync(unitId);
            if (existingUnit == null)
            {
                throw new InvalidOperationException($"未找到指定电子立档单元：{unitId}");
            }

            await ValidateCopySubmissionMediumCapacityAsync(request, mediaItems, existingUnit);

            var records = mediaEntries
                .Select(item => item.RegisterRecord!)
                .DistinctBy(item => item.Id)
                .ToList();

            ValidateElectronicAppendConstraints(existingUnit, archiveUnit, records);

            var changeTracker = new ElectronicArchiveSubmissionChangeTracker();
            changeTracker.BeginSection("提交概要");
            changeTracker.AddLine($"立档方式：并入既有电子介质袋 / 模式 [{request.SubmissionMode}]");
            changeTracker.AddLine($"目标电子袋编号：{archiveUnit.ElectronicArchiveNo}");
            changeTracker.AddLine($"本次入袋明细数：{mediaItemIds.Count}");
            AppendRetainedHardDiskUsageSummary(changeTracker, request, archiveUnit);
            changeTracker.AddLine("说明：以下为拟执行结果预览，尚未写入数据库。");

            PreviewPersistPendingExternalHardDisk(request.PendingExternalHardDisk, changeTracker);

            var borrowedHardDiskCandidate = await ResolveBorrowedHardDiskCandidateForSubmissionAsync(request, mediaEntries);
            DateTime archivedAt = DateTime.Now;
            var mergedUnit = MergeElectronicArchiveUnit(existingUnit, archiveUnit, archivedAt);
            var linkedMedia = await PreviewPrepareElectronicArchiveUnitAsync(
                mergedUnit,
                archivedAt,
                borrowedHardDiskCandidate,
                changeTracker,
                request.PendingExternalHardDisk);

            changeTracker.BeginSection("电子介质袋（YearlyElectronicArchiveUnit）");
            changeTracker.AddLine(
                $"并入电子介质袋 [{existingUnit.ElectronicArchiveNo}]；本次新增关联 {mediaItems.Count} 条资料明细；"
                + $"档口 [{mergedUnit.StorageLocation}]；关联硬盘 [{mergedUnit.LinkedMediumCodes}]");

            foreach (var item in mediaItems)
            {
                string formNo = item.MediaEntry?.RegisterRecord?.FormNo?.Trim() ?? "-";
                changeTracker.AddLine(
                    $"资料子项 Id={item.Id}（单号 {formNo} / {item.ContentDesc}）将并入电子袋");
            }

            var appendFilingMediaEntryIds = mediaEntries.Select(entry => entry.Id).ToList();
            if (borrowedHardDiskCandidate == null || linkedMedia.Count > 0)
            {
                await PreviewBorrowedHardDiskReturnAsync(
                    mergedUnit,
                    linkedMedia,
                    borrowedHardDiskCandidate,
                    currentUser,
                    archivedAt,
                    changeTracker,
                    appendFilingMediaEntryIds);
            }

            PreviewRegisterRecordStatusUpdates(records, mediaItemIds, changeTracker, archivedAt);

            await PreviewFinalizeRetainedHardDiskAfterSubmissionAsync(
                request with { BorrowedHardDiskCandidate = borrowedHardDiskCandidate },
                mergedUnit,
                appendFilingMediaEntryIds,
                currentUser,
                changeTracker);

            return new ElectronicArchiveSubmissionResult(
                archiveUnit.ElectronicArchiveNo,
                mediaItemIds.Count,
                true,
                changeTracker.BuildReport());
        }

        private static void PreviewPersistPendingExternalHardDisk(
            PendingExternalHardDiskRegistration? medium,
            ElectronicArchiveSubmissionChangeTracker changeTracker)
        {
            if (medium == null)
            {
                return;
            }

            string targetLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(
                string.IsNullOrWhiteSpace(medium.FormattedBlankTargetLocation)
                    ? medium.CurrentLocation
                    : medium.FormattedBlankTargetLocation);

            changeTracker.BeginSection("外来硬盘登记入账（HardDiskMedium / HardDiskLedger）");
            changeTracker.AddLine(
                $"硬盘 [{medium.DiskCode}] 将写入资料室台账；登记方式 [{medium.RegistrationMethod}]；"
                + $"状态 [{medium.CurrentStatus}]；介质属性 [{medium.MediaNature}]；存放位置 [{targetLocation}]");
        }

        private async Task PreviewCreateElectronicArchiveUnitAsync(
            YearlyElectronicArchiveUnit archiveUnit,
            IReadOnlyList<YearlyArchiveRegisterMedia> mediaEntries,
            IReadOnlyCollection<int> mediaEntryIds,
            HardDiskMediaReturnCandidate? borrowedHardDiskCandidate,
            User? currentUser,
            ElectronicArchiveSubmissionChangeTracker changeTracker,
            PendingExternalHardDiskRegistration? pendingExternalHardDisk = null)
        {
            DateTime archivedAt = DateTime.Now;
            var records = mediaEntries
                .Select(item => item.RegisterRecord!)
                .DistinctBy(item => item.Id)
                .ToList();

            var linkedMedia = await PreviewPrepareElectronicArchiveUnitAsync(
                archiveUnit,
                archivedAt,
                borrowedHardDiskCandidate,
                changeTracker,
                pendingExternalHardDisk);

            changeTracker.BeginSection("电子介质袋（YearlyElectronicArchiveUnit）");
            changeTracker.AddLine(
                $"新建电子介质袋 [{archiveUnit.ElectronicArchiveNo}]；项目 [{archiveUnit.ProjectName}]；年度 [{archiveUnit.Year}]；"
                + $"载体 [{archiveUnit.StorageCarrierType}]；档口 [{archiveUnit.StorageLocation}]；关联硬盘 [{archiveUnit.LinkedMediumCodes}]");

            changeTracker.BeginSection("登记介质关联（YearlyElectronicArchiveUnitMediaLink / MediumLink）");
            foreach (var entry in mediaEntries)
            {
                string formNo = entry.RegisterRecord?.FormNo?.Trim() ?? "-";
                changeTracker.AddLine(
                    $"登记介质条目 Id={entry.Id}（单号 {formNo} / {entry.MediaType}）将关联至电子袋 [{archiveUnit.ElectronicArchiveNo}]");
            }

            foreach (var linked in linkedMedia)
            {
                changeTracker.AddLine($"硬盘 [{linked.DiskCode}] 将写入 YearlyElectronicArchiveUnitMediumLink");
            }

            PreviewOpticalDiscLinks(archiveUnit, changeTracker);

            var filingMediaEntryIds = mediaEntries.Select(entry => entry.Id).ToList();
            if (borrowedHardDiskCandidate == null || linkedMedia.Count > 0)
            {
                await PreviewBorrowedHardDiskReturnAsync(
                    archiveUnit,
                    linkedMedia,
                    borrowedHardDiskCandidate,
                    currentUser,
                    archivedAt,
                    changeTracker,
                    filingMediaEntryIds);
            }

            PreviewRegisterRecordStatusUpdates(records, mediaEntryIds, changeTracker, archivedAt);
        }

        private async Task<List<HardDiskMedium>> PreviewPrepareElectronicArchiveUnitAsync(
            YearlyElectronicArchiveUnit unit,
            DateTime archivedAt,
            HardDiskMediaReturnCandidate? borrowedHardDiskCandidate,
            ElectronicArchiveSubmissionChangeTracker changeTracker,
            PendingExternalHardDiskRegistration? pendingExternalHardDisk = null)
        {
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
            await ValidateMediumLinkConflictsAsync(unit.Id, unit.ElectronicArchiveNo, linkedMedia);

            if (RequiresHardDiskLink(unit) && linkedMedia.Count != 1)
            {
                throw new InvalidOperationException("电子介质袋需要且仅能关联一块入袋硬盘。");
            }

            if (RequiresHardDiskLink(unit))
            {
                unit.MediaCount = 1;
            }

            foreach (var medium in linkedMedia)
            {
                if (medium.Ledger == null)
                {
                    continue;
                }

                var projected = ComputeProjectedLedgerAfterSync(medium, unit, borrowedHardDiskCandidate);
                changeTracker.AddLedgerChange(
                    medium.DiskCode,
                    medium.Ledger.MediaStatus,
                    projected.Status,
                    medium.Ledger.StorageLocation,
                    projected.Location,
                    medium.Ledger.MediaNature,
                    projected.Nature,
                    "入袋关联后同步 HardDiskLedger");
                if (!string.Equals(medium.Ledger.MediaStatus, projected.Status, StringComparison.Ordinal)
                    || !HardDiskLedgerSyncSupport.IsSameFullLocation(medium.Ledger.StorageLocation, projected.Location)
                    || !string.Equals(medium.Ledger.MediaNature, projected.Nature, StringComparison.Ordinal))
                {
                    changeTracker.AddLine(
                        $"硬盘 [{medium.DiskCode}] 将写入 HardDiskMediaTransaction 流转记录（{HardDiskLedgerSyncSupport.ResolveSyncTransactionType(
                            new HardDiskLedgerSyncSupport.LedgerSnapshot(
                                medium.Ledger.MediaStatus,
                                medium.Ledger.StorageLocation,
                                medium.Ledger.MediaNature),
                            new HardDiskLedger
                            {
                                MediaStatus = projected.Status,
                                StorageLocation = projected.Location,
                                MediaNature = projected.Nature
                            })}）。");
                }
            }

            return linkedMedia;
        }

        private static (string Status, string Location, string Nature) ComputeProjectedLedgerAfterSync(
            HardDiskMedium medium,
            YearlyElectronicArchiveUnit unit,
            HardDiskMediaReturnCandidate? borrowedHardDiskCandidate)
        {
            var ledger = medium.Ledger
                ?? throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 缺少台账信息。");

            if (ledger.MediaStatus == HardDiskMedium.StatusOutDestroyed || ledger.MediaStatus == HardDiskMedium.StatusOutLost)
            {
                throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 当前状态为 {ledger.MediaStatus}，不能关联电子立档。");
            }

            string afterStatus = ledger.MediaStatus;
            string afterLocation = ledger.StorageLocation ?? string.Empty;
            string afterNature = HardDiskMedium.NatureDataCarrier;

            bool isBorrowedRetainedMedium = borrowedHardDiskCandidate != null
                && medium.Id == borrowedHardDiskCandidate.MediumId
                && string.Equals(medium.DiskCode, borrowedHardDiskCandidate.DiskCode, StringComparison.Ordinal);

            if (afterStatus == HardDiskMedium.StatusInStockBlank
                || (!isBorrowedRetainedMedium
                    && (afterStatus == HardDiskMedium.StatusOutTemporary
                        || afterStatus == HardDiskMedium.StatusOutLongTerm)))
            {
                afterStatus = HardDiskMedium.StatusInStockData;
            }

            if (!isBorrowedRetainedMedium && !string.IsNullOrWhiteSpace(unit.StorageLocation))
            {
                afterLocation = unit.StorageLocation.Trim();
            }

            return (afterStatus, afterLocation, afterNature);
        }

        private static void PreviewOpticalDiscLinks(YearlyElectronicArchiveUnit unit, ElectronicArchiveSubmissionChangeTracker changeTracker)
        {
            if (!ArchiveFilingBusinessRules.IsOpticalDiscArchiveCarrierType(unit.StorageCarrierType))
            {
                return;
            }

            int discCount = unit.MediaCount > 0 ? unit.MediaCount : 1;
            string baseDiscCode = unit.ElectronicArchiveNo.Trim();

            changeTracker.BeginSection("光盘介质（OpticalDiscMedium / DiscLink）");
            for (int index = 1; index <= discCount; index++)
            {
                string discCode = discCount == 1
                    ? baseDiscCode
                    : $"{baseDiscCode}-DISC-{index:D2}";

                changeTracker.AddLine(
                    $"光盘 [{discCode}] 将写入或更新台账；状态 → {OpticalDiscMedium.StatusInStock}；档口 [{unit.StorageLocation}]");
            }
        }

        private async Task PreviewBorrowedHardDiskReturnAsync(
            YearlyElectronicArchiveUnit archiveUnit,
            IReadOnlyList<HardDiskMedium> linkedMedia,
            HardDiskMediaReturnCandidate? borrowedHardDiskCandidate,
            User? currentUser,
            DateTime archivedAt,
            ElectronicArchiveSubmissionChangeTracker changeTracker,
            IReadOnlyCollection<int>? filingMediaEntryIds = null)
        {
            if (borrowedHardDiskCandidate == null || linkedMedia.Count != 1)
            {
                if (borrowedHardDiskCandidate != null)
                {
                    changeTracker.AddDeferred(
                        $"借出硬盘 [{borrowedHardDiskCandidate.DiskCode}] 未生成自动归还登记：关联入袋硬盘数量={linkedMedia.Count}（需且仅能关联借出盘本身）。");
                }

                return;
            }

            var linkedMedium = linkedMedia[0];
            if (linkedMedium.Id != borrowedHardDiskCandidate.MediumId
                || !string.Equals(linkedMedium.DiskCode, borrowedHardDiskCandidate.DiskCode, StringComparison.Ordinal))
            {
                return;
            }

            var medium = await _archiveFilingRepository.GetHardDiskMediumByIdWithLedgerAsync(linkedMedium.Id);
            if (medium == null
                || !string.Equals(medium.DiskCode, borrowedHardDiskCandidate.DiskCode, StringComparison.Ordinal))
            {
                return;
            }

            if (!HardDiskRegisterLock.IsArchiveRegister(medium.RegisterLock))
            {
                return;
            }

            bool hasCompletedReturn = await _archiveFilingRepository.HasCompletedReturnApplicationAsync(
                medium.Id,
                borrowedHardDiskCandidate.SourceApplicationId);
            if (hasCompletedReturn)
            {
                changeTracker.AddDeferred(
                    $"借出硬盘 [{medium.DiskCode}] 已存在来源借出单 [{borrowedHardDiskCandidate.SourceApplicationId}] 对应的已办结归还记录，本次未重复写入。");
                return;
            }

            if (await _archiveFilingRepository.HasPendingRetainedRegisterEntriesForBorrowedDiskAsync(
                    medium.DiskCode,
                    filingMediaEntryIds))
            {
                changeTracker.AddDeferred(
                    $"借出硬盘 [{medium.DiskCode}] 仍有未立档的留存登记条目，暂未写入归还登记(资料)及台账变更。");
                return;
            }

            string returnLocation = string.IsNullOrWhiteSpace(archiveUnit.StorageLocation)
                ? medium.Ledger?.StorageLocation?.Trim() ?? string.Empty
                : archiveUnit.StorageLocation.Trim();
            if (string.IsNullOrWhiteSpace(returnLocation))
            {
                returnLocation = medium.Ledger?.StorageLocation?.Trim() ?? string.Empty;
            }

            string beforeStatus = medium.Ledger?.MediaStatus ?? string.Empty;
            string beforeLocation = medium.Ledger?.StorageLocation ?? string.Empty;
            string beforeNature = medium.Ledger?.MediaNature ?? string.Empty;

            changeTracker.AddApplication(
                PreviewApplicationNoPlaceholder,
                HardDiskMediaApplication.TypeReturnDataRegistration,
                medium.DiskCode,
                "自动办理归还登记(资料)，硬盘随资料留存于资料室");
            changeTracker.AddLedgerChange(
                medium.DiskCode,
                beforeStatus,
                HardDiskMedium.StatusInStockData,
                beforeLocation,
                returnLocation,
                beforeNature,
                HardDiskMedium.NatureDataCarrier,
                "已解除 HardDiskRegisterLock 占用");
            changeTracker.AddTransaction(
                medium.DiskCode,
                $"将写入归还流水；申请单 [{PreviewApplicationNoPlaceholder}]");
        }

        private async Task PreviewFinalizeRetainedHardDiskAfterSubmissionAsync(
            ElectronicArchiveSubmissionRequest request,
            YearlyElectronicArchiveUnit archiveUnit,
            IReadOnlyCollection<int> filingMediaEntryIds,
            User? currentUser,
            ElectronicArchiveSubmissionChangeTracker changeTracker)
        {
            if (!RequiresRetainedHardDiskFormatting(request))
            {
                return;
            }

            if (request.BorrowedHardDiskCandidate != null)
            {
                if (await _archiveFilingRepository.HasPendingRetainedRegisterEntriesForBorrowedDiskAsync(
                        request.BorrowedHardDiskCandidate.DiskCode,
                        filingMediaEntryIds))
                {
                    changeTracker.AddDeferred(
                        $"借出留存硬盘 [{request.BorrowedHardDiskCandidate.DiskCode}] 仍有未立档登记条目，本次未执行格式化空盘归还。");
                    return;
                }

                await PreviewCompleteFormattedBorrowedRetainedHardDiskAsync(
                    request.BorrowedHardDiskCandidate,
                    archiveUnit,
                    changeTracker);
                return;
            }

            if (request.PendingExternalHardDisk != null)
            {
                var registerRecordIds = await _archiveFilingRepository.GetRegisterRecordIdsForMediaEntriesAsync(filingMediaEntryIds);
                if (await _archiveFilingRepository.HasPendingExternalRetainedRegisterEntriesOnRecordsAsync(registerRecordIds))
                {
                    changeTracker.AddDeferred(
                        $"外来留存硬盘 [{request.PendingExternalHardDisk.DiskCode}] 所在登记单仍有未立档条目，本次未执行格式化空盘入库。");
                    return;
                }

                await PreviewCompleteFormattedExternalRetainedHardDiskAsync(
                    request.PendingExternalHardDisk,
                    archiveUnit,
                    changeTracker);
            }
        }

        private async Task PreviewCompleteFormattedBorrowedRetainedHardDiskAsync(
            HardDiskMediaReturnCandidate candidate,
            YearlyElectronicArchiveUnit archiveUnit,
            ElectronicArchiveSubmissionChangeTracker changeTracker)
        {
            var medium = await _archiveFilingRepository.GetHardDiskMediumByIdWithLedgerAsync(candidate.MediumId);
            if (medium == null)
            {
                throw new InvalidOperationException($"未找到需要格式化后归还的留存硬盘 [{candidate.DiskCode}]。");
            }

            bool hasCompletedReturn = await _archiveFilingRepository.HasCompletedReturnApplicationAsync(
                medium.Id,
                candidate.SourceApplicationId);
            if (hasCompletedReturn)
            {
                changeTracker.AddDeferred(
                    $"借出留存硬盘 [{medium.DiskCode}] 已完成空盘归还登记，本次未重复写入。");
                return;
            }

            string targetLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(
                await ResolveFormattedBorrowedHardDiskReturnLocationAsync(candidate));
            string beforeStatus = medium.Ledger?.MediaStatus ?? string.Empty;
            string beforeLocation = medium.Ledger?.StorageLocation ?? string.Empty;
            string beforeNature = medium.Ledger?.MediaNature ?? string.Empty;

            changeTracker.AddApplication(
                PreviewApplicationNoPlaceholder,
                HardDiskMediaApplication.TypeReturnBlankRegistration,
                medium.DiskCode,
                "留存源盘已格式化，办理空盘归还登记");
            changeTracker.AddLedgerChange(
                medium.DiskCode,
                beforeStatus,
                HardDiskMedium.StatusInStockBlank,
                beforeLocation,
                targetLocation,
                beforeNature,
                HardDiskMedium.NatureBlank,
                "已解除 HardDiskRegisterLock 占用");
            changeTracker.AddTransaction(
                medium.DiskCode,
                $"将写入空盘归还流水；申请单 [{PreviewApplicationNoPlaceholder}]；目标档口 [{targetLocation}]");
        }

        private async Task PreviewCompleteFormattedExternalRetainedHardDiskAsync(
            PendingExternalHardDiskRegistration pendingExternalHardDisk,
            YearlyElectronicArchiveUnit archiveUnit,
            ElectronicArchiveSubmissionChangeTracker changeTracker)
        {
            var medium = await _archiveFilingRepository.GetHardDiskMediumByDiskCodeWithLedgerAsync(pendingExternalHardDisk.DiskCode);
            if (medium == null)
            {
                changeTracker.AddDeferred(
                    $"外来硬盘 [{pendingExternalHardDisk.DiskCode}] 将在提交时先登记入账，再执行格式化空盘入库。");
                return;
            }

            string targetLocation = await ResolveBlankHardDiskSlotLocationAsync(pendingExternalHardDisk.FormattedBlankTargetLocation);
            string beforeStatus = medium.Ledger?.MediaStatus ?? string.Empty;
            string beforeLocation = medium.Ledger?.StorageLocation ?? string.Empty;

            changeTracker.AddLedgerChange(
                medium.DiskCode,
                beforeStatus,
                HardDiskMedium.StatusInStockBlank,
                beforeLocation,
                targetLocation,
                beforeNature: null,
                afterNature: HardDiskMedium.NatureBlank,
                "外来留存源盘已格式化并归入空白硬盘档口；已解除 HardDiskRegisterLock 占用");
            changeTracker.AddTransaction(
                medium.DiskCode,
                $"将写入格式化空盘入库流水；目标档口 [{targetLocation}]");
        }

        private static void PreviewRegisterRecordStatusUpdates(
            IEnumerable<YearlyArchiveRegisterRecord> records,
            IReadOnlyCollection<int> filingMediaEntryIds,
            ElectronicArchiveSubmissionChangeTracker changeTracker,
            DateTime archivedAt)
        {
            var filingEntryIdSet = filingMediaEntryIds.ToHashSet();

            changeTracker.BeginSection("登记单状态（YearlyArchiveRegisterRecord）");
            foreach (var record in records)
            {
                bool allElectronicArchived = record.MediaEntries
                    .Where(media => string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase))
                    .All(media => media.ElectronicArchiveUnitLinks.Any() || filingEntryIdSet.Contains(media.Id));

                string electronicStatus = allElectronicArchived
                    ? YearlyArchiveRegisterRecord.TrackArchived.ToString()
                    : YearlyArchiveRegisterRecord.TrackPending.ToString();

                string archivedDate = allElectronicArchived
                    ? archivedAt.ToString("yyyy-MM-dd")
                    : "—";

                changeTracker.AddLine(
                    $"登记单 [{record.FormNo}]：ElectronicArchiveStatus={electronicStatus}；Status={record.Status}；ArchivedDate={archivedDate}");
            }
        }
    }
}
