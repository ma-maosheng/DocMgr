namespace DocMgr.ViewModels.Cabinets
{
    /// <summary>
    /// 档口专用用途编辑结果；<see cref="CategoryName"/> 为空表示清除专用用途。
    /// </summary>
    public sealed class CabinetHardDiskSlotCategoryEditResult
    {
        public CabinetHardDiskSlotCategoryEditResult(string? categoryName)
        {
            CategoryName = categoryName?.Trim() ?? string.Empty;
        }

        public string CategoryName { get; }
    }
}
