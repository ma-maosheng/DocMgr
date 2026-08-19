namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 业务编号生成器。
    /// </summary>
    public interface IBusinessNoGenerator
    {
        /// <summary>
        /// 根据业务编号类别生成下一编号。
        /// </summary>
        Task<string> GenerateNextNoAsync(
            BusinessNoCategory category,
            int? numberingYear = null,
            CancellationToken cancellationToken = default);
    }
}
