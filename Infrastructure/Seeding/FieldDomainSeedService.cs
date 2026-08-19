using DocMgr.Infrastructure.Schema;
using DocMgr.Repositories.Interfaces;
using System.Text.RegularExpressions;

namespace DocMgr.Infrastructure.Seeding;

/// <summary>
/// 字段域值初始化入口。
/// 该类型已按职责拆分为 partial：Core / AliasMaps / SeedCatalog / Models。
/// </summary>
public static partial class FieldDomainSeedService
{
    public static string GenerateAlias(string entityName, string fieldName)
        => BuildChineseAlias(entityName, fieldName);

    /// <summary>
    /// 仅使用内置 alias 映射生成字段中文名（供 YAML 同步时使用）。
    /// </summary>
    public static string GenerateAliasFromLegacyMaps(string entityName, string fieldName)
        => BuildChineseAliasFromLegacyMaps(entityName, fieldName);

    public static bool IsCompliantAlias(string? alias, string fieldName)
        => IsAliasCompliant(alias, fieldName);

    public static void SeedDefaults(IFieldDomainSeedRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var seeds = BuildSeeds();
        bool changed = false;

        foreach (var seed in seeds)
        {
            var definition = repository.GetDefinitionWithOptions(seed.EntityName, seed.FieldName);

            if (definition == null)
            {
                definition = new FieldDomainDefinition
                {
                    EntityName = seed.EntityName,
                    FieldName = seed.FieldName,
                    DisplayName = seed.DisplayName,
                    Description = seed.Description,
                    IsDomainEnabled = seed.IsDomainEnabled,
                    SortOrder = seed.SortOrder
                };
                repository.AddDefinition(definition);
                changed = true;
            }
            else
            {
                if (definition.DisplayName != seed.DisplayName)
                {
                    definition.DisplayName = seed.DisplayName;
                    changed = true;
                }

                if (definition.Description != seed.Description)
                {
                    definition.Description = seed.Description;
                    changed = true;
                }

                if (definition.IsDomainEnabled != seed.IsDomainEnabled)
                {
                    definition.IsDomainEnabled = seed.IsDomainEnabled;
                    changed = true;
                }

                if (definition.SortOrder != seed.SortOrder)
                {
                    definition.SortOrder = seed.SortOrder;
                    changed = true;
                }
            }

            var seedOptionKeys = new HashSet<string>(
                seed.Options.Select(optionSeed => BuildOptionKey(optionSeed.Scope, optionSeed.OptionValue)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var optionSeed in seed.Options)
            {
                var optionKey = BuildOptionKey(optionSeed.Scope, optionSeed.OptionValue);
                var option = definition.Options.FirstOrDefault(o => string.Equals(BuildOptionKey(o.Scope, o.OptionValue), optionKey, StringComparison.OrdinalIgnoreCase));
                if (option == null)
                {
                    definition.Options.Add(new FieldDomainOption
                    {
                        Scope = optionSeed.Scope,
                        OptionValue = optionSeed.OptionValue,
                        OptionLabel = optionSeed.OptionLabel,
                        IsEnabled = optionSeed.IsEnabled,
                        SortOrder = optionSeed.SortOrder
                    });
                    changed = true;
                }
                else
                {
                    if (option.OptionLabel != optionSeed.OptionLabel)
                    {
                        option.OptionLabel = optionSeed.OptionLabel;
                        changed = true;
                    }

                    if (option.IsEnabled != optionSeed.IsEnabled)
                    {
                        option.IsEnabled = optionSeed.IsEnabled;
                        changed = true;
                    }

                    if (option.SortOrder != optionSeed.SortOrder)
                    {
                        option.SortOrder = optionSeed.SortOrder;
                        changed = true;
                    }
                }
            }

            List<FieldDomainOption> staleOptions;
            if (seed.PreserveUserOptions)
            {
                // 开放域允许业务页自助增项；启动时只回收已从种子移除的「其他」兜底项。
                staleOptions = definition.Options
                    .Where(o => string.Equals((o.OptionValue ?? string.Empty).Trim(), RetiredCatchAllOptionValue, StringComparison.Ordinal)
                        && !seedOptionKeys.Contains(BuildOptionKey(o.Scope, o.OptionValue)))
                    .ToList();
            }
            else
            {
                staleOptions = definition.Options
                    .Where(o => !seedOptionKeys.Contains(BuildOptionKey(o.Scope, o.OptionValue)))
                    .ToList();
            }

            if (staleOptions.Count > 0)
            {
                foreach (var staleOption in staleOptions)
                {
                    definition.Options.Remove(staleOption);
                }

                repository.RemoveOptions(staleOptions.Where(o => o.Id > 0));
                changed = true;
            }
        }

        EnsureAllFieldAliases(repository, ref changed);

        if (changed)
        {
            repository.SaveChanges();
        }
    }

    private static void EnsureAllFieldAliases(IFieldDomainSeedRepository repository, ref bool changed)
    {
        var existingDefinitions = repository.GetTrackedAndAllDefinitions();

        var definitionMap = existingDefinitions.ToDictionary(
            d => $"{d.EntityName}.{d.FieldName}",
            d => d,
            StringComparer.OrdinalIgnoreCase);

        int sortOrder = (existingDefinitions.Select(d => d.SortOrder).DefaultIfEmpty(0).Max() / 10 + 1) * 10;

        var entityTypes = repository.GetSeedEntityTypes();

        foreach (var entity in entityTypes)
        {
            var entityName = AdvancedDataDictionaryEntitySupport.IsDictionaryBackedEntity(entity)
                ? entity.Name
                : entity.ClrType!.Name;
            var properties = entity.GetProperties().OrderBy(p => p.Name).ToList();

            foreach (var property in properties)
            {
                var key = $"{entityName}.{property.Name}";
                var generatedAlias = BuildChineseAlias(entityName, property.Name);
                if (definitionMap.TryGetValue(key, out var existing))
                {
                    if (!IsAliasCompliant(existing.DisplayName, property.Name))
                    {
                        if (!string.Equals(existing.DisplayName, generatedAlias, StringComparison.Ordinal))
                        {
                            existing.DisplayName = generatedAlias;
                            changed = true;
                        }
                    }
                    continue;
                }

                var definition = new FieldDomainDefinition
                {
                    EntityName = entityName,
                    FieldName = property.Name,
                    DisplayName = generatedAlias,
                    Description = "系统自动生成的字段别名",
                    IsDomainEnabled = false,
                    SortOrder = sortOrder
                };

                sortOrder += 10;
                repository.AddDefinition(definition);
                definitionMap[key] = definition;
                changed = true;
            }
        }
    }

    private const string RetiredCatchAllOptionValue = "其他";

    private static string BuildOptionKey(string? scope, string? optionValue)
    {
        return $"{scope?.Trim() ?? string.Empty}|{optionValue?.Trim() ?? string.Empty}";
    }

    private static string BuildChineseAlias(string entityName, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return string.Empty;
        }

        if (string.Equals(fieldName, "Id", StringComparison.OrdinalIgnoreCase))
        {
            return "ID";
        }

        if (SchemaDictionaryStore.TryGetFieldChineseName(entityName, fieldName, out var yamlAlias))
        {
            return yamlAlias;
        }

        return BuildChineseAliasFromLegacyMaps(entityName, fieldName);
    }

    private static string BuildChineseAliasFromLegacyMaps(string entityName, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return string.Empty;
        }

        if (string.Equals(fieldName, "Id", StringComparison.OrdinalIgnoreCase))
        {
            return "ID";
        }

        var exactKey = $"{entityName}.{fieldName}";
        if (ExactAliasMap.TryGetValue(exactKey, out var exactByEntityAlias))
        {
            return exactByEntityAlias;
        }

        if (FieldAliasMap.TryGetValue(fieldName, out var exactAlias))
        {
            return exactAlias;
        }

        var normalized = Regex.Replace(fieldName, "([A-Z]+)([A-Z][a-z])", "$1 $2");
        normalized = Regex.Replace(normalized, "(?<=[a-z0-9])([A-Z])", " $1");
        var tokens = normalized.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
        {
            return fieldName;
        }

        var pieces = new List<string>();
        foreach (var token in tokens)
        {
            if (TokenAliasMap.TryGetValue(token, out var alias))
            {
                pieces.Add(alias);
            }
            else
            {
                pieces.Add("未映射");
            }
        }

        var merged = string.Concat(pieces);
        if (string.IsNullOrWhiteSpace(merged))
        {
            return "未映射字段";
        }

        return IsAliasCompliant(merged, fieldName) ? merged : "未映射字段";
    }

    private static bool ContainsChinese(string value)
        => Regex.IsMatch(value, "[\u4e00-\u9fff]");

    private static bool IsPureChineseAlias(string value)
        => ContainsChinese(value) && !Regex.IsMatch(value, "[A-Za-z]");

    private static bool IsAliasCompliant(string? alias, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }

        if (alias.Contains("未映射", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(fieldName, "Id", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(alias, "ID", StringComparison.OrdinalIgnoreCase);
        }

        if (IsPureChineseAlias(alias))
        {
            return true;
        }

        var cleaned = Regex.Replace(alias, "ID", string.Empty, RegexOptions.IgnoreCase);
        return ContainsChinese(cleaned) && !Regex.IsMatch(cleaned, "[A-Za-z]");
    }
}
