using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质业务待办提供器。
    /// </summary>
    public class HardDiskMediaToDoProvider : IToDoProvider
    {
        private readonly IHardDiskMediaRepository _hardDiskMediaRepository;
        private readonly IHardDiskDisposalRepository _hardDiskDisposalRepository;
        private readonly IHardDiskInventoryRegisterRepository _hardDiskInventoryRegisterRepository;

        public HardDiskMediaToDoProvider(
            IHardDiskMediaRepository hardDiskMediaRepository,
            IHardDiskDisposalRepository hardDiskDisposalRepository,
            IHardDiskInventoryRegisterRepository hardDiskInventoryRegisterRepository)
        {
            _hardDiskMediaRepository = hardDiskMediaRepository;
            _hardDiskDisposalRepository = hardDiskDisposalRepository;
            _hardDiskInventoryRegisterRepository = hardDiskInventoryRegisterRepository;
        }

        /// <inheritdoc/>
        public async Task<List<ToDoItem>> GetToDosAsync(User currentUser)
        {
            var result = new List<ToDoItem>();
            if (currentUser == null)
            {
                return result;
            }

            bool isArchiveRoomAdmin = IsArchiveRoomAdmin(currentUser);
            string currentUserName = currentUser.RealName?.Trim() ?? string.Empty;

            if (isArchiveRoomAdmin)
            {
                var submitted = await _hardDiskMediaRepository.GetSubmittedApplicationsForToDoAsync(200);

                result.AddRange(submitted.Select(item => new ToDoItem
                {
                    Id = $"HDM-{item.Id}-APPROVAL-PENDING",
                    Title = $"【硬盘介质】待审批：{item.ApplicationType} / {item.Medium?.DiskCode}",
                    BizType = "HardDiskMediaApplication",
                    BizId = item.Id,
                    BizNo = item.ApplicationNo,
                    Stage = BuildApprovalPendingStage(item),
                    CreatedTime = item.ApplyTime,
                    Priority = "高"
                }));

                var pendingReturnRegistrations = await _hardDiskMediaRepository.GetPendingReturnRegistrationsForToDoAsync(200);
                result.AddRange(pendingReturnRegistrations.Select(item => new ToDoItem
                {
                    Id = $"HDM-{item.Id}-RETURN-PENDING",
                    Title = $"【硬盘归还登记】待办理：{item.ApplicationType} / {item.Medium?.DiskCode}",
                    BizType = "HardDiskMediaReturnRegistration",
                    BizId = item.Id,
                    BizNo = item.ApplicationNo,
                    Stage = BuildApprovalPendingStage(item),
                    CreatedTime = item.ApplyTime,
                    Priority = "高"
                }));

                // 离库处置：草稿起直至办结前保留资料室待办。
                var pendingDisposals = await _hardDiskDisposalRepository.GetPendingRecordsForToDoAsync(200);
                result.AddRange(pendingDisposals.Select(item => new ToDoItem
                {
                    Id = $"HDD-{item.Id}-DISPOSAL-PENDING",
                    Title = $"【硬盘离库处置】{ResolveDisposalToDoTitle(item)}：{BuildDisposalSummary(item)}",
                    BizType = "HardDiskDisposal",
                    BizId = item.Id,
                    BizNo = item.DisposalNo,
                    Stage = BuildDisposalPendingStage(item),
                    CreatedTime = item.SubmittedAt ?? item.ApplyTime,
                    Priority = "高"
                }));

                // 盘库登记：草稿起直至办结前保留资料室待办。
                var pendingInventory = await _hardDiskInventoryRegisterRepository.GetPendingRecordsForToDoAsync(200);
                result.AddRange(pendingInventory.Select(item => new ToDoItem
                {
                    Id = $"HDI-{item.Id}-INVENTORY-PENDING",
                    Title = $"【硬盘盘库登记】{ResolveInventoryToDoTitle(item)}：{BuildInventorySummary(item)}",
                    BizType = "HardDiskInventoryRegister",
                    BizId = item.Id,
                    BizNo = item.RegisterNo,
                    Stage = BuildInventoryPendingStage(item),
                    CreatedTime = item.SubmittedAt ?? item.ApplyTime,
                    Priority = "高"
                }));
            }

            var overdueReturns = await _hardDiskMediaRepository.GetOverdueOutboundApplicationsForToDoAsync(DateTime.Now, 200);
            IEnumerable<HardDiskMediaApplication> overdueForCurrentUser = overdueReturns;

            if (!isArchiveRoomAdmin)
            {
                if (string.IsNullOrWhiteSpace(currentUserName))
                {
                    return result;
                }

                overdueForCurrentUser = overdueReturns.Where(item =>
                    string.Equals(item.ApplicantName?.Trim(), currentUserName, StringComparison.OrdinalIgnoreCase));
            }

            result.AddRange(overdueForCurrentUser.Select(item => new ToDoItem
            {
                Id = $"HDM-RETURN-OVERDUE-{item.Id}",
                Title = $"【硬盘归还】已超期未归还：{item.Medium?.DiskCode} / {item.ApplicationType}",
                BizType = "HardDiskMediaOutboundOverdue",
                BizId = item.Id,
                BizNo = item.ApplicationNo,
                Stage = $"应还日期 {item.ExpectedReturnDate:yyyy-MM-dd}（申请人 {item.ApplicantName}）",
                CreatedTime = item.ExpectedReturnDate ?? item.ExecutedTime ?? item.ApplyTime,
                Priority = "高"
            }));

            return result;
        }

        private static string BuildApprovalPendingStage(HardDiskMediaApplication application)
        {
            return application.ApplicationStatus switch
            {
                HardDiskMediaApplication.StatusSubmitted => "已提交-待审批",
                HardDiskMediaApplication.StatusApproved => "已审批-待实物交接",
                HardDiskMediaApplication.StatusSignedUploaded when !application.SignedAttachmentUploaded => "已实物交接-待上传签批交接单",
                HardDiskMediaApplication.StatusSignedUploaded => "已上传签批交接单-待办结",
                _ => ApplicationWorkflowStatus.ToDisplay(application.ApplicationStatus)
            };
        }

        private static string ResolveDisposalToDoTitle(HardDiskDisposalRecord record) =>
            record.Status switch
            {
                HardDiskDisposalRecord.StatusDraft => "待提交",
                HardDiskDisposalRecord.StatusSubmitted => "待审批",
                HardDiskDisposalRecord.StatusApproved => "待确认可上传",
                HardDiskDisposalRecord.StatusSignedUploaded
                    when !record.SignedAttachmentUploaded || !record.DiskPhotoUploaded => "待上传签批单/照片",
                HardDiskDisposalRecord.StatusSignedUploaded => "待办结",
                _ => "待办理"
            };

        private static string BuildDisposalPendingStage(HardDiskDisposalRecord record) =>
            record.Status switch
            {
                HardDiskDisposalRecord.StatusDraft => "草稿-待提交",
                HardDiskDisposalRecord.StatusSubmitted => "已提交-待审批",
                HardDiskDisposalRecord.StatusApproved => "已审批-待确认可上传",
                HardDiskDisposalRecord.StatusSignedUploaded
                    when !record.SignedAttachmentUploaded || !record.DiskPhotoUploaded => "已确认可上传-待上传签批单/照片",
                HardDiskDisposalRecord.StatusSignedUploaded => "已上传附件-待办结",
                _ => HardDiskDisposalDomainValues.ToStatusDisplay(record.Status)
            };

        private static string BuildDisposalSummary(HardDiskDisposalRecord record)
        {
            string reason = HardDiskDisposalDomainValues.BuildReasonSummary(
                (record.Items ?? Array.Empty<HardDiskDisposalItem>())
                    .Select(item => string.IsNullOrWhiteSpace(item.DisposalReason)
                        ? record.DisposalReason
                        : item.DisposalReason));
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = record.DisposalReason?.Trim() ?? string.Empty;
            }

            string disks = record.DiskCodesSummary?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(disks))
            {
                disks = record.ItemCount > 0 ? $"{record.ItemCount} 块硬盘" : record.DisposalNo;
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return disks;
            }

            return $"{reason} / {disks}";
        }

        private static string ResolveInventoryToDoTitle(HardDiskInventoryRegisterRecord record) =>
            record.Status switch
            {
                HardDiskInventoryRegisterRecord.StatusDraft => "待提交",
                HardDiskInventoryRegisterRecord.StatusSubmitted => "待审批",
                HardDiskInventoryRegisterRecord.StatusApproved => "待确认可上传",
                HardDiskInventoryRegisterRecord.StatusSignedUploaded when !record.SignedAttachmentUploaded => "待上传签批单",
                HardDiskInventoryRegisterRecord.StatusSignedUploaded => "待办结",
                _ => "待办理"
            };

        private static string BuildInventoryPendingStage(HardDiskInventoryRegisterRecord record) =>
            record.Status switch
            {
                HardDiskInventoryRegisterRecord.StatusDraft => "草稿-待提交",
                HardDiskInventoryRegisterRecord.StatusSubmitted => "已提交-待审批",
                HardDiskInventoryRegisterRecord.StatusApproved => "已审批-待确认可上传",
                HardDiskInventoryRegisterRecord.StatusSignedUploaded when !record.SignedAttachmentUploaded => "已确认可上传-待上传签批单",
                HardDiskInventoryRegisterRecord.StatusSignedUploaded => "已上传附件-待办结",
                _ => HardDiskInventoryRegisterDomainValues.ToStatusDisplay(record.Status)
            };

        private static string BuildInventorySummary(HardDiskInventoryRegisterRecord record)
        {
            string kind = record.RegisterKind?.Trim() ?? string.Empty;
            string disks = record.Items == null || record.Items.Count == 0
                ? record.RegisterNo
                : string.Join("、", record.Items.OrderBy(item => item.SortOrder).Select(item => item.DiskCode).Where(code => !string.IsNullOrWhiteSpace(code)));
            if (string.IsNullOrWhiteSpace(disks))
            {
                disks = $"{record.Items?.Count ?? 0} 块硬盘";
            }

            return string.IsNullOrWhiteSpace(kind) ? disks : $"{kind} / {disks}";
        }

        private static bool IsArchiveRoomAdmin(User user)
        {
            return ArchiveRegisterBusinessRules.IsArchiveAdminUser(user)
                   || ArchiveRegisterBusinessRules.IsSystemAdministrator(user);
        }
    }
}
