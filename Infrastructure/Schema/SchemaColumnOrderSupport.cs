using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DocMgr.Infrastructure.Schema;

/// <summary>
/// 按实体类属性声明顺序（与数据库列物理顺序一致）排列 EF 属性。
/// </summary>
public static class SchemaColumnOrderSupport
{
    /// <summary>
    /// 返回与数据库列顺序一致的属性列表（排除 shadow 属性）。
    /// </summary>
    public static IReadOnlyList<IProperty> OrderProperties(IEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        var mappedProperties = entityType.GetProperties()
            .Where(property => !property.IsShadowProperty())
            .ToDictionary(property => property.Name, StringComparer.Ordinal);

        if (entityType.ClrType == null)
        {
            return FallbackOrder(mappedProperties.Values);
        }

        var ordered = new List<IProperty>();
        foreach (var clrProperty in entityType.ClrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (mappedProperties.Remove(clrProperty.Name, out var efProperty))
            {
                ordered.Add(efProperty);
            }
        }

        if (mappedProperties.Count > 0)
        {
            ordered.AddRange(FallbackOrder(mappedProperties.Values));
        }

        return ordered;
    }

    private static List<IProperty> FallbackOrder(IEnumerable<IProperty> properties)
        => properties
            .OrderBy(property =>
            {
                var annotation = property.FindAnnotation("Relational:ColumnOrder");
                return annotation?.Value is int order ? order : int.MaxValue;
            })
            .ThenBy(property => property.Name, StringComparer.Ordinal)
            .ToList();
}
