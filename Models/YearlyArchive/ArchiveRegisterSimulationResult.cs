namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 模拟登记生成结果。
    /// </summary>
    /// <param name="GeneratedCount">生成数量。</param>
    /// <param name="FormNos">生成的申请单编号列表。</param>
    /// <param name="ChecklistLines">生成/提交过程清单（复杂电子场景用于核对与操作台一致性）。</param>
    public sealed record ArchiveRegisterSimulationResult(
        int GeneratedCount,
        IReadOnlyList<string> FormNos,
        IReadOnlyList<string> ChecklistLines);

    /// <summary>
    /// 自动化立档测试结果。
    /// </summary>
    /// <param name="ProcessedCount">已处理登记单数量。</param>
    /// <param name="SucceededCount">成功数量。</param>
    /// <param name="FailedCount">失败数量。</param>
    /// <param name="ChecklistLines">测试清单说明行。</param>
    public sealed record ArchiveFilingAutomationResult(
        int ProcessedCount,
        int SucceededCount,
        int FailedCount,
        IReadOnlyList<string> ChecklistLines);
}
