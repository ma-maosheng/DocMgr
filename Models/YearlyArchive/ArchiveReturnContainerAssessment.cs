namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 归还明细容器状态评估结果（借出快照 vs 办结当下活数据）。
    /// </summary>
    public sealed class ArchiveReturnContainerAssessment
    {
        public const string StatusOk = "Ok";
        public const string StatusLocationChanged = "LocationChanged";
        public const string StatusBoxInvalid = "BoxInvalid";

        public string BorrowedContainerCode { get; init; } = string.Empty;

        public string BorrowedStorageLocation { get; init; } = string.Empty;

        public string CurrentContainerCode { get; init; } = string.Empty;

        public string CurrentStorageLocation { get; init; } = string.Empty;

        public string StatusKind { get; init; } = StatusOk;

        public string StatusDisplay { get; init; } = "正常";

        public string WarningText { get; init; } = string.Empty;

        /// <summary>当前仍有在用档案盒，可按活数据自动归位。</summary>
        public bool CanAutoRestore { get; init; }

        public int? LiveBoxId { get; init; }

        /// <summary>盒已失效且未指定归还目标盒时，阻断登记/办结。</summary>
        public bool BlocksWithoutRehome { get; init; }

        public static ArchiveReturnContainerAssessment Ok(
            string borrowedCode,
            string borrowedLocation,
            string currentCode,
            string currentLocation,
            int? liveBoxId) =>
            new()
            {
                BorrowedContainerCode = borrowedCode,
                BorrowedStorageLocation = borrowedLocation,
                CurrentContainerCode = currentCode,
                CurrentStorageLocation = currentLocation,
                StatusKind = StatusOk,
                StatusDisplay = "正常",
                CanAutoRestore = liveBoxId.HasValue && liveBoxId.Value > 0,
                LiveBoxId = liveBoxId
            };

        public static ArchiveReturnContainerAssessment LocationChanged(
            string borrowedCode,
            string borrowedLocation,
            string currentCode,
            string currentLocation,
            int liveBoxId) =>
            new()
            {
                BorrowedContainerCode = borrowedCode,
                BorrowedStorageLocation = borrowedLocation,
                CurrentContainerCode = currentCode,
                CurrentStorageLocation = currentLocation,
                StatusKind = StatusLocationChanged,
                StatusDisplay = "盒位已变",
                WarningText = $"借出后档案盒已迁至 {currentLocation}（盒号 {currentCode}），办结将归入当前位置。",
                CanAutoRestore = true,
                LiveBoxId = liveBoxId
            };

        public static ArchiveReturnContainerAssessment BoxInvalid(
            string borrowedCode,
            string borrowedLocation,
            string reason) =>
            new()
            {
                BorrowedContainerCode = borrowedCode,
                BorrowedStorageLocation = borrowedLocation,
                CurrentContainerCode = string.Empty,
                CurrentStorageLocation = string.Empty,
                StatusKind = StatusBoxInvalid,
                StatusDisplay = "盒已失效",
                WarningText = reason,
                CanAutoRestore = false,
                LiveBoxId = null,
                BlocksWithoutRehome = true
            };
    }
}
