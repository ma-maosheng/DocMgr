using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Services.HardDiskMedia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 电子立档中的硬盘留存、格式化归还、关联硬盘同步逻辑。
    /// </summary>
    public partial class ArchiveFilingService
    {
        private async Task PersistPendingExternalHardDiskAsync(PendingExternalHardDiskRegistration? medium, User? currentUser)
        {
            if (medium == null)
            {
                return;
            }

            string targetLocation = await ResolveBlankHardDiskSlotLocationAsync(medium.FormattedBlankTargetLocation);

            await _hardDiskMediaService.SaveMediumAsync(new HardDiskMedium
            {
                DiskCode = medium.DiskCode,
                SerialNumber = medium.SerialNumber,
                DiskType = medium.DiskType,
                Brand = medium.Brand,
                Capacity = medium.Capacity,
                InterfaceType = medium.InterfaceType,
                RegisterPerson = medium.RegisterPerson,
                RegisterDate = medium.RegisterDate,
                FactoryDate = medium.FactoryDate,
                RegistrationMethod = medium.RegistrationMethod,
                Ledger = new HardDiskLedger
                {
                    DiskCode = medium.DiskCode,
                    MediaStatus = medium.CurrentStatus,
                    MediaNature = medium.MediaNature,
                    StorageLocation = targetLocation,
                    HolderOrOrganization = medium.CurrentHolder,
                    NeedReturn = medium.NeedReturn,
                    RegisterPerson = medium.RegisterPerson,
                    RegisterDate = medium.RegisterDate,
                    Remark = medium.Remark
                },
                Remark = medium.Remark
            }, currentUser);

            var persistedMedium = await _archiveFilingRepository.GetHardDiskMediumByDiskCodeWithLedgerAsync(medium.DiskCode);
            _submissionChangeTracker?.BeginSection("外来硬盘登记入账（HardDiskMedium / HardDiskLedger）");
            _submissionChangeTracker?.AddLine(
                $"硬盘 [{medium.DiskCode}] 已写入资料室台账；登记方式 [{medium.RegistrationMethod}]；"
                + $"状态 [{persistedMedium?.Ledger?.MediaStatus ?? medium.CurrentStatus}]；"
                + $"介质属性 [{persistedMedium?.Ledger?.MediaNature ?? medium.MediaNature}]；"
                + $"存放位置 [{persistedMedium?.Ledger?.StorageLocation ?? targetLocation}]");
        }

        private async Task<BorrowedHardDiskReturnDiagnosticResult> MaintainBorrowedHardDiskReturnAsync(
            YearlyElectronicArchiveUnit archiveUnit,
            IReadOnlyList<HardDiskMedium> linkedMedia,
            HardDiskMediaReturnCandidate? borrowedHardDiskCandidate,
            User? currentUser,
            DateTime archivedAt,
            IReadOnlyCollection<int>? filingMediaEntryIds = null)
        {
            ArgumentNullException.ThrowIfNull(archiveUnit);
            ArgumentNullException.ThrowIfNull(linkedMedia);

            WriteBorrowedHardDiskReturnDiagnostic(
                stage: "Begin",
                message: $"开始检查自动归还登记。LinkedMediaCount={linkedMedia.Count}",
                archiveUnit: archiveUnit,
                candidate: borrowedHardDiskCandidate);

            if (borrowedHardDiskCandidate == null || linkedMedia.Count != 1)
            {
                if (borrowedHardDiskCandidate != null)
                {
                    _submissionChangeTracker?.AddDeferred(
                        $"借出硬盘 [{borrowedHardDiskCandidate.DiskCode}] 未生成自动归还登记：关联入袋硬盘数量={linkedMedia.Count}（需且仅能关联借出盘本身）。");
                }

                return ReturnBorrowedHardDiskDiagnostic(
                    stage: "SkipNoCandidateOrInvalidLinkedMediaCount",
                    message: $"跳过自动归还登记。CandidateNull={borrowedHardDiskCandidate == null}，LinkedMediaCount={linkedMedia.Count}",
                    archiveUnit: archiveUnit,
                    candidate: borrowedHardDiskCandidate,
                    shouldWarnInDebug: borrowedHardDiskCandidate != null && linkedMedia.Count != 1);
            }

            var linkedMedium = linkedMedia[0];
            if (linkedMedium.Id != borrowedHardDiskCandidate.MediumId ||
                !string.Equals(linkedMedium.DiskCode, borrowedHardDiskCandidate.DiskCode, StringComparison.Ordinal))
            {
                return ReturnBorrowedHardDiskDiagnostic(
                    stage: "SkipCandidateMismatch",
                    message: $"跳过自动归还登记。LinkedMediumId={linkedMedium.Id}，CandidateMediumId={borrowedHardDiskCandidate.MediumId}，LinkedDiskCode={linkedMedium.DiskCode}，CandidateDiskCode={borrowedHardDiskCandidate.DiskCode}",
                    archiveUnit: archiveUnit,
                    candidate: borrowedHardDiskCandidate,
                    linkedMedium: linkedMedium,
                    shouldWarnInDebug: true);
            }

            var medium = await _archiveFilingRepository.GetHardDiskMediumByIdWithLedgerAsync(linkedMedium.Id);
            if (medium == null ||
                !string.Equals(medium.DiskCode, borrowedHardDiskCandidate.DiskCode, StringComparison.Ordinal))
            {
                return ReturnBorrowedHardDiskDiagnostic(
                    stage: "SkipTrackedMediumMissingOrMismatch",
                    message: medium == null
                        ? "跳过自动归还登记。未找到跟踪态硬盘介质。"
                        : $"跳过自动归还登记。TrackedDiskCode={medium.DiskCode} 与 CandidateDiskCode={borrowedHardDiskCandidate.DiskCode} 不一致。",
                    archiveUnit: archiveUnit,
                    candidate: borrowedHardDiskCandidate,
                    linkedMedium: linkedMedium,
                    trackedMedium: medium,
                    shouldWarnInDebug: true);
            }

            if (!HardDiskRegisterLock.IsArchiveRegister(medium.RegisterLock))
            {
                return ReturnBorrowedHardDiskDiagnostic(
                    stage: "SkipMediumNotArchiveRegisterLocked",
                    message: $"跳过自动归还登记。硬盘 [{medium.DiskCode}] 未处于年度资料登记占用锁状态。",
                    archiveUnit: archiveUnit,
                    candidate: borrowedHardDiskCandidate,
                    linkedMedium: linkedMedium,
                    trackedMedium: medium,
                    shouldWarnInDebug: true);
            }

            bool hasCompletedReturn = await _archiveFilingRepository.HasCompletedReturnApplicationAsync(medium.Id, borrowedHardDiskCandidate.SourceApplicationId);
            if (hasCompletedReturn)
            {
                _submissionChangeTracker?.AddDeferred(
                    $"借出硬盘 [{medium.DiskCode}] 已存在来源借出单 [{borrowedHardDiskCandidate.SourceApplicationId}] 对应的已办结归还记录，本次未重复写入。");

                return ReturnBorrowedHardDiskDiagnostic(
                    stage: "SkipCompletedReturnExists",
                    message: $"跳过自动归还登记。已存在来源借出单 [{borrowedHardDiskCandidate.SourceApplicationId}] 对应的已办结归还记录。",
                    archiveUnit: archiveUnit,
                    candidate: borrowedHardDiskCandidate,
                    linkedMedium: linkedMedium,
                    trackedMedium: medium,
                    shouldWarnInDebug: false);
            }

            string returnLocation = string.IsNullOrWhiteSpace(archiveUnit.StorageLocation)
                ? medium.Ledger?.StorageLocation?.Trim() ?? string.Empty
                : archiveUnit.StorageLocation.Trim();
            if (string.IsNullOrWhiteSpace(returnLocation))
            {
                returnLocation = medium.Ledger?.StorageLocation?.Trim() ?? string.Empty;
            }

            if (await _archiveFilingRepository.HasPendingRetainedRegisterEntriesForBorrowedDiskAsync(
                    medium.DiskCode,
                    filingMediaEntryIds))
            {
                _submissionChangeTracker?.AddDeferred(
                    $"借出硬盘 [{medium.DiskCode}] 仍有未立档的留存登记条目，暂未写入归还登记(资料)及台账变更。");

                return ReturnBorrowedHardDiskDiagnostic(
                    stage: "SkipPendingRegisterEntriesRemain",
                    message: $"跳过自动归还登记。借出硬盘 [{medium.DiskCode}] 仍有未立档的留存登记介质条目，待全部立档完成后再同步台账。",
                    archiveUnit: archiveUnit,
                    candidate: borrowedHardDiskCandidate,
                    linkedMedium: linkedMedium,
                    trackedMedium: medium,
                    shouldWarnInDebug: false);
            }

            WriteBorrowedHardDiskReturnDiagnostic(
                stage: "ProceedCreateReturnRecords",
                message: $"命中自动归还登记。ReturnLocation={returnLocation}",
                archiveUnit: archiveUnit,
                candidate: borrowedHardDiskCandidate,
                linkedMedium: linkedMedium,
                trackedMedium: medium);

            string operatorName = currentUser?.RealName?.Trim() ?? currentUser?.LoginName?.Trim() ?? string.Empty;
            string sourceApplicationNo = borrowedHardDiskCandidate.SourceApplicationNo?.Trim() ?? string.Empty;
            string archiveUnitNo = archiveUnit.ElectronicArchiveNo.Trim();
            string archiveSummary = archiveUnit.ContentSummary.Trim();
            string autoReturnReason = $"年度资料电子立档 [{archiveUnitNo}] 完成后，借出硬盘 [{medium.DiskCode}] 随资料留存于资料室，自动办理归还登记。";
            string autoReturnRemark = string.IsNullOrWhiteSpace(sourceApplicationNo)
                ? $"自动归还登记：电子立档 {archiveUnitNo}"
                : $"自动归还登记：电子立档 {archiveUnitNo}，来源借出单 {sourceApplicationNo}";

            var returnApplication = new HardDiskMediaApplication
            {
                ApplicationNo = await _hardDiskMediaService.GenerateNextReturnRegistrationNoAsync(),
                MediumId = medium.Id,
                SourceApplicationId = borrowedHardDiskCandidate.SourceApplicationId,
                ApplicationType = HardDiskMediaApplication.TypeReturnDataRegistration,
                ApplicationStatus = HardDiskMediaApplication.StatusCompleted,
                ApplicantName = borrowedHardDiskCandidate.ApplicantName?.Trim() ?? string.Empty,
                ApplicantDept = borrowedHardDiskCandidate.ApplicantDept?.Trim() ?? string.Empty,
                ApplyTime = archivedAt,
                Reason = autoReturnReason,
                TargetPersonOrUnit = "资料室",
                CurrentLocation = string.IsNullOrWhiteSpace(borrowedHardDiskCandidate.BorrowedLocation)
                    ? medium.Ledger?.StorageLocation?.Trim() ?? string.Empty
                    : borrowedHardDiskCandidate.BorrowedLocation.Trim(),
                TargetLocation = returnLocation,
                ExpectedReturnDate = borrowedHardDiskCandidate.ExpectedReturnDate,
                RelatedBatch = archiveUnitNo,
                RelatedArchiveTitle = archiveSummary,
                FormatConfirmation = "不适用",
                SignedAttachmentUploaded = true,
                SignedAttachmentUploadedTime = archivedAt,
                SignedAttachmentUploader = operatorName,
                ApprovedBy = operatorName,
                ApprovedTime = archivedAt,
                ApprovalOpinion = "随资料立档留存资料室",
                ExecutedBy = operatorName,
                ExecutedTime = archivedAt,
                Remark = autoReturnRemark,
                CreatedTime = archivedAt,
                UpdatedTime = archivedAt
            };

            _archiveFilingRepository.AddHardDiskMediaApplication(returnApplication);

            var ledger = EnsureHardDiskLedger(medium, archivedAt);
            string beforeStatus = ledger.MediaStatus;
            string beforeLocation = ledger.StorageLocation;
            string beforeNature = ledger.MediaNature;
            ledger.MediaStatus = HardDiskMedium.StatusInStockData;
            ledger.MediaNature = HardDiskMedium.NatureDataCarrier;
            ledger.HolderOrOrganization = "资料室";
            ledger.StorageLocation = returnLocation;
            ledger.NeedReturn = false;
            ledger.UpdatedTime = archivedAt;
            medium.RegisterLock = null;
            medium.UpdatedTime = archivedAt;

            await _archiveFilingRepository.SaveChangesAsync();

            _archiveFilingRepository.AddHardDiskMediaTransaction(new HardDiskMediaTransaction
            {
                MediumId = medium.Id,
                ApplicationId = returnApplication.Id,
                TransactionType = HardDiskMediaTransaction.TypeReturnRegistration,
                BeforeStatus = beforeStatus,
                AfterStatus = ledger.MediaStatus,
                BeforeLocation = beforeLocation,
                AfterLocation = ledger.StorageLocation,
                OperatorName = operatorName,
                OperateTime = archivedAt,
                RelatedPerson = borrowedHardDiskCandidate.ApplicantName?.Trim() ?? string.Empty,
                TargetOrganization = "资料室",
                NeedReturn = false,
                ExpectedReturnDate = borrowedHardDiskCandidate.ExpectedReturnDate,
                ActualReturnDate = archivedAt,
                RelatedBatch = returnApplication.RelatedBatch,
                RelatedArchiveTitle = returnApplication.RelatedArchiveTitle,
                Description = returnApplication.Reason,
                Remark = returnApplication.Remark
            });

            await _archiveFilingRepository.SaveChangesAsync();

            _submissionChangeTracker?.AddApplication(
                returnApplication.ApplicationNo,
                HardDiskMediaApplication.TypeReturnDataRegistration,
                medium.DiskCode,
                "自动办理归还登记(资料)，硬盘随资料留存于资料室");
            _submissionChangeTracker?.AddLedgerChange(
                medium.DiskCode,
                beforeStatus,
                ledger.MediaStatus,
                beforeLocation,
                ledger.StorageLocation,
                beforeNature,
                ledger.MediaNature,
                "已解除 HardDiskRegisterLock 占用");
            _submissionChangeTracker?.AddTransaction(
                medium.DiskCode,
                $"写入归还流水；申请单 [{returnApplication.ApplicationNo}]");

            WriteBorrowedHardDiskReturnDiagnostic(
                stage: "Completed",
                message: $"自动归还登记已写入。ReturnApplicationNo={returnApplication.ApplicationNo}，BeforeStatus={beforeStatus}，AfterStatus={ledger.MediaStatus}，BeforeLocation={beforeLocation}，AfterLocation={ledger.StorageLocation}",
                archiveUnit: archiveUnit,
                candidate: borrowedHardDiskCandidate,
                linkedMedium: linkedMedium,
                trackedMedium: medium);

            return new BorrowedHardDiskReturnDiagnosticResult(
                Stage: "Completed",
                Message: $"自动归还登记已写入。ReturnApplicationNo={returnApplication.ApplicationNo}",
                ReturnCreated: true,
                ShouldWarnInDebug: false);
        }

        private static BorrowedHardDiskReturnDiagnosticResult ReturnBorrowedHardDiskDiagnostic(
            string stage,
            string message,
            YearlyElectronicArchiveUnit? archiveUnit = null,
            HardDiskMediaReturnCandidate? candidate = null,
            HardDiskMedium? linkedMedium = null,
            HardDiskMedium? trackedMedium = null,
            bool shouldWarnInDebug = false)
        {
            WriteBorrowedHardDiskReturnDiagnostic(stage, message, archiveUnit, candidate, linkedMedium, trackedMedium);
            return new BorrowedHardDiskReturnDiagnosticResult(stage, message, false, shouldWarnInDebug);
        }

        [Conditional("DEBUG")]
        private static void WriteBorrowedHardDiskReturnDiagnostic(
            string stage,
            string message,
            YearlyElectronicArchiveUnit? archiveUnit = null,
            HardDiskMediaReturnCandidate? candidate = null,
            HardDiskMedium? linkedMedium = null,
            HardDiskMedium? trackedMedium = null)
        {
            string archiveNo = archiveUnit?.ElectronicArchiveNo?.Trim() ?? "-";
            string candidateDiskCode = candidate?.DiskCode?.Trim() ?? "-";
            string candidateSourceApplicationNo = candidate?.SourceApplicationNo?.Trim() ?? "-";
            string linkedDiskCode = linkedMedium?.DiskCode?.Trim() ?? "-";
            string linkedStatus = linkedMedium?.Ledger?.MediaStatus?.Trim() ?? "-";
            string trackedDiskCode = trackedMedium?.DiskCode?.Trim() ?? "-";
            string trackedStatus = trackedMedium?.Ledger?.MediaStatus?.Trim() ?? "-";

            Debug.WriteLine($"[ArchiveFilingService][BorrowedHardDiskReturn][{stage}] {message} | ArchiveNo={archiveNo} | CandidateDisk={candidateDiskCode} | SourceApplicationNo={candidateSourceApplicationNo} | LinkedDisk={linkedDiskCode} | LinkedStatus={linkedStatus} | TrackedDisk={trackedDiskCode} | TrackedStatus={trackedStatus}");
        }

#if DEBUG
        private static void ThrowIfBorrowedHardDiskReturnDiagnosticRequiresAttention(
            BorrowedHardDiskReturnDiagnosticResult diagnostic,
            YearlyElectronicArchiveUnit archiveUnit,
            HardDiskMediaReturnCandidate? candidate)
        {
            if (!diagnostic.ShouldWarnInDebug)
            {
                return;
            }

            string archiveNo = archiveUnit?.ElectronicArchiveNo?.Trim() ?? "-";
            string candidateDiskCode = candidate?.DiskCode?.Trim() ?? "-";
            string sourceApplicationNo = candidate?.SourceApplicationNo?.Trim() ?? "-";
            throw new InvalidOperationException($"[调试诊断] 借出硬盘自动归还登记未生成。Stage={diagnostic.Stage}；ArchiveNo={archiveNo}；CandidateDisk={candidateDiskCode}；SourceApplicationNo={sourceApplicationNo}；Detail={diagnostic.Message}");
        }
#endif

        private sealed record BorrowedHardDiskReturnDiagnosticResult(
            string Stage,
            string Message,
            bool ReturnCreated,
            bool ShouldWarnInDebug);

        private async Task CreateElectronicArchiveUnitCoreAsync(
            YearlyElectronicArchiveUnit newUnit,
            List<YearlyArchiveRegisterMediaItem> mediaItems,
            IReadOnlyDictionary<int, string> filingStoragePathByMediaItemId,
            string mediumCode,
            HardDiskMediaReturnCandidate? borrowedHardDiskCandidate = null,
            User? currentUser = null,
            PendingExternalHardDiskRegistration? pendingExternalHardDisk = null)
        {
            ArgumentNullException.ThrowIfNull(newUnit);
            ArgumentNullException.ThrowIfNull(mediaItems);

            DateTime archivedAt = DateTime.Now;
            var mediaEntries = mediaItems
                .Select(item => item.MediaEntry!)
                .DistinctBy(item => item.Id)
                .ToList();
            var records = mediaEntries
                .Select(item => item.RegisterRecord!)
                .DistinctBy(item => item.Id)
                .ToList();

            var linkedMedia = await PrepareElectronicArchiveUnitAsync(newUnit, archivedAt, borrowedHardDiskCandidate, pendingExternalHardDisk);

            newUnit.RegisterRecords.AddRange(records);
            AssignElectronicArchiveMediumLinks(newUnit, linkedMedia);
            var createdItemLinks = AddElectronicMediaItemLinks(newUnit, mediaItems, filingStoragePathByMediaItemId, mediumCode, archivedAt);
            SyncElectronicMediaEntryLinksAfterItemFiling(newUnit, mediaItems, archivedAt);
            _archiveFilingRepository.AddElectronicArchiveUnit(newUnit);
            await UpsertElectronicArchiveDiscLinksAsync(newUnit, archivedAt);

            _submissionChangeTracker?.BeginSection("电子介质袋（YearlyElectronicArchiveUnit）");
            _submissionChangeTracker?.AddLine(
                $"新建电子介质袋 [{newUnit.ElectronicArchiveNo}]；项目 [{newUnit.ProjectName}]；年度 [{newUnit.Year}]；"
                + $"载体 [{newUnit.StorageCarrierType}]；档口 [{newUnit.StorageLocation}]；关联硬盘 [{newUnit.LinkedMediumCodes}]");

            _submissionChangeTracker?.BeginSection("登记介质关联（YearlyElectronicArchiveUnitMediaItemLink / MediumLink）");
            foreach (var item in mediaItems)
            {
                string formNo = item.MediaEntry?.RegisterRecord?.FormNo?.Trim() ?? "-";
                _submissionChangeTracker?.AddLine(
                    $"资料子项 Id={item.Id}（单号 {formNo} / {item.ContentDesc}）已关联至电子袋 [{newUnit.ElectronicArchiveNo}]");
            }

            foreach (var linked in linkedMedia)
            {
                _submissionChangeTracker?.AddLine(
                    $"硬盘 [{linked.DiskCode}] 已写入 YearlyElectronicArchiveUnitMediumLink");
            }

            await _archiveFilingRepository.SaveChangesAsync();

            await _filingFactWriter.WriteForElectronicLinksAsync(
                newUnit,
                createdItemLinks,
                archivedAt,
                newUnit.ArchivedBy);

            if (borrowedHardDiskCandidate == null || linkedMedia.Count > 0)
            {
                var filingMediaEntryIds = mediaEntries.Select(entry => entry.Id).ToList();
                var borrowedHardDiskReturnDiagnostic = await MaintainBorrowedHardDiskReturnAsync(
                    newUnit,
                    linkedMedia,
                    borrowedHardDiskCandidate,
                    currentUser,
                    archivedAt,
                    filingMediaEntryIds);
#if DEBUG
                ThrowIfBorrowedHardDiskReturnDiagnosticRequiresAttention(borrowedHardDiskReturnDiagnostic, newUnit, borrowedHardDiskCandidate);
#endif
            }

            await UpdateElectronicArchiveStatusesAsync(records.Select(item => item.Id), archivedAt);
            TrackRegisterRecordStatusUpdates(records, archivedAt);

            await _archiveFilingRepository.SaveChangesAsync();
        }

        private async Task UpsertElectronicArchiveDiscLinksAsync(YearlyElectronicArchiveUnit unit, DateTime archivedAt)
        {
            ArgumentNullException.ThrowIfNull(unit);

            if (!ArchiveFilingBusinessRules.IsOpticalDiscArchiveCarrierType(unit.StorageCarrierType))
            {
                return;
            }

            int discCount = unit.MediaCount > 0 ? unit.MediaCount : 1;
            string location = unit.StorageLocation?.Trim() ?? string.Empty;
            string projectName = unit.ProjectName?.Trim() ?? string.Empty;
            string year = unit.Year?.Trim() ?? string.Empty;
            string sourceType = string.IsNullOrWhiteSpace(unit.SourceType) ? "YearlyElectronicArchiveUnit" : unit.SourceType.Trim();
            string sourceRecordKey = string.IsNullOrWhiteSpace(unit.SourceRecordKey) ? unit.ElectronicArchiveNo.Trim() : unit.SourceRecordKey.Trim();
            string remarks = unit.Remarks?.Trim() ?? string.Empty;
            string baseDiscCode = unit.ElectronicArchiveNo.Trim();

            for (int index = 1; index <= discCount; index++)
            {
                string discCode = discCount == 1
                    ? baseDiscCode
                    : $"{baseDiscCode}-DISC-{index:D2}";

                var discMedium = await _archiveFilingRepository.GetOpticalDiscMediumByCodeAsync(discCode);

                bool isNewDisc = discMedium == null;
                if (discMedium == null)
                {
                    discMedium = new OpticalDiscMedium
                    {
                        DiscCode = discCode,
                        DiscType = "数据光盘",
                        Capacity = string.Empty,
                        RegistrationMethod = OpticalDiscMedium.RegistrationMethodArchive,
                        RegisterDate = archivedAt,
                        CreatedTime = archivedAt
                    };
                    _archiveFilingRepository.AddOpticalDiscMedium(discMedium);
                }

                discMedium.SourceType = sourceType;
                discMedium.SourceRecordKey = sourceRecordKey;
                discMedium.Remarks = remarks;
                discMedium.IsDeleted = false;
                discMedium.UpdatedTime = archivedAt;

                var discLedger = EnsureOpticalDiscLedger(discMedium, archivedAt);
                string beforeDiscStatus = discLedger.MediaStatus;
                string beforeDiscLocation = discLedger.StorageLocation;
                discLedger.MediaStatus = OpticalDiscMedium.StatusInStock;
                discLedger.HolderOrOrganization = "资料室";
                discLedger.StorageLocation = location;
                discLedger.NeedReturn = false;
                discLedger.UpdatedTime = archivedAt;

                if (isNewDisc)
                {
                    discMedium.Transactions.Add(new OpticalDiscMediaTransaction
                    {
                        Medium = discMedium,
                        TransactionType = OpticalDiscMediaTransaction.TypeArchiveInbound,
                        BusinessNo = unit.ElectronicArchiveNo.Trim(),
                        BeforeStatus = beforeDiscStatus,
                        AfterStatus = discLedger.MediaStatus,
                        BeforeLocation = beforeDiscLocation,
                        AfterLocation = location,
                        OperatorName = unit.ArchivedBy?.Trim() ?? string.Empty,
                        OperateTime = archivedAt,
                        TargetOrganization = "资料室",
                        NeedReturn = false,
                        RelatedBatch = sourceRecordKey,
                        RelatedArchiveTitle = string.IsNullOrWhiteSpace(unit.ContentSummary) ? unit.ElectronicArchiveNo.Trim() : unit.ContentSummary.Trim(),
                        Description = "电子立档光盘入库",
                        Remark = remarks
                    });
                }

                var existingLink = unit.DiscLinks
                    .FirstOrDefault(item => item.OpticalDiscMediumId == discMedium.Id)
                    ?? unit.DiscLinks.FirstOrDefault(item =>
                        item.OpticalDiscMedium != null
                        && string.Equals(item.OpticalDiscMedium.DiscCode, discCode, StringComparison.OrdinalIgnoreCase))
                    ?? await _archiveFilingRepository.GetElectronicArchiveUnitDiscLinkAsync(unit.Id, discMedium.Id, discCode);

                if (existingLink != null)
                {
                    continue;
                }

                var discLink = new YearlyElectronicArchiveUnitDiscLink
                {
                    YearlyElectronicArchiveUnitId = unit.Id,
                    OpticalDiscMedium = discMedium,
                    ElectronicArchiveUnit = unit,
                    CreatedAt = archivedAt
                };

                unit.DiscLinks.Add(discLink);
            }
        }

        private async Task FinalizeRetainedHardDiskAfterSubmissionAsync(
            ElectronicArchiveSubmissionRequest request,
            YearlyElectronicArchiveUnit archiveUnit,
            IReadOnlyCollection<int> filingMediaEntryIds,
            User? currentUser,
            DateTime archivedAt)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(archiveUnit);

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
                    _submissionChangeTracker?.AddDeferred(
                        $"借出留存硬盘 [{request.BorrowedHardDiskCandidate.DiskCode}] 仍有未立档登记条目，本次未执行格式化空盘归还。");
                    return;
                }

                await CompleteFormattedBorrowedRetainedHardDiskAsync(request.BorrowedHardDiskCandidate, archiveUnit, currentUser, archivedAt);
                return;
            }

            if (request.PendingExternalHardDisk != null)
            {
                var registerRecordIds = await _archiveFilingRepository.GetRegisterRecordIdsForMediaEntriesAsync(filingMediaEntryIds);
                if (await _archiveFilingRepository.HasPendingExternalRetainedRegisterEntriesOnRecordsAsync(registerRecordIds))
                {
                    _submissionChangeTracker?.AddDeferred(
                        $"外来留存硬盘 [{request.PendingExternalHardDisk.DiskCode}] 所在登记单仍有未立档条目，本次未执行格式化空盘入库。");
                    return;
                }

                await CompleteFormattedExternalRetainedHardDiskAsync(request.PendingExternalHardDisk, archiveUnit, currentUser, archivedAt);
            }
        }

        private async Task CompleteFormattedBorrowedRetainedHardDiskAsync(
            HardDiskMediaReturnCandidate candidate,
            YearlyElectronicArchiveUnit archiveUnit,
            User? currentUser,
            DateTime archivedAt)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            ArgumentNullException.ThrowIfNull(archiveUnit);

            var medium = await _archiveFilingRepository.GetHardDiskMediumByIdWithLedgerAsync(candidate.MediumId);
            if (medium == null)
            {
                throw new InvalidOperationException($"未找到需要格式化后归还的留存硬盘 [{candidate.DiskCode}]。");
            }

            bool hasCompletedReturn = await _archiveFilingRepository.HasCompletedReturnApplicationAsync(medium.Id, candidate.SourceApplicationId);
            if (hasCompletedReturn)
            {
                _submissionChangeTracker?.AddDeferred(
                    $"借出留存硬盘 [{medium.DiskCode}] 已完成空盘归还登记，本次未重复写入。");
                return;
            }

            string operatorName = currentUser?.RealName?.Trim() ?? currentUser?.LoginName?.Trim() ?? string.Empty;
            string targetLocation = await ResolveFormattedBorrowedHardDiskReturnLocationAsync(candidate);
            string remark = $"电子立档 [{archiveUnit.ElectronicArchiveNo}] 完成后，原留存硬盘 [{medium.DiskCode}] 已格式化并办理归还。";

            var returnApplication = new HardDiskMediaApplication
            {
                ApplicationNo = await _hardDiskMediaService.GenerateNextReturnRegistrationNoAsync(),
                MediumId = medium.Id,
                SourceApplicationId = candidate.SourceApplicationId,
                ApplicationType = HardDiskMediaApplication.TypeReturnBlankRegistration,
                ApplicationStatus = HardDiskMediaApplication.StatusCompleted,
                ApplicantName = candidate.ApplicantName.Trim(),
                ApplicantDept = candidate.ApplicantDept.Trim(),
                ApplyTime = archivedAt,
                Reason = $"年度资料电子立档 [{archiveUnit.ElectronicArchiveNo}] 完成后，原留存硬盘 [{medium.DiskCode}] 已格式化，办理空盘归还登记。",
                TargetPersonOrUnit = "资料室",
                CurrentLocation = string.IsNullOrWhiteSpace(candidate.BorrowedLocation)
                    ? medium.Ledger?.StorageLocation?.Trim() ?? string.Empty
                    : candidate.BorrowedLocation.Trim(),
                TargetLocation = targetLocation,
                ExpectedReturnDate = candidate.ExpectedReturnDate,
                RelatedBatch = archiveUnit.ElectronicArchiveNo.Trim(),
                RelatedArchiveTitle = archiveUnit.ContentSummary.Trim(),
                FormatConfirmation = "已格式化",
                SignedAttachmentUploaded = true,
                SignedAttachmentUploadedTime = archivedAt,
                SignedAttachmentUploader = operatorName,
                ApprovedBy = operatorName,
                ApprovedTime = archivedAt,
                ApprovalOpinion = "留存硬盘已格式化后归还资料室",
                ExecutedBy = operatorName,
                ExecutedTime = archivedAt,
                Remark = remark,
                CreatedTime = archivedAt,
                UpdatedTime = archivedAt
            };

            _archiveFilingRepository.AddHardDiskMediaApplication(returnApplication);

            var ledger = EnsureHardDiskLedger(medium, archivedAt);
            string beforeStatus = ledger.MediaStatus;
            string beforeLocation = ledger.StorageLocation;
            string beforeNature = ledger.MediaNature;
            ledger.MediaStatus = HardDiskMedium.StatusInStockBlank;
            ledger.MediaNature = HardDiskMedium.NatureBlank;
            ledger.HolderOrOrganization = "资料室";
            ledger.StorageLocation = targetLocation;
            ledger.NeedReturn = false;
            ledger.UpdatedTime = archivedAt;
            medium.RegisterLock = null;
            medium.Remark = string.Join("；",
                new[]
                {
                    medium.Remark?.Trim(),
                    $"电子立档 [{archiveUnit.ElectronicArchiveNo}] 完成后已格式化，可作为新增空盘继续管理。"
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
            medium.UpdatedTime = archivedAt;

            await _archiveFilingRepository.SaveChangesAsync();

            _archiveFilingRepository.AddHardDiskMediaTransaction(new HardDiskMediaTransaction
            {
                MediumId = medium.Id,
                ApplicationId = returnApplication.Id,
                TransactionType = HardDiskMediaTransaction.TypeReturnRegistration,
                BeforeStatus = beforeStatus,
                AfterStatus = ledger.MediaStatus,
                BeforeLocation = beforeLocation,
                AfterLocation = ledger.StorageLocation,
                OperatorName = operatorName,
                OperateTime = archivedAt,
                RelatedPerson = candidate.ApplicantName.Trim(),
                TargetOrganization = "资料室",
                NeedReturn = false,
                ExpectedReturnDate = candidate.ExpectedReturnDate,
                ActualReturnDate = archivedAt,
                RelatedBatch = archiveUnit.ElectronicArchiveNo.Trim(),
                RelatedArchiveTitle = archiveUnit.ContentSummary.Trim(),
                Description = returnApplication.Reason,
                Remark = returnApplication.Remark
            });

            await _archiveFilingRepository.SaveChangesAsync();

            _submissionChangeTracker?.AddApplication(
                returnApplication.ApplicationNo,
                HardDiskMediaApplication.TypeReturnBlankRegistration,
                medium.DiskCode,
                "留存源盘已格式化，办理空盘归还登记");
            _submissionChangeTracker?.AddLedgerChange(
                medium.DiskCode,
                beforeStatus,
                ledger.MediaStatus,
                beforeLocation,
                ledger.StorageLocation,
                beforeNature,
                ledger.MediaNature,
                "已解除 HardDiskRegisterLock 占用");
            _submissionChangeTracker?.AddTransaction(
                medium.DiskCode,
                $"写入空盘归还流水；申请单 [{returnApplication.ApplicationNo}]；目标档口 [{targetLocation}]");
        }

        private async Task CompleteFormattedExternalRetainedHardDiskAsync(
            PendingExternalHardDiskRegistration pendingExternalHardDisk,
            YearlyElectronicArchiveUnit archiveUnit,
            User? currentUser,
            DateTime archivedAt)
        {
            ArgumentNullException.ThrowIfNull(pendingExternalHardDisk);
            ArgumentNullException.ThrowIfNull(archiveUnit);

            var medium = await _archiveFilingRepository.GetHardDiskMediumByDiskCodeWithLedgerAsync(pendingExternalHardDisk.DiskCode);
            if (medium == null)
            {
                throw new InvalidOperationException($"未找到需要格式化后重新登记的外来硬盘 [{pendingExternalHardDisk.DiskCode}]。");
            }

            string operatorName = currentUser?.RealName?.Trim() ?? currentUser?.LoginName?.Trim() ?? string.Empty;
            string targetLocation = await ResolveBlankHardDiskSlotLocationAsync(pendingExternalHardDisk.FormattedBlankTargetLocation);
            var ledger = EnsureHardDiskLedger(medium, archivedAt);
            string beforeStatus = ledger.MediaStatus;
            string beforeLocation = ledger.StorageLocation;
            ledger.MediaStatus = HardDiskMedium.StatusInStockBlank;
            ledger.MediaNature = HardDiskMedium.NatureBlank;
            ledger.HolderOrOrganization = "资料室";
            ledger.StorageLocation = targetLocation;
            ledger.NeedReturn = false;
            ledger.UpdatedTime = archivedAt;
            medium.RegisterLock = null;
            medium.Remark = string.Join("；",
                new[]
                {
                    medium.Remark?.Trim(),
                    $"电子立档 [{archiveUnit.ElectronicArchiveNo}] 完成后已格式化，可作为新增空盘继续管理。"
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
            medium.UpdatedTime = archivedAt;

            await _archiveFilingRepository.SaveChangesAsync();

            _archiveFilingRepository.AddHardDiskMediaTransaction(new HardDiskMediaTransaction
            {
                MediumId = medium.Id,
                ApplicationId = null,
                TransactionType = HardDiskMediaTransaction.TypeReturnRegistration,
                BeforeStatus = beforeStatus,
                AfterStatus = ledger.MediaStatus,
                BeforeLocation = beforeLocation,
                AfterLocation = ledger.StorageLocation,
                OperatorName = operatorName,
                OperateTime = archivedAt,
                RelatedPerson = pendingExternalHardDisk.RegisterPerson?.Trim() ?? string.Empty,
                TargetOrganization = "资料室",
                NeedReturn = false,
                ActualReturnDate = archivedAt,
                RelatedBatch = archiveUnit.ElectronicArchiveNo.Trim(),
                RelatedArchiveTitle = archiveUnit.ContentSummary.Trim(),
                Description = $"年度资料电子立档 [{archiveUnit.ElectronicArchiveNo}] 完成后，外来留存硬盘 [{medium.DiskCode}] 已格式化并归入空白硬盘档口。",
                Remark = $"外来硬盘格式化空盘入库：{targetLocation}"
            });

            await _archiveFilingRepository.SaveChangesAsync();

            _submissionChangeTracker?.AddLedgerChange(
                medium.DiskCode,
                beforeStatus,
                ledger.MediaStatus,
                beforeLocation,
                ledger.StorageLocation,
                beforeNature: null,
                afterNature: ledger.MediaNature,
                "外来留存源盘已格式化并归入空白硬盘档口；已解除 HardDiskRegisterLock 占用");
            _submissionChangeTracker?.AddTransaction(
                medium.DiskCode,
                $"写入格式化空盘入库流水；目标档口 [{targetLocation}]");
        }

        private async Task<string> ResolveFormattedBorrowedHardDiskReturnLocationAsync(HardDiskMediaReturnCandidate candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            return await ResolveBlankHardDiskSlotLocationAsync(null);
        }

        private async Task<string> ResolveBlankHardDiskSlotLocationAsync(string? requestedLocation)
        {
            if (!string.IsNullOrWhiteSpace(requestedLocation))
            {
                return await _hardDiskMediaService.ResolveBlankInStockSlotLocationAsync(requestedLocation);
            }

            string? targetLocation = await _hardDiskMediaService.RecommendBlankDedicatedSlotLocationAsync();
            if (string.IsNullOrWhiteSpace(targetLocation))
            {
                throw new InvalidOperationException("未找到空白硬盘专用档口，请先在磁盘柜开柜界面完成设置。留存硬盘在拷贝立档或并档后，必须格式化并归入空白硬盘专用档口。");
            }

            return targetLocation;
        }

        private static List<int> NormalizeElectronicSubmissionMediaEntryIds(IEnumerable<int>? mediaEntryIds)
        {
            return mediaEntryIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList()
                ?? [];
        }

        private static void ApplyOpticalDiscSingleArchiveRules(
            ElectronicArchiveSubmissionRequest request,
            YearlyElectronicArchiveUnit archiveUnit,
            IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(archiveUnit);
            ArgumentNullException.ThrowIfNull(mediaEntries);

            if (!ArchiveFilingBusinessRules.IsOpticalDiscArchiveCarrierType(archiveUnit.StorageCarrierType))
            {
                return;
            }

            int totalMediaCount = mediaEntries.Sum(item => Math.Max(item.MediaCount, 0));
            if (totalMediaCount != 1)
            {
                throw new InvalidOperationException("光盘立档场景必须每次仅处理1张光盘。请分张完成立档。");
            }

            archiveUnit.MediaCount = 1;
        }

        private static void ValidateElectronicSubmissionRequest(
            ElectronicArchiveSubmissionRequest request,
            IReadOnlyCollection<int> mediaItemIds,
            IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries,
            bool requireExistingUnitId)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.ArchiveUnit);

            if (mediaItemIds.Count == 0)
            {
                throw new InvalidOperationException("请先勾选本次需要入袋的资料明细。");
            }

            int distinctMediaEntryCount = mediaEntries.Select(entry => entry.Id).Distinct().Count();
            if (IsRetainedHardDiskScenarioRestricted(request) && distinctMediaEntryCount != 1)
            {
                throw new InvalidOperationException("硬盘留存场景一次只能处理一条电子介质，请逐条完成入袋立档。");
            }

            if (ParseMediumCodes(request.ArchiveUnit.LinkedMediumCodes).Skip(1).Any())
            {
                throw new InvalidOperationException("电子介质袋一次只能关联一块入袋硬盘。");
            }

            bool isAppendMode = IsAppendSubmissionMode(request.SubmissionMode);
            if (requireExistingUnitId && request.ExistingElectronicArchiveUnitId.GetValueOrDefault() <= 0)
            {
                throw new InvalidOperationException("请先选择要并入的电子介质袋。");
            }

            if (isAppendMode && request.ExistingElectronicArchiveUnitId.GetValueOrDefault() <= 0)
            {
                throw new InvalidOperationException("当前立档方式要求先选择要并入的电子介质袋。");
            }

            if (UsesOpticalDiscCarrier(request.SubmissionMode) && ParseMediumCodes(request.ArchiveUnit.LinkedMediumCodes).Count > 0)
            {
                throw new InvalidOperationException("光盘直接留袋场景不应关联硬盘编号，请清空目标硬盘后重试。");
            }

            if (!Enum.IsDefined(typeof(ElectronicArchiveSubmissionMode), request.SubmissionMode))
            {
                throw new InvalidOperationException("未识别的电子介质立档方式，请重新选择后重试。");
            }

            bool archiveUsesOpticalDisc = UsesOpticalDiscCarrier(request.SubmissionMode)
                || ArchiveFilingBusinessRules.IsOpticalDiscArchiveCarrierType(request.ArchiveUnit.StorageCarrierType);
            if (archiveUsesOpticalDisc && request.ArchiveUnit.MediaCount != 1)
            {
                throw new InvalidOperationException("光盘立档场景必须按单张光盘立档。请确认袋内光盘数量为1后重试。");
            }

            if (requireExistingUnitId && archiveUsesOpticalDisc)
            {
                throw new InvalidOperationException("光盘相关场景不允许并档，请改为新建立档。");
            }

            if (isAppendMode && archiveUsesOpticalDisc)
            {
                throw new InvalidOperationException("光盘相关场景不允许并档，请改为新建立档。");
            }

            if (request.SubmissionMode == ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew
                && request.RequiresFormatRetainedHardDisk)
            {
                throw new InvalidOperationException("直接使用留存硬盘立档时，不应再对原硬盘执行格式化处理。");
            }

            if (RequiresRetainedHardDiskFormatting(request)
                && !request.RequiresFormatRetainedHardDisk)
            {
                throw new InvalidOperationException("当前硬盘留存立档方式要求在提交后将原硬盘格式化为空盘入库。");
            }
        }

        private static bool IsRetainedHardDiskScenarioRestricted(ElectronicArchiveSubmissionRequest request)
            => request.IsRetainedHardDiskScenario;

        private static bool RequiresRetainedHardDiskFormatting(ElectronicArchiveSubmissionRequest request)
            => request.IsRetainedHardDiskScenario
                && request.SubmissionMode is ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc
                    or ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk
                    or ElectronicArchiveSubmissionMode.CopyNewHardDisk;

        private static bool UsesOpticalDiscCarrier(ElectronicArchiveSubmissionMode submissionMode)
            => submissionMode is ElectronicArchiveSubmissionMode.CopyNewOpticalDisc
                or ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew
                or ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc;

        private static bool IsAppendSubmissionMode(ElectronicArchiveSubmissionMode submissionMode)
            => submissionMode is ElectronicArchiveSubmissionMode.CopyAppendExistingHardDisk
                or ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk;

        private static YearlyElectronicArchiveUnit CreateSubmissionArchiveUnit(YearlyElectronicArchiveUnit source, User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(source);

            return new YearlyElectronicArchiveUnit
            {
                Id = source.Id,
                ElectronicArchiveNo = source.ElectronicArchiveNo.Trim(),
                ProjectName = source.ProjectName.Trim(),
                Year = source.Year.Trim(),
                StorageCarrierType = source.StorageCarrierType.Trim(),
                StoragePath = source.StoragePath.Trim(),
                StorageLocation = source.StorageLocation.Trim(),
                LinkedMediumCodes = source.LinkedMediumCodes.Trim(),
                Disposition = source.Disposition.Trim(),
                MediaCount = source.MediaCount,
                ContentSummary = source.ContentSummary.Trim(),
                ArchivedBy = string.IsNullOrWhiteSpace(source.ArchivedBy)
                    ? currentUser?.RealName?.Trim() ?? "Unknown"
                    : source.ArchivedBy.Trim(),
                ArchivedDate = source.ArchivedDate,
                SourceType = source.SourceType.Trim(),
                SourceRecordKey = source.SourceRecordKey.Trim(),
                Remarks = source.Remarks.Trim()
            };
        }

        private static void ValidateNewElectronicArchiveConstraints(
            YearlyElectronicArchiveUnit archiveUnit,
            IEnumerable<YearlyArchiveRegisterRecord> records)
        {
            ArgumentNullException.ThrowIfNull(archiveUnit);
            ArgumentNullException.ThrowIfNull(records);

            if (string.IsNullOrWhiteSpace(archiveUnit.ProjectName))
            {
                throw new InvalidOperationException("请先选择待立档资料以确定所属项目。");
            }

            if (string.IsNullOrWhiteSpace(archiveUnit.Year))
            {
                throw new InvalidOperationException("请先选择待立档资料以确定所属年度。");
            }

            foreach (var record in records)
            {
                string recordProject = ArchiveFilingBusinessRules.ResolveElectronicArchiveProjectName(record);
                string recordYear = record.CreatedDate.Year.ToString();

                if (!string.Equals(archiveUnit.ProjectName, recordProject, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"登记单 [{record.FormNo}] 与当前电子介质袋所属项目不一致，不能混合立档。");
                }

                if (!string.Equals(archiveUnit.Year, recordYear, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"登记单 [{record.FormNo}] 与当前电子介质袋所属年度不一致，不能混合立档。");
                }
            }
        }

        /// <summary>
        /// 校验电子介质袋物理存放位置与立档载体类型对应的档口专用类别是否一致。
        /// </summary>
        private async Task ValidateElectronicStorageLocationSlotCategoryAsync(
            YearlyElectronicArchiveUnit unit,
            IReadOnlyList<HardDiskMedium> linkedMedia)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(linkedMedia);

            string location = unit.StorageLocation?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(location))
            {
                return;
            }

            string linkedMediumStatus = ArchiveElectronicStorageSlotCategorySupport.ResolveLinkedMediumMediaStatus(linkedMedia);
            string expectedCategory = ArchiveElectronicStorageSlotCategorySupport.ResolveExpectedDedicatedSlotCategory(
                unit.StorageCarrierType,
                linkedMediumStatus);
            string expectedDisplay = ArchiveElectronicStorageSlotCategorySupport.ResolveCategoryDisplayName(expectedCategory);

            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(location, out string cabinetName, out string side, out int row, out int column))
            {
                throw new InvalidOperationException($"无法解析电子介质存放位置 [{location}]，请重新选择物理存放位置。");
            }

            var cabinets = await _cabinetRepository.GetAllAsync();
            var cabinet = cabinets.FirstOrDefault(item => string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                throw new InvalidOperationException($"未找到存放位置对应的防磁磁盘柜 [{cabinetName}]，请重新选择物理存放位置。");
            }

            if (cabinet.Type != CabinetType.MagneticDisk)
            {
                throw new InvalidOperationException("电子介质袋只能放入防磁磁盘柜专用档口，当前所选位置不属于防磁磁盘柜。");
            }

            string faceCode = side.Trim().ToUpperInvariant();
            string slotCode = $"{row}-{column}";
            var assignment = _cabinetRepository.GetSlotCategoryAssignment(cabinet.Id, faceCode, slotCode);
            if (assignment == null || string.IsNullOrWhiteSpace(assignment.CategoryName))
            {
                throw new InvalidOperationException(
                    $"档口 [{cabinet.Name}{faceCode}-{slotCode}] 尚未设置专用类别。当前立档介质应放入「{expectedDisplay}」档口，请重新选择位置或在开柜界面完成设置。");
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(assignment.CategoryName, expectedCategory))
            {
                return;
            }

            string actualDisplay = ArchiveElectronicStorageSlotCategorySupport.ResolveCategoryDisplayName(assignment.CategoryName);
            throw new InvalidOperationException(
                $"物理存放位置 [{location}] 的档口用途为「{actualDisplay}」，与当前立档介质类型要求的「{expectedDisplay}」不一致，请重新选择位置。");
        }

        private static void ValidateElectronicArchiveUnit(YearlyElectronicArchiveUnit unit)
        {
            if (string.IsNullOrWhiteSpace(unit.ElectronicArchiveNo))
            {
                throw new ArgumentException("电子立档编号不能为空。", nameof(unit));
            }

            if (string.IsNullOrWhiteSpace(unit.Year) || unit.Year.Length != 4 || !unit.Year.All(char.IsDigit))
            {
                throw new ArgumentException("电子立档年度必须是四位年份。", nameof(unit));
            }

            Match match = ElectronicArchiveNoRegex.Match(unit.ElectronicArchiveNo);
            if (!match.Success || !string.Equals(match.Groups[1].Value, unit.Year, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"电子立档编号 [{unit.ElectronicArchiveNo}] 不符合 年度电子-年份-顺序号 规则。");
            }

            if (string.IsNullOrWhiteSpace(unit.StorageCarrierType))
            {
                throw new ArgumentException("电子介质载体类型不能为空。", nameof(unit));
            }

            if (string.IsNullOrWhiteSpace(unit.StorageLocation))
            {
                throw new ArgumentException("电子介质存放位置不能为空。", nameof(unit));
            }

            if (unit.MediaCount < 0)
            {
                throw new ArgumentException("电子介质数量不能为负数。", nameof(unit));
            }

            bool isOpticalDiscBag = unit.StorageCarrierType.Contains("光盘", StringComparison.OrdinalIgnoreCase);
            if (isOpticalDiscBag && ParseMediumCodes(unit.LinkedMediumCodes).Count > 0)
            {
                throw new InvalidOperationException("光盘介质袋不应关联硬盘编号。");
            }

            if (RequiresHardDiskLink(unit) && string.IsNullOrWhiteSpace(unit.LinkedMediumCodes))
            {
                throw new InvalidOperationException("电子介质载体类型包含硬盘时，必须填写关联硬盘编号。");
            }

            if (RequiresHardDiskLink(unit) && ParseMediumCodes(unit.LinkedMediumCodes).Count != 1)
            {
                throw new InvalidOperationException("电子介质袋需要且仅能关联一块入袋硬盘。");
            }
        }

        private static HardDiskMedium BuildHardDiskMediumFromPendingRegistration(PendingExternalHardDiskRegistration pending)
        {
            ArgumentNullException.ThrowIfNull(pending);

            string targetLocation = string.IsNullOrWhiteSpace(pending.FormattedBlankTargetLocation)
                ? pending.CurrentLocation
                : pending.FormattedBlankTargetLocation;

            return new HardDiskMedium
            {
                Id = 0,
                DiskCode = pending.DiskCode.Trim(),
                SerialNumber = pending.SerialNumber.Trim(),
                DiskType = pending.DiskType.Trim(),
                Brand = pending.Brand.Trim(),
                Capacity = pending.Capacity.Trim(),
                InterfaceType = pending.InterfaceType.Trim(),
                RegisterPerson = pending.RegisterPerson.Trim(),
                RegisterDate = pending.RegisterDate,
                FactoryDate = pending.FactoryDate,
                RegistrationMethod = pending.RegistrationMethod.Trim(),
                Remark = pending.Remark.Trim(),
                Ledger = new HardDiskLedger
                {
                    DiskCode = pending.DiskCode.Trim(),
                    MediaStatus = pending.CurrentStatus.Trim(),
                    MediaNature = pending.MediaNature.Trim(),
                    StorageLocation = targetLocation.Trim(),
                    HolderOrOrganization = pending.CurrentHolder.Trim(),
                    NeedReturn = pending.NeedReturn,
                    RegisterPerson = pending.RegisterPerson.Trim(),
                    RegisterDate = pending.RegisterDate,
                    Remark = pending.Remark.Trim()
                }
            };
        }

        private async Task<List<HardDiskMedium>> LoadLinkedMediaAsync(
            string linkedMediumCodes,
            PendingExternalHardDiskRegistration? pendingExternalHardDisk = null)
        {
            if (string.IsNullOrWhiteSpace(linkedMediumCodes))
            {
                return new List<HardDiskMedium>();
            }

            List<string> codes = ParseMediumCodes(linkedMediumCodes);
            var media = await _archiveFilingRepository.GetHardDiskMediaByCodesWithLedgerAsync(codes);

            var missingCodes = codes
                .Where(code => !media.Any(item => string.Equals(item.DiskCode, code, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missingCodes.Count > 0
                && pendingExternalHardDisk != null
                && !string.IsNullOrWhiteSpace(pendingExternalHardDisk.DiskCode))
            {
                string pendingCode = pendingExternalHardDisk.DiskCode.Trim();
                if (missingCodes.Any(code => string.Equals(code, pendingCode, StringComparison.OrdinalIgnoreCase)))
                {
                    var persistedMedium = await _archiveFilingRepository.GetHardDiskMediumByDiskCodeWithLedgerAsync(pendingCode);
                    if (persistedMedium != null)
                    {
                        media.Add(persistedMedium);
                    }
                    else
                    {
                        media.Add(BuildHardDiskMediumFromPendingRegistration(pendingExternalHardDisk));
                    }
                }
            }

            missingCodes = codes
                .Where(code => !media.Any(item => string.Equals(item.DiskCode, code, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (missingCodes.Count > 0)
            {
                throw new InvalidOperationException($"未找到关联硬盘编号：{string.Join("、", missingCodes)}");
            }

            return media;
        }

        private static void SyncLinkedMedia(
            YearlyElectronicArchiveUnit unit,
            IEnumerable<HardDiskMedium> media,
            DateTime archivedAt,
            HardDiskMediaReturnCandidate? borrowedHardDiskCandidate = null)
        {
            foreach (var medium in media)
            {
                var ledger = EnsureHardDiskLedger(medium, archivedAt);
                if (HardDiskMedium.IsTerminalUnavailableStatus(ledger.MediaStatus))
                {
                    throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 当前状态为 {ledger.MediaStatus}，不能关联电子立档。");
                }

                ledger.MediaNature = HardDiskMedium.NatureDataCarrier;
                ledger.UpdatedTime = archivedAt;
                medium.UpdatedTime = archivedAt;

                bool isBorrowedRetainedMedium = borrowedHardDiskCandidate != null
                    && medium.Id == borrowedHardDiskCandidate.MediumId
                    && string.Equals(medium.DiskCode, borrowedHardDiskCandidate.DiskCode, StringComparison.Ordinal);

                if (ledger.MediaStatus == HardDiskMedium.StatusInStockBlank
                    || (!isBorrowedRetainedMedium
                        && (ledger.MediaStatus == HardDiskMedium.StatusOutTemporary
                            || ledger.MediaStatus == HardDiskMedium.StatusOutLongTerm)))
                {
                    ledger.MediaStatus = HardDiskMedium.StatusInStockData;
                }

                if (ledger.MediaStatus == HardDiskMedium.StatusInStockData)
                {
                    ledger.HolderOrOrganization = "资料室";
                    ledger.NeedReturn = false;
                }

                if (!isBorrowedRetainedMedium && !string.IsNullOrWhiteSpace(unit.StorageLocation))
                {
                    ledger.StorageLocation = unit.StorageLocation;
                }
            }
        }

        private static HardDiskLedger EnsureHardDiskLedger(HardDiskMedium medium, DateTime time)
        {
            ArgumentNullException.ThrowIfNull(medium);

            medium.Ledger ??= new HardDiskLedger
            {
                MediumId = medium.Id,
                DiskCode = medium.DiskCode,
                MediaStatus = HardDiskMedium.StatusInStockBlank,
                MediaNature = HardDiskMedium.NatureBlank,
                StorageLocation = string.Empty,
                HolderOrOrganization = "资料室",
                NeedReturn = false,
                RegisterPerson = medium.RegisterPerson,
                RegisterDate = medium.RegisterDate,
                Remark = medium.Remark,
                CreatedTime = medium.CreatedTime == default ? time : medium.CreatedTime,
                UpdatedTime = time
            };

            return medium.Ledger;
        }

        private static OpticalDiscLedger EnsureOpticalDiscLedger(OpticalDiscMedium medium, DateTime time)
        {
            ArgumentNullException.ThrowIfNull(medium);

            medium.Ledger ??= new OpticalDiscLedger
            {
                MediumId = medium.Id,
                DiscCode = medium.DiscCode,
                MediaStatus = OpticalDiscMedium.StatusInStock,
                StorageLocation = string.Empty,
                HolderOrOrganization = "资料室",
                NeedReturn = false,
                RegisterPerson = medium.RegisterPerson,
                RegisterDate = medium.RegisterDate,
                Remark = medium.Remarks,
                CreatedTime = medium.CreatedTime == default ? time : medium.CreatedTime,
                UpdatedTime = time
            };

            return medium.Ledger;
        }

        private static bool RequiresHardDiskLink(YearlyElectronicArchiveUnit unit)
            => unit.StorageCarrierType.Contains("硬盘", StringComparison.OrdinalIgnoreCase);

        private void EnrichBorrowedHardDiskSubmissionAsync(
            ElectronicArchiveSubmissionRequest request,
            IReadOnlyList<YearlyArchiveRegisterMedia> mediaEntries)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(mediaEntries);

            YearlyElectronicArchiveUnit archiveUnit = request.ArchiveUnit;
            if (!RequiresHardDiskLink(archiveUnit))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(archiveUnit.LinkedMediumCodes))
            {
                return;
            }

            string? borrowedCode = ExtractSingleBorrowedHardDiskCode(mediaEntries);
            if (string.IsNullOrWhiteSpace(borrowedCode))
            {
                return;
            }

            archiveUnit.LinkedMediumCodes = borrowedCode;
        }

        private async Task<HardDiskMediaReturnCandidate?> ResolveBorrowedHardDiskCandidateForSubmissionAsync(
            ElectronicArchiveSubmissionRequest request,
            IReadOnlyList<YearlyArchiveRegisterMedia>? mediaEntries = null)
        {
            if (request.BorrowedHardDiskCandidate != null)
            {
                return request.BorrowedHardDiskCandidate;
            }

            if (!request.IsRetainedHardDiskScenario)
            {
                return null;
            }

            string? diskCode = ParseMediumCodes(request.ArchiveUnit.LinkedMediumCodes).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(diskCode) && mediaEntries != null)
            {
                diskCode = ExtractSingleBorrowedHardDiskCode(mediaEntries);
            }

            if (string.IsNullOrWhiteSpace(diskCode))
            {
                return null;
            }

            return await _hardDiskMediaService.GetReturnRegistrationCandidateByDiskCodeAsync(diskCode);
        }

        private static string? ExtractSingleBorrowedHardDiskCode(IReadOnlyList<YearlyArchiveRegisterMedia> mediaEntries)
        {
            var codes = mediaEntries
                .Where(entry => entry.IsBorrowedHardDisk && !string.IsNullOrWhiteSpace(entry.BorrowedHardDiskCode))
                .Select(entry => entry.BorrowedHardDiskCode.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return codes.Count == 1 ? codes[0] : null;
        }

        private static string NormalizeMediumCodes(string linkedMediumCodes)
        {
            List<string> codes = ParseMediumCodes(linkedMediumCodes);
            return codes.Count == 0 ? string.Empty : string.Join(", ", codes);
        }

        private static string MergeMediumCodes(string existingCodes, string updatedCodes)
        {
            var mergedCodes = ParseMediumCodes(existingCodes)
                .Concat(ParseMediumCodes(updatedCodes))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return mergedCodes.Count == 0 ? string.Empty : string.Join(", ", mergedCodes);
        }

        private static string MergeDelimitedText(string existingValue, string updatedValue)
        {
            var mergedValues = SplitTextSegments(existingValue)
                .Concat(SplitTextSegments(updatedValue))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return mergedValues.Count == 0 ? string.Empty : string.Join("；", mergedValues);
        }

        private static IEnumerable<string> SplitTextSegments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Enumerable.Empty<string>();
            }

            return value
                .Split(['；', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(segment => !string.IsNullOrWhiteSpace(segment));
        }

        private static List<string> ParseMediumCodes(string linkedMediumCodes)
        {
            if (string.IsNullOrWhiteSpace(linkedMediumCodes))
            {
                return new List<string>();
            }

            return linkedMediumCodes
                .Split([',', '，', ';', '；', '\r', '\n', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }
}
