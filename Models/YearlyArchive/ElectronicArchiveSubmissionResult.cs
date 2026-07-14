namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质立档提交结果。
    /// </summary>
    /// <param name="ElectronicArchiveNo">电子介质袋编号。</param>
    /// <param name="MediaEntryCount">本次入袋的电子介质条目数量。</param>
    /// <param name="IsAppendMode">是否为并入既有电子介质袋。</param>
    public sealed record ElectronicArchiveSubmissionResult(
        string ElectronicArchiveNo,
        int MediaEntryCount,
        bool IsAppendMode,
        ElectronicArchiveDatabaseChangeReport? DatabaseChanges = null);
}
