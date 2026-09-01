using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.OpticalDiscMedia;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 数据光盘介质总览统计与洞察分析。
    /// </summary>
    public partial class HardDiskMediaService
    {
        /// <inheritdoc/>
        public async Task<OpticalDiscMediaOverview> GetOpticalDiscOverviewAsync()
        {
            var media = await _hardDiskMediaRepository.GetOpticalDiscOverviewMediaAsync();
            var mediaItems = media
                .Where(item => item.Ledger != null)
                .Select(item => new OpticalDiscOverviewMediumSnapshot(
                    item.Ledger!.MediaStatus,
                    item.Ledger.StorageLocation,
                    item.Ledger.HolderOrOrganization,
                    item.Capacity,
                    item.Ledger.NeedReturn,
                    item.RegistrationMethod,
                    item.DiscType))
                .ToList();

            var transactionItems = (await _hardDiskMediaRepository.GetOpticalDiscOverviewTransactionsAsync())
                .Select(item => new OpticalDiscOverviewTransactionSnapshot(item.TransactionType, item.OperateTime))
                .ToList();

            var recentTransactions = transactionItems
                .Where(item => item.OperateTime >= DateTime.Now.AddDays(-90))
                .ToList();

            return new OpticalDiscMediaOverview
            {
                TotalMediumCount = mediaItems.Count,
                InStockCount = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusInStock),
                OutTemporaryCount = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusOut),
                DamagedInStockCount = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusDamaged),
                LostInStockCount = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusLost),
                ScrapInStockCount = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusScrap),
                DestroyedCount = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusDestroyed),
                NeedReturnMediumCount = mediaItems.Count(item => item.NeedReturn),
                MissingLocationMediumCount = mediaItems.Count(item => string.IsNullOrWhiteSpace(item.CurrentLocation)),
                OutboundWithoutKeeperMediumCount = mediaItems.Count(item =>
                    IsOpticalDiscOutboundStatus(item) &&
                    string.IsNullOrWhiteSpace(item.CurrentHolder)),
                RecentTransactionCount = recentTransactions.Count,
                LocationInsights = BuildOpticalDiscLocationInsights(mediaItems),
                LifecycleInsights = BuildOpticalDiscLifecycleInsights(mediaItems),
                CirculationInsights = BuildOpticalDiscCirculationInsights(recentTransactions, transactionItems.Count),
                RiskInsights = BuildOpticalDiscRiskInsights(mediaItems)
            };
        }

        private static IReadOnlyList<string> BuildOpticalDiscLocationInsights(IReadOnlyList<OpticalDiscOverviewMediumSnapshot> mediaItems)
        {
            if (mediaItems.Count == 0)
            {
                return ["当前暂无数据光盘台账。光盘仅在电子立档写入数据后自动建档。"];
            }

            var allLocations = FormatOpticalDiscDistribution(
                mediaItems
                    .GroupBy(item => NormalizeOpticalDiscLocation(item.CurrentLocation))
                    .Select(group => new OpticalDiscDistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label, StringComparer.Ordinal)
                    .Take(5));

            var inStockSummary = FormatOpticalDiscDistribution(
                mediaItems
                    .Where(IsOpticalDiscInStockStatus)
                    .GroupBy(item => NormalizeOpticalDiscLocation(item.CurrentLocation))
                    .Select(group => new OpticalDiscDistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label, StringComparer.Ordinal)
                    .Take(3));

            var keeperSummary = FormatOpticalDiscDistribution(
                mediaItems
                    .Where(IsOpticalDiscOutboundStatus)
                    .GroupBy(item => NormalizeOpticalDiscKeeper(item))
                    .Select(group => new OpticalDiscDistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label, StringComparer.Ordinal)
                    .Take(4));

            return
            [
                $"当前位置分布前五：{allLocations}。",
                $"资料室在库位置：{inStockSummary}。",
                $"出库在外保管分布：{keeperSummary}。"
            ];
        }

        private static IReadOnlyList<string> BuildOpticalDiscLifecycleInsights(IReadOnlyList<OpticalDiscOverviewMediumSnapshot> mediaItems)
        {
            if (mediaItems.Count == 0)
            {
                return ["当前暂无数据光盘生命周期数据。"];
            }

            int archiveRegistered = mediaItems.Count(item =>
                string.Equals(item.RegistrationMethod, OpticalDiscMedium.RegistrationMethodArchive, StringComparison.Ordinal));
            int inStock = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusInStock);
            int outTemporary = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusOut);
            int damaged = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusDamaged);
            int lost = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusLost);
            int scrap = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusScrap);
            int destroyed = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusDestroyed);
            int needReturn = mediaItems.Count(item => item.NeedReturn);

            var discTypeSummary = FormatOpticalDiscDistribution(
                mediaItems
                    .GroupBy(item => string.IsNullOrWhiteSpace(item.DiscType) ? "(未登记类型)" : item.DiscType.Trim())
                    .Select(group => new OpticalDiscDistributionItem(group.Key, group.Count()))
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Label, StringComparer.Ordinal));

            return
            [
                $"登记来源：资料存档登记 {archiveRegistered} 张（系统不管理空白光盘，仅管理已写入数据的数据光盘）。",
                $"状态结构：在库(资料) {inStock} 张、出库(临时) {outTemporary} 张、在库(损坏) {damaged} 张、在库(盘失) {lost} 张、在库(拟销) {scrap} 张、出库(销毁) {destroyed} 张。",
                $"归还控制：需归还 {needReturn} 张。",
                $"光盘类型分布：{discTypeSummary}。"
            ];
        }

        private static IReadOnlyList<string> BuildOpticalDiscCirculationInsights(
            IReadOnlyList<OpticalDiscOverviewTransactionSnapshot> recentTransactions,
            int totalTransactionCount)
        {
            if (totalTransactionCount == 0)
            {
                return ["当前暂无流转流水。立档入库、资料出库/归还、迁档与销毁等业务会自动写入。"];
            }

            string recentSummary = recentTransactions.Count == 0
                ? "近90天暂无流转"
                : FormatOpticalDiscDistribution(
                    recentTransactions
                        .GroupBy(item => string.IsNullOrWhiteSpace(item.TransactionType) ? "(未登记类型)" : item.TransactionType.Trim())
                        .Select(group => new OpticalDiscDistributionItem(group.Key, group.Count()))
                        .OrderByDescending(item => item.Count)
                        .ThenBy(item => item.Label, StringComparer.Ordinal)
                        .Take(6),
                    suffix: "次");

            return
            [
                $"累计流转流水 {totalTransactionCount} 条；近90天 {recentTransactions.Count} 次。",
                $"近90天流转类型：{recentSummary}。",
                "光盘无独立借还审批菜单；流转随年度资料立档、出库、归还与迁档业务被动形成。"
            ];
        }

        private static IReadOnlyList<string> BuildOpticalDiscRiskInsights(IReadOnlyList<OpticalDiscOverviewMediumSnapshot> mediaItems)
        {
            int missingLocationCount = mediaItems.Count(item => string.IsNullOrWhiteSpace(item.CurrentLocation));
            int outboundWithoutKeeperCount = mediaItems.Count(item =>
                IsOpticalDiscOutboundStatus(item) &&
                string.IsNullOrWhiteSpace(item.CurrentHolder));
            int needReturnCount = mediaItems.Count(item => item.NeedReturn);
            int damagedCount = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusDamaged);
            int lostCount = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusLost);
            int scrapCount = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusScrap);
            int outTemporaryCount = mediaItems.Count(item => item.CurrentStatus == OpticalDiscMedium.StatusOut);

            if (mediaItems.Count == 0)
            {
                return ["暂无风险项：尚未形成数据光盘台账。"];
            }

            return
            [
                $"基础台账风险：未登记当前位置 {missingLocationCount} 张，出库但未明确保管人/接收单位 {outboundWithoutKeeperCount} 张。",
                $"在外与中间态风险：临时出库 {outTemporaryCount} 张（需归还 {needReturnCount} 张）；在库损坏 {damagedCount} 张、盘失 {lostCount} 张、拟销 {scrapCount} 张。"
            ];
        }

        private static string FormatOpticalDiscDistribution(IEnumerable<OpticalDiscDistributionItem> items, string suffix = "张")
        {
            var materialized = items.Where(item => item.Count > 0).ToList();
            if (materialized.Count == 0)
            {
                return "暂无";
            }

            return string.Join("、", materialized.Select(item => $"{item.Label} {item.Count}{suffix}"));
        }

        private static string NormalizeOpticalDiscLocation(string? location)
        {
            return string.IsNullOrWhiteSpace(location) ? "(未登记位置)" : location.Trim();
        }

        private static string NormalizeOpticalDiscKeeper(OpticalDiscOverviewMediumSnapshot item)
        {
            if (!string.IsNullOrWhiteSpace(item.CurrentHolder))
            {
                return item.CurrentHolder.Trim();
            }

            return NormalizeOpticalDiscLocation(item.CurrentLocation);
        }

        private static bool IsOpticalDiscOutboundStatus(OpticalDiscOverviewMediumSnapshot item)
        {
            return string.Equals(item.CurrentStatus, OpticalDiscMedium.StatusOut, StringComparison.Ordinal)
                || string.Equals(item.CurrentStatus, OpticalDiscMedium.StatusDestroyed, StringComparison.Ordinal);
        }

        private static bool IsOpticalDiscInStockStatus(OpticalDiscOverviewMediumSnapshot item)
        {
            return string.Equals(item.CurrentStatus, OpticalDiscMedium.StatusInStock, StringComparison.Ordinal)
                || string.Equals(item.CurrentStatus, OpticalDiscMedium.StatusDamaged, StringComparison.Ordinal)
                || string.Equals(item.CurrentStatus, OpticalDiscMedium.StatusLost, StringComparison.Ordinal)
                || string.Equals(item.CurrentStatus, OpticalDiscMedium.StatusScrap, StringComparison.Ordinal);
        }

        private sealed record OpticalDiscOverviewMediumSnapshot(
            string CurrentStatus,
            string CurrentLocation,
            string CurrentHolder,
            string Capacity,
            bool NeedReturn,
            string RegistrationMethod,
            string DiscType);

        private sealed record OpticalDiscOverviewTransactionSnapshot(string TransactionType, DateTime OperateTime);

        private sealed record OpticalDiscDistributionItem(string Label, int Count);
    }
}
