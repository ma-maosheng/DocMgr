using DocMgr.Data;
using DocMgr.Infrastructure.Seeding;
using DocMgr.Services.SystemSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Text.RegularExpressions;

namespace DocMgr.Infrastructure.Schema;

/// <summary>
/// 为 SchemaDictionary 同步工具提供 EF 模型快照与既有中文元数据。
/// </summary>
public static class SchemaDictionaryCatalog
{
    /// <summary>
    /// 从 <see cref="AppDbContext"/> 读取全部实体/视图及其标量字段快照。
    /// </summary>
    public static IReadOnlyList<SchemaEntitySnapshot> GetEntitySnapshots(AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return dbContext.Model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned()
                                 && (entityType.GetTableName() != null || entityType.GetViewName() != null)
                                 && !entityType.Name.Contains("Dictionary<string, object>", StringComparison.OrdinalIgnoreCase)
                                 && (HasScalarEntityClrType(entityType)
                                     || AdvancedDataDictionaryEntitySupport.IsDictionaryBackedEntity(entityType)))
            .Select(entityType =>
            {
                var entityName = ResolveEntityDictionaryName(entityType);
                var tableName = entityType.GetTableName() ?? entityName;
                var isView = entityType.GetViewName() != null;

                var fields = SchemaColumnOrderSupport.OrderProperties(entityType)
                    .Select(property =>
                    {
                        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                        return new SchemaEntityFieldSnapshot(
                            property.Name,
                            property.IsNullable ? $"{clrType.Name}?" : clrType.Name,
                            property.IsNullable);
                    })
                    .ToList();

                return new SchemaEntitySnapshot(
                    entityName,
                    tableName,
                    isView,
                    fields);
            })
            .OrderBy(snapshot => snapshot.TableName, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.EntityName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 读取高级数据浏览页维护的表级中文元数据。
    /// </summary>
    public static bool TryGetTableMetadata(string entityName, out SchemaTableMetadataSnapshot metadata)
    {
        if (AdvancedDataTableMetadata.TryGet(entityName, out var entry))
        {
            metadata = new SchemaTableMetadataSnapshot(
                entry.Description,
                entry.Relationships,
                entry.MaintenanceNotes);
            return true;
        }

        metadata = default!;
        return false;
    }

    /// <summary>
    /// 解析字段中文显示名（同步 YAML 时使用 legacy alias，避免读取尚未写入的字典）。
    /// </summary>
    public static string ResolveFieldChineseNameForSync(string entityName, string fieldName)
        => FieldDomainSeedService.GenerateAliasFromLegacyMaps(entityName, fieldName);

    /// <summary>
    /// 解析字段中文显示名，优先 YAML，其次 legacy alias。
    /// </summary>
    public static string ResolveFieldChineseName(string entityName, string fieldName)
    {
        if (SchemaDictionaryStore.TryGetFieldChineseName(entityName, fieldName, out var yamlName))
        {
            return yamlName;
        }

        return FieldDomainSeedService.GenerateAlias(entityName, fieldName);
    }

    /// <summary>
    /// 判断字段中文名是否仍需人工补全。
    /// </summary>
    public static bool NeedsFieldReview(string? chineseName, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(chineseName))
        {
            return true;
        }

        if (string.Equals(chineseName, fieldName, StringComparison.Ordinal))
        {
            return true;
        }

        if (chineseName.Contains("未映射", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(fieldName, "Id", StringComparison.OrdinalIgnoreCase)
            && string.Equals(chineseName, "ID", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !ContainsChinese(chineseName);
    }

    private static bool ContainsChinese(string value)
        => Regex.IsMatch(value, "[\u4e00-\u9fff]");

    private static bool HasScalarEntityClrType(IEntityType entityType)
        => entityType.ClrType != null
           && entityType.ClrType != typeof(Dictionary<string, object>);

    private static string ResolveEntityDictionaryName(IEntityType entityType)
        => HasScalarEntityClrType(entityType)
            ? entityType.ClrType!.Name
            : entityType.Name;

    /// <summary>
    /// 创建仅用于模型 introspection 的内存 SQLite <see cref="AppDbContext"/>。
    /// </summary>
    public static AppDbContext CreateDesignTimeContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=:memory:");
        return new AppDbContext(optionsBuilder.Options);
    }
}

/// <summary>
/// EF 实体字段快照。
/// </summary>
public sealed record SchemaEntityFieldSnapshot(
    string FieldName,
    string ClrTypeName,
    bool IsNullable);

/// <summary>
/// EF 实体/视图快照。
/// </summary>
public sealed record SchemaEntitySnapshot(
    string EntityName,
    string TableName,
    bool IsView,
    IReadOnlyList<SchemaEntityFieldSnapshot> Fields);

/// <summary>
/// 表级中文元数据快照。
/// </summary>
public sealed record SchemaTableMetadataSnapshot(
    string Description,
    string? Relationships,
    string? MaintenanceNotes);
