namespace DocMgr.Models.YearlyArchive
{
    public sealed record ArchiveRegisterApprovalValidationResult(IReadOnlyList<string> Errors)
    {
        public bool IsValid => Errors.Count == 0;

        public string ErrorMessage => string.Join("\n", Errors);
    }
}
