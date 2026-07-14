using Microsoft.EntityFrameworkCore.Metadata;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 字段域种子数据访问契约：下拉值（字段域）初始化数据读写。
/// </summary>
public interface IFieldDomainSeedRepository
{
    FieldDomainDefinition? GetDefinitionWithOptions(string entityName, string fieldName);

    void AddDefinition(FieldDomainDefinition definition);

    void RemoveOptions(IEnumerable<FieldDomainOption> options);

    List<FieldDomainDefinition> GetTrackedAndAllDefinitions();

    IReadOnlyList<IEntityType> GetSeedEntityTypes();

    int SaveChanges();
}
