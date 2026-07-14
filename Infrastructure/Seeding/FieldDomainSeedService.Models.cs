namespace DocMgr.Infrastructure.Seeding;

public static partial class FieldDomainSeedService
{
    private sealed record FieldDomainSeed(
        string EntityName,
        string FieldName,
        string DisplayName,
        string Description,
        bool IsDomainEnabled,
        int SortOrder,
        List<FieldDomainOptionSeed> Options);

    private sealed record FieldDomainOptionSeed(
        string Scope,
        string OptionValue,
        string OptionLabel,
        bool IsEnabled,
        int SortOrder);
}
