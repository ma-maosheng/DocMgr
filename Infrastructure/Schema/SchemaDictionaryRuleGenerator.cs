using System.Text;

namespace DocMgr.Infrastructure.Schema;

/// <summary>
/// 由 SchemaDictionary.yaml 生成 Cursor 规则文件。
/// </summary>
public static class SchemaDictionaryRuleGenerator
{
    /// <summary>
    /// 生成 schema-dictionary.mdc 内容。
    /// </summary>
    /// <param name="document">字典文档。</param>
    /// <param name="entitySnapshots">可选 EF 快照，用于按数据库列顺序输出字段。</param>
    public static string Generate(
        SchemaDictionaryDocument document,
        IReadOnlyDictionary<string, SchemaEntitySnapshot>? entitySnapshots = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine("description: 数据库表/字段中英对照字典；用户用中文指代表或字段时必须先查本字典再写代码");
        builder.AppendLine("alwaysApply: true");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("# 数据字典（中英对照）");
        builder.AppendLine();
        builder.AppendLine("## 使用约定");
        builder.AppendLine();
        builder.AppendLine("1. 用户以**中文表名/字段名**描述需求时，先在本字典中定位 `Entity` / `Property`，再修改 C# 与 SQL。");
        builder.AppendLine("2. 代码与数据库物理名保持英文；中文仅作语义对照与 UI 显示。");
        builder.AppendLine("3. 权威源文件：`.cursor/schema/SchemaDictionary.yaml`；运行时从 `schema/SchemaDictionary.yaml` 加载。");
        builder.AppendLine("4. 同步命令：`dotnet run --project tools/SchemaDictionarySync`");
        builder.AppendLine("5. **勿直接改本 mdc 文件**；请改 `.cursor/schema/SchemaDictionary.yaml`，或在「高级数据管理」保存显示名后再重置 DB。");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(document.GeneratedAt))
        {
            builder.AppendLine($"> 生成时间（UTC）：{document.GeneratedAt}");
            builder.AppendLine();
        }

        var activeTables = document.Tables
            .Where(pair => !pair.Value.Deprecated)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();

        builder.AppendLine("## 表索引");
        builder.AppendLine();
        builder.AppendLine("| 中文表名 | Entity | Table | 类型 |");
        builder.AppendLine("| --- | --- | --- | --- |");

        foreach (var (entityName, table) in activeTables)
        {
            var typeLabel = table.IsView ? "视图" : "表";
            builder.AppendLine($"| {EscapeMarkdown(table.ChineseName)} | `{entityName}` | `{table.TableName}` | {typeLabel} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 字段明细");
        builder.AppendLine();

        foreach (var (entityName, table) in activeTables)
        {
            builder.AppendLine($"### {entityName}（{EscapeMarkdown(table.ChineseName)}）");
            builder.AppendLine();
            builder.AppendLine("| 中文字段 | Property | CLR 类型 | 待补全 |");
            builder.AppendLine("| --- | --- | --- | --- |");

            foreach (var fieldName in ResolveFieldNames(entityName, table, entitySnapshots))
            {
                if (!table.Fields.TryGetValue(fieldName, out var field))
                {
                    continue;
                }

                var reviewFlag = field.NeedsReview ? "是" : "否";
                builder.AppendLine(
                    $"| {EscapeMarkdown(field.ChineseName)} | `{fieldName}` | `{field.ClrType}` | {reviewFlag} |");
            }

            builder.AppendLine();
        }

        var deprecatedTables = document.Tables
            .Where(pair => pair.Value.Deprecated)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();

        if (deprecatedTables.Count > 0)
        {
            builder.AppendLine("## 已废弃（模型中已不存在）");
            builder.AppendLine();
            foreach (var (entityName, table) in deprecatedTables)
            {
                builder.AppendLine($"- `{entityName}`（{EscapeMarkdown(table.ChineseName)}）");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static IEnumerable<string> ResolveFieldNames(
        string entityName,
        SchemaDictionaryTableEntry table,
        IReadOnlyDictionary<string, SchemaEntitySnapshot>? entitySnapshots)
    {
        if (entitySnapshots != null
            && entitySnapshots.TryGetValue(entityName, out var snapshot))
        {
            foreach (var field in snapshot.Fields)
            {
                yield return field.FieldName;
            }

            yield break;
        }

        foreach (var fieldName in table.Fields.Keys)
        {
            yield return fieldName;
        }
    }

    private static string EscapeMarkdown(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal);
}
