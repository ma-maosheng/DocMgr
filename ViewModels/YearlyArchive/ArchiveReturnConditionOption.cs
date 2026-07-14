namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 归还物状态下拉项（值 + 中文显示）。
    /// </summary>
    public sealed class ArchiveReturnConditionOption
    {
        public ArchiveReturnConditionOption(string value, string display)
        {
            Value = value;
            Display = display;
        }

        public string Value { get; }

        public string Display { get; }
    }
}
