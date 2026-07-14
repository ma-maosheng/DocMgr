namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 其他在途出库单对拟提档资料的 Active 预订快照（供提交时冲突校验）。
    /// </summary>
    public sealed class ActiveWithdrawalReservationSnapshot
    {
        public int FilingFactId { get; init; }

        public int OutboundRecordId { get; init; }

        public string OutboundNo { get; init; } = string.Empty;

        public int ReservedCopyCount { get; init; }
    }
}
