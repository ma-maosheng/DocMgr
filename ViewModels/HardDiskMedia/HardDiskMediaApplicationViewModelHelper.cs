using System.Collections.ObjectModel;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.ViewModels.HardDiskMedia
{
    internal static class HardDiskMediaApplicationViewModelHelper
    {
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

        internal static bool IsArchiveRoomMediaAdmin(User? currentUser)
        {
            string dept = currentUser?.Department?.Trim() ?? string.Empty;
            string role = currentUser?.Role?.Trim() ?? string.Empty;

            return (string.Equals(dept, "资料室", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(role, "部门资料管理员", StringComparison.OrdinalIgnoreCase)) ||
                   string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase);
        }

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
