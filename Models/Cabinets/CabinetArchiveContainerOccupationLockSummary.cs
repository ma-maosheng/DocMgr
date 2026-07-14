namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档案盒 / 电子介质袋内容窗体的容器级占用说明。
    /// </summary>
    public sealed class CabinetArchiveContainerOccupationLockSummary
    {
        public static CabinetArchiveContainerOccupationLockSummary Empty { get; } = new();

        public bool HasAnyLock { get; init; }

        public string NoticeTitle { get; init; } = "占用说明";

        public string NoticeText { get; init; } = string.Empty;
    }
}
