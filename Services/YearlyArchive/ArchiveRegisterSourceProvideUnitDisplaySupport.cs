using System;
using System.Collections.Generic;
using System.Linq;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料来源与提供单位的只读展示：单条「来源 . 单位」，聚合「来源 . 单位、来源 . 单位」。
    /// </summary>
    public static class ArchiveRegisterSourceProvideUnitDisplaySupport
    {
        public const string EmptyPlaceholder = "(无)";

        private const string PairSeparator = " . ";
        private const string AggregateSeparator = "、";

        /// <summary>单条子项展示：内部 . 资料室。</summary>
        public static string FormatPair(string? sourceType, string? provideUnit)
        {
            string source = sourceType?.Trim() ?? string.Empty;
            string unit = provideUnit?.Trim() ?? string.Empty;
            if (source.Length == 0 && unit.Length == 0)
            {
                return EmptyPlaceholder;
            }

            if (source.Length == 0)
            {
                return unit;
            }

            if (unit.Length == 0)
            {
                return source;
            }

            return string.Concat(source, PairSeparator, unit);
        }

        /// <summary>聚合展示：按「来源 . 单位」去重后以顿号连接。</summary>
        public static string FormatAggregated(IEnumerable<(string? SourceType, string? ProvideUnit)> pairs)
        {
            ArgumentNullException.ThrowIfNull(pairs);

            var distinct = pairs
                .Select(pair => FormatPair(pair.SourceType, pair.ProvideUnit))
                .Where(text => !string.Equals(text, EmptyPlaceholder, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return distinct.Count == 0 ? EmptyPlaceholder : string.Join(AggregateSeparator, distinct);
        }
    }
}
