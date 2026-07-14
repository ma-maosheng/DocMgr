using DocMgr.Models.YearlyArchive;



namespace DocMgr.Services.YearlyArchive

{

    /// <summary>

    /// 长期存档模拟介质「提档」借出且办结后库内份数归零的识别与文案。

    /// </summary>

    public static class ArchiveSimulatedLongTermWithdrawalDepletionSupport

    {

        public const string PrintItemMarker = "【重点关注·库内归零·长期存档提档】";



        private const string PrintReviewNoticeText =

            "本单含长期存档模拟介质以提档方式借出、办结后资料子项库内份数将归零的项目（明细已标注），请审核审批人重点关注，督促申请人妥善保管与及时处置。";



        /// <summary>

        /// 是否长期存档模拟介质提档明细（参与库内归零判定）。

        /// </summary>

        public static bool IsTargetItem(YearlyArchiveOutboundItem item) =>

            ArchiveSimulatedMediaItemStockSupport.IsSimulatedWithdrawalStockItem(item)

            && ArchiveOutboundDomainValues.IsLongTermArchivePurpose(item.ArchivePurpose);



        /// <summary>

        /// 拟领用份数是否将耗尽当前库内可用份数。

        /// </summary>

        public static bool WillDepleteAvailableStock(int availableCopyCount, int requestedCopyCount) =>

            availableCopyCount > 0 && requestedCopyCount >= availableCopyCount;



        public static string BuildPrintReviewNoticeText() => PrintReviewNoticeText;



        public static string BuildApplicantReminderText(IReadOnlyList<SimulatedLongTermStockDepletionWarning> warnings)

        {

            ArgumentNullException.ThrowIfNull(warnings);



            if (warnings.Count == 0)

            {

                return string.Empty;

            }



            var lines = new List<string>

            {

                "以下资料为「院管资料、长期存档」模拟介质，拟采用提档方式借出；申请办结并办理实物出库后，对应资料子项库内份数将归零：",

                string.Empty,

            };



            foreach (var warning in warnings)

            {

                lines.Add($"• [{warning.ItemLabel}] 当前库内 {warning.AvailableCopyCount} 份，拟提 {warning.RequestedCopyCount} 份");

            }



            lines.Add(string.Empty);

            lines.Add("请妥善保管借出资料，并在不再需要时按院有关规定及时处置（如归还资料室、按规定移交或销毁等）。");

            lines.Add(string.Empty);

            lines.Add("请确认已知悉上述情况后，再继续提交申请。");



            return string.Join(Environment.NewLine, lines);

        }



        public static int ResolveOutboundCopyCount(YearlyArchiveOutboundItem item) =>

            Math.Max(1, item.CopyCount ?? 1);

    }



    /// <summary>

    /// 长期存档模拟介质提档后库内归零提醒项。

    /// </summary>

    public sealed record SimulatedLongTermStockDepletionWarning(

        int FilingFactId,

        string ItemLabel,

        int AvailableCopyCount,

        int RequestedCopyCount);

}


