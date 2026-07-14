using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DocMgr.Models.YearlyArchive
{
    public static class ElectronicMediaCapacitySupport
    {
        public const string DefaultCapacityUnit = "GB";

        public static readonly IReadOnlyList<string> CapacityUnits = ["GB", "TB", "MB"];

        /// <summary>
        /// 将容量文本解析为 MB（无法解析时返回 0）。
        /// </summary>
        public static decimal ParseCapacityTextToMb(string? capacityText)
        {
            if (string.IsNullOrWhiteSpace(capacityText))
            {
                return 0;
            }

            var match = Regex.Match(capacityText.Trim(), @"(?<value>\d+(\.\d+)?)\s*(?<unit>TB|T|GB|G|MB|M)", RegexOptions.IgnoreCase);
            if (!match.Success
                || !decimal.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value))
            {
                return 0;
            }

            string unit = match.Groups["unit"].Value.ToUpperInvariant();
            return unit switch
            {
                "TB" or "T" => value * 1024m * 1024m,
                "GB" or "G" => value * 1024m,
                "MB" or "M" => value,
                _ => 0
            };
        }

        public static string FormatCapacityMb(decimal capacityMb)
        {
            if (capacityMb <= 0)
            {
                return "—";
            }

            if (capacityMb >= 1024m * 1024m)
            {
                return $"{capacityMb / (1024m * 1024m):0.##} TB";
            }

            if (capacityMb >= 1024m)
            {
                return $"{capacityMb / 1024m:0.##} GB";
            }

            return $"{capacityMb:0.##} MB";
        }

        public static bool TrySplitCapacityText(string? capacityText, out string value, out string unit)
        {
            value = string.Empty;
            unit = DefaultCapacityUnit;

            if (string.IsNullOrWhiteSpace(capacityText))
            {
                return false;
            }

            string trimmed = capacityText.Trim();
            var match = Regex.Match(trimmed, @"^(?<value>\d+(\.\d+)?)\s*(?<unit>TB|T|GB|G|MB|M)?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                value = match.Groups["value"].Value;
                unit = NormalizeCapacityUnit(match.Groups["unit"].Value);
                return true;
            }

            value = trimmed;
            return true;
        }

        public static string CombineCapacityText(string? value, string? unit)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmedValue = value.Trim();
            if (!Regex.IsMatch(trimmedValue, @"^\d+(\.\d+)?$"))
            {
                return trimmedValue;
            }

            return $"{trimmedValue} {NormalizeCapacityUnit(unit)}";
        }

        private static string NormalizeCapacityUnit(string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                return DefaultCapacityUnit;
            }

            return unit.Trim().ToUpperInvariant() switch
            {
                "TB" or "T" => "TB",
                "GB" or "G" => "GB",
                "MB" or "M" => "MB",
                _ => DefaultCapacityUnit
            };
        }
    }
}
