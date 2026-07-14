using DocMgr.Data;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.SystemSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DocMgr.Repositories.SystemSettings;

public class FieldDomainSeedRepository : IFieldDomainSeedRepository
{
    private readonly AppDbContext _dbContext;

    public FieldDomainSeedRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public FieldDomainDefinition? GetDefinitionWithOptions(string entityName, string fieldName)
    {
        return _dbContext.FieldDomainDefinitions
            .Include(definition => definition.Options)
            .FirstOrDefault(definition => definition.EntityName == entityName && definition.FieldName == fieldName);
    }

    public void AddDefinition(FieldDomainDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _dbContext.FieldDomainDefinitions.Add(definition);
    }

    public void RemoveOptions(IEnumerable<FieldDomainOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _dbContext.FieldDomainOptions.RemoveRange(options);
    }

    public List<FieldDomainDefinition> GetTrackedAndAllDefinitions()
    {
        return _dbContext.FieldDomainDefinitions.Local
            .Concat(_dbContext.FieldDomainDefinitions.ToList())
            .GroupBy(definition => $"{definition.EntityName}.{definition.FieldName}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public IReadOnlyList<IEntityType> GetSeedEntityTypes()
    {
        return _dbContext.Model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned()
                                 && entityType.FindPrimaryKey() != null
                                 && (entityType.ClrType != null
                                     || AdvancedDataDictionaryEntitySupport.IsDictionaryBackedEntity(entityType)))
            .OrderBy(entityType => AdvancedDataDictionaryEntitySupport.IsDictionaryBackedEntity(entityType)
                ? entityType.Name
                : entityType.ClrType!.Name)
            .ToList();
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }
}
