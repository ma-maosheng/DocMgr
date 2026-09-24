using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.SystemSettings;

namespace DocMgr.Services.YearlyArchive;

/// <summary>
/// 年度资料盘库登记：B 流审批、签字、打印与附件。
/// </summary>
public sealed partial class ArchiveInventoryRegisterService
{
    public async Task ApproveAsync(int recordId, string approvalOpinion, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        var gate = OfflineApprovalLifecycleSupport.TryTransition(
            new OfflineApprovalLifecycleSupport.GateContext(
                existing.Status,
                existing.SignedAttachmentUploaded,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.ApprovePass);
        if (!gate.Allowed)
        {
            throw new InvalidOperationException(gate.DenyMessage ?? "当前状态不允许审批。");
        }

        DateTime now = DateTime.Now;
        var users = _userService.GetAllUsers();
        var chain = await _approvalWorkflowService.ResolveAsync(
            new ApprovalChainResolveRequest
            {
                BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveInventoryRegister,
                ApplicantDept = existing.ApplicantDept,
                FieldValues = ApprovalChainApplySupport.BuildYearlyArchiveInventoryRegisterFieldValues(existing)
            },
            users);
        ApprovalChainApplySupport.ApplyToYearlyArchiveInventoryRegister(existing, chain, now);

        var missing = ApprovalChainApplySupport.CollectMissingSignerErrors(
            chain,
            nodeKey => ApprovalChainApplySupport.ReadYearlyArchiveInventoryRegisterSigner(existing, nodeKey));
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, missing)
                + Environment.NewLine
                + "请在「审核审批」中配置或在用户管理中维护对应角色后再审批通过。");
        }

        existing.Status = gate.NextStatus ?? YearlyArchiveInventoryRegisterRecord.StatusApproved;
        existing.ApprovedBy = ResolveUserDisplayName(currentUser);
        existing.ApprovedTime = now;
        existing.ApprovalOpinion = string.IsNullOrWhiteSpace(approvalOpinion) ? "同意" : approvalOpinion.Trim();
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateReviewSignersAsync(
        int recordId,
        string? deptHead,
        DateTime? deptHeadDate,
        string? archiveRoomHead,
        DateTime? archiveRoomHeadDate,
        string? productionHead,
        DateTime? productionHeadDate,
        string? archiveDeputyPresident,
        DateTime? archiveDeputyPresidentDate,
        string? productionVicePresident,
        DateTime? productionVicePresidentDate,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        if (existing.Status is not (YearlyArchiveInventoryRegisterRecord.StatusApproved
            or YearlyArchiveInventoryRegisterRecord.StatusSignedUploaded))
        {
            throw new InvalidOperationException("仅已审批或已确认可上传状态可修改审核审批人。");
        }

        var minDate = ApprovalSignatureDateSupport.ResolveMinDate(
            existing.FirstPrintedAt,
            existing.LastPrintedAt,
            existing.PrintCount);
        ThrowIfSignatureDateInvalid(deptHeadDate, minDate, "部门审核日期");
        ThrowIfSignatureDateInvalid(archiveRoomHeadDate, minDate, "资料室签字日期");
        ThrowIfSignatureDateInvalid(productionHeadDate, minDate, "生产科签字日期");
        ThrowIfSignatureDateInvalid(archiveDeputyPresidentDate, minDate, "分管资料院长签字日期");
        ThrowIfSignatureDateInvalid(productionVicePresidentDate, minDate, "分管生产院长签字日期");

        existing.DeptHead = deptHead?.Trim() ?? string.Empty;
        existing.DeptHeadDate = ApprovalSignatureDateSupport.Clamp(deptHeadDate, minDate);
        existing.ArchiveRoomHead = archiveRoomHead?.Trim() ?? string.Empty;
        existing.ArchiveRoomHeadDate = ApprovalSignatureDateSupport.Clamp(archiveRoomHeadDate, minDate);
        existing.ProductionHead = productionHead?.Trim() ?? string.Empty;
        existing.ProductionHeadDate = ApprovalSignatureDateSupport.Clamp(productionHeadDate, minDate);
        existing.ArchiveDeputyPresident = archiveDeputyPresident?.Trim() ?? string.Empty;
        existing.ArchiveDeputyPresidentDate = ApprovalSignatureDateSupport.Clamp(archiveDeputyPresidentDate, minDate);
        existing.ProductionVicePresident = productionVicePresident?.Trim() ?? string.Empty;
        existing.ProductionVicePresidentDate = ApprovalSignatureDateSupport.Clamp(productionVicePresidentDate, minDate);
        existing.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
    }

    public async Task ConfirmReadyForUploadAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        var gate = OfflineApprovalLifecycleSupport.TryTransition(
            new OfflineApprovalLifecycleSupport.GateContext(
                existing.Status,
                existing.SignedAttachmentUploaded,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.ConfirmMidStep);
        if (!gate.Allowed)
        {
            throw new InvalidOperationException(gate.DenyMessage ?? "当前状态不允许确认可上传。");
        }

        DateTime now = DateTime.Now;
        existing.Status = gate.NextStatus ?? YearlyArchiveInventoryRegisterRecord.StatusSignedUploaded;
        existing.ConfirmedBy = ResolveUserDisplayName(currentUser);
        existing.ConfirmedTime = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task RecordPrintAsync(int recordId)
    {
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        if (existing.Status is YearlyArchiveInventoryRegisterRecord.StatusDraft
            or YearlyArchiveInventoryRegisterRecord.StatusWithdrawn
            or YearlyArchiveInventoryRegisterRecord.StatusForceWithdrawn)
        {
            throw new InvalidOperationException("当前状态不可打印签批单。");
        }

        DateTime now = DateTime.Now;
        int printCount = existing.PrintCount;
        DateTime? firstPrintedAt = existing.FirstPrintedAt;
        DateTime? lastPrintedAt = existing.LastPrintedAt;
        ApprovalSignatureDateSupport.RecordPrint(ref printCount, ref firstPrintedAt, ref lastPrintedAt, now);
        existing.PrintCount = printCount;
        existing.FirstPrintedAt = firstPrintedAt;
        existing.LastPrintedAt = lastPrintedAt;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task<ArchiveInventoryRegisterPrintData> BuildPrintDataAsync(int recordId)
    {
        var record = await _repository.GetRecordByIdAsync(recordId)
            ?? throw new InvalidOperationException("未找到盘库登记单。");

        var orderedItems = record.Items.OrderBy(item => item.SortOrder).ToList();
        var chain = await _approvalWorkflowService.ResolveAsync(
            new ApprovalChainResolveRequest
            {
                BusinessType = ApprovalWorkflowBusinessTypes.YearlyArchiveInventoryRegister,
                ApplicantDept = record.ApplicantDept,
                FieldValues = ApprovalChainApplySupport.BuildYearlyArchiveInventoryRegisterFieldValues(record)
            },
            _userService.GetAllUsers());

        return new ArchiveInventoryRegisterPrintData
        {
            RegisterNo = record.RegisterNo,
            MediaKind = record.MediaKind,
            RegisterKind = record.RegisterKind,
            ApplyDateText = record.ApplyTime.ToString("yyyy-MM-dd"),
            Reason = record.Reason,
            Remark = record.Remark,
            ApplicantName = record.ApplicantName,
            ApplicantDept = record.ApplicantDept,
            ApprovedBy = record.ApprovedBy,
            ApprovedDateText = record.ApprovedTime?.ToString("yyyy-MM-dd") ?? string.Empty,
            ApprovalOpinion = record.ApprovalOpinion,
            CompletedBy = record.CompletedBy,
            CompletedDateText = record.CompletedAt?.ToString("yyyy-MM-dd") ?? string.Empty,
            IsCompleted = record.IsCompleted,
            DeptHead = record.DeptHead,
            DeptHeadDateText = record.DeptHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ArchiveRoomHead = record.ArchiveRoomHead,
            ArchiveRoomHeadDateText = record.ArchiveRoomHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ProductionHead = record.ProductionHead,
            ProductionHeadDateText = record.ProductionHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ArchiveDeputyPresident = record.ArchiveDeputyPresident,
            ArchiveDeputyPresidentDateText = record.ArchiveDeputyPresidentDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ProductionVicePresident = record.ProductionVicePresident,
            ProductionVicePresidentDateText = record.ProductionVicePresidentDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            EnableDeptHead = chain.DeptHead.IsEnabled,
            EnableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled,
            EnableProductionHead = chain.ProductionHead.IsEnabled,
            EnableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled,
            EnableProductionVicePresident = chain.ProductionVicePresident.IsEnabled,
            PrintCount = record.PrintCount,
            Items = orderedItems
                .Select(item => new ArchiveInventoryRegisterPrintItemData
                {
                    SortOrder = item.SortOrder,
                    MaterialName = item.MaterialName,
                    ItemName = item.ItemName,
                    ContainerCode = item.ContainerCode,
                    MediumKind = item.MediumKind,
                    MediumCode = ArchiveInventoryRegisterDomainValues.ResolveMediumCodeDisplay(
                        item.MediumKind,
                        item.MediumCode),
                    LostCopyCount = item.LostCopyCount,
                    BeforeStorageLocation = item.BeforeStorageLocation
                })
                .ToList()
        };
    }

    public async Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(string registerNo)
    {
        return await _repository.GetAttachmentsAsync(registerNo);
    }

    public Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId)
    {
        return _repository.GetAttachmentByIdAsync(attachmentId);
    }

    public async Task<(bool Ok, string Message, SystemAttachment? Attachment)> UploadAttachmentAsync(
        int recordId,
        string fileCategory,
        string fileName,
        string extension,
        long fileSize,
        byte[] fileContent,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);

        if (string.IsNullOrWhiteSpace(fileName) || fileContent == null || fileContent.Length == 0)
        {
            return (false, "附件内容为空，无法上传。", null);
        }

        string? formatError = SystemAttachmentUploadSupport.ValidateUploadFormat(fileName, extension, fileContent);
        if (!string.IsNullOrWhiteSpace(formatError))
        {
            return (false, formatError, null);
        }

        string category = fileCategory?.Trim() ?? string.Empty;
        if (!ArchiveInventoryRegisterDomainValues.AttachmentCategoryOptions.Contains(category, StringComparer.Ordinal))
        {
            return (false, "附件分类无效。", null);
        }

        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId);
        if (existing == null)
        {
            return (false, "未找到盘库登记单。", null);
        }

        if (existing.Status is YearlyArchiveInventoryRegisterRecord.StatusDraft
            or YearlyArchiveInventoryRegisterRecord.StatusSubmitted
            or YearlyArchiveInventoryRegisterRecord.StatusWithdrawn
            or YearlyArchiveInventoryRegisterRecord.StatusForceWithdrawn)
        {
            return (false, "当前状态不允许上传附件（请在审批通过并确认可上传后操作）。", null);
        }

        bool isOther = string.Equals(
            category,
            ArchiveInventoryRegisterDomainValues.AttachmentCategoryOther,
            StringComparison.Ordinal);
        var attachGate = OfflineApprovalLifecycleSupport.EvaluateAttachmentUpload(
            new OfflineApprovalLifecycleSupport.GateContext(
                existing.Status,
                existing.SignedAttachmentUploaded,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            isOtherCategory: isOther,
            isArchiveAdmin: true,
            allowOtherWhileApproved: true);
        if (!attachGate.Allowed)
        {
            return (false, attachGate.DenyMessage ?? "当前状态不允许上传附件。", null);
        }

        DateTime now = DateTime.Now;
        var attachment = new SystemAttachment
        {
            BusinessType = ArchiveInventoryRegisterDomainValues.AttachmentBusinessType,
            BusinessNo = existing.RegisterNo,
            BusinessId = existing.Id,
            FileName = fileName,
            Extension = extension ?? string.Empty,
            FileSize = fileSize,
            FileContent = fileContent,
            FileCategory = category,
            UploadTime = now,
            UploaderName = ResolveUserDisplayName(currentUser)
        };

        _repository.AddAttachment(attachment);

        if (string.Equals(
                category,
                ArchiveInventoryRegisterDomainValues.AttachmentCategorySignedForm,
                StringComparison.Ordinal))
        {
            existing.SignedAttachmentUploaded = true;
            existing.SignedAttachmentUploadedTime = now;
            existing.SignedAttachmentUploader = attachment.UploaderName;
        }

        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
        return (true, "附件上传成功。", attachment);
    }

    public async Task<(bool Ok, string Message)> DeleteAttachmentAsync(int attachmentId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var attachment = await _repository.GetAttachmentByIdAsync(attachmentId);
        if (attachment == null)
        {
            return (false, "附件不存在。");
        }

        var existing = await _repository.GetRecordByIdForUpdateAsync(attachment.BusinessId);
        if (existing == null)
        {
            return (false, "未找到关联盘库登记单。");
        }

        var deleteGate = OfflineApprovalLifecycleSupport.EvaluateAttachmentDelete(existing.Status);
        if (!deleteGate.Allowed)
        {
            return (false, deleteGate.DenyMessage ?? "当前状态不允许删除附件。");
        }

        string category = attachment.FileCategory?.Trim() ?? string.Empty;
        var remainingBefore = await _repository.GetAttachmentsAsync(existing.RegisterNo);
        var remaining = remainingBefore.Where(item => item.Id != attachmentId).ToList();
        _repository.RemoveAttachment(attachment);

        if (string.Equals(
                category,
                ArchiveInventoryRegisterDomainValues.AttachmentCategorySignedForm,
                StringComparison.Ordinal))
        {
            bool stillHas = remaining.Any(item =>
                string.Equals(
                    item.FileCategory,
                    ArchiveInventoryRegisterDomainValues.AttachmentCategorySignedForm,
                    StringComparison.Ordinal));
            if (!stillHas)
            {
                existing.SignedAttachmentUploaded = false;
                existing.SignedAttachmentUploadedTime = null;
                existing.SignedAttachmentUploader = string.Empty;
            }
        }

        existing.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
        return (true, "附件已删除。");
    }

    private static void ThrowIfSignatureDateInvalid(DateTime? value, DateTime? minDate, string fieldLabel)
    {
        var error = ApprovalSignatureDateSupport.ValidateNotBeforePrint(value, minDate, fieldLabel);
        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(error);
        }
    }
}
