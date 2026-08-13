using System.Collections.Generic;
using System.Threading.Tasks;
using DocMgr.Models.NetworkTransfer;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 年度资料登记服务契约：登记单录入、审批、维护与打印。
    /// </summary>
    public interface IArchiveRegisterService
    {
        // 核心：保存或更新（无论是草稿还是修正反填，都走这个入口）
        Task SaveOrUpdateAsync(YearlyArchiveRegisterRecord record);

        Task SubmitApplicationAsync(YearlyArchiveRegisterRecord record);

        // 根据表单编号获取完整记录（包含子表 Items 和 Proofs）
        Task<YearlyArchiveRegisterRecord?> GetByFormNoAsync(string formNo);

        // 根据已知的ID获取记录
        Task<YearlyArchiveRegisterRecord?> GetByIdAsync(int id);

        // 根据申请人姓名获取列表（用于“我的申请”）
        Task<List<YearlyArchiveRegisterRecord>> GetMyRecordsAsync(string applicantName);

        // 创建新的登记草稿记录（含默认字段）
        YearlyArchiveRegisterRecord CreateDraftRecord(User? currentUser);

        // 创建新的登记草稿记录并分配下一个可用编号
        Task<YearlyArchiveRegisterRecord> CreateDraftRecordWithNextFormNoAsync(User? currentUser);

        // 获取资料登记页字段域值
        Task<ArchiveRegisterPageDomainOptions> GetPageDomainOptionsAsync();

        // 获取指定电子介质类型允许的处置方式
        IReadOnlyList<string> GetAllowedElectronicDispositions(string? mediaType, IReadOnlyCollection<string> allDispositionOptions);

        // 判断资料来源是否为外来
        bool IsExternalSourceType(string? sourceType);

        // 校验字段值是否属于允许域值
        bool IsAllowedDomainValue(string? value, IReadOnlyCollection<string> options);

        // 规范化密级值
        string NormalizeConfidentialLevel(string? value);

        // 流程编排：保存草稿（仅部门资料管理员或系统管理员）
        Task<ArchiveRegisterFlowResult> SaveDraftFlowAsync(
            YearlyArchiveRegisterRecord? record,
            IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries,
            User? operatorUser);

        // 流程编排：保存审批
        Task<ArchiveRegisterFlowResult> SaveApprovalFlowAsync(YearlyArchiveRegisterRecord? record, IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries, IReadOnlyCollection<SystemAttachment> attachments, User? currentUser);

        // 流程编排：确认实物交接（审批通过后，上传签批交接单前）
        Task<ArchiveRegisterFlowResult> ConfirmPhysicalHandoverFlowAsync(YearlyArchiveRegisterRecord? record, User? currentUser);

        // 流程编排：确认办结
        Task<ArchiveRegisterFlowResult> CompleteRegisterFlowAsync(YearlyArchiveRegisterRecord? record, IReadOnlyCollection<SystemAttachment> attachments, User? currentUser);

        // 流程编排：提交申请（仅部门资料管理员或系统管理员）
        Task<ArchiveRegisterFlowResult> SubmitApplicationFlowAsync(
            YearlyArchiveRegisterRecord? record,
            IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries,
            bool isExternalSource,
            User? operatorUser);

        /// <summary>
        /// 同步借出留存硬盘的台账状态与登记占用锁（与提交申请时一致；用于补全未走提交流程的数据）。
        /// </summary>
        Task SyncBorrowedHardDiskRegisterLocksAsync(YearlyArchiveRegisterRecord record);

        // 流程编排：撤销登记
        Task<ArchiveRegisterFlowResult> CancelRegisterFlowAsync(YearlyArchiveRegisterRecord? record, User? currentUser);

        // 流程编排：申请单强制清理
        Task<ArchiveRegisterFlowResult> ForceCleanupRegisterFlowAsync(YearlyArchiveRegisterRecord? record, User? currentUser);

        // 流程编排：上传附件
        Task<ArchiveRegisterAttachmentFlowResult> UploadAttachmentFlowAsync(
            YearlyArchiveRegisterRecord? record,
            User? currentUser,
            string attachmentKind,
            string fileName,
            string extension,
            long fileSize,
            byte[] fileContent);

        // 流程编排：删除附件
        Task<ArchiveRegisterAttachmentFlowResult> DeleteAttachmentFlowAsync(SystemAttachment? attachment);

        // 流程编排：查看附件前的数据准备
        Task<ArchiveRegisterAttachmentFlowResult> PrepareAttachmentViewFlowAsync(SystemAttachment? attachment);

        // 上传附件
        Task UploadAttachmentAsync(SystemAttachment attachment);

        // 获取某表单的所有附件
        Task<List<SystemAttachment>> GetAttachmentsByFormNoAsync(string formNo);

        // [新增] 获取单个附件详情 (包含文件内容)
        Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId);

        // 删除附件
        Task DeleteAttachmentAsync(int attachmentId);

        // 新增：自动生成下一个表单编号
        Task<string> GenerateNextFormNoAsync();

        // [新增] 管理员查询所有记录 (按年份)
        Task<List<YearlyArchiveRegisterRecord>> GetAllRecordsByYearAsync(int year);

        // [新增] 获取数据库中所有存在的年份 (用于筛选)
        Task<List<int>> GetExistingYearsAsync();

        // [新增] 综合检索
        Task<List<YearlyArchiveRegisterRecord>> SearchRecordsAsync(string keyword, int? year = null, int? status = null, int? projectId = null);

        // 校验审批保存所需业务规则（含域值与附件完整性）
        Task<ArchiveRegisterApprovalValidationResult> ValidateApprovalAsync(YearlyArchiveRegisterRecord record, IReadOnlyCollection<SystemAttachment> attachments);

        // 校验附件材料是否包含必须的“登记申请单”“资料照片”（有证明材料时另须“证明材料”）
        Task<ArchiveRegisterApprovalValidationResult> ValidateMandatoryAttachmentsAsync(
            YearlyArchiveRegisterRecord record,
            IReadOnlyCollection<SystemAttachment> attachments);

        // 填充审批默认信息（仅资料室资料管理员可执行）
        Task ApplyDefaultApprovalInfoAsync(YearlyArchiveRegisterRecord record, User currentUser);

        // 填充入网申请审批默认信息（仅资料室资料管理员可执行）
        Task ApplyDefaultInboundApprovalInfoAsync(NetworkInboundRecord record, User currentUser);

        // 填充出网申请审批默认信息（仅资料室资料管理员可执行）
        Task ApplyDefaultNetworkOutboundApprovalInfoAsync(NetworkOutboundRecord record, User currentUser);

        // 填充资料借出申请审批默认信息（仅资料室资料管理员可执行）
        Task ApplyDefaultOutboundApprovalInfoAsync(YearlyArchiveOutboundRecord record, User currentUser);

        // 校验提交申请所需业务规则（含域值与介质完整性）
        Task<ArchiveRegisterApplicationValidationResult> ValidateApplicationAsync(YearlyArchiveRegisterRecord record, IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries, bool isExternalSource);

        // 自动补填审批信息（用于加载记录/打印前的轻量补全）
        Task<bool> TryAutoFillApprovalForArchiveAdminAsync(YearlyArchiveRegisterRecord record, User currentUser);

        // 规范化打印所需的密级与审批结论字段
        ArchiveRegisterPrintNormalizationResult NormalizePrintFields(
            string? confidentialLevel,
            string? prodOpinion,
            string? rndOpinion,
            string? deputyOpinion);

        // 角色判定：资料室资料管理员/系统管理员（审批及后续办理）
        bool IsArchiveAdminUser(User? user);

        // 角色判定：部门资料管理员（不含资料室，仅可发起申请）
        bool IsDepartmentArchiveAdmin(User? user);

        // 角色判定：申请侧操作人（同 IsDepartmentArchiveAdmin）
        bool IsApplicantUser(User? user);

        // 角色判定：是否允许发起申请（部门资料管理员或系统管理员）
        bool CanSubmitApplication(User? user);

        // 计算登记页界面权限状态
        ArchiveRegisterUiPermissionState ResolveUiPermissionState(User? user, YearlyArchiveRegisterRecord? currentRecord);

        // 组装打印 DTO（审批页/申请页通用数据）
        ArchiveRegisterPrintData BuildPrintData(
            YearlyArchiveRegisterRecord record,
            string? selectedSourceType,
            IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries);

        /// <summary>
        /// 构建打印页光盘台账摘要信息。
        /// </summary>
        Task<string> BuildOpticalDiscLedgerSummaryAsync(YearlyArchiveRegisterRecord record);

        Task RemoveRegisterRecordAsync(int id);
    }
}