namespace DocMgr.Infrastructure.Schema;

/// <summary>
/// SchemaDictionary 与 EF 模型同步结果。
/// </summary>
public sealed class SchemaDictionarySyncResult
{
    public required SchemaDictionaryDocument Document { get; init; }

    public int AddedTables { get; init; }

    public int AddedFields { get; init; }

    public int DeprecatedTables { get; init; }

    public IReadOnlyList<string> NeedsReviewFields { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 将 EF 模型快照合并进 SchemaDictionary.yaml，不覆盖已有中文名。
/// </summary>
public static class SchemaDictionarySyncService
{
    /// <summary>
    /// 合并 EF 快照到字典文档。
    /// </summary>
    public static SchemaDictionarySyncResult Sync(
        IReadOnlyList<SchemaEntitySnapshot> snapshots,
        SchemaDictionaryDocument document)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(document);

        document.Tables ??= new Dictionary<string, SchemaDictionaryTableEntry>(StringComparer.Ordinal);

        var snapshotMap = snapshots.ToDictionary(snapshot => snapshot.EntityName, StringComparer.Ordinal);
        int addedTables = 0;
        int addedFields = 0;
        var needsReview = new List<string>();

        foreach (var snapshot in snapshots)
        {
            if (!document.Tables.TryGetValue(snapshot.EntityName, out var tableEntry))
            {
                tableEntry = CreateTableEntry(snapshot);
                document.Tables[snapshot.EntityName] = tableEntry;
                addedTables++;
            }
            else
            {
                MergeTableEntry(tableEntry, snapshot);
            }

            tableEntry.Deprecated = false;
            tableEntry.IsView = snapshot.IsView;

            if (string.IsNullOrWhiteSpace(tableEntry.TableName))
            {
                tableEntry.TableName = snapshot.TableName;
            }

            if (string.IsNullOrWhiteSpace(tableEntry.ChineseName))
            {
                tableEntry.ChineseName = ResolveDefaultTableChineseName(snapshot);
            }

            if (string.IsNullOrWhiteSpace(tableEntry.Description)
                && SchemaDictionaryCatalog.TryGetTableMetadata(snapshot.EntityName, out var metadata))
            {
                tableEntry.Description = metadata.Description;
            }

            tableEntry.Fields ??= new Dictionary<string, SchemaDictionaryFieldEntry>(StringComparer.Ordinal);
            var snapshotFieldNames = snapshot.Fields
                .Select(field => field.FieldName)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var fieldSnapshot in snapshot.Fields)
            {
                if (!tableEntry.Fields.TryGetValue(fieldSnapshot.FieldName, out var fieldEntry))
                {
                    fieldEntry = CreateFieldEntry(snapshot.EntityName, fieldSnapshot);
                    tableEntry.Fields[fieldSnapshot.FieldName] = fieldEntry;
                    addedFields++;
                }
                else if (string.IsNullOrWhiteSpace(fieldEntry.ChineseName))
                {
                    fieldEntry.ChineseName = SchemaDictionaryCatalog.ResolveFieldChineseNameForSync(
                        snapshot.EntityName,
                        fieldSnapshot.FieldName);
                }

                if (string.IsNullOrWhiteSpace(fieldEntry.ClrType))
                {
                    fieldEntry.ClrType = fieldSnapshot.ClrTypeName;
                }

                fieldEntry.NeedsReview = SchemaDictionaryCatalog.NeedsFieldReview(
                    fieldEntry.ChineseName,
                    fieldSnapshot.FieldName);

                if (fieldEntry.NeedsReview)
                {
                    needsReview.Add($"{snapshot.EntityName}.{fieldSnapshot.FieldName}");
                }
            }

            ReorderTableFields(tableEntry, snapshot);

            foreach (var staleFieldName in tableEntry.Fields.Keys.ToList())
            {
                if (!snapshotFieldNames.Contains(staleFieldName))
                {
                    tableEntry.Fields.Remove(staleFieldName);
                }
            }
        }

        int deprecatedTables = 0;
        foreach (var tableName in document.Tables.Keys.ToList())
        {
            if (snapshotMap.ContainsKey(tableName))
            {
                continue;
            }

            document.Tables[tableName].Deprecated = true;
            deprecatedTables++;
        }

        needsReview.Sort(StringComparer.Ordinal);
        return new SchemaDictionarySyncResult
        {
            Document = document,
            AddedTables = addedTables,
            AddedFields = addedFields,
            DeprecatedTables = deprecatedTables,
            NeedsReviewFields = needsReview
        };
    }

    private static SchemaDictionaryTableEntry CreateTableEntry(SchemaEntitySnapshot snapshot)
    {
        var entry = new SchemaDictionaryTableEntry
        {
            TableName = snapshot.TableName,
            ChineseName = ResolveDefaultTableChineseName(snapshot),
            IsView = snapshot.IsView,
            Deprecated = false
        };

        if (SchemaDictionaryCatalog.TryGetTableMetadata(snapshot.EntityName, out var metadata))
        {
            entry.Description = metadata.Description;
        }

        entry.Fields = snapshot.Fields.ToDictionary(
            field => field.FieldName,
            field => CreateFieldEntry(snapshot.EntityName, field),
            StringComparer.Ordinal);

        return entry;
    }

    private static void MergeTableEntry(SchemaDictionaryTableEntry entry, SchemaEntitySnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(entry.TableName))
        {
            entry.TableName = snapshot.TableName;
        }
    }

    private static SchemaDictionaryFieldEntry CreateFieldEntry(
        string entityName,
        SchemaEntityFieldSnapshot fieldSnapshot)
    {
        var chineseName = SchemaDictionaryCatalog.ResolveFieldChineseNameForSync(entityName, fieldSnapshot.FieldName);
        return new SchemaDictionaryFieldEntry
        {
            ChineseName = chineseName,
            ClrType = fieldSnapshot.ClrTypeName,
            NeedsReview = SchemaDictionaryCatalog.NeedsFieldReview(chineseName, fieldSnapshot.FieldName)
        };
    }

    private static string ResolveDefaultTableChineseName(SchemaEntitySnapshot snapshot)
    {
        if (SchemaDictionaryStore.TryGetTableChineseName(snapshot.EntityName, out var yamlName)
            && !string.IsNullOrWhiteSpace(yamlName)
            && !string.Equals(yamlName, snapshot.EntityName, StringComparison.Ordinal))
        {
            return yamlName;
        }

        return snapshot.IsView
            ? $"{snapshot.EntityName}（视图）"
            : snapshot.EntityName;
    }

    private static void ReorderTableFields(SchemaDictionaryTableEntry tableEntry, SchemaEntitySnapshot snapshot)
    {
        if (tableEntry.Fields == null || tableEntry.Fields.Count == 0)
        {
            return;
        }

        var reordered = new Dictionary<string, SchemaDictionaryFieldEntry>(StringComparer.Ordinal);
        foreach (var fieldSnapshot in snapshot.Fields)
        {
            if (tableEntry.Fields.TryGetValue(fieldSnapshot.FieldName, out var fieldEntry))
            {
                reordered[fieldSnapshot.FieldName] = fieldEntry;
            }
        }

        foreach (var (fieldName, fieldEntry) in tableEntry.Fields)
        {
            if (!reordered.ContainsKey(fieldName))
            {
                reordered[fieldName] = fieldEntry;
            }
        }

        tableEntry.Fields = reordered;
    }
}
