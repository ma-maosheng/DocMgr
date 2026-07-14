using System.IO;

namespace DocMgr.Infrastructure.Schema;

/// <summary>
/// 运行时 SchemaDictionary 缓存；供 UI、Seed 与业务层读取中英对照。
/// </summary>
public static class SchemaDictionaryStore
{
    private static readonly object SyncRoot = new();
    private static SchemaDictionaryDocument? _cachedDocument;
    private static string? _cachedPath;

    /// <summary>
    /// 清除缓存，下次读取时重新加载文件。
    /// </summary>
    public static void Reload()
    {
        lock (SyncRoot)
        {
            _cachedDocument = null;
            _cachedPath = null;
        }
    }

    /// <summary>
    /// 尝试读取表中文名。
    /// </summary>
    public static bool TryGetTableChineseName(string entityName, out string chineseName)
    {
        chineseName = string.Empty;
        if (string.IsNullOrWhiteSpace(entityName))
        {
            return false;
        }

        var document = GetDocument();
        if (!document.Tables.TryGetValue(entityName, out var table)
            || table.Deprecated
            || string.IsNullOrWhiteSpace(table.ChineseName))
        {
            return false;
        }

        chineseName = table.ChineseName.Trim();
        return true;
    }

    /// <summary>
    /// 尝试读取字段中文名。
    /// </summary>
    public static bool TryGetFieldChineseName(string entityName, string fieldName, out string chineseName)
    {
        chineseName = string.Empty;
        if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        var document = GetDocument();
        if (!document.Tables.TryGetValue(entityName, out var table)
            || table.Deprecated
            || !table.Fields.TryGetValue(fieldName, out var field)
            || string.IsNullOrWhiteSpace(field.ChineseName))
        {
            return false;
        }

        chineseName = field.ChineseName.Trim();
        return true;
    }

    /// <summary>
    /// 解析字典文件路径：开发仓库中的 .cursor/schema 优先，否则使用输出目录副本。
    /// </summary>
    public static string ResolveDictionaryPath()
        => SchemaDictionaryPathSupport.ResolvePreferredWritableDictionaryPath();

    private static SchemaDictionaryDocument GetDocument()
    {
        var path = ResolveDictionaryPath();
        lock (SyncRoot)
        {
            if (_cachedDocument != null && string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return _cachedDocument;
            }

            _cachedDocument = SchemaDictionaryYaml.LoadOrCreate(path);
            _cachedPath = path;
            return _cachedDocument;
        }
    }
}
