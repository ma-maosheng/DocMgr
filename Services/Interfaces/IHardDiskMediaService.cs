using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 硬盘介质模块服务。
    /// </summary>
    public interface IHardDiskMediaService
    {
        /// <summary>
        /// 获取模块概览统计。
        /// </summary>
        Task<HardDiskMediaOverview> GetOverviewAsync();

        /// <summary>
        /// 获取数据光盘模块概览统计。
        /// </summary>
        Task<OpticalDiscMediaOverview> GetOpticalDiscOverviewAsync();

        /// <summary>
        /// 查询介质台账。
        /// </summary>
        Task<IReadOnlyList<HardDiskMedium>> SearchMediaAsync(string? keyword, string? status, string? nature);

        /// <summary>
        /// 查询光盘台账。
        /// </summary>
        Task<IReadOnlyList<OpticalDiscMedium>> SearchOpticalDiscMediaAsync(string? keyword, string? status);

        /// <summary>
        /// 导出光盘台账。
        /// </summary>
        Task ExportOpticalDiscMediaLedgerAsync(string filePath);

        /// <summary>
        /// 查询光盘流转记录。
        /// </summary>
        Task<IReadOnlyList<OpticalDiscMediumTransactionRecord>> SearchOpticalDiscTransactionsAsync(string? keyword);

        /// <summary>
        /// 按光盘编号、业务单号、介质与流转类型查询光盘流转记录。
        /// </summary>
        Task<IReadOnlyList<OpticalDiscMediumTransactionRecord>> SearchOpticalDiscTransactionsAsync(
            string? discCodeKeyword,
            string? businessNoKeyword,
            int? mediumId = null,
            string? transactionType = null);

        /// <summary>
        /// 获取可用于申请的介质列表。
        /// </summary>
        Task<IReadOnlyList<HardDiskMedium>> GetSelectableMediaAsync();

        /// <summary>
        /// 查询资料立档可选取的在库空白硬盘。
        /// </summary>
        Task<IReadOnlyList<HardDiskMedium>> GetArchiveFilingCandidateBlankHardDisksAsync(string? keyword);

        /// <summary>
        /// 获取可用于归还登记的借出介质列表。
        /// </summary>
        Task<IReadOnlyList<HardDiskMediaReturnCandidate>> GetReturnRegistrationCandidatesAsync();

        /// <summary>
        /// 获取指定介质上尚未办结的归还登记单。
        /// </summary>
        Task<HardDiskMediaApplication?> GetActiveReturnRegistrationByMediumIdAsync(int mediumId);

        /// <summary>
        /// 按硬盘编号解析归还登记候选项（含已被年度资料登记占用锁占用的借出盘，供电子立档使用）。
        /// </summary>
        Task<HardDiskMediaReturnCandidate?> GetReturnRegistrationCandidateByDiskCodeAsync(string diskCode);

        /// <summary>
        /// 解析归还登记关联的来源借出单号（硬盘借出申请单号或资料出库单号）。
        /// </summary>
        Task<string> ResolveReturnSourceApplicationNoAsync(int? sourceApplicationId, int? sourceOutboundRecordId);

        /// <summary>
        /// 获取「年度资料登记」中资料室借出硬盘随资料归档归还场景下，指定申请人当前名下仍可用于新登记申请的借出硬盘介质编号列表（仅限临时/长期出库，排除已被占用锁占用的硬盘），按 DiskCode 返回。
        /// </summary>
        Task<IReadOnlyList<string>> GetCurrentUserBorrowedHardDiskCodesAsync(User? user);

        /// <summary>
        /// 获取归还登记可选归位位置。
        /// </summary>
        Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetReturnTargetLocationOptionsAsync(
            string applicationType,
            int mediumId,
            int? sourceApplicationId,
            int? sourceOutboundRecordId = null);

        /// <summary>
        /// 读取导入文件中的工作表名称。
        /// </summary>
        Task<IReadOnlyList<string>> GetImportSheetNamesAsync(string filePath);

        /// <summary>
        /// 判断当前是否已存在介质台账数据。
        /// </summary>
        Task<bool> HasMediaRecordsAsync();

        /// <summary>
        /// 导入介质台账数据。
        /// </summary>
        Task<HardDiskMediaImportResult> ImportMediaAsync(string filePath, string sheetName, ImportMode importMode, User? currentUser);

        /// <summary>
        /// 获取介质台账导入模板说明。
        /// </summary>
        string GetMediaImportTemplateDescription();

        /// <summary>
        /// 导出介质台账导入模板。
        /// </summary>
        Task ExportMediaImportTemplateAsync(string filePath);

        /// <summary>
        /// 保存介质台账。
        /// </summary>
        Task SaveMediumAsync(HardDiskMedium medium, User? currentUser);

        /// <summary>
        /// 生成下一个硬盘编号。
        /// </summary>
        Task<string> GenerateNextDiskCodeAsync();

        /// <summary>
        /// 删除介质台账。
        /// </summary>
        Task DeleteMediumAsync(int mediumId);

        /// <summary>
        /// 查询流转记录。
        /// </summary>
        Task<IReadOnlyList<HardDiskMediaTransaction>> SearchTransactionsAsync(string? keyword, string? transactionType);

        /// <summary>
        /// 查询业务申请。
        /// </summary>
        Task<IReadOnlyList<HardDiskMediaApplication>> SearchApplicationsAsync(string? keyword, int? status, string? applicationType);

        /// <summary>
        /// 保存业务申请。
        /// </summary>
        Task SaveApplicationAsync(HardDiskMediaApplication application, User? currentUser);

        /// <summary>
        /// 提交业务申请。
        /// </summary>
        Task SubmitApplicationAsync(int applicationId, User? currentUser);

        /// <summary>
        /// 生成下一个申请单编号。
        /// </summary>
        Task<string> GenerateNextApplicationNoAsync();

        /// <summary>
        /// 生成下一个归还登记单编号。
        /// </summary>
        Task<string> GenerateNextReturnRegistrationNoAsync();

        /// <summary>
        /// 获取申请单附件。
        /// </summary>
        Task<IReadOnlyList<SystemAttachment>> GetApplicationAttachmentsAsync(string applicationNo);

        /// <summary>
        /// 获取单个附件详情。
        /// </summary>
        Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

        /// <summary>
        /// 上传签字件。
        /// </summary>
        Task<HardDiskMediaAttachmentFlowResult> UploadSignedAttachmentAsync(HardDiskMediaApplication? application, User? currentUser, string fileName, string extension, long fileSize, byte[] fileContent);

        /// <summary>
        /// 删除申请附件。
        /// </summary>
        Task<HardDiskMediaAttachmentFlowResult> DeleteApplicationAttachmentAsync(SystemAttachment? attachment);

        /// <summary>
        /// 准备查看附件。
        /// </summary>
        Task<HardDiskMediaAttachmentFlowResult> PrepareApplicationAttachmentViewAsync(SystemAttachment? attachment);

        /// <summary>
        /// 上传非正常归还情况表扫描件。
        /// </summary>
        Task<HardDiskMediaAttachmentFlowResult> UploadAbnormalReturnReportAsync(
            HardDiskMediaApplication? application,
            User? currentUser,
            string fileName,
            string extension,
            long fileSize,
            byte[] fileContent);

        /// <summary>
        /// 删除非正常归还情况表扫描件。
        /// </summary>
        Task<HardDiskMediaAttachmentFlowResult> DeleteAbnormalReturnReportAsync(SystemAttachment? attachment);

        /// <summary>
        /// 是否已上传非正常归还情况表扫描件。
        /// </summary>
        Task<bool> HasUploadedAbnormalReturnReportAsync(int applicationId, string? applicationNo);

        /// <summary>
        /// 组装非正常归还情况表打印数据。
        /// </summary>
        Task<HardDiskMediaAbnormalReturnReportPrintData> BuildAbnormalReturnReportPrintDataAsync(
            HardDiskMediaApplication? application,
            bool blankReturnerSignature);

        /// <summary>
        /// 审批通过申请。
        /// </summary>
        Task<HardDiskMediaFlowResult> ApproveApplicationAsync(HardDiskMediaApplication? application, User? currentUser, HardDiskMediaApprovalInput? approvalInput);

        /// <summary>
        /// 确认实物交接（审批通过后、上传签批交接单前）。
        /// </summary>
        Task<HardDiskMediaFlowResult> ConfirmPhysicalHandoverAsync(HardDiskMediaApplication? application, User? currentUser, HardDiskMediaApprovalInput? handoverInput);

        /// <summary>
        /// 申请人撤回作废申请。
        /// </summary>
        Task<HardDiskMediaFlowResult> WithdrawApplicationAsync(HardDiskMediaApplication? application, User? currentUser, string? opinion);

        /// <summary>
        /// 资料室资料管理员强制撤回作废申请。
        /// </summary>
        Task<HardDiskMediaFlowResult> ForceWithdrawApplicationAsync(HardDiskMediaApplication? application, User? currentUser, string? opinion);

        /// <summary>
        /// 办结申请。
        /// </summary>
        Task<HardDiskMediaFlowResult> CompleteApplicationAsync(HardDiskMediaApplication? application, User? currentUser);

        /// <summary>
        /// 组装申请审批单打印数据。
        /// </summary>
        Task<HardDiskMediaPrintData> BuildPrintDataAsync(HardDiskMediaApplication? application);

        /// <summary>
        /// 记录申请单打印次数。
        /// </summary>
        Task MarkApplicationPrintedAsync(HardDiskMediaApplication? application);

        /// <summary>
        /// 将无存放位置的在库空白硬盘，按空白专用档口次序依次写入档口位置。
        /// </summary>
        Task<int> AssignBlankInStockMediaToBlankSlotsInOrderAsync();

        /// <summary>
        /// 获取指定专用类别的防磁磁盘柜档口候选位置。
        /// </summary>
        Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetDedicatedTargetLocationOptionsAsync(string categoryName);

        /// <summary>
        /// 按空白专用档口从小到大返回候选位置（含当前在库空盘数量）。
        /// </summary>
        Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetOrderedBlankDedicatedSlotLocationOptionsAsync(int slotCapacity = 10);

        /// <summary>
        /// 推荐第一个仍有容量的空白专用档口（按档口编号从小到大）。
        /// </summary>
        Task<string?> RecommendBlankDedicatedSlotLocationAsync(int slotCapacity = 10);

        /// <summary>
        /// 在指定专用档口类别中分配带档内序号的完整存放位置（如 壬A-1-2-01）。不用于空白专用档口。
        /// <paramref name="slotCapacity"/> 为 0 时按类别自动解析（硬盘 10、光盘 20）。
        /// </summary>
        Task<string?> AllocateNextDedicatedFullLocationAsync(
            string categoryName,
            int slotCapacity = 0,
            ISet<string>? reservedFullLocations = null);

        /// <summary>
        /// 将空白在库硬盘位置规范为档口键（如 壬A-1-2），不含档内序号。
        /// </summary>
        Task<string> ResolveBlankInStockSlotLocationAsync(string? requestedLocation);

        /// <summary>
        /// 将数据等在库硬盘位置规范为含档内序号的完整位置（如 壬A-1-2-01）。
        /// </summary>
        Task<string> ResolveDataInStockFullLocationAsync(string? requestedLocation);

        /// <summary>
        /// 获取指定位置当前在库硬盘数量。
        /// </summary>
        Task<int> GetInStockMediumCountAsync(string location);

        /// <summary>
        /// 获取指定字段的启用域值标签。
        /// </summary>
        Task<IReadOnlyList<string>> GetDomainOptionLabelsAsync(string entityName, string fieldName);
    }
}
