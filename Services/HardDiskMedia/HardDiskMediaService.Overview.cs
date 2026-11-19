using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocMgr.Models.HardDiskMedia;
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
            HardDiskOverviewAggregate data = await _hardDiskMediaRepository.GetOverviewAggregateAsync();
            int overdueCount = (await _hardDiskMediaRepository.GetOverdueOutboundApplicationsForToDoAsync(DateTime.Now, 500)).Count;

            int pendingHandoverCount = CountApplications(data.Applications, IsPendingHandover);
            int pendingSignedCount = CountApplications(data.Applications, IsPendingSignedUpload);
            int pendingCompleteCount = CountApplications(data.Applications, IsPendingComplete);

            return new HardDiskMediaOverview
            {
                TotalMediumCount = data.StatusCounts.Values.Sum(),
                MissingLedgerMediumCount = data.MissingLedgerCount,
                BlankInStockCount = CountOf(data.StatusCounts, HardDiskMedium.StatusInStockBlank),
                DataCarrierInStockCount = CountOf(data.StatusCounts, HardDiskMedium.StatusInStockData),
                DamagedInStockCount = CountOf(data.StatusCounts, HardDiskMedium.StatusInStockDamaged),
                InStockLostCount = CountOf(data.StatusCounts, HardDiskMedium.StatusInStockLost),
                InStockScrapCount = CountOf(data.StatusCounts, HardDiskMedium.StatusInStockScrap),
                BorrowedCount = CountOf(data.StatusCounts, HardDiskMedium.StatusOutTemporary)
                    + CountOf(data.StatusCounts, HardDiskMedium.StatusOutLongTerm),
                PermanentTransferCount = CountOf(data.StatusCounts, HardDiskMedium.StatusOutPermanent),
                DisposedCount = CountOf(data.StatusCounts, HardDiskMedium.StatusDisposed),
                OutLostCount = CountOf(data.StatusCounts, HardDiskMedium.StatusOutLost),
                NeedReturnMediumCount = data.NeedReturnCount,
                TemporaryNeedReturnMediumCount = data.TemporaryNeedReturnCount,
                LongTermNeedReturnMediumCount = data.LongTermNeedReturnCount,
                OverdueNeedReturnCount = overdueCount,
                MissingLocationMediumCount = data.MissingLocationCount,
                OutboundWithoutKeeperMediumCount = data.OutboundWithoutKeeperCount,
                LockedMediumCount = data.LockedCount,
                SubmittedApplicationCount = CountApplications(
                    data.Applications,
                    item => item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted),
                PendingHandoverApplicationCount = pendingHandoverCount,
                PendingSignedFileCount = pendingSignedCount,
                PendingCompleteApplicationCount = pendingCompleteCount,
                PendingDisposalCount = CountDisposals(data.Disposals, IsPendingDisposal),
                DraftInventoryRegisterCount = CountInventories(
                    data.Inventories,
                    item => item.Status == HardDiskInventoryRegisterRecord.StatusDraft),
                LocationInsights = BuildLocationInsights(data),
                OutboundCapacityInsights = BuildOutboundCapacityInsights(data.OutboundCapacities),
                HandoverInsights = BuildHandoverInsights(data),
                LifecycleInsights = BuildLifecycleInsights(data),
                RiskInsights = BuildRiskInsights(data, overdueCount)
            };
        }

        private static IReadOnlyList<string> BuildLocationInsights(HardDiskOverviewAggregate data)
        {
            if (data.StatusCounts.Count == 0)
            {
                return ["当前暂无硬盘介质台账数据。"];
            }

            var allLocations = FormatDistribution(MergeLocations(data.AllLocations).Take(5));
            var inStockSummary = FormatDistribution(MergeLocations(data.InStockLocations).Take(3));
            var keeperSummary = FormatDistribution(
                data.BorrowedKeepers
                    .GroupBy(item => NormalizeKeeper(item.Holder, item.Location))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label, StringComparer.Ordinal)
                    .Take(4));
            var lockSummary = FormatDistribution(
                data.LockTypes
                    .Select(item => new DistributionItem(
                        string.IsNullOrWhiteSpace(item.Label) ? "(未登记占用类型)" : item.Label.Trim(),
                        item.Count))
                    .GroupBy(item => item.Label, StringComparer.Ordinal)
                    .Select(group => new DistributionItem(group.Key, group.Sum(item => item.Count)))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label, StringComparer.Ordinal));

            return
            [
                $"当前位置（按档口归并）前五：{allLocations}。",
                $"资料室在库位置：{inStockSummary}。",
                $"临时/长期借出保管分布：{keeperSummary}。",
                $"征用锁占用：{lockSummary}。"
            ];
        }

        private static IReadOnlyList<string> BuildOutboundCapacityInsights(
            IReadOnlyList<HardDiskOverviewCapacityRow> capacityRows)
        {
            var borrowedMedia = capacityRows.Where(item => IsBorrowedStatus(item.Status)).ToList();
            var terminalMedia = capacityRows.Where(item => IsTerminalAwayStatus(item.Status)).ToList();

            if (borrowedMedia.Count == 0 && terminalMedia.Count == 0)
            {
                return ["当前无借出或终态离库硬盘，容量占用风险为 0。"];
            }

            var borrowCapacitySummary = FormatDistribution(
                borrowedMedia
                    .GroupBy(item => NormalizeCapacity(item.Capacity))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label, StringComparer.Ordinal));

            var terminalCapacitySummary = FormatDistribution(
                terminalMedia
                    .GroupBy(item => NormalizeCapacity(item.Capacity))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label, StringComparer.Ordinal));

            double borrowCapacityGb = borrowedMedia.Sum(item => GetCapacityInGb(item.Capacity));
            double terminalCapacityGb = terminalMedia.Sum(item => GetCapacityInGb(item.Capacity));

            return
            [
                $"临时/长期借出 {borrowedMedia.Count} 块，容量约 {FormatCapacityAmount(borrowCapacityGb)}；容量档分布：{borrowCapacitySummary}。",
                $"永久移交/离库处置/出库挂失 {terminalMedia.Count} 块，容量约 {FormatCapacityAmount(terminalCapacityGb)}；容量档分布：{terminalCapacitySummary}。"
            ];
        }

        private static IReadOnlyList<string> BuildHandoverInsights(HardDiskOverviewAggregate data)
        {
            var outboundApplications = data.Applications
                .Where(item => !IsReturnOrLossRegistrationType(item.ApplicationType ?? string.Empty))
                .ToList();
            var registrationApplications = data.Applications
                .Where(item => IsReturnOrLossRegistrationType(item.ApplicationType ?? string.Empty))
                .ToList();

            return
            [
                $"出库申请环节：{BuildApplicationStageSummary(outboundApplications)}。",
                $"归还/挂失登记环节：{BuildApplicationStageSummary(registrationApplications)}。",
                $"离库处置环节：{BuildDisposalStageSummary(data.Disposals)}。",
                $"盘库登记环节：{BuildInventoryStageSummary(data.Inventories)}。",
                $"近90天流转记录：{BuildTransactionStageSummary(data.RecentTransactionTypes)}。"
            ];
        }

        private static IReadOnlyList<string> BuildLifecycleInsights(HardDiskOverviewAggregate data)
        {
            if (data.StatusCounts.Count == 0)
            {
                return ["当前暂无硬盘介质生命周期数据。"];
            }

            return
            [
                $"介质属性结构：空白介质 {CountOf(data.NatureCounts, HardDiskMedium.NatureBlank)} 块，资料载体 {CountOf(data.NatureCounts, HardDiskMedium.NatureDataCarrier)} 块，纳入需归还控制 {data.NeedReturnCount} 块，征用锁占用 {data.LockedCount} 块。",
                $"在库结构：空盘 {CountOf(data.StatusCounts, HardDiskMedium.StatusInStockBlank)} 块、资料载体 {CountOf(data.StatusCounts, HardDiskMedium.StatusInStockData)} 块、损坏待处置 {CountOf(data.StatusCounts, HardDiskMedium.StatusInStockDamaged)} 块、盘失 {CountOf(data.StatusCounts, HardDiskMedium.StatusInStockLost)} 块、拟销 {CountOf(data.StatusCounts, HardDiskMedium.StatusInStockScrap)} 块。",
                $"在外/终态结构：临时借出 {CountOf(data.StatusCounts, HardDiskMedium.StatusOutTemporary)} 块、长期借出 {CountOf(data.StatusCounts, HardDiskMedium.StatusOutLongTerm)} 块、永久移交 {CountOf(data.StatusCounts, HardDiskMedium.StatusOutPermanent)} 块、离库处置 {CountOf(data.StatusCounts, HardDiskMedium.StatusDisposed)} 块、出库挂失 {CountOf(data.StatusCounts, HardDiskMedium.StatusOutLost)} 块。",
                $"归还控制结构：临时借出且需归还 {data.TemporaryNeedReturnCount} 块，长期借出且需归还 {data.LongTermNeedReturnCount} 块。"
            ];
        }

        private static IReadOnlyList<string> BuildRiskInsights(HardDiskOverviewAggregate data, int overdueCount)
        {
            int lostCount = CountOf(data.StatusCounts, HardDiskMedium.StatusOutLost)
                + CountOf(data.StatusCounts, HardDiskMedium.StatusInStockLost)
                + CountOf(data.StatusCounts, HardDiskMedium.StatusInStockScrap);
            int submittedCount = CountApplications(
                data.Applications,
                item => item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted);
            int pendingDisposalCount = CountDisposals(data.Disposals, IsPendingDisposal);
            int draftInventoryCount = CountInventories(
                data.Inventories,
                item => item.Status == HardDiskInventoryRegisterRecord.StatusDraft);

            return
            [
                $"归还控制风险：需归还 {data.NeedReturnCount} 块（临时 {data.TemporaryNeedReturnCount}、长期 {data.LongTermNeedReturnCount}），逾期未归还 {overdueCount} 单。",
                $"基础台账风险：缺台账 {data.MissingLedgerCount} 块，在库未登记位置 {data.MissingLocationCount} 块，出库未明确保管 {data.OutboundWithoutKeeperCount} 块，征用锁占用 {data.LockedCount} 块。",
                $"流程积压风险：申请待审批 {submittedCount} / 待实物交接 {CountApplications(data.Applications, IsPendingHandover)} / 待上传签批 {CountApplications(data.Applications, IsPendingSignedUpload)} / 待办结 {CountApplications(data.Applications, IsPendingComplete)}；离库处置进行中 {pendingDisposalCount} 单；盘库草稿 {draftInventoryCount} 单。",
                $"介质状态风险：长期借出 {CountOf(data.StatusCounts, HardDiskMedium.StatusOutLongTerm)} 块，在库损坏 {CountOf(data.StatusCounts, HardDiskMedium.StatusInStockDamaged)} 块，挂失/盘失/拟销 {lostCount} 块。"
            ];
        }

        private static string BuildApplicationStageSummary(IReadOnlyList<HardDiskOverviewApplicationGroup> applications)
        {
            if (applications.Sum(item => item.Count) == 0)
            {
                return "暂无记录";
            }

            return string.Join("，",
            [
                $"草稿 {CountApplications(applications, item => item.ApplicationStatus == HardDiskMediaApplication.StatusDraft)} 单",
                $"待审批 {CountApplications(applications, item => item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted)} 单",
                $"待实物交接 {CountApplications(applications, IsPendingHandover)} 单",
                $"待上传签批 {CountApplications(applications, IsPendingSignedUpload)} 单",
                $"待办结 {CountApplications(applications, IsPendingComplete)} 单",
                $"已办结 {CountApplications(applications, item => item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted)} 单",
                $"已作废（撤回） {CountApplications(applications, item => item.ApplicationStatus == HardDiskMediaApplication.StatusWithdrawn)} 单",
                $"已作废（强制） {CountApplications(applications, item => item.ApplicationStatus == HardDiskMediaApplication.StatusForceWithdrawn)} 单"
            ]);
        }

        private static string BuildDisposalStageSummary(IReadOnlyList<HardDiskOverviewDisposalGroup> disposalItems)
        {
            if (disposalItems.Sum(item => item.Count) == 0)
            {
                return "暂无记录";
            }

            return string.Join("，",
            [
                $"草稿 {CountDisposals(disposalItems, item => item.Status == HardDiskDisposalRecord.StatusDraft)} 单",
                $"待审批 {CountDisposals(disposalItems, item => item.Status == HardDiskDisposalRecord.StatusSubmitted)} 单",
                $"待确认可上传 {CountDisposals(disposalItems, item => item.Status == HardDiskDisposalRecord.StatusApproved)} 单",
                $"待上传签批 {CountDisposals(disposalItems, item => item.Status == HardDiskDisposalRecord.StatusSignedUploaded && !item.SignedAttachmentUploaded)} 单",
                $"待办结 {CountDisposals(disposalItems, item => item.Status == HardDiskDisposalRecord.StatusSignedUploaded && item.SignedAttachmentUploaded)} 单",
                $"已办结 {CountDisposals(disposalItems, item => item.Status == HardDiskDisposalRecord.StatusCompleted)} 单",
                $"已作废 {CountDisposals(disposalItems, item => item.Status is HardDiskDisposalRecord.StatusWithdrawn or HardDiskDisposalRecord.StatusForceWithdrawn)} 单"
            ]);
        }

        private static string BuildInventoryStageSummary(IReadOnlyList<HardDiskOverviewInventoryGroup> inventoryItems)
        {
            if (inventoryItems.Sum(item => item.Count) == 0)
            {
                return "暂无记录";
            }

            var kindSummary = FormatDistribution(
                inventoryItems
                    .Where(item => item.Status == HardDiskInventoryRegisterRecord.StatusCompleted)
                    .GroupBy(item => string.IsNullOrWhiteSpace(item.RegisterKind) ? "(未登记类型)" : item.RegisterKind.Trim())
                    .Select(group => new DistributionItem(group.Key, group.Sum(item => item.Count)))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label, StringComparer.Ordinal),
                suffix: "单");

            return string.Join("，",
            [
                $"草稿 {CountInventories(inventoryItems, item => item.Status == HardDiskInventoryRegisterRecord.StatusDraft)} 单",
                $"已办结 {CountInventories(inventoryItems, item => item.Status == HardDiskInventoryRegisterRecord.StatusCompleted)} 单",
                $"已作废 {CountInventories(inventoryItems, item => item.Status == HardDiskInventoryRegisterRecord.StatusWithdrawn)} 单",
                $"已办结类型分布：{kindSummary}"
            ]);
        }

        private static string BuildTransactionStageSummary(IReadOnlyList<HardDiskOverviewLabelCount> transactions)
        {
            if (transactions.Sum(item => item.Count) == 0)
            {
                return "暂无交接流转记录";
            }

            return FormatDistribution(
                transactions
                    .Select(item => new DistributionItem(
                        string.IsNullOrWhiteSpace(item.Label) ? "(未登记类型)" : item.Label.Trim(),
                        item.Count))
                    .GroupBy(item => item.Label, StringComparer.Ordinal)
                    .Select(group => new DistributionItem(group.Key, group.Sum(item => item.Count)))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label, StringComparer.Ordinal)
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

        private static double GetCapacityInGb(string? capacity)
        {
            if (string.IsNullOrWhiteSpace(capacity))
            {
                return 0;
            }

            var match = Regex.Match(capacity, @"(?<value>\d+(\.\d+)?)\s*(?<unit>TB|T|GB|G|MB|M)", RegexOptions.IgnoreCase);
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

        private static string NormalizeKeeper(string? holder, string? location)
        {
            if (!string.IsNullOrWhiteSpace(holder))
            {
                return holder.Trim();
            }

            return string.IsNullOrWhiteSpace(location) ? "(未明确保管)" : location.Trim();
        }

        private static IEnumerable<DistributionItem> MergeLocations(IReadOnlyList<HardDiskOverviewLabelCount> locations)
        {
            return locations
                .GroupBy(item => NormalizeLocationGroup(item.Label), StringComparer.Ordinal)
                .Select(group => new DistributionItem(group.Key, group.Sum(item => item.Count)))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Label, StringComparer.Ordinal);
        }

        private static int CountOf(IReadOnlyDictionary<string, int> counts, string key)
            => counts.TryGetValue(key, out int count) ? count : 0;

        private static int CountApplications(
            IReadOnlyList<HardDiskOverviewApplicationGroup> groups,
            Func<HardDiskOverviewApplicationGroup, bool> predicate)
            => groups.Where(predicate).Sum(item => item.Count);

        private static int CountDisposals(
            IReadOnlyList<HardDiskOverviewDisposalGroup> groups,
            Func<HardDiskOverviewDisposalGroup, bool> predicate)
            => groups.Where(predicate).Sum(item => item.Count);

        private static int CountInventories(
            IReadOnlyList<HardDiskOverviewInventoryGroup> groups,
            Func<HardDiskOverviewInventoryGroup, bool> predicate)
            => groups.Where(predicate).Sum(item => item.Count);

        /// <summary>临时/长期借出（可归还的在外状态）。</summary>
        private static bool IsBorrowedStatus(string? status)
        {
            return status == HardDiskMedium.StatusOutTemporary
                || status == HardDiskMedium.StatusOutLongTerm;
        }

        /// <summary>终态离库：永久移交 / 离库处置 / 出库挂失。</summary>
        private static bool IsTerminalAwayStatus(string? status)
        {
            return status == HardDiskMedium.StatusOutPermanent
                || status == HardDiskMedium.StatusDisposed
                || status == HardDiskMedium.StatusOutLost;
        }

        private static bool IsPendingHandover(HardDiskOverviewApplicationGroup item)
            => item.ApplicationStatus == HardDiskMediaApplication.StatusApproved;

        private static bool IsPendingSignedUpload(HardDiskOverviewApplicationGroup item)
            => item.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded && !item.SignedAttachmentUploaded;

        private static bool IsPendingComplete(HardDiskOverviewApplicationGroup item)
            => item.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded && item.SignedAttachmentUploaded;

        private static bool IsPendingDisposal(HardDiskOverviewDisposalGroup item)
            => item.Status is HardDiskDisposalRecord.StatusSubmitted
                or HardDiskDisposalRecord.StatusApproved
                or HardDiskDisposalRecord.StatusSignedUploaded;

        private sealed record DistributionItem(string Label, int Count);
    }
}
