namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档口特例规则配置。
    /// </summary>
    public sealed class CabinetSlotSpecialRule
    {
        public int Id { get; set; }

        public string RuleKey { get; set; } = string.Empty;

        public string CabinetName { get; set; } = string.Empty;

        public string OpenFaceCode { get; set; } = string.Empty;

        public string SlotCode { get; set; } = string.Empty;

        public string RequiredBoxSpecification { get; set; } = string.Empty;

        public string RequiredArchiveFaceCode { get; set; } = string.Empty;

        public string LayoutModeOverride { get; set; } = string.Empty;

        public string SpecialRuleText { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }

        public int SortOrder { get; set; }
    }
}
