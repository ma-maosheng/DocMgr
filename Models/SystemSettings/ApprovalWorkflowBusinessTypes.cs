namespace DocMgr.Models.SystemSettings
{
    /// <summary>可配置审核审批的业务类型编码。</summary>
    public static class ApprovalWorkflowBusinessTypes
    {
        public const string YearlyArchiveRegister = "YearlyArchiveRegister";
        public const string YearlyArchiveOutbound = "YearlyArchiveOutbound";
        public const string YearlyArchiveReturn = "YearlyArchiveReturn";
        public const string NetworkInbound = "NetworkInbound";
        public const string NetworkOutbound = "NetworkOutbound";
        public const string YearlyArchiveDisposal = "YearlyArchiveDisposal";
        public const string HistoryArchiveDisposal = "HistoryArchiveDisposal";
        public const string NetworkOnNetDisposal = "NetworkOnNetDisposal";
        public const string HardDiskOutbound = "HardDiskOutbound";
        public const string HardDiskReturn = "HardDiskReturn";
        public const string HardDiskDisposal = "HardDiskDisposal";

        public static IReadOnlyList<string> All { get; } =
        [
            YearlyArchiveRegister,
            YearlyArchiveOutbound,
            YearlyArchiveReturn,
            NetworkInbound,
            NetworkOutbound,
            YearlyArchiveDisposal,
            HistoryArchiveDisposal,
            NetworkOnNetDisposal,
            HardDiskOutbound,
            HardDiskReturn,
            HardDiskDisposal
        ];

        /// <summary>
        /// 业务类型显示名：与左侧导航完整路径「顶级·[二级·]叶子」对齐。
        /// </summary>
        public static string ToDisplay(string? businessType) => businessType?.Trim() switch
        {
            YearlyArchiveRegister => "年度资料档案化管理·资料建档·建档申请",
            YearlyArchiveOutbound => "年度资料档案化管理·资料流转·借出申请",
            YearlyArchiveReturn => "年度资料档案化管理·资料流转·归还申请",
            YearlyArchiveDisposal => "年度资料档案化管理·离库处置·模拟/电子资料离库处置",
            NetworkInbound => "年度资料出入网管理·入网申请",
            NetworkOutbound => "年度资料出入网管理·出网申请",
            NetworkOnNetDisposal => "年度资料出入网管理·在网数据处置",
            HistoryArchiveDisposal => "历史存档资料管理·资料离库处置",
            HardDiskOutbound => "介质管理·硬盘·出库申请",
            HardDiskReturn => "介质管理·硬盘·归还申请",
            HardDiskDisposal => "介质管理·硬盘·离库处置",
            _ => businessType?.Trim() ?? string.Empty
        };
    }
}
