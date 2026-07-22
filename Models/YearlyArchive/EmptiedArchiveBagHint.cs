namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质袋因资料不还/灭失变为空袋后释放档口的提示信息。
    /// </summary>
    public sealed record EmptiedArchiveBagHint(
        int UnitId,
        string ElectronicArchiveNo,
        string LastStorageLocation);
}
