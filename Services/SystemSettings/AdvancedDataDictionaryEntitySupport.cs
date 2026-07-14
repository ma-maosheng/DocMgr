using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DocMgr.Services.SystemSettings
{
    /// <summary>
    /// EF Core 隐式中间表（Dictionary 行）在高级数据管理中的读取辅助。
    /// </summary>
    internal static class AdvancedDataDictionaryEntitySupport
    {
        /// <summary>
        /// 判断实体是否以 <see cref="Dictionary{TKey, TValue}"/> 行形式存储（含无 CLR 类型的隐式关联表）。
        /// </summary>
        public static bool IsDictionaryBackedEntity(IEntityType entityType)
        {
            if (entityType.ClrType == typeof(Dictionary<string, object>))
            {
                return true;
            }

            if (entityType.Name.Contains("Dictionary<string, object>", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return entityType.ClrType == null
                   && !entityType.IsOwned()
                   && entityType.GetTableName() != null;
        }

        /// <summary>
        /// 从实体实例或 Dictionary 行读取字段值。
        /// </summary>
        public static object? TryReadPropertyValue(object item, IProperty property)
        {
            if (TryReadDictionaryValue(item, property.Name, out var dictionaryValue))
            {
                return dictionaryValue;
            }

            var propertyInfo = property.PropertyInfo;
            if (propertyInfo != null && propertyInfo.GetIndexParameters().Length == 0)
            {
                return propertyInfo.GetValue(item);
            }

            return null;
        }

        /// <summary>
        /// 判断 EF 属性是否映射为 Dictionary 索引器（不能直接 GetValue）。
        /// </summary>
        public static bool IsDictionaryIndexerProperty(IProperty property)
            => property.PropertyInfo?.GetIndexParameters().Length > 0;

        private static bool TryReadDictionaryValue(object item, string propertyName, out object? value)
        {
            value = null;

            if (item is IReadOnlyDictionary<string, object?> readOnlyNullable
                && readOnlyNullable.TryGetValue(propertyName, out value))
            {
                return true;
            }

            if (item is IReadOnlyDictionary<string, object> readOnly
                && readOnly.TryGetValue(propertyName, out value))
            {
                return true;
            }

            if (item is IDictionary<string, object?> mutableNullable
                && mutableNullable.TryGetValue(propertyName, out value))
            {
                return true;
            }

            if (item is IDictionary<string, object> mutable
                && mutable.TryGetValue(propertyName, out value))
            {
                return true;
            }

            return false;
        }
    }
}
