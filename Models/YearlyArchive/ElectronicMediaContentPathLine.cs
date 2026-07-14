namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质登记子项的子项名称与来源路径（一一对应）。
    /// </summary>
    public readonly record struct ElectronicMediaContentPathLine(string ItemName, string StoragePath);
}
