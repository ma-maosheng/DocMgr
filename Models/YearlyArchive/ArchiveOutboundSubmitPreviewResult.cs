namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 借出申请提交前校验与拟执行逻辑预览结果。
    /// </summary>
    public sealed record ArchiveOutboundSubmitPreviewResult(
        IReadOnlyList<string> Errors,
        string ExecutionSummary,
        string LongTermSimulatedStockDepletionReminder = "")
    {
        public bool IsValid => Errors.Count == 0;

        public string ErrorMessage => string.Join(Environment.NewLine, Errors);

        public bool HasLongTermSimulatedStockDepletionReminder =>
            !string.IsNullOrWhiteSpace(LongTermSimulatedStockDepletionReminder);
    }
}
