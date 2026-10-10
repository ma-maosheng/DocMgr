using System.Collections.ObjectModel;

namespace DocMgr.Services.Shared;

/// <summary>
/// ComboBox ItemsSource 重建时保留已选值，避免 <see cref="ObservableCollection{T}.Clear"/> 触发 TwoWay 回写 null。
/// </summary>
internal static class ComboBoxSelectionPreserveSupport
{
    /// <summary>
    /// 重建字符串选项列表（不处理选中值）。
    /// </summary>
    public static void ReplaceStringOptions(
        ObservableCollection<string> target,
        IEnumerable<string?> values)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.Clear();
        foreach (string item in values
                     .Where(static value => !string.IsNullOrWhiteSpace(value))
                     .Select(static value => value!.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            target.Add(item);
        }
    }

    /// <summary>
    /// 在 Clear 前读取已选字符串，重建选项后写回仍合法的值。
    /// </summary>
    /// <param name="fallbackToFirst">已选为空或不在新列表中时，是否落到首项。</param>
    /// <param name="clearWhenInvalid">已选不在新列表且不回落首项时，是否清空选中。</param>
    public static void RefreshBoundStringOptions(
        ObservableCollection<string> options,
        IEnumerable<string?> newValues,
        Func<string?> getSelected,
        Action<string> setSelected,
        bool fallbackToFirst = false,
        bool clearWhenInvalid = true)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(getSelected);
        ArgumentNullException.ThrowIfNull(setSelected);

        string preferred = getSelected()?.Trim() ?? string.Empty;
        ReplaceStringOptions(options, newValues);

        string? matched = options.FirstOrDefault(option =>
            string.Equals(option, preferred, StringComparison.OrdinalIgnoreCase));
        if (matched != null)
        {
            if (!string.Equals(getSelected()?.Trim(), matched, StringComparison.Ordinal))
            {
                setSelected(matched);
            }

            return;
        }

        if (fallbackToFirst)
        {
            setSelected(options.FirstOrDefault() ?? string.Empty);
            return;
        }

        if (clearWhenInvalid && !string.IsNullOrWhiteSpace(preferred))
        {
            setSelected(string.Empty);
        }
        else if (string.IsNullOrWhiteSpace(getSelected()))
        {
            // Clear 已把绑定冲空时，若原本就无选中则保持空。
            setSelected(string.Empty);
        }
    }

    /// <summary>
    /// 在选项列表中解析与偏好匹配的项；无匹配时按需回落首项。
    /// </summary>
    public static string ResolvePreferredOrFirst(
        IReadOnlyList<string> options,
        string? preferred,
        bool fallbackToFirst = true)
    {
        string trimmed = preferred?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            string? matched = options.FirstOrDefault(option =>
                string.Equals(option, trimmed, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                return matched;
            }
        }

        return fallbackToFirst
            ? (options.FirstOrDefault() ?? string.Empty)
            : trimmed;
    }
}
