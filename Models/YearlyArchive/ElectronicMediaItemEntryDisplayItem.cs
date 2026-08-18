namespace DocMgr.Models.YearlyArchive
{
    public sealed record ElectronicMediaItemEntryDisplayItem(
        string EntryKind,
        string EntryName,
        string CreatedDateText,
        string ModifiedDateText,
        decimal? SizeMb)
    {
        public string SizeMbText => SizeMb.HasValue ? SizeMb.Value.ToString("0.##") : string.Empty;
    }
}
