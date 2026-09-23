using System.Collections.ObjectModel;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.Interfaces;
using DocMgr.Services.SystemSettings;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.ViewModels.HardDiskMedia
{
    internal static class HardDiskMediaApplicationViewModelHelper
    {
        /// <summary>
        /// 按审核审批配置解析默认审核/审批人并写入申请单空字段。
        /// </summary>
        internal static async Task ApplyDefaultApprovalFromWorkflowAsync(
            HardDiskMediaApplication application,
            IApprovalWorkflowService approvalWorkflowService,
            IReadOnlyList<User> users,
            User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(approvalWorkflowService);
            ArgumentNullException.ThrowIfNull(users);

            var chain = await approvalWorkflowService.ResolveAsync(
                new ApprovalChainResolveRequest
                {
                    BusinessType = ApprovalChainApplySupport.ResolveHardDiskApplicationBusinessType(application),
                    ApplicantDept = application.ApplicantDept,
                    FieldValues = ApprovalChainApplySupport.BuildHardDiskApplicationFieldValues(application)
                },
                users);

            ApprovalChainApplySupport.ApplyToHardDiskApplication(application, chain, DateTime.Now);

            if (chain.DeptHead.IsEnabled && string.IsNullOrWhiteSpace(application.DeptHead))
            {
                application.DeptHead = currentUser?.RealName?.Trim() ?? string.Empty;
            }

            if (chain.ArchiveRoomHead.IsEnabled && string.IsNullOrWhiteSpace(application.ArchiveRoomHead))
            {
                application.ArchiveRoomHead = currentUser?.RealName?.Trim() ?? string.Empty;
            }

            if (chain.ArchiveDeputyPresident.IsEnabled
                && string.IsNullOrWhiteSpace(application.ArchiveDeputyPresident))
            {
                application.ArchiveDeputyPresident = currentUser?.RealName?.Trim() ?? string.Empty;
            }
        }

        /// <summary>按签批链校验必填签字人。</summary>
        internal static async Task<IReadOnlyList<string>> CollectMissingApprovalErrorsAsync(
            HardDiskMediaApplication application,
            IApprovalWorkflowService approvalWorkflowService,
            IReadOnlyList<User> users)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(approvalWorkflowService);
            ArgumentNullException.ThrowIfNull(users);

            var chain = await approvalWorkflowService.ResolveAsync(
                new ApprovalChainResolveRequest
                {
                    BusinessType = ApprovalChainApplySupport.ResolveHardDiskApplicationBusinessType(application),
                    ApplicantDept = application.ApplicantDept,
                    FieldValues = ApprovalChainApplySupport.BuildHardDiskApplicationFieldValues(application)
                },
                users);

            return ApprovalChainApplySupport.CollectMissingSignerErrors(
                chain,
                nodeKey => ApprovalChainApplySupport.ReadHardDiskSigner(application, nodeKey));
        }

        /// <summary>
        /// 默认审核人：申请人所属部门的「部门负责人」；找不到时回退到申请人姓名/当前用户。
        /// </summary>
        [Obsolete("请改用 ApplyDefaultApprovalFromWorkflowAsync")]
        internal static string ResolveDefaultDeptHead(
            HardDiskMediaApplication application,
            IReadOnlyList<User> users,
            User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(users);

            string applicantDept = application.ApplicantDept?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(applicantDept))
            {
                string reviewer = users
                    .FirstOrDefault(user => string.Equals(user.Department, applicantDept, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(user.RealName)
                        && (user.Role?.Contains("部门负责人", StringComparison.OrdinalIgnoreCase) ?? false))
                    ?.RealName
                    ?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(reviewer))
                {
                    return reviewer;
                }
            }

            if (!string.IsNullOrWhiteSpace(application.ApplicantName))
            {
                return application.ApplicantName.Trim();
            }

            return currentUser?.RealName?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 默认审批人：申请单已有审批人则保持；否则取资料室「负责人」；再回退到当前用户。
        /// </summary>
        [Obsolete("请改用 ApplyDefaultApprovalFromWorkflowAsync")]
        internal static string ResolveDefaultApproverName(
            HardDiskMediaApplication application,
            IReadOnlyList<User> users,
            User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(users);

            if (!string.IsNullOrWhiteSpace(application.ArchiveRoomHead))
            {
                return application.ArchiveRoomHead.Trim();
            }

            string approver = users
                .FirstOrDefault(user => string.Equals(user.Department, "资料室", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(user.RealName)
                    && (user.Role?.Contains("负责人", StringComparison.OrdinalIgnoreCase) ?? false))
                ?.RealName
                ?.Trim() ?? string.Empty;

            return string.IsNullOrWhiteSpace(approver)
                ? currentUser?.RealName?.Trim() ?? string.Empty
                : approver;
        }

        internal static HardDiskMediaApplication CloneApplication(HardDiskMediaApplication source)
        {
            ArgumentNullException.ThrowIfNull(source);

            return new HardDiskMediaApplication
            {
                Id = source.Id,
                ApplicationNo = source.ApplicationNo,
                MediumId = source.MediumId,
                SourceApplicationId = source.SourceApplicationId,
                SourceOutboundRecordId = source.SourceOutboundRecordId,
                ApplicationType = source.ApplicationType,
                ApplicationStatus = source.ApplicationStatus,
                ApplicantName = source.ApplicantName,
                ApplicantDept = source.ApplicantDept,
                ApplyTime = source.ApplyTime,
                Reason = source.Reason,
                ProofMaterialNote = source.ProofMaterialNote,
                TargetPersonOrUnit = source.TargetPersonOrUnit,
                DestinationKind = source.DestinationKind,
                CurrentLocation = source.CurrentLocation,
                TargetLocation = source.TargetLocation,
                ExpectedReturnDate = source.ExpectedReturnDate,
                InspectionResult = source.InspectionResult,
                FormatConfirmation = source.FormatConfirmation,
                RelatedBatch = source.RelatedBatch,
                RelatedArchiveTitle = source.RelatedArchiveTitle,
                PrintCount = source.PrintCount,
                PrintedTime = source.PrintedTime,
                SignedAttachmentUploaded = source.SignedAttachmentUploaded,
                SignedAttachmentUploadedTime = source.SignedAttachmentUploadedTime,
                SignedAttachmentUploader = source.SignedAttachmentUploader,
                DeptHead = source.DeptHead,
                DeptHeadDate = source.DeptHeadDate,
                ArchiveRoomHead = source.ArchiveRoomHead,
                ArchiveRoomHeadDate = source.ArchiveRoomHeadDate,
                ArchiveDeputyPresident = source.ArchiveDeputyPresident,
                ArchiveDeputyPresidentDate = source.ArchiveDeputyPresidentDate,
                ApprovalOpinion = source.ApprovalOpinion,
                ExecutedBy = source.ExecutedBy,
                ExecutedTime = source.ExecutedTime,
                Remark = source.Remark,
                Medium = source.Medium
            };
        }

        internal static bool IsSelectableOutboundApplicationType(string? applicationType)
        {
            return applicationType == HardDiskMediaApplication.TypeOutboundTemporary ||
                   applicationType == HardDiskMediaApplication.TypeOutboundLongTerm ||
                   applicationType == HardDiskMediaApplication.TypeOutboundPermanent;
        }

        internal static bool IsOutboundApplicationType(string? applicationType)
        {
            return IsSelectableOutboundApplicationType(applicationType) ||
                   applicationType == HardDiskMediaApplication.TypeRelocate;
        }

        internal static bool IsReturnRegistrationType(string? applicationType)
        {
            return applicationType == HardDiskMediaApplication.TypeReturnBlankRegistration ||
                   applicationType == HardDiskMediaApplication.TypeReturnDataRegistration ||
                   applicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration ||
                   applicationType == HardDiskMediaApplication.TypeLossRegistration;
        }

        internal static bool IsArchiveRoomMediaAdmin(User? currentUser) =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser);

        internal static bool IsDepartmentArchiveAdmin(User? currentUser) =>
            ArchiveRegisterBusinessRules.IsDepartmentArchiveAdmin(currentUser);

        internal static bool CanSubmitApplication(User? currentUser) =>
            ArchiveRegisterBusinessRules.CanSubmitApplication(currentUser);

        internal static void ResetReturnRegistrationKindOptions(ObservableCollection<string> target)
        {
            ArgumentNullException.ThrowIfNull(target);

            target.Clear();
            target.Add("全部");
            foreach (string kind in HardDiskMediaReturnDomainValues.RegistrationKindFilterOptions)
            {
                target.Add(kind);
            }
        }

        internal static void ResetOptions(ObservableCollection<string> target, IReadOnlyList<string> values)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(values);

            target.Clear();
            target.Add("全部");
            foreach (var value in values)
            {
                target.Add(value);
            }
        }

        internal static string EmptyAsPlaceholder(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(无)" : value.Trim();
        }

        internal static string FormatExpectedReturnDateDisplay(string? applicationType, DateTime? expectedReturnDate)
        {
            return HardDiskMediaOutboundReturnSupport.FormatExpectedReturnDateDisplay(applicationType, expectedReturnDate);
        }

        internal static string FormatDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "(无)";
        }

        internal static string FormatDateTime(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm") : "(无)";
        }
    }
}
