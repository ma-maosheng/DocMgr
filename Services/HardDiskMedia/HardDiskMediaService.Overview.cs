using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质总览统计与洞察分析（对齐现行台账、申请工作流、离库处置、盘库登记与征用锁）。
    /// </summary>
    public partial class HardDiskMediaService
    {
        /// <inheritdoc/>
        public async Task<HardDiskMediaOverview> GetOverviewAsync()
        {
            var media = await _hardDiskMediaRepository.GetOverviewMediaAsync();
            int missingLedgerCount = media.Count(item => item.Ledger == null);

            var mediaItems = media
                .Where(item => item.Ledger != null)
                .Select(item => new OverviewMediumSnapshot(
                    item.Ledger!.MediaStatus,
                    item.Ledger.StorageLocation,
                    item.Ledger.HolderOrOrganization,
                    item.Capacity,
                    item.Ledger.MediaNature,
                    item.Ledger.NeedReturn,
                    item.RegisterLock != null,
                    item.RegisterLock?.BusinessType))
                .ToList();

            var applicationItems = (await _hardDiskMediaRepository.GetOverviewApplicationsAsync())
                .Select(item => new OverviewApplicationSnapshot(
                    item.ApplicationType,
                    item.ApplicationStatus,
                    item.SignedAttachmentUploaded))
                .ToList();

            var transactionItems = (await _hardDiskMediaRepository.GetOverviewTransactionsAsync())
                .Select(item => new OverviewTransactionSnapshot(item.TransactionType, item.OperateTime))
                .ToList();

            var disposalItems = await _hardDiskMediaRepository.GetOverviewDisposalRecordsAsync();
            var inventoryItems = await _hardDiskMediaRepository.GetOverviewInventoryRegisterRecordsAsync();
            var overdueApplications = await _hardDiskMediaRepository.GetOverdueOutboundApplicationsForToDoAsync(DateTime.Now, 500);

            int pendingHandoverCount = applicationItems.Count(IsPendingHandover);
            int pendingSignedCount = applicationItems.Count(IsPendingSignedUpload);
            int pendingCompleteCount = applicationItems.Count(IsPendingComplete);

            return new HardDiskMediaOverview
            {
                TotalMediumCount = mediaItems.Count,
                MissingLedgerMediumCount = missingLedgerCount,
                BlankInStockCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockBlank),
                DataCarrierInStockCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockData),
                DamagedInStockCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockDamaged),
                InStockLostCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockLost),
                BorrowedCount = mediaItems.Count(IsBorrowedStatus),
                PermanentTransferCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutPermanent),
                DisposedCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusDisposed),
                OutLostCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutLost),
                NeedReturnMediumCount = mediaItems.Count(item => item.NeedReturn),
                TemporaryNeedReturnMediumCount = mediaItems.Count(item =>
                    item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutTemporary),
                LongTermNeedReturnMediumCount = mediaItems.Count(item =>
                    item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutLongTerm),
                OverdueNeedReturnCount = overdueApplications.Count,
                MissingLocationMediumCount = mediaItems.Count(item =>
                    IsLocatableInStockStatus(item) && string.IsNullOrWhiteSpace(item.CurrentLocation)),
                OutboundWithoutKeeperMediumCount = mediaItems.Count(item =>
                    IsActiveOutboundStatus(item) && string.IsNullOrWhiteSpace(item.CurrentHolder)),
                LockedMediumCount = mediaItems.Count(item => item.IsLocked),
                SubmittedApplicationCount = applicationItems.Count(item =>
                    item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted),
                PendingHandoverApplicationCount = pendingHandoverCount,
                PendingSignedFileCount = pendingSignedCount,
                PendingCompleteApplicationCount = pendingCompleteCount,
                PendingDisposalCount = disposalItems.Count(IsPendingDisposal),
                DraftInventoryRegisterCount = inventoryItems.Count(item =>
                    item.Status == HardDiskInventoryRegisterRecord.StatusDraft),
                LocationInsights = BuildLocationInsights(mediaItems),
                OutboundCapacityInsights = BuildOutboundCapacityInsights(mediaItems),
                HandoverInsights = BuildHandoverInsights(applicationItems, transactionItems, disposalItems, inventoryItems),
                LifecycleInsights = BuildLifecycleInsights(mediaItems),
                RiskInsights = BuildRiskInsights(
                    mediaItems,
                    applicationItems,
                    disposalItems,
                    inventoryItems,
                    missingLedgerCount,
                    overdueApplications.Count)
            };
        }

        private static IReadOnlyList<string> BuildLocationInsights(IReadOnlyList<OverviewMediumSnapshot> mediaItems)
        {
            if (mediaItems.Count == 0)
            {
                return ["当前暂无硬盘介质台账数据。"];
            }

            var allLocations = FormatDistribution(
                mediaItems
                    .GroupBy(item => NormalizeLocationGroup(item.CurrentLocation))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label)
                    .Take(5));

            var inStockSummary = FormatDistribution(
                mediaItems
                    .Where(IsLocatableInStockStatus)
                    .GroupBy(item => NormalizeLocationGroup(item.CurrentLocation))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label)
                    .Take(3));

            var keeperSummary = FormatDistribution(
                mediaItems
                    .Where(IsBorrowedStatus)
                    .GroupBy(item => NormalizeKeeper(item))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label)
                    .Take(4));

            var lockSummary = FormatDistribution(
                mediaItems
                    .Where(item => item.IsLocked)
                    .GroupBy(item => string.IsNullOrWhiteSpace(item.LockBusinessType) ? "(未登记占用类型)" : item.LockBusinessType!.Trim())
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label));

            return
            [
                $"当前位置（按档口归并）前五：{allLocations}。",
                $"资料室在库位置：{inStockSummary}。",
                $"临时/长期借出保管分布：{keeperSummary}。",
                $"征用锁占用：{lockSummary}。"
            ];
        }

        private static IReadOnlyList<string> BuildOutboundCapacityInsights(IReadOnlyList<OverviewMediumSnapshot> mediaItems)
        {
            var borrowedMedia = mediaItems.Where(IsBorrowedStatus).ToList();
            var terminalMedia = mediaItems.Where(IsTerminalAwayStatus).ToList();

            if (borrowedMedia.Count == 0 && terminalMedia.Count == 0)
            {
                return ["当前无借出或终态离库硬盘，容量占用风险为 0。"];
            }

            var borrowCapacitySummary = FormatDistribution(
                borrowedMedia
                    .GroupBy(item => NormalizeCapacity(item.Capacity))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label));

            var terminalCapacitySummary = FormatDistribution(
                terminalMedia
                    .GroupBy(item => NormalizeCapacity(item.Capacity))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label));

            double borrowCapacityGb = borrowedMedia.Sum(GetCapacityInGb);
            double terminalCapacityGb = terminalMedia.Sum(GetCapacityInGb);

            return
            [
                $"临时/长期借出 {borrowedMedia.Count} 块，容量约 {FormatCapacityAmount(borrowCapacityGb)}；容量档分布：{borrowCapacitySummary}。",
                $"永久移交/离库处置/出库挂失 {terminalMedia.Count} 块，容量约 {FormatCapacityAmount(terminalCapacityGb)}；容量档分布：{terminalCapacitySummary}。"
            ];
        }

        private static IReadOnlyList<string> BuildHandoverInsights(
            IReadOnlyList<OverviewApplicationSnapshot> applicationItems,
            IReadOnlyList<OverviewTransactionSnapshot> transactionItems,
            IReadOnlyList<HardDiskDisposalRecord> disposalItems,
            IReadOnlyList<HardDiskInventoryRegisterRecord> inventoryItems)
        {
            var outboundApplications = applicationItems.Where(item => !IsReturnOrLossRegistrationType(item.ApplicationType)).ToList();
            var registrationApplications = applicationItems.Where(item => IsReturnOrLossRegistrationType(item.ApplicationType)).ToList();

            var recentTransactions = transactionItems
                .Where(item => item.OperateTime >= DateTime.Now.AddDays(-90))
                .ToList();

            return
            [
                $"出库申请环节：{BuildApplicationStageSummary(outboundApplications)}。",
                $"归还/挂失登记环节：{BuildApplicationStageSummary(registrationApplications)}。",
                $"离库处置环节：{BuildDisposalStageSummary(disposalItems)}。",
                $"盘库登记环节：{BuildInventoryStageSummary(inventoryItems)}。",
                $"近90天流转记录：{BuildTransactionStageSummary(recentTransactions)}。"
            ];
        }

        private static IReadOnlyList<string> BuildLifecycleInsights(IReadOnlyList<OverviewMediumSnapshot> mediaItems)
        {
            if (mediaItems.Count == 0)
            {
                return ["当前暂无硬盘介质生命周期数据。"];
            }

            int blankCount = mediaItems.Count(item => item.MediaNature == HardDiskMedium.NatureBlank);
            int carrierCount = mediaItems.Count(item => item.MediaNature == HardDiskMedium.NatureDataCarrier);
            int needReturnCount = mediaItems.Count(item => item.NeedReturn);
            int temporaryNeedReturnCount = mediaItems.Count(item =>
                item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutTemporary);
            int longTermNeedReturnCount = mediaItems.Count(item =>
                item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutLongTerm);

            int inStockBlank = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockBlank);
            int inStockData = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockData);
            int inStockDamaged = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockDamaged);
            int inStockLost = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockLost);
            int outTemporary = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutTemporary);
            int outLongTerm = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutLongTerm);
            int outPermanent = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutPermanent);
            int disposed = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusDisposed);
            int outLost = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutLost);
            int locked = mediaItems.Count(item => item.IsLocked);

            return
            [
                $"介质属性结构：空白介质 {blankCount} 块，资料载体 {carrierCount} 块，纳入需归还控制 {needReturnCount} 块，征用锁占用 {locked} 块。",
                $"在库结构：空盘 {inStockBlank} 块、资料载体 {inStockData} 块、损坏待处置 {inStockDamaged} 块、盘失 {inStockLost} 块。",
                $"在外/终态结构：临时借出 {outTemporary} 块、长期借出 {outLongTerm} 块、永久移交 {outPermanent} 块、离库处置 {disposed} 块、出库挂失 {outLost} 块。",
                $"归还控制结构：临时借出且需归还 {temporaryNeedReturnCount} 块，长期借出且需归还 {longTermNeedReturnCount} 块。"
            ];
        }

        private static IReadOnlyList<string> BuildRiskInsights(
            IReadOnlyList<OverviewMediumSnapshot> mediaItems,
            IReadOnlyList<OverviewApplicationSnapshot> applicationItems,
            IReadOnlyList<HardDiskDisposalRecord> disposalItems,
            IReadOnlyList<HardDiskInventoryRegisterRecord> inventoryItems,
            int missingLedgerCount,
            int overdueCount)
        {
            int missingLocationCount = mediaItems.Count(item =>
                IsLocatableInStockStatus(item) && string.IsNullOrWhiteSpace(item.CurrentLocation));
            int outboundWithoutKeeperCount = mediaItems.Count(item =>
                IsActiveOutboundStatus(item) && string.IsNullOrWhiteSpace(item.CurrentHolder));

            int needReturnCount = mediaItems.Count(item => item.NeedReturn);
            int temporaryNeedReturnCount = mediaItems.Count(item =>
                item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutTemporary);
            int longTermNeedReturnCount = mediaItems.Count(item =>
                item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutLongTerm);
            int longTermBorrowedCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutLongTerm);
            int damagedInStockCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockDamaged);
            int lostCount = mediaItems.Count(item =>
                item.CurrentStatus == HardDiskMedium.StatusOutLost
                || item.CurrentStatus == HardDiskMedium.StatusInStockLost);
            int lockedCount = mediaItems.Count(item => item.IsLocked);

            int submittedCount = applicationItems.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted);
            int pendingHandoverCount = applicationItems.Count(IsPendingHandover);
            int pendingUploadCount = applicationItems.Count(IsPendingSignedUpload);
            int pendingCompleteCount = applicationItems.Count(IsPendingComplete);
            int pendingDisposalCount = disposalItems.Count(IsPendingDisposal);
            int draftInventoryCount = inventoryItems.Count(item => item.Status == HardDiskInventoryRegisterRecord.StatusDraft);

            return
            [
                $"归还控制风险：需归还 {needReturnCount} 块（临时 {temporaryNeedReturnCount}、长期 {longTermNeedReturnCount}），逾期未归还 {overdueCount} 单。",
                $"基础台账风险：缺台账 {missingLedgerCount} 块，在库未登记位置 {missingLocationCount} 块，出库未明确保管 {outboundWithoutKeeperCount} 块，征用锁占用 {lockedCount} 块。",
                $"流程积压风险：申请待审批 {submittedCount} / 待实物交接 {pendingHandoverCount} / 待上传签批 {pendingUploadCount} / 待办结 {pendingCompleteCount}；离库处置进行中 {pendingDisposalCount} 单；盘库草稿 {draftInventoryCount} 单。",
                $"介质状态风险：长期借出 {longTermBorrowedCount} 块，在库损坏 {damagedInStockCount} 块，挂失/盘失 {lostCount} 块。"
            ];
        }

        private static string BuildApplicationStageSummary(IReadOnlyList<OverviewApplicationSnapshot> applications)
        {
            if (applications.Count == 0)
            {
                return "暂无记录";
            }

            return string.Join("，",
            [
                $"草稿 {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusDraft)} 单",
                $"待审批 {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted)} 单",
                $"待实物交接 {applications.Count(IsPendingHandover)} 单",
                $"待上传签批 {applications.Count(IsPendingSignedUpload)} 单",
                $"待办结 {applications.Count(IsPendingComplete)} 单",
                $"已办结 {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted)} 单",
                $"已作废（撤回） {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusWithdrawn)} 单",
                $"已作废（强制） {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusForceWithdrawn)} 单"
            ]);
        }

        private static string BuildDisposalStageSummary(IReadOnlyList<HardDiskDisposalRecord> disposalItems)
        {
            if (disposalItems.Count == 0)
            {
                return "暂无记录";
            }

            return string.Join("，",
            [
                $"草稿 {disposalItems.Count(item => item.Status == HardDiskDisposalRecord.StatusDraft)} 单",
                $"待审批 {disposalItems.Count(item => item.Status == HardDiskDisposalRecord.StatusSubmitted)} 单",
                $"待确认可上传 {disposalItems.Count(item => item.Status == HardDiskDisposalRecord.StatusApproved)} 单",
                $"待上传签批 {disposalItems.Count(item => item.Status == HardDiskDisposalRecord.StatusSignedUploaded && !item.SignedAttachmentUploaded)} 单",
                $"待办结 {disposalItems.Count(item => item.Status == HardDiskDisposalRecord.StatusSignedUploaded && item.SignedAttachmentUploaded)} 单",
                $"已办结 {disposalItems.Count(item => item.Status == HardDiskDisposalRecord.StatusCompleted)} 单",
                $"已作废 {disposalItems.Count(item => item.Status is HardDiskDisposalRecord.StatusWithdrawn or HardDiskDisposalRecord.StatusForceWithdrawn)} 单"
            ]);
        }

        private static string BuildInventoryStageSummary(IReadOnlyList<HardDiskInventoryRegisterRecord> inventoryItems)
        {
            if (inventoryItems.Count == 0)
            {
                return "暂无记录";
            }

            var kindSummary = FormatDistribution(
                inventoryItems
                    .Where(item => item.Status == HardDiskInventoryRegisterRecord.StatusCompleted)
                    .GroupBy(item => string.IsNullOrWhiteSpace(item.RegisterKind) ? "(未登记类型)" : item.RegisterKind.Trim())
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label),
                suffix: "单");

            return string.Join("，",
            [
                $"草稿 {inventoryItems.Count(item => item.Status == HardDiskInventoryRegisterRecord.StatusDraft)} 单",
                $"已办结 {inventoryItems.Count(item => item.Status == HardDiskInventoryRegisterRecord.StatusCompleted)} 单",
                $"已作废 {inventoryItems.Count(item => item.Status == HardDiskInventoryRegisterRecord.StatusWithdrawn)} 单",
                $"已办结类型分布：{kindSummary}"
            ]);
        }

        private static string BuildTransactionStageSummary(IReadOnlyList<OverviewTransactionSnapshot> transactions)
        {
            if (transactions.Count == 0)
            {
                return "暂无交接流转记录";
            }

            return FormatDistribution(
                transactions
                    .GroupBy(item => string.IsNullOrWhiteSpace(item.TransactionType) ? "(未登记类型)" : item.TransactionType.Trim())
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label)
                    .Take(6),
                suffix: "次");
        }

        private static string FormatDistribution(IEnumerable<DistributionItem> items, string suffix = "块")
        {
            var materialized = items.Where(item => item.Count > 0).ToList();
            if (materialized.Count == 0)
            {
                return "暂无";
            }

            return string.Join("、", materialized.Select(item => $"{item.Label} {item.Count}{suffix}"));
        }

        /// <summary>
        /// 位置洞察按档口键归并（忽略盒内序号），无法解析时回退原文。
        /// </summary>
        private static string NormalizeLocationGroup(string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return "(未登记位置)";
            }

            string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(location);
            return string.IsNullOrWhiteSpace(slotKey) ? location.Trim() : slotKey;
        }

        private static string NormalizeCapacity(string? capacity)
        {
            return string.IsNullOrWhiteSpace(capacity) ? "(未登记容量)" : capacity.Trim();
        }

        private static double GetCapacityInGb(OverviewMediumSnapshot item)
        {
            if (string.IsNullOrWhiteSpace(item.Capacity))
            {
                return 0;
            }

            var match = Regex.Match(item.Capacity, @"(?<value>\d+(\.\d+)?)\s*(?<unit>TB|T|GB|G|MB|M)", RegexOptions.IgnoreCase);
            if (!match.Success ||
                !double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return 0;
            }

            string unit = match.Groups["unit"].Value.ToUpperInvariant();
            return unit switch
            {
                "TB" or "T" => value * 1024,
                "GB" or "G" => value,
                "MB" or "M" => value / 1024,
                _ => 0
            };
        }

        private static string FormatCapacityAmount(double capacityGb)
        {
            if (capacityGb <= 0)
            {
                return "0 GB";
            }

            return capacityGb >= 1024
                ? $"{capacityGb / 1024:0.##} TB"
                : $"{capacityGb:0.##} GB";
        }

        private static string NormalizeKeeper(OverviewMediumSnapshot item)
        {
            if (!string.IsNullOrWhiteSpace(item.CurrentHolder))
            {
                return item.CurrentHolder.Trim();
            }

            return string.IsNullOrWhiteSpace(item.CurrentLocation) ? "(未明确保管)" : item.CurrentLocation.Trim();
        }

        /// <summary>临时/长期借出（可归还的在外状态）。</summary>
        private static bool IsBorrowedStatus(OverviewMediumSnapshot item)
        {
            return item.CurrentStatus == HardDiskMedium.StatusOutTemporary
                || item.CurrentStatus == HardDiskMedium.StatusOutLongTerm;
        }

        /// <summary>仍处于资料室外的有效出库状态（不含离库处置、在库盘失）。</summary>
        private static bool IsActiveOutboundStatus(OverviewMediumSnapshot item)
        {
            return IsBorrowedStatus(item)
                || item.CurrentStatus == HardDiskMedium.StatusOutPermanent
                || item.CurrentStatus == HardDiskMedium.StatusOutLost;
        }

        /// <summary>终态离库：永久移交 / 离库处置 / 出库挂失。</summary>
        private static bool IsTerminalAwayStatus(OverviewMediumSnapshot item)
        {
            return item.CurrentStatus == HardDiskMedium.StatusOutPermanent
                || item.CurrentStatus == HardDiskMedium.StatusDisposed
                || item.CurrentStatus == HardDiskMedium.StatusOutLost;
        }

        /// <summary>资料室在库且通常需要档口定位的状态（不含盘失）。</summary>
        private static bool IsLocatableInStockStatus(OverviewMediumSnapshot item)
        {
            return item.CurrentStatus == HardDiskMedium.StatusInStockBlank
                || item.CurrentStatus == HardDiskMedium.StatusInStockData
                || item.CurrentStatus == HardDiskMedium.StatusInStockDamaged;
        }

        private static bool IsPendingHandover(OverviewApplicationSnapshot item)
            => item.ApplicationStatus == HardDiskMediaApplication.StatusApproved;

        private static bool IsPendingSignedUpload(OverviewApplicationSnapshot item)
            => item.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded && !item.SignedAttachmentUploaded;

        private static bool IsPendingComplete(OverviewApplicationSnapshot item)
            => item.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded && item.SignedAttachmentUploaded;

        private static bool IsPendingDisposal(HardDiskDisposalRecord item)
            => item.Status is HardDiskDisposalRecord.StatusSubmitted
                or HardDiskDisposalRecord.StatusApproved
                or HardDiskDisposalRecord.StatusSignedUploaded;

        private sealed record OverviewMediumSnapshot(
            string CurrentStatus,
            string CurrentLocation,
            string CurrentHolder,
            string Capacity,
            string MediaNature,
            bool NeedReturn,
            bool IsLocked,
            string? LockBusinessType);

        private sealed record OverviewApplicationSnapshot(string ApplicationType, int ApplicationStatus, bool SignedAttachmentUploaded);

        private sealed record OverviewTransactionSnapshot(string TransactionType, DateTime OperateTime);

        private sealed record DistributionItem(string Label, int Count);
    }
}
