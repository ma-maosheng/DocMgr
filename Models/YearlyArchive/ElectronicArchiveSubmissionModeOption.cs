namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质立档方式选项。
    /// </summary>
    public sealed record ElectronicArchiveSubmissionModeOption(
        ElectronicArchiveSubmissionMode Mode,
        string DisplayName,
        string Description,
        bool IsEnabled,
        string DisabledReason,
        bool RequiresExistingElectronicUnit,
        bool IsDefault);
}
