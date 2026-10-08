namespace DocMgr.Models.SystemSettings
{
    /// <summary>可配置审核审批的业务类型编码。</summary>
    public static class ApprovalWorkflowBusinessTypes
    {
        // 常量声明顺序与主窗口左侧导航一致（供阅读对照；展示顺序以 Catalog / All 为准）。
        public const string HistoryArchiveDisposal = "HistoryArchiveDisposal";
        public const string YearlyArchiveRegister = "YearlyArchiveRegister";
        public const string YearlyArchiveOutbound = "YearlyArchiveOutbound";
        public const string YearlyArchiveReturn = "YearlyArchiveReturn";
        public const string YearlyArchiveInventoryRegister = "YearlyArchiveInventoryRegister";
        public const string YearlyArchiveDisposal = "YearlyArchiveDisposal";
        public const string NetworkInbound = "NetworkInbound";
        public const string NetworkOutbound = "NetworkOutbound";
        public const string NetworkOnNetDisposal = "NetworkOnNetDisposal";
        public const string HardDiskOutbound = "HardDiskOutbound";
        public const string HardDiskReturn = "HardDiskReturn";
        public const string HardDiskInventoryRegister = "HardDiskInventoryRegister";
        public const string HardDiskDisposal = "HardDiskDisposal";

        /// <summary>全部业务类型编码（顺序与主窗口左侧导航 / SS-APPR 目录一致）。</summary>
        public static IReadOnlyList<string> All { get; } =
        [
            HistoryArchiveDisposal,
            YearlyArchiveRegister,
            YearlyArchiveOutbound,
            YearlyArchiveReturn,
            YearlyArchiveInventoryRegister,
            YearlyArchiveDisposal,
            NetworkInbound,
            NetworkOutbound,
            NetworkOnNetDisposal,
            HardDiskOutbound,
            HardDiskReturn,
            HardDiskInventoryRegister,
            HardDiskDisposal
        ];

        /// <summary>
        /// 业务类型显示名：与左侧导航完整路径「顶级·[二级·]叶子」对齐。
        /// </summary>
        public static string ToDisplay(string? businessType) => businessType?.Trim() switch
        {
            HistoryArchiveDisposal => "历史存档资料管理·资料离库处置",
            YearlyArchiveRegister => "年度资料档案化管理·资料建档·建档申请",
            YearlyArchiveOutbound => "年度资料档案化管理·资料流转·借出申请",
            YearlyArchiveReturn => "年度资料档案化管理·资料流转·归还申请",
            YearlyArchiveInventoryRegister => "年度资料档案化管理·盘库登记·模拟/电子资料盘库",
            YearlyArchiveDisposal => "年度资料档案化管理·离库处置·模拟/电子资料离库处置",
            NetworkInbound => "年度资料出入网管理·入网申请",
            NetworkOutbound => "年度资料出入网管理·出网申请",
            NetworkOnNetDisposal => "年度资料出入网管理·在网数据处置",
            HardDiskOutbound => "介质管理·硬盘·出库申请",
            HardDiskReturn => "介质管理·硬盘·归还申请",
            HardDiskInventoryRegister => "介质管理·硬盘·盘库登记",
            HardDiskDisposal => "介质管理·硬盘·离库处置",
            _ => businessType?.Trim() ?? string.Empty
        };
    }
}
