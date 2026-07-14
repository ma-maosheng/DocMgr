namespace DocMgr.Models.SystemSettings
{
    public class FieldDomainDefinition
    {
        public int Id { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDomainEnabled { get; set; }
        public int SortOrder { get; set; }

        public virtual List<FieldDomainOption> Options { get; set; } = new();
    }
}
