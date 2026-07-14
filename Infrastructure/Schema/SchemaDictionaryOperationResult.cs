namespace DocMgr.Infrastructure.Schema;

/// <summary>
/// 数据字典维护操作结果。
/// </summary>
public sealed record SchemaDictionaryOperationResult(
    string DictionaryPath,
    int UpdatedFields,
    int UpdatedTables,
    int CreatedFields,
    int SkippedEntries,
    string Summary);
