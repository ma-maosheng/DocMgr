namespace DocMgr.Infrastructure.Schema;

/// <summary>
/// SchemaDictionary.yaml 根文档。
/// </summary>
public sealed class SchemaDictionaryDocument
{
    public int Version { get; set; } = 1;

    public string? GeneratedAt { get; set; }

    public Dictionary<string, SchemaDictionaryTableEntry> Tables { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// 表级字典条目。
/// </summary>
public sealed class SchemaDictionaryTableEntry
{
    public string TableName { get; set; } = string.Empty;

    public string ChineseName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsView { get; set; }

    public bool Deprecated { get; set; }

    public List<string> Aliases { get; set; } = new();

    public Dictionary<string, SchemaDictionaryFieldEntry> Fields { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// 字段级字典条目。
/// </summary>
public sealed class SchemaDictionaryFieldEntry
{
    public string ChineseName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ClrType { get; set; } = string.Empty;

    public bool NeedsReview { get; set; }

    public List<string> Aliases { get; set; } = new();
}
