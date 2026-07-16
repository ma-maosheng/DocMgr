using System.Collections.ObjectModel;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.ViewModels.HardDiskMedia
{
    internal static class HardDiskMediaApplicationViewModelHelper
    {
        /// <summary>
        /// 默认审核人：申请人所属部门的「部门负责人」；找不到时回退到申请人姓名/当前用户。
        /// </summary>
        internal static string ResolveDefaultReviewerName(
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
        internal static string ResolveDefaultApproverName(
            HardDiskMediaApplication application,
            IReadOnlyList<User> users,
            User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(users);

            if (!string.IsNullOrWhiteSpace(application.ApprovedBy))
            {
                return application.ApprovedBy.Trim();
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
                TargetPersonOrUnit = source.TargetPersonOrUnit,
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
                ReviewerName = source.ReviewerName,
                ReviewerDate = source.ReviewerDate,
                ApprovedBy = source.ApprovedBy,
                ApprovedTime = source.ApprovedTime,
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
                   applicationType == HardDiskMediaApplication.TypeOutboundDestroy ||
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
