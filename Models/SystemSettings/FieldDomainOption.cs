namespace DocMgr.Models.SystemSettings
{
    public class FieldDomainOption
    {
        public int Id { get; set; }
        public int FieldDomainDefinitionId { get; set; }
        public string Scope { get; set; } = string.Empty;
        public string OptionValue { get; set; } = string.Empty;
        public string OptionLabel { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int SortOrder { get; set; }

        public virtual FieldDomainDefinition? FieldDefinition { get; set; }
    }
}
