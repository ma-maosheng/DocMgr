namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 登记/立档仿真服务契约：生成用于测试与演练的登记、立档模拟数据。
    /// </summary>
    public interface IArchiveRegisterSimulationService
    {
        /// <summary>
        /// 生成 5 个真实硬盘借出业务，使申请人持有真实借出硬盘。
        /// </summary>
        /// <param name="operatorUser">当前执行操作的用户。</param>
        Task<ArchiveRegisterSimulationResult> GenerateFiveHardDiskBorrowBusinessesAsync(User? operatorUser);

        /// <summary>
        /// 生成一批可直接进入资料立档阶段的模拟登记申请单。
        /// </summary>
        /// <param name="operatorUser">当前执行操作的用户。</param>
        Task<ArchiveRegisterSimulationResult> GenerateApprovedReceivedSamplesAsync(User? operatorUser);

        /// <summary>
        /// 生成一批复杂电子介质模拟登记申请单。
        /// </summary>
        /// <param name="operatorUser">当前执行操作的用户。</param>
        Task<ArchiveRegisterSimulationResult> GenerateComplexElectronicSamplesAsync(User? operatorUser);

        /// <summary>
        /// 对模拟登记数据执行自动化立档测试并返回测试清单说明。
        /// </summary>
        /// <param name="operatorUser">当前执行操作的用户。</param>
        Task<ArchiveFilingAutomationResult> RunAutomatedFilingTestAsync(User? operatorUser);

        /// <summary>
        /// 将所有已提交申请单批量自动审批为已办结状态。
        /// </summary>
        /// <param name="operatorUser">当前执行操作的用户。</param>
        Task<ArchiveRegisterSimulationResult> AutoApproveSubmittedApplicationsAsync(User? operatorUser);

        /// <summary>
        /// 清理由模拟登记功能生成的申请单数据。
        /// </summary>
        /// <param name="operatorUser">当前执行操作的用户。</param>
        Task<ArchiveRegisterSimulationResult> ClearGeneratedSamplesAsync(User? operatorUser);
    }
}
