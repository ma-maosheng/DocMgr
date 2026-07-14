using DocMgr.Infrastructure.Seeding;
using DocMgr.Infrastructure.Schema;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DocMgr.Services.SystemSettings
{
    public class AdvancedDataService : IAdvancedDataService
    {
        private readonly IAdvancedDataRepository _advancedDataRepository;
        private static readonly HashSet<string> MaintainableEntityWhitelist = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(Department),
            nameof(Cabinet),
            nameof(ProjectInfo),
            nameof(AerialPhoto),
            nameof(TopoMap),
            nameof(YearlyArchiveRegisterRecord),
            nameof(YearlyArchiveRegisterMedia),
            nameof(YearlyArchiveRegisterMediaItem),
            nameof(YearlyArchiveBox),
            nameof(SystemAttachment),
            nameof(FieldDomainDefinition),
            nameof(FieldDomainOption),
            nameof(ToDoReadState),
            nameof(UserPreference)
        };

        public AdvancedDataService(IAdvancedDataRepository advancedDataRepository)
        {
            _advancedDataRepository = advancedDataRepository;
        }

        public List<TableBrowseEntryDto> GetManageableTables()
        {
            var result = new List<TableBrowseEntryDto>();

            var entityTypes = _advancedDataRepository.GetEntityTypes()
                .Where(e => !e.IsOwned() && e.GetTableName() != null)
                .OrderBy(e => e.GetTableName())
                .ThenBy(e => e.Name)
                .ToList();

            var usedDisplayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in entityTypes)
            {
                bool isSharedType = AdvancedDataDictionaryEntitySupport.IsDictionaryBackedEntity(entity);
                bool canMaintain = CanMaintainTable(entity.Name);

                var tableName = entity.GetTableName() ?? entity.Name;
                var displayName = tableName;
                var index = 2;
                while (!usedDisplayNames.Add(displayName))
                {
                    displayName = $"{tableName} #{index++}";
                }

                result.Add(new TableBrowseEntryDto(
                    displayName,
                    entity.Name,
                    tableName,
                    isSharedType,
                    canMaintain));
            }

            return result;
        }

        public bool CanMaintainTable(string entityTypeName)
        {
            if (string.IsNullOrWhiteSpace(entityTypeName))
            {
                return false;
            }

            var entityType = _advancedDataRepository.ResolveEntityType(entityTypeName);
            if (entityType == null || entityType.ClrType == null || entityType.FindPrimaryKey() == null)
            {
                return false;
            }

            if (AdvancedDataDictionaryEntitySupport.IsDictionaryBackedEntity(entityType))
            {
                return false;
            }

            return MaintainableEntityWhitelist.Contains(entityType.ClrType.Name);
        }

        public TableBrowseInfoDto? GetTableBrowseInfo(string entityTypeName)
        {
            if (string.IsNullOrWhiteSpace(entityTypeName))
            {
                return null;
            }

            var entityMeta = _advancedDataRepository.ResolveEntityType(entityTypeName);
            if (entityMeta == null)
            {
                return null;
            }

            var isSharedType = AdvancedDataDictionaryEntitySupport.IsDictionaryBackedEntity(entityMeta);
            var shortName = entityMeta.ClrType != null && !isSharedType
                ? entityMeta.ClrType.Name
                : entityMeta.Name;
            var tableName = entityMeta.GetTableName() ?? shortName;
            var canMaintain = CanMaintainTable(entityTypeName);

            AdvancedDataTableMetadata.TryGet(shortName, out var metadata);

            var chineseName = SchemaDictionaryStore.TryGetTableChineseName(shortName, out var yamlTableName)
                              && !string.IsNullOrWhiteSpace(yamlTableName)
                ? yamlTableName
                : GetEntityDisplayNameZh(shortName, tableName, isSharedType);
            var description = metadata?.Description
                              ?? "暂无详细说明，请参考实体类定义与数据库迁移脚本。";
            var relationships = !string.IsNullOrWhiteSpace(metadata?.Relationships)
                ? metadata.Relationships
                : BuildForeignKeySummary(entityMeta);
            var maintenanceNotes = BuildMaintenanceNotes(canMaintain, metadata?.MaintenanceNotes);

            return new TableBrowseInfoDto(
                tableName,
                chineseName,
                description,
                relationships,
                maintenanceNotes);
        }

        public async Task<DataTable> LoadTableDataAsync(string entityTypeName)
        {
            var entityMeta = _advancedDataRepository.ResolveEntityType(entityTypeName)
                ?? throw new InvalidOperationException($"未找到实体类型: {entityTypeName}");

            var totalCount = await _advancedDataRepository.GetEntityRowCountAsync(entityMeta);
            if (totalCount == 0)
            {
                var properties = GetOrderedProperties(entityMeta);
                return CreateEmptyDataTable(entityMeta, properties);
            }

            var page = await LoadTableDataPageAsync(entityTypeName, 1, totalCount);
            return page.Data;
        }

        public async Task<AdvancedDataTablePageDto> LoadTableDataPageAsync(string entityTypeName, int pageIndex, int pageSize)
        {
            if (pageIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageIndex));
            }

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            var entityMeta = _advancedDataRepository.ResolveEntityType(entityTypeName)
                ?? throw new InvalidOperationException($"未找到实体类型: {entityTypeName}");

            var properties = GetOrderedProperties(entityMeta);
            var table = CreateEmptyDataTable(entityMeta, properties);
            var totalCount = await _advancedDataRepository.GetEntityRowCountAsync(entityMeta);
            if (totalCount == 0)
            {
                return new AdvancedDataTablePageDto(table, 0);
            }

            var skip = (pageIndex - 1) * pageSize;
            if (skip >= totalCount)
            {
                pageIndex = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                skip = (pageIndex - 1) * pageSize;
            }

            var take = pageSize;
            if (skip + take > totalCount)
            {
                take = totalCount - skip;
            }

            var rows = await _advancedDataRepository.GetEntityRowsPagedAsync(entityMeta, skip, take);
            PopulateDataTableRows(table, properties, rows);

            return new AdvancedDataTablePageDto(table, totalCount);
        }

        public async Task DeleteRecordAsync(string entityTypeName, object recordId)
        {
            if (!CanMaintainTable(entityTypeName))
            {
                throw new InvalidOperationException("当前表为只读浏览，不允许删除记录。");
            }

            var entityType = _advancedDataRepository.ResolveEntityType(entityTypeName)
                ?? throw new InvalidOperationException($"未找到实体类型: {entityTypeName}");

            var record = await _advancedDataRepository.FindRecordAsync(entityType, recordId);
            if (record == null)
            {
                throw new InvalidOperationException("记录未找到或已被删除。");
            }

            _advancedDataRepository.RemoveRecord(record);
            await _advancedDataRepository.SaveChangesAsync();
        }

        public async Task ClearTableAsync(string entityTypeName)
        {
            if (!CanMaintainTable(entityTypeName))
            {
                throw new InvalidOperationException("当前表为只读浏览，不允许清空。");
            }

            var entityType = _advancedDataRepository.ResolveEntityType(entityTypeName)
                ?? throw new InvalidOperationException($"未找到实体类型: {entityTypeName}");

            var rows = _advancedDataRepository.GetEntityRows(entityType);
            if (rows.Count > 0)
            {
                _advancedDataRepository.RemoveRecords(rows);
                await _advancedDataRepository.SaveChangesAsync();
            }
        }

        public async Task<List<TableFieldStructureDto>> LoadTableStructureAsync(string entityTypeName)
        {
            var entityMeta = _advancedDataRepository.ResolveEntityType(entityTypeName)
                ?? throw new InvalidOperationException($"未找到实体类型: {entityTypeName}");

            var entityName = AdvancedDataDictionaryEntitySupport.IsDictionaryBackedEntity(entityMeta)
                || entityMeta.ClrType == null
                ? entityMeta.Name
                : entityMeta.ClrType.Name;

            var definitions = await _advancedDataRepository.GetFieldDomainDefinitionsWithOptionsAsync(entityName);

            var definitionMap = definitions.ToDictionary(d => d.FieldName, StringComparer.OrdinalIgnoreCase);
            var properties = entityMeta.GetProperties()
                .OrderBy(GetColumnOrderOrDefault)
                .ThenBy(p => GetBusinessColumnRank(p.Name))
                .ThenBy(p => p.Name)
                .ToList();

            var results = new List<TableFieldStructureDto>(properties.Count);
            foreach (var property in properties)
            {
                definitionMap.TryGetValue(property.Name, out var definition);
                var alias = definition?.DisplayName;
                if (!FieldDomainSeedService.IsCompliantAlias(alias, property.Name))
                {
                    alias = FieldDomainSeedService.GenerateAlias(entityName, property.Name);
                }

                results.Add(new TableFieldStructureDto(
                    entityName,
                    property.Name,
                    GetDisplayTypeName(property),
                    property.IsNullable,
                    CanConfigureDomain(property),
                    definition?.Id,
                    alias ?? property.Name,
                    definition?.IsDomainEnabled ?? false,
                    definition?.Options.Count(o => o.IsEnabled) ?? 0));
            }

            return results;
        }

        public async Task<FieldDomainDefinitionDto?> GetFieldDomainDefinitionAsync(string entityName, string fieldName)
        {
            ValidateEntityAndField(entityName, fieldName);

            var definition = await _advancedDataRepository.GetFieldDomainDefinitionAsync(entityName, fieldName);

            return definition == null ? null : ToDto(definition);
        }

        public async Task<FieldDomainDefinitionDto> SaveFieldDomainDefinitionAsync(string entityName, string fieldName, string displayName, bool isDomainEnabled)
        {
            ValidateEntityAndField(entityName, fieldName);

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("字段显示名不能为空。", nameof(displayName));
            }

            var definition = await _advancedDataRepository.GetFieldDomainDefinitionAsync(entityName, fieldName);

            if (definition == null)
            {
                int maxSort = await _advancedDataRepository.GetMaxFieldDomainSortOrderAsync(entityName) ?? 0;

                definition = new FieldDomainDefinition
                {
                    EntityName = entityName,
                    FieldName = fieldName,
                    DisplayName = displayName.Trim(),
                    Description = string.Empty,
                    IsDomainEnabled = isDomainEnabled,
                    SortOrder = maxSort + 10
                };

                _advancedDataRepository.AddFieldDomainDefinition(definition);
            }
            else
            {
                definition.DisplayName = displayName.Trim();
                definition.IsDomainEnabled = isDomainEnabled;
            }

            await _advancedDataRepository.SaveChangesAsync();
            return ToDto(definition);
        }

        public string BuildExportFileName(string entityTypeName)
        {
            var browseInfo = GetTableBrowseInfo(entityTypeName);
            var tableName = browseInfo?.TableName ?? entityTypeName;
            var chineseName = browseInfo?.ChineseName ?? entityTypeName;
            var baseName = $"{tableName}_{chineseName}";
            return $"{AdvancedDataExcelExportSupport.SanitizeFileName(baseName)}.xlsx";
        }

        public async Task ExportTableToExcelAsync(string filePath, string entityTypeName, int? maxRowCount)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("导出文件路径不能为空。", nameof(filePath));
            }

            var directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("导出文件目录无效。", nameof(filePath));
            }

            if (maxRowCount is <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRowCount));
            }

            var entityMeta = _advancedDataRepository.ResolveEntityType(entityTypeName)
                ?? throw new InvalidOperationException($"未找到实体类型: {entityTypeName}");

            var fields = await LoadTableStructureAsync(entityTypeName);
            var totalCount = await _advancedDataRepository.GetEntityRowCountAsync(entityMeta);
            var exportCount = maxRowCount.HasValue
                ? Math.Min(maxRowCount.Value, totalCount)
                : totalCount;

            DataTable data;
            if (exportCount == 0)
            {
                data = CreateEmptyDataTable(entityMeta, GetOrderedProperties(entityMeta));
            }
            else
            {
                var page = await LoadTableDataPageAsync(entityTypeName, 1, exportCount);
                data = page.Data;
            }

            var sheetName = entityMeta.GetTableName() ?? entityTypeName;
            await Task.Run(() => AdvancedDataExcelExportSupport.Write(filePath, sheetName, fields, data));
        }

        public async Task<FieldDomainDefinitionDto> SaveFieldDisplayNameAsync(string entityName, string fieldName, string displayName)
        {
            ValidateEntityAndField(entityName, fieldName);

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("字段显示名不能为空。", nameof(displayName));
            }

            var definition = await _advancedDataRepository.GetFieldDomainDefinitionAsync(entityName, fieldName);
            var isDomainEnabled = definition?.IsDomainEnabled ?? false;
            return await SaveFieldDomainDefinitionAsync(entityName, fieldName, displayName, isDomainEnabled);
        }

        public async Task<List<FieldDomainOptionDto>> GetFieldDomainOptionsAsync(int definitionId)
        {
            if (definitionId <= 0)
            {
                throw new ArgumentException("字段定义 Id 非法。", nameof(definitionId));
            }

            var options = await _advancedDataRepository.GetFieldDomainOptionsAsync(definitionId);
            return options
                .Select(o => new FieldDomainOptionDto(o.Id, o.FieldDomainDefinitionId, o.Scope, o.OptionValue, o.OptionLabel, o.IsEnabled, o.SortOrder))
                .ToList();
        }

        public async Task<FieldDomainOptionDto> SaveFieldDomainOptionAsync(int definitionId, int? optionId, string scope, string optionValue, string optionLabel, bool isEnabled, int sortOrder)
        {
            if (definitionId <= 0)
            {
                throw new ArgumentException("字段定义 Id 非法。", nameof(definitionId));
            }

            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (string.IsNullOrWhiteSpace(optionValue))
            {
                throw new ArgumentException("域值不能为空。", nameof(optionValue));
            }

            if (string.IsNullOrWhiteSpace(optionLabel))
            {
                throw new ArgumentException("显示名称不能为空。", nameof(optionLabel));
            }

            bool existsDefinition = await _advancedDataRepository.ExistsFieldDomainDefinitionAsync(definitionId);
            if (!existsDefinition)
            {
                throw new InvalidOperationException("字段定义不存在，无法保存域值。");
            }

            var normalizedScope = scope.Trim();
            var normalizedValue = optionValue.Trim();
            var normalizedLabel = optionLabel.Trim();
            FieldDomainOption entity;

            if (optionId.HasValue && optionId.Value > 0)
            {
                entity = await _advancedDataRepository.GetFieldDomainOptionAsync(optionId.Value, definitionId)
                    ?? throw new InvalidOperationException("域值记录不存在，可能已被删除。");
            }
            else
            {
                bool duplicate = await _advancedDataRepository.ExistsDuplicateFieldDomainOptionAsync(definitionId, normalizedScope, normalizedValue);
                if (duplicate)
                {
                    throw new InvalidOperationException("同一字段同一作用域下已存在相同域值，请勿重复添加。");
                }

                entity = new FieldDomainOption { FieldDomainDefinitionId = definitionId };
                _advancedDataRepository.AddFieldDomainOption(entity);
            }

            entity.Scope = normalizedScope;
            entity.OptionValue = normalizedValue;
            entity.OptionLabel = normalizedLabel;
            entity.IsEnabled = isEnabled;
            entity.SortOrder = sortOrder;

            await _advancedDataRepository.SaveChangesAsync();

            return new FieldDomainOptionDto(entity.Id, entity.FieldDomainDefinitionId, entity.Scope, entity.OptionValue, entity.OptionLabel, entity.IsEnabled, entity.SortOrder);
        }

        public async Task DeleteFieldDomainOptionAsync(int optionId)
        {
            if (optionId <= 0)
            {
                throw new ArgumentException("域值 Id 非法。", nameof(optionId));
            }

            var option = await _advancedDataRepository.GetFieldDomainOptionByIdAsync(optionId)
                ?? throw new InvalidOperationException("域值记录不存在，可能已被删除。");

            _advancedDataRepository.RemoveFieldDomainOption(option);
            await _advancedDataRepository.SaveChangesAsync();
        }

        public async Task<List<string>> GetEnabledDomainValuesAsync(string entityName, string fieldName, string? scope = null)
        {
            ValidateEntityAndField(entityName, fieldName);

            var definition = await _advancedDataRepository.GetEnabledFieldDomainDefinitionAsync(entityName, fieldName);

            if (definition == null)
            {
                return new List<string>();
            }

            return await _advancedDataRepository.GetEnabledFieldDomainValuesAsync(definition.Id, scope?.Trim());
        }

        private static bool CanConfigureDomain(IProperty property)
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            return type == typeof(string) || type == typeof(int) || type == typeof(long);
        }

        private static string GetDisplayTypeName(IProperty property)
        {
            var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            var typeName = clrType.Name;
            return property.IsNullable ? $"{typeName}?" : typeName;
        }

        private static int GetColumnOrderOrDefault(IProperty property)
        {
            var annotation = property.FindAnnotation("Relational:ColumnOrder");
            return annotation?.Value is int order ? order : int.MaxValue;
        }

        private static int GetBusinessColumnRank(string propertyName)
        {
            if (string.Equals(propertyName, "Id", StringComparison.OrdinalIgnoreCase)) return 0;
            if (propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)) return 10;
            if (propertyName.EndsWith("No", StringComparison.OrdinalIgnoreCase) || propertyName.EndsWith("Code", StringComparison.OrdinalIgnoreCase)) return 20;
            if (propertyName.Contains("Name", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Type", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Status", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Kind", StringComparison.OrdinalIgnoreCase)) return 30;
            if (propertyName.Contains("Count", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Row", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Column", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Index", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Width", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Height", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Size", StringComparison.OrdinalIgnoreCase)) return 40;
            if (propertyName.Contains("Date", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Time", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("At", StringComparison.OrdinalIgnoreCase)) return 80;
            if (propertyName.Contains("Remark", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Description", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Note", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Other", StringComparison.OrdinalIgnoreCase)) return 90;
            return 50;
        }

        private static FieldDomainDefinitionDto ToDto(FieldDomainDefinition definition)
            => new(definition.Id, definition.EntityName, definition.FieldName, definition.DisplayName, definition.Description, definition.IsDomainEnabled, definition.SortOrder);

        private static void ValidateEntityAndField(string entityName, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(entityName)) throw new ArgumentException("实体名称不能为空。", nameof(entityName));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException("字段名称不能为空。", nameof(fieldName));
        }

        private static List<IProperty> GetOrderedProperties(IEntityType entityMeta)
            => entityMeta.GetProperties()
                .OrderBy(GetColumnOrderOrDefault)
                .ThenBy(p => GetBusinessColumnRank(p.Name))
                .ThenBy(p => p.Name)
                .ToList();

        private static DataTable CreateEmptyDataTable(IEntityType entityMeta, IReadOnlyList<IProperty> properties)
        {
            var table = new DataTable(entityMeta.GetTableName() ?? entityMeta.Name);
            foreach (var property in properties)
            {
                var columnType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (columnType.IsEnum)
                {
                    columnType = typeof(string);
                }

                table.Columns.Add(property.Name, columnType);
            }

            return table;
        }

        private static void PopulateDataTableRows(DataTable table, IReadOnlyList<IProperty> properties, IEnumerable<object> rows)
        {
            foreach (var item in rows)
            {
                var row = table.NewRow();
                foreach (var property in properties)
                {
                    var value = TryReadPropertyValue(item, property);
                    row[property.Name] = value ?? DBNull.Value;
                }

                table.Rows.Add(row);
            }
        }

        private static object? TryReadPropertyValue(object item, IProperty property)
            => AdvancedDataDictionaryEntitySupport.TryReadPropertyValue(item, property);

        private static string GetEntityDisplayNameZh(string shortName, string tableName, bool isSharedType)
        {
            if (isSharedType)
            {
                return $"共享映射:{tableName}";
            }

            if (SchemaDictionaryStore.TryGetTableChineseName(shortName, out var yamlTableName)
                && !string.IsNullOrWhiteSpace(yamlTableName))
            {
                return yamlTableName;
            }

            return shortName;
        }

        private static string BuildForeignKeySummary(IEntityType entityType)
        {
            var lines = new List<string>();

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                var principalTable = foreignKey.PrincipalEntityType.GetTableName()
                                     ?? foreignKey.PrincipalEntityType.ClrType?.Name
                                     ?? foreignKey.PrincipalEntityType.Name;
                var dependentColumns = string.Join(", ", foreignKey.Properties.Select(property => property.Name));
                lines.Add($"→ 引用 {principalTable}（外键：{dependentColumns}）");
            }

            foreach (var referencingForeignKey in entityType.GetReferencingForeignKeys())
            {
                var dependentTable = referencingForeignKey.DeclaringEntityType.GetTableName()
                                     ?? referencingForeignKey.DeclaringEntityType.ClrType?.Name
                                     ?? referencingForeignKey.DeclaringEntityType.Name;
                var dependentColumns = string.Join(", ", referencingForeignKey.Properties.Select(property => property.Name));
                lines.Add($"← 被 {dependentTable} 引用（外键：{dependentColumns}）");
            }

            return lines.Count > 0
                ? string.Join("；", lines)
                : "无外键关联（独立表或仅逻辑关联）";
        }

        private static string BuildMaintenanceNotes(bool canMaintain, string? customNotes)
        {
            if (!string.IsNullOrWhiteSpace(customNotes))
            {
                return customNotes;
            }

            return canMaintain
                ? "可维护：允许删除单条记录或清空整表，操作不可逆，请注意外键依赖顺序。"
                : "只读浏览：不允许删除或清空，仅供数据排查与架构理解。";
        }
    }
}