using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using DocMgr.Data;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.SystemSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DocMgr.Repositories.SystemSettings;

public class AdvancedDataRepository : IAdvancedDataRepository
{
    private static readonly MethodInfo DbSetMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!;

    private readonly AppDbContext _dbContext;

    public AdvancedDataRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<IEntityType> GetEntityTypes()
    {
        return _dbContext.Model.GetEntityTypes().ToList();
    }

    public IEntityType? ResolveEntityType(string entityTypeName)
    {
        return _dbContext.Model.GetEntityTypes()
            .FirstOrDefault(entityType => string.Equals(entityType.Name, entityTypeName, StringComparison.OrdinalIgnoreCase));
    }

    public List<object> GetEntityRows(IEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        var rows = new List<object>();
        foreach (var item in GetEntityEnumerable(entityType))
        {
            if (item != null)
            {
                rows.Add(item);
            }
        }

        return rows;
    }

    public Task<int> GetEntityRowCountAsync(IEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        var queryable = ApplyStableOrdering(GetEntityQueryable(entityType), entityType);
        return InvokeCountAsync(queryable, ResolveEntityClrType(entityType));
    }

    public async Task<List<object>> GetEntityRowsPagedAsync(IEntityType entityType, int skip, int take)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip));
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take));
        }

        var clrType = ResolveEntityClrType(entityType);
        var queryable = ApplyStableOrdering(GetEntityQueryable(entityType), entityType);
        queryable = InvokeSkip(queryable, clrType, skip);
        queryable = InvokeTake(queryable, clrType, take);

        var rows = await InvokeToListAsync(queryable, clrType);
        return rows;
    }

    private IEnumerable GetEntityEnumerable(IEntityType entityType)
    {
        if (IsSharedDictionaryEntity(entityType))
        {
            return _dbContext.Set<Dictionary<string, object>>(entityType.Name);
        }

        var method = DbSetMethod.MakeGenericMethod(entityType.ClrType!);
        return method.Invoke(_dbContext, null) as IEnumerable
            ?? throw new InvalidOperationException($"无法访问实体查询集: {entityType.Name}");
    }

    private IQueryable GetEntityQueryable(IEntityType entityType)
    {
        var enumerable = GetEntityEnumerable(entityType);
        if (enumerable is IQueryable queryable)
        {
            return queryable;
        }

        return enumerable.AsQueryable();
    }

    private static bool IsSharedDictionaryEntity(IEntityType entityType)
        => AdvancedDataDictionaryEntitySupport.IsDictionaryBackedEntity(entityType);

    private static Type ResolveEntityClrType(IEntityType entityType)
        => entityType.ClrType ?? typeof(Dictionary<string, object>);

    private static IQueryable ApplyStableOrdering(IQueryable queryable, IEntityType entityType)
    {
        var clrType = ResolveEntityClrType(entityType);
        var keyProperties = entityType.FindPrimaryKey()?.Properties;
        if (keyProperties == null || keyProperties.Count == 0)
        {
            return queryable;
        }

        IOrderedQueryable? orderedQueryable = null;
        foreach (var property in keyProperties)
        {
            orderedQueryable = orderedQueryable == null
                ? InvokeOrderBy(queryable, clrType, property)
                : InvokeThenBy(orderedQueryable, clrType, property);
        }

        return orderedQueryable ?? queryable;
    }

    private static IOrderedQueryable InvokeOrderBy(IQueryable queryable, Type clrType, IProperty property)
    {
        var parameter = Expression.Parameter(clrType, "entity");
        var body = BuildPropertyAccess(parameter, property, clrType);
        var lambda = Expression.Lambda(body, parameter);
        var method = typeof(Queryable).GetMethods()
            .First(methodInfo => methodInfo.Name == nameof(Queryable.OrderBy)
                                 && methodInfo.GetParameters().Length == 2)
            .MakeGenericMethod(clrType, body.Type);
        return (IOrderedQueryable)method.Invoke(null, new object[] { queryable, lambda })!;
    }

    private static IOrderedQueryable InvokeThenBy(IOrderedQueryable queryable, Type clrType, IProperty property)
    {
        var parameter = Expression.Parameter(clrType, "entity");
        var body = BuildPropertyAccess(parameter, property, clrType);
        var lambda = Expression.Lambda(body, parameter);
        var method = typeof(Queryable).GetMethods()
            .First(methodInfo => methodInfo.Name == nameof(Queryable.ThenBy)
                                 && methodInfo.GetParameters().Length == 2)
            .MakeGenericMethod(clrType, body.Type);
        return (IOrderedQueryable)method.Invoke(null, new object[] { queryable, lambda })!;
    }

    private static Expression BuildPropertyAccess(ParameterExpression parameter, IProperty property, Type clrType)
    {
        if (clrType == typeof(Dictionary<string, object>))
        {
            return BuildEfPropertyAccess(parameter, property);
        }

        if (property.PropertyInfo != null
            && !AdvancedDataDictionaryEntitySupport.IsDictionaryIndexerProperty(property))
        {
            return Expression.Property(parameter, property.PropertyInfo);
        }

        return BuildEfPropertyAccess(parameter, property);
    }

    private static Expression BuildEfPropertyAccess(ParameterExpression parameter, IProperty property)
    {
        var propertyClrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        var efPropertyMethod = typeof(EF).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(methodInfo => methodInfo.Name == nameof(EF.Property)
                                  && methodInfo.IsGenericMethodDefinition
                                  && methodInfo.GetParameters().Length == 2
                                  && methodInfo.GetParameters()[0].ParameterType == typeof(object));
        efPropertyMethod = efPropertyMethod.MakeGenericMethod(propertyClrType);
        return Expression.Call(
            efPropertyMethod,
            parameter,
            Expression.Constant(property.Name, typeof(string)));
    }

    private static IQueryable InvokeSkip(IQueryable queryable, Type clrType, int skip)
    {
        var method = typeof(Queryable).GetMethods()
            .First(methodInfo => methodInfo.Name == nameof(Queryable.Skip)
                                 && methodInfo.GetParameters().Length == 2)
            .MakeGenericMethod(clrType);
        return (IQueryable)method.Invoke(null, new object[] { queryable, skip })!;
    }

    private static IQueryable InvokeTake(IQueryable queryable, Type clrType, int take)
    {
        var method = typeof(Queryable).GetMethods()
            .First(methodInfo => methodInfo.Name == nameof(Queryable.Take)
                                 && methodInfo.GetParameters().Length == 2)
            .MakeGenericMethod(clrType);
        return (IQueryable)method.Invoke(null, new object[] { queryable, take })!;
    }

    private static Task<int> InvokeCountAsync(IQueryable queryable, Type clrType)
    {
        var method = typeof(EntityFrameworkQueryableExtensions).GetMethods()
            .First(methodInfo => methodInfo.Name == nameof(EntityFrameworkQueryableExtensions.CountAsync)
                                 && methodInfo.GetParameters().Length == 2)
            .MakeGenericMethod(clrType);
        return (Task<int>)method.Invoke(null, new object[] { queryable, CancellationToken.None })!;
    }

    private static async Task<List<object>> InvokeToListAsync(IQueryable queryable, Type clrType)
    {
        var method = typeof(EntityFrameworkQueryableExtensions).GetMethods()
            .First(methodInfo => methodInfo.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync)
                                 && methodInfo.GetParameters().Length == 2)
            .MakeGenericMethod(clrType);
        var task = (Task)method.Invoke(null, new object[] { queryable, CancellationToken.None })!;
        await task.ConfigureAwait(false);
        var resultProperty = task.GetType().GetProperty(nameof(Task<object>.Result))!;
        var typedList = resultProperty.GetValue(task)!;
        return ((IEnumerable)typedList).Cast<object>().ToList();
    }

    public Task<object?> FindRecordAsync(IEntityType entityType, object recordId)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(recordId);
        return _dbContext.FindAsync(entityType.ClrType!, recordId).AsTask();
    }

    public void RemoveRecord(object record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _dbContext.Remove(record);
    }

    public void RemoveRecords(IEnumerable<object> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        _dbContext.RemoveRange(records);
    }

    public Task<int> SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }

    public Task<List<FieldDomainDefinition>> GetFieldDomainDefinitionsWithOptionsAsync(string entityName)
    {
        return _dbContext.FieldDomainDefinitions
            .Include(definition => definition.Options)
            .Where(definition => definition.EntityName == entityName)
            .ToListAsync();
    }

    public Task<FieldDomainDefinition?> GetFieldDomainDefinitionAsync(string entityName, string fieldName)
    {
        return _dbContext.FieldDomainDefinitions
            .FirstOrDefaultAsync(definition => definition.EntityName == entityName && definition.FieldName == fieldName);
    }

    public async Task<int?> GetMaxFieldDomainSortOrderAsync(string entityName)
    {
        return await _dbContext.FieldDomainDefinitions
            .Where(definition => definition.EntityName == entityName)
            .Select(definition => (int?)definition.SortOrder)
            .MaxAsync();
    }

    public void AddFieldDomainDefinition(FieldDomainDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _dbContext.FieldDomainDefinitions.Add(definition);
    }

    public Task<bool> ExistsFieldDomainDefinitionAsync(int definitionId)
    {
        return _dbContext.FieldDomainDefinitions.AnyAsync(definition => definition.Id == definitionId);
    }

    public Task<List<FieldDomainOption>> GetFieldDomainOptionsAsync(int definitionId)
    {
        return _dbContext.FieldDomainOptions
            .Where(option => option.FieldDomainDefinitionId == definitionId)
            .OrderBy(option => option.SortOrder)
            .ThenBy(option => option.Id)
            .ToListAsync();
    }

    public Task<FieldDomainOption?> GetFieldDomainOptionAsync(int optionId, int definitionId)
    {
        return _dbContext.FieldDomainOptions
            .FirstOrDefaultAsync(option => option.Id == optionId && option.FieldDomainDefinitionId == definitionId);
    }

    public Task<FieldDomainOption?> GetFieldDomainOptionByIdAsync(int optionId)
    {
        return _dbContext.FieldDomainOptions
            .FirstOrDefaultAsync(option => option.Id == optionId);
    }

    public Task<bool> ExistsDuplicateFieldDomainOptionAsync(int definitionId, string scope, string optionValue)
    {
        return _dbContext.FieldDomainOptions.AnyAsync(option =>
            option.FieldDomainDefinitionId == definitionId
            && option.Scope == scope
            && option.OptionValue == optionValue);
    }

    public void AddFieldDomainOption(FieldDomainOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        _dbContext.FieldDomainOptions.Add(option);
    }

    public void RemoveFieldDomainOption(FieldDomainOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        _dbContext.FieldDomainOptions.Remove(option);
    }

    public Task<FieldDomainDefinition?> GetEnabledFieldDomainDefinitionAsync(string entityName, string fieldName)
    {
        return _dbContext.FieldDomainDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(definition =>
                definition.EntityName == entityName
                && definition.FieldName == fieldName
                && definition.IsDomainEnabled);
    }

    public Task<List<string>> GetEnabledFieldDomainValuesAsync(int definitionId, string? scope)
    {
        IQueryable<FieldDomainOption> query = _dbContext.FieldDomainOptions
            .AsNoTracking()
            .Where(option => option.FieldDomainDefinitionId == definitionId && option.IsEnabled);

        if (!string.IsNullOrWhiteSpace(scope))
        {
            query = query.Where(option => option.Scope == scope || option.Scope == string.Empty);
        }

        return query
            .OrderBy(option => option.SortOrder)
            .ThenBy(option => option.Id)
            .Select(option => option.OptionValue)
            .ToListAsync();
    }
}
