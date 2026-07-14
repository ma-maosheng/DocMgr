namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 模拟介质资料子项在库份数快照：立档份数、已办结未归还提档份数及展示文案。
    /// </summary>
    public sealed class SimulatedInArchiveCopyCountInfo
    {
        public int FiledCopyCount { get; init; }

        public int WithdrawnCopyCount { get; init; }

        public int CurrentInArchiveCopyCount { get; init; }

        public string Display { get; init; } = string.Empty;
    }
}
