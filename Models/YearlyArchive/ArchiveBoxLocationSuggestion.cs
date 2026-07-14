namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 档案盒建议档口位置。
    /// </summary>
    public sealed record ArchiveBoxLocationSuggestion(
        string CabinetName,
        string Side,
        int Row,
        int Column,
        int ExistingBoxCount,
        string SuggestedBoxLocationCode,
        string SuggestionSummary);
}
