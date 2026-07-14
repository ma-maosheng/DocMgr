using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质总览统计与洞察分析。
    /// </summary>
    public partial class HardDiskMediaService
    {
        /// <inheritdoc/>
        public async Task<HardDiskMediaOverview> GetOverviewAsync()
        {
            var media = await _hardDiskMediaRepository.GetOverviewMediaAsync();
            var mediaItems = media
                .Where(item => item.Ledger != null)
                .Select(item => new OverviewMediumSnapshot(
                    item.Ledger!.MediaStatus,
                    item.Ledger.StorageLocation,
                    item.Ledger.HolderOrOrganization,
                    item.Capacity,
                    item.Ledger.MediaNature,
                    item.Ledger.NeedReturn))
                .ToList();

            var applicationItems = (await _hardDiskMediaRepository.GetOverviewApplicationsAsync())
                .Select(item => new OverviewApplicationSnapshot(item.ApplicationType, item.ApplicationStatus, item.SignedAttachmentUploaded))
                .ToList();

            var transactionItems = (await _hardDiskMediaRepository.GetOverviewTransactionsAsync())
                .Select(item => new OverviewTransactionSnapshot(item.TransactionType, item.OperateTime))
                .ToList();

            return new HardDiskMediaOverview
            {
                TotalMediumCount = mediaItems.Count,
                BlankInStockCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockBlank),
                BorrowedCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutTemporary || item.CurrentStatus == HardDiskMedium.StatusOutLongTerm),
                DataCarrierInStockCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockData),
                DamagedInStockCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockDamaged),
                TransferOutCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutPermanent || item.CurrentStatus == HardDiskMedium.StatusOutDestroyed || item.CurrentStatus == HardDiskMedium.StatusOutLost),
                NeedReturnMediumCount = mediaItems.Count(item => item.NeedReturn),
                LongTermNeedReturnMediumCount = mediaItems.Count(item => item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutLongTerm),
                TemporaryNeedReturnMediumCount = mediaItems.Count(item => item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutTemporary),
                MissingLocationMediumCount = mediaItems.Count(item => string.IsNullOrWhiteSpace(item.CurrentLocation)),
                OutboundWithoutKeeperMediumCount = mediaItems.Count(item =>
                    IsOutboundStatus(item) &&
                    string.IsNullOrWhiteSpace(item.CurrentHolder)),
                PendingProcessApplicationCount = applicationItems.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusPendingProcess),
                PendingSignedFileCount = applicationItems.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusPendingUpload),
                SubmittedApplicationCount = applicationItems.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted),
                LocationInsights = BuildLocationInsights(mediaItems),
                OutboundCapacityInsights = BuildOutboundCapacityInsights(mediaItems),
                HandoverInsights = BuildHandoverInsights(applicationItems, transactionItems),
                LifecycleInsights = BuildLifecycleInsights(mediaItems),
                RiskInsights = BuildRiskInsights(mediaItems, applicationItems)
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
                    .GroupBy(item => NormalizeLocation(item.CurrentLocation))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label)
                    .Take(5));

            var inStockSummary = FormatDistribution(
                mediaItems
                    .Where(IsInStockStatus)
                    .GroupBy(item => NormalizeLocation(item.CurrentLocation))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label)
                    .Take(3));

            var keeperSummary = FormatDistribution(
                mediaItems
                    .Where(IsOutboundStatus)
                    .GroupBy(item => NormalizeKeeper(item))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label)
                    .Take(4));

            return
            [
                $"当前位置分布前五：{allLocations}。",
                $"资料室在库位置：{inStockSummary}。",
                $"出库在外保管分布：{keeperSummary}。"
            ];
        }

        private static IReadOnlyList<string> BuildOutboundCapacityInsights(IReadOnlyList<OverviewMediumSnapshot> mediaItems)
        {
            var outboundMedia = mediaItems.Where(IsOutboundStatus).ToList();
            if (outboundMedia.Count == 0)
            {
                return ["当前无出库状态硬盘，容量占用风险为 0。"];
            }

            var totalCapacitySummary = FormatDistribution(
                outboundMedia
                    .GroupBy(item => NormalizeCapacity(item.Capacity))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label));

            var borrowCapacitySummary = FormatDistribution(
                outboundMedia
                    .Where(item => item.CurrentStatus == HardDiskMedium.StatusOutTemporary || item.CurrentStatus == HardDiskMedium.StatusOutLongTerm)
                    .GroupBy(item => NormalizeCapacity(item.Capacity))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label));

            var closedCapacitySummary = FormatDistribution(
                outboundMedia
                    .Where(item => item.CurrentStatus == HardDiskMedium.StatusOutPermanent || item.CurrentStatus == HardDiskMedium.StatusOutDestroyed || item.CurrentStatus == HardDiskMedium.StatusOutLost)
                    .GroupBy(item => NormalizeCapacity(item.Capacity))
                    .Select(group => new DistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label));

            double totalCapacityGb = outboundMedia.Sum(GetCapacityInGb);
            double borrowCapacityGb = outboundMedia
                .Where(item => item.CurrentStatus == HardDiskMedium.StatusOutTemporary || item.CurrentStatus == HardDiskMedium.StatusOutLongTerm)
                .Sum(GetCapacityInGb);
            double closedCapacityGb = outboundMedia
                .Where(item => item.CurrentStatus == HardDiskMedium.StatusOutPermanent || item.CurrentStatus == HardDiskMedium.StatusOutDestroyed || item.CurrentStatus == HardDiskMedium.StatusOutLost)
                .Sum(GetCapacityInGb);

            return
            [
                $"全部出库介质共 {outboundMedia.Count} 块，已登记容量约 {FormatCapacityAmount(totalCapacityGb)}；容量档分布：{totalCapacitySummary}。",
                $"临时/长期借出容量约 {FormatCapacityAmount(borrowCapacityGb)}，容量档分布：{borrowCapacitySummary}。",
                $"永久移交/销毁/挂失容量约 {FormatCapacityAmount(closedCapacityGb)}，容量档分布：{closedCapacitySummary}。"
            ];
        }

        private static IReadOnlyList<string> BuildHandoverInsights(
            IReadOnlyList<OverviewApplicationSnapshot> applicationItems,
            IReadOnlyList<OverviewTransactionSnapshot> transactionItems)
        {
            var outboundApplications = applicationItems.Where(item => !IsRegistrationWithoutApprovalType(item.ApplicationType)).ToList();
            var registrationApplications = applicationItems.Where(item => IsRegistrationWithoutApprovalType(item.ApplicationType)).ToList();

            var recentTransactions = transactionItems
                .Where(item => item.OperateTime >= DateTime.Now.AddDays(-90))
                .ToList();

            return
            [
                $"出库申请环节：{BuildApplicationStageSummary(outboundApplications)}。",
                $"归还/挂失登记环节：{BuildApplicationStageSummary(registrationApplications)}。",
                $"近90天交接记录：{BuildTransactionStageSummary(recentTransactions)}。"
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
            int temporaryNeedReturnCount = mediaItems.Count(item => item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutTemporary);
            int longTermNeedReturnCount = mediaItems.Count(item => item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutLongTerm);

            int inStockBlank = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockBlank);
            int inStockData = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockData);
            int inStockDamaged = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockDamaged);
            int outTemporary = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutTemporary);
            int outLongTerm = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutLongTerm);
            int outPermanent = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutPermanent);
            int outDestroyed = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutDestroyed);
            int outLost = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutLost);

            return
            [
                $"介质属性结构：空白介质 {blankCount} 块，资料载体 {carrierCount} 块，纳入需归还控制的介质 {needReturnCount} 块。",
                $"在库结构：空盘 {inStockBlank} 块、资料载体 {inStockData} 块、损坏待处置 {inStockDamaged} 块。",
                $"在外结构：临时借出 {outTemporary} 块、长期借出 {outLongTerm} 块、永久移交 {outPermanent} 块、销毁 {outDestroyed} 块、挂失 {outLost} 块。",
                $"归还控制结构：临时借出且需归还 {temporaryNeedReturnCount} 块，长期借出且需归还 {longTermNeedReturnCount} 块。"
            ];
        }

        private static IReadOnlyList<string> BuildRiskInsights(
            IReadOnlyList<OverviewMediumSnapshot> mediaItems,
            IReadOnlyList<OverviewApplicationSnapshot> applicationItems)
        {
            int missingLocationCount = mediaItems.Count(item => string.IsNullOrWhiteSpace(item.CurrentLocation));
            int outboundWithoutKeeperCount = mediaItems.Count(item =>
                IsOutboundStatus(item) &&
                string.IsNullOrWhiteSpace(item.CurrentHolder));

            int needReturnCount = mediaItems.Count(item => item.NeedReturn);
            int temporaryNeedReturnCount = mediaItems.Count(item => item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutTemporary);
            int longTermNeedReturnCount = mediaItems.Count(item => item.NeedReturn && item.CurrentStatus == HardDiskMedium.StatusOutLongTerm);
            int longTermBorrowedCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutLongTerm);
            int damagedInStockCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusInStockDamaged);
            int lostCount = mediaItems.Count(item => item.CurrentStatus == HardDiskMedium.StatusOutLost);

            int submittedCount = applicationItems.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted);
            int pendingHandoverCount = applicationItems.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusApproved);
            int pendingUploadCount = applicationItems.Count(item =>
                item.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded && !item.SignedAttachmentUploaded);
            int pendingCompleteCount = applicationItems.Count(item =>
                item.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded && item.SignedAttachmentUploaded);

            return
            [
                $"归还控制风险：需归还介质 {needReturnCount} 块，其中临时借出且需归还 {temporaryNeedReturnCount} 块、长期借出且需归还 {longTermNeedReturnCount} 块。",
                $"基础台账风险：未登记当前位置 {missingLocationCount} 块，出库但未明确保管人/接收单位 {outboundWithoutKeeperCount} 块。",
                    $"流程积压风险：待审批 {submittedCount} 单，待实物交接 {pendingHandoverCount} 单，待上传签批交接单 {pendingUploadCount} 单，待办结 {pendingCompleteCount} 单，已办结 {applicationItems.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted)} 单，已作废（撤回） {applicationItems.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusWithdrawn)} 单，已作废（强制） {applicationItems.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusForceWithdrawn)} 单",
                $"介质状态风险：长期借出 {longTermBorrowedCount} 块，在库损坏 {damagedInStockCount} 块，挂失 {lostCount} 块。"
            ];
        }

        private static string BuildApplicationStageSummary(IReadOnlyList<OverviewApplicationSnapshot> applications)
        {
            if (applications.Count == 0)
            {
                return "暂无记录";
            }

            return $"待审批 {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted)} 单，待实物交接 {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusApproved)} 单，待上传签批交接单 {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded && !item.SignedAttachmentUploaded)} 单，待办结 {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded && item.SignedAttachmentUploaded)} 单，已办结 {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted)} 单，已作废（撤回） {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusWithdrawn)} 单，已作废（强制） {applications.Count(item => item.ApplicationStatus == HardDiskMediaApplication.StatusForceWithdrawn)} 单";
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

        private static string NormalizeLocation(string? location)
        {
            return string.IsNullOrWhiteSpace(location) ? "(未登记位置)" : location.Trim();
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

            return NormalizeLocation(item.CurrentLocation);
        }

        private static bool IsOutboundStatus(OverviewMediumSnapshot item)
        {
            return item.CurrentStatus == HardDiskMedium.StatusOutTemporary ||
                   item.CurrentStatus == HardDiskMedium.StatusOutLongTerm ||
                   item.CurrentStatus == HardDiskMedium.StatusOutPermanent ||
                   item.CurrentStatus == HardDiskMedium.StatusOutDestroyed ||
                   item.CurrentStatus == HardDiskMedium.StatusOutLost;
        }

        private static bool IsInStockStatus(OverviewMediumSnapshot item)
        {
            return item.CurrentStatus == HardDiskMedium.StatusInStockBlank ||
                   item.CurrentStatus == HardDiskMedium.StatusInStockData ||
                   item.CurrentStatus == HardDiskMedium.StatusInStockDamaged;
        }

        private sealed record OverviewMediumSnapshot(
            string CurrentStatus,
            string CurrentLocation,
            string CurrentHolder,
            string Capacity,
            string MediaNature,
            bool NeedReturn);

        private sealed record OverviewApplicationSnapshot(string ApplicationType, string ApplicationStatus, bool SignedAttachmentUploaded);

        private sealed record OverviewTransactionSnapshot(string TransactionType, DateTime OperateTime);

        private sealed record DistributionItem(string Label, int Count);
    }
}
