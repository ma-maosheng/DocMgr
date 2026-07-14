namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档案柜档口规格。
    /// </summary>
    public sealed class CabinetSlotSpecification
    {
        public int Id { get; set; }

        public string CabinetTypeCode { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public decimal WidthCm { get; set; }

        public decimal HeightCm { get; set; }

        public decimal DepthCm { get; set; }

        public int SortOrder { get; set; }
    }
}
