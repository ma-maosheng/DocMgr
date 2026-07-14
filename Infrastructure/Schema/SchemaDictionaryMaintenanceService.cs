using DocMgr.Data;
using DocMgr.Infrastructure.Seeding;
using DocMgr.Models.SystemSettings;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace DocMgr.Infrastructure.Schema;

/// <summary>
/// SchemaDictionary.yaml 与 FieldDomainDefinitions 的双向维护。
/// </summary>
public interface ISchemaDictionaryMaintenanceService
{
    /// <summary>
    /// 当前生效的字典文件路径。
    /// </summary>
    string GetActiveDictionaryPath();

    /// <summary>
    /// 将 EF 模型结构合并进字典（不覆盖已有中文名）。
    /// </summary>
    Task<SchemaDictionaryOperationResult> SyncModelToDictionaryAsync(string targetDictionaryPath);

    /// <summary>
    /// 将数据库中的字段显示名导出合并进字典。
    /// </summary>
    Task<SchemaDictionaryOperationResult> ExportDatabaseDisplayNamesToDictionaryAsync(string targetDictionaryPath);

    /// <summary>
    /// 用字典中的字段显示名重置数据库 FieldDomainDefinitions。
    /// </summary>
    Task<SchemaDictionaryOperationResult> ApplyDictionaryDisplayNamesToDatabaseAsync(string? dictionaryPath = null);

    /// <summary>
    /// 将单个字段显示名写回字典文件。
    /// </summary>
    Task MergeFieldDisplayNameToDictionaryAsync(
        string entityName,
        string fieldName,
        string displayName,
        string? targetDictionaryPath = null);
}

/// <summary>
/// <see cref="ISchemaDictionaryMaintenanceService"/> 默认实现。
/// </summary>
public sealed class SchemaDictionaryMaintenanceService : ISchemaDictionaryMaintenanceService
{
    private readonly AppDbContext _dbContext;

    public SchemaDictionaryMaintenanceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public string GetActiveDictionaryPath()
        => SchemaDictionaryPathSupport.GetActiveDictionaryPath();

    /// <inheritdoc />
    public Task<SchemaDictionaryOperationResult> SyncModelToDictionaryAsync(string targetDictionaryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDictionaryPath);

        var snapshots = SchemaDictionaryCatalog.GetEntitySnapshots(_dbContext);
        var document = SchemaDictionaryYaml.LoadOrCreate(targetDictionaryPath);
        var syncResult = SchemaDictionarySyncService.Sync(snapshots, document);

        SchemaDictionaryYaml.Save(targetDictionaryPath, syncResult.Document);
        PublishDictionaryArtifacts(targetDictionaryPath, syncResult.Document);
        SchemaDictionaryStore.Reload();

        return Task.FromResult(new SchemaDictionaryOperationResult(
            targetDictionaryPath,
            syncResult.AddedFields,
            syncResult.AddedTables,
            0,
            0,
            $"已同步 EF 模型：新增 {syncResult.AddedTables} 表、{syncResult.AddedFields} 字段；待补全 {syncResult.NeedsReviewFields.Count} 项。"));
    }

    /// <inheritdoc />
    public async Task<SchemaDictionaryOperationResult> ExportDatabaseDisplayNamesToDictionaryAsync(string targetDictionaryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDictionaryPath);

        var snapshots = SchemaDictionaryCatalog.GetEntitySnapshots(_dbContext);
        var document = SchemaDictionaryYaml.LoadOrCreate(targetDictionaryPath);
        document = SchemaDictionarySyncService.Sync(snapshots, document).Document;

        var definitions = await _dbContext.FieldDomainDefinitions
            .AsNoTracking()
            .ToListAsync();

        int updatedFields = 0;
        int skippedEntries = 0;

        foreach (var definition in definitions)
        {
            if (!FieldDomainSeedService.IsCompliantAlias(definition.DisplayName, definition.FieldName))
            {
                skippedEntries++;
                continue;
            }

            if (!document.Tables.TryGetValue(definition.EntityName, out var tableEntry))
            {
                skippedEntries++;
                continue;
            }

            tableEntry.Fields ??= new Dictionary<string, SchemaDictionaryFieldEntry>(StringComparer.Ordinal);
            if (!tableEntry.Fields.TryGetValue(definition.FieldName, out var fieldEntry))
            {
                fieldEntry = new SchemaDictionaryFieldEntry();
                tableEntry.Fields[definition.FieldName] = fieldEntry;
            }

            var trimmedDisplayName = definition.DisplayName.Trim();
            if (string.Equals(fieldEntry.ChineseName, trimmedDisplayName, StringComparison.Ordinal))
            {
                continue;
            }

            fieldEntry.ChineseName = trimmedDisplayName;
            fieldEntry.NeedsReview = SchemaDictionaryCatalog.NeedsFieldReview(trimmedDisplayName, definition.FieldName);
            updatedFields++;
        }

        SchemaDictionaryYaml.Save(targetDictionaryPath, document);
        PublishDictionaryArtifacts(targetDictionaryPath, document);
        SchemaDictionaryStore.Reload();

        return new SchemaDictionaryOperationResult(
            targetDictionaryPath,
            updatedFields,
            0,
            0,
            skippedEntries,
            $"已从数据库导出 {updatedFields} 个字段显示名到字典；跳过 {skippedEntries} 项。");
    }

    /// <inheritdoc />
    public async Task<SchemaDictionaryOperationResult> ApplyDictionaryDisplayNamesToDatabaseAsync(string? dictionaryPath = null)
    {
        var resolvedPath = string.IsNullOrWhiteSpace(dictionaryPath)
            ? SchemaDictionaryPathSupport.ResolvePreferredWritableDictionaryPath()
            : dictionaryPath;
        var document = SchemaDictionaryYaml.LoadOrCreate(resolvedPath);

        int updatedFields = 0;
        int createdFields = 0;
        int skippedEntries = 0;

        foreach (var (entityName, tableEntry) in document.Tables)
        {
            if (tableEntry.Deprecated)
            {
                continue;
            }

            foreach (var (fieldName, fieldEntry) in tableEntry.Fields)
            {
                if (string.IsNullOrWhiteSpace(fieldEntry.ChineseName))
                {
                    skippedEntries++;
                    continue;
                }

                var trimmedDisplayName = fieldEntry.ChineseName.Trim();
                var definition = await _dbContext.FieldDomainDefinitions
                    .FirstOrDefaultAsync(item => item.EntityName == entityName && item.FieldName == fieldName);

                if (definition == null)
                {
                    int maxSort = await _dbContext.FieldDomainDefinitions
                        .Where(item => item.EntityName == entityName)
                        .Select(item => (int?)item.SortOrder)
                        .MaxAsync() ?? 0;

                    definition = new FieldDomainDefinition
                    {
                        EntityName = entityName,
                        FieldName = fieldName,
                        DisplayName = trimmedDisplayName,
                        Description = string.IsNullOrWhiteSpace(fieldEntry.Description)
                            ? "由数据字典导入的字段显示名"
                            : fieldEntry.Description.Trim(),
                        IsDomainEnabled = false,
                        SortOrder = maxSort + 10
                    };

                    _dbContext.FieldDomainDefinitions.Add(definition);
                    createdFields++;
                    continue;
                }

                if (string.Equals(definition.DisplayName, trimmedDisplayName, StringComparison.Ordinal))
                {
                    continue;
                }

                definition.DisplayName = trimmedDisplayName;
                updatedFields++;
            }
        }

        if (createdFields > 0 || updatedFields > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        SchemaDictionaryStore.Reload();
        SyncRuntimeDictionaryCopy(resolvedPath, document);

        return new SchemaDictionaryOperationResult(
            resolvedPath,
            updatedFields,
            0,
            createdFields,
            skippedEntries,
            $"已从字典重置显示名：更新 {updatedFields} 项，新建 {createdFields} 项；跳过 {skippedEntries} 项。");
    }

    /// <inheritdoc />
    public Task MergeFieldDisplayNameToDictionaryAsync(
        string entityName,
        string fieldName,
        string displayName,
        string? targetDictionaryPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("字段显示名不能为空。", nameof(displayName));
        }

        if (!FieldDomainSeedService.IsCompliantAlias(displayName, fieldName))
        {
            return Task.CompletedTask;
        }

        var dictionaryPath = string.IsNullOrWhiteSpace(targetDictionaryPath)
            ? SchemaDictionaryPathSupport.ResolvePreferredWritableDictionaryPath()
            : targetDictionaryPath;

        var snapshots = SchemaDictionaryCatalog.GetEntitySnapshots(_dbContext);
        var document = SchemaDictionaryYaml.LoadOrCreate(dictionaryPath);
        document = SchemaDictionarySyncService.Sync(snapshots, document).Document;

        if (!document.Tables.TryGetValue(entityName, out var tableEntry))
        {
            return Task.CompletedTask;
        }

        tableEntry.Fields ??= new Dictionary<string, SchemaDictionaryFieldEntry>(StringComparer.Ordinal);
        if (!tableEntry.Fields.TryGetValue(fieldName, out var fieldEntry))
        {
            fieldEntry = new SchemaDictionaryFieldEntry();
            tableEntry.Fields[fieldName] = fieldEntry;
        }

        var trimmedDisplayName = displayName.Trim();
        fieldEntry.ChineseName = trimmedDisplayName;
        fieldEntry.NeedsReview = SchemaDictionaryCatalog.NeedsFieldReview(trimmedDisplayName, fieldName);

        SchemaDictionaryYaml.Save(dictionaryPath, document);
        PublishDictionaryArtifacts(dictionaryPath, document);
        SchemaDictionaryStore.Reload();

        return Task.CompletedTask;
    }

    private void PublishDictionaryArtifacts(string targetDictionaryPath, SchemaDictionaryDocument document)
    {
        SyncRuntimeDictionaryCopy(targetDictionaryPath, document);

        if (SchemaDictionaryPathSupport.TryResolveDevelopmentRulePath(out var rulePath))
        {
            var snapshotMap = SchemaDictionaryCatalog.GetEntitySnapshots(_dbContext)
                .ToDictionary(snapshot => snapshot.EntityName, StringComparer.Ordinal);
            File.WriteAllText(rulePath, SchemaDictionaryRuleGenerator.Generate(document, snapshotMap));
        }
    }

    private static void SyncRuntimeDictionaryCopy(string sourceDictionaryPath, SchemaDictionaryDocument document)
    {
        var runtimeCopyPath = SchemaDictionaryPathSupport.GetRuntimeDictionaryCopyPath();
        if (string.Equals(
                Path.GetFullPath(sourceDictionaryPath),
                Path.GetFullPath(runtimeCopyPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SchemaDictionaryYaml.Save(runtimeCopyPath, document);
    }
}
