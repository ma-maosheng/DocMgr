namespace DocMgr.ViewModels.Cabinets
{
    /// <summary>
    /// 标准滑道式档案柜档口用途编辑结果。
    /// </summary>
    public sealed class CabinetArchiveSlotCategoryEditResult
    {
        public CabinetArchiveSlotCategoryEditResult(string? categoryName)
        {
            CategoryName = categoryName;
        }

        public string? CategoryName { get; }
    }
}
