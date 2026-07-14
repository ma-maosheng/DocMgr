namespace DocMgr.Models.YearlyArchive
{
    public sealed record ArchiveRegisterUiPermissionState(
        bool IsArchiveAdmin,
        bool IsApplicant,
        bool CanEditForm,
        bool CanApprove,
        bool CanUpload,
        bool CanEditItemConfidentialLevel);
}
