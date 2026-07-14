using System.IO;

namespace DocMgr.Infrastructure.Schema;

/// <summary>
/// 解析 SchemaDictionary.yaml 的读写路径。
/// </summary>
public static class SchemaDictionaryPathSupport
{
    /// <summary>
    /// 当前运行时加载的字典路径。
    /// </summary>
    public static string GetActiveDictionaryPath()
        => SchemaDictionaryStore.ResolveDictionaryPath();

    /// <summary>
    /// 尝试定位开发仓库中的字典目录（含 .cursor/schema）。
    /// </summary>
    public static bool TryResolveDevelopmentDictionaryDirectory(out string schemaDirectory)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DocMgr.sln")))
            {
                schemaDirectory = Path.Combine(current.FullName, ".cursor", "schema");
                return true;
            }

            current = current.Parent;
        }

        schemaDirectory = string.Empty;
        return false;
    }

    /// <summary>
    /// 尝试定位开发仓库中的字典文件路径。
    /// </summary>
    public static bool TryResolveDevelopmentDictionaryPath(out string dictionaryPath)
    {
        if (TryResolveDevelopmentDictionaryDirectory(out var schemaDirectory))
        {
            dictionaryPath = Path.Combine(schemaDirectory, "SchemaDictionary.yaml");
            return true;
        }

        dictionaryPath = string.Empty;
        return false;
    }

    /// <summary>
    /// 解析首选可写字典路径：开发仓库优先，否则为输出目录副本。
    /// </summary>
    public static string ResolvePreferredWritableDictionaryPath()
    {
        if (TryResolveDevelopmentDictionaryPath(out var developmentPath))
        {
            return developmentPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "schema", "SchemaDictionary.yaml");
    }

    /// <summary>
    /// 尝试定位开发仓库中的 Cursor 规则输出路径。
    /// </summary>
    public static bool TryResolveDevelopmentRulePath(out string rulePath)
    {
        if (TryResolveDevelopmentDictionaryDirectory(out var schemaDirectory))
        {
            var cursorDirectory = Path.GetDirectoryName(schemaDirectory);
            if (!string.IsNullOrWhiteSpace(cursorDirectory))
            {
                rulePath = Path.Combine(cursorDirectory, "rules", "schema-dictionary.mdc");
                return true;
            }
        }

        rulePath = string.Empty;
        return false;
    }

    /// <summary>
    /// 运行时输出目录中的字典副本路径。
    /// </summary>
    public static string GetRuntimeDictionaryCopyPath()
        => Path.Combine(AppContext.BaseDirectory, "schema", "SchemaDictionary.yaml");
}
