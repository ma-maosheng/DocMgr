using Microsoft.EntityFrameworkCore.Metadata;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 高级数据维护数据访问契约：数据校验与批量维护的底层访问。
/// </summary>
public interface IAdvancedDataRepository
{
    IReadOnlyList<IEntityType> GetEntityTypes();

    IEntityType? ResolveEntityType(string entityTypeName);

    List<object> GetEntityRows(IEntityType entityType);

    Task<int> GetEntityRowCountAsync(IEntityType entityType);

    Task<List<object>> GetEntityRowsPagedAsync(IEntityType entityType, int skip, int take);

    Task<object?> FindRecordAsync(IEntityType entityType, object recordId);

    void RemoveRecord(object record);

    void RemoveRecords(IEnumerable<object> records);

    Task<int> SaveChangesAsync();

    Task<List<FieldDomainDefinition>> GetFieldDomainDefinitionsWithOptionsAsync(string entityName);

    Task<FieldDomainDefinition?> GetFieldDomainDefinitionAsync(string entityName, string fieldName);

    Task<int?> GetMaxFieldDomainSortOrderAsync(string entityName);

    void AddFieldDomainDefinition(FieldDomainDefinition definition);

    Task<bool> ExistsFieldDomainDefinitionAsync(int definitionId);

    Task<List<FieldDomainOption>> GetFieldDomainOptionsAsync(int definitionId);

    Task<FieldDomainOption?> GetFieldDomainOptionAsync(int optionId, int definitionId);

    Task<FieldDomainOption?> GetFieldDomainOptionByIdAsync(int optionId);

    Task<bool> ExistsDuplicateFieldDomainOptionAsync(int definitionId, string scope, string optionValue);

    void AddFieldDomainOption(FieldDomainOption option);

    void RemoveFieldDomainOption(FieldDomainOption option);

    Task<FieldDomainDefinition?> GetEnabledFieldDomainDefinitionAsync(string entityName, string fieldName);

    Task<List<string>> GetEnabledFieldDomainValuesAsync(int definitionId, string? scope);
}
