using System.Data;

namespace DocMgr.Models.SystemSettings
{
    public sealed record AdvancedDataTablePageDto(DataTable Data, int TotalCount);

    public sealed record TableBrowseEntryDto(
        string DisplayName,
        string EntityTypeName,
        string TableName,
        bool IsSharedType,
        bool CanMaintain);

    public sealed record TableBrowseInfoDto(
        string TableName,
        string ChineseName,
        string Description,
        string Relationships,
        string MaintenanceNotes);

    public sealed record TableFieldStructureDto(
        string EntityName,
        string FieldName,
        string FieldType,
        bool IsNullable,
        bool CanConfigureDomain,
        int? DefinitionId,
        string DisplayName,
        bool IsDomainEnabled,
        int DomainOptionCount);

    public sealed record FieldDomainDefinitionDto(
        int Id,
        string EntityName,
        string FieldName,
        string DisplayName,
        string Description,
        bool IsDomainEnabled,
        int SortOrder);

    public sealed record FieldDomainOptionDto(
        int Id,
        int FieldDomainDefinitionId,
        string Scope,
        string OptionValue,
        string OptionLabel,
        bool IsEnabled,
        int SortOrder);
}
