namespace DocMgr.Models.YearlyArchive
{
    public sealed record ArchiveRegisterPrintNormalizationResult(
        string ConfidentialLevel,
        string ProdOpinion,
        string RndOpinion,
        string DeputyOpinion,
        IReadOnlyList<string> ConfidentialLevels);
}
