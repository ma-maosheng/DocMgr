namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 办结后因灭失/不还释放档口占位的空档案盒提示信息。
    /// </summary>
    public sealed record EmptiedArchiveBoxHint(
        int BoxId,
        string ArchiveSequenceNo,
        string LastStorageLocation);
}
