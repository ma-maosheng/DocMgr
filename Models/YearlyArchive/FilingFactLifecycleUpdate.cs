namespace DocMgr.Models.YearlyArchive
{
    public readonly record struct FilingFactLifecycleUpdate(
        int FilingFactId,
        string LifecycleStatus,
        string BorrowHintLevel,
        string BorrowHintText);
}
