namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 立档事实生命周期批量更新项。
    /// </summary>
    /// <param name="LifecycleRemark">
    /// 可选业务备注；为空时由仓储写入「{业务标签}：{操作人}」。
    /// </param>
    public readonly record struct FilingFactLifecycleUpdate(
        int FilingFactId,
        string LifecycleStatus,
        string BorrowHintLevel,
        string BorrowHintText,
        string? LifecycleRemark = null);
}
