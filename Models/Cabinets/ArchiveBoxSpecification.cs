namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档案盒规格。
    /// </summary>
    public sealed class ArchiveBoxSpecification
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal WidthCm { get; set; }

        public decimal HeightCm { get; set; }

        public decimal ThicknessCm { get; set; }

        public int SortOrder { get; set; }
    }
}
