namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质台账导入结果。
    /// </summary>
    public sealed record HardDiskMediaImportResult
    {
        /// <summary>
        /// 导入模式。
        /// </summary>
        public ImportMode Mode { get; init; }

        /// <summary>
        /// 成功导入数量。
        /// </summary>
        public int ImportedCount { get; init; }

        /// <summary>
        /// 覆盖导入前清理的记录数。
        /// </summary>
        public int ClearedCount { get; init; }

        /// <summary>
        /// 导入后按空白专用档口自动入位的硬盘数量。
        /// </summary>
        public int AssignedSlotCount { get; init; }
    }
}
