using System;

namespace DocMgr.Models.YearlyArchive
{
    public static class ElectronicContentEntryDisplaySupport
    {
        public static string FormatEntryDisplayName(string? entryName, string? relativePath)
        {
            string name = entryName?.Trim() ?? string.Empty;
            string path = relativePath?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(path)
                || string.Equals(name, path, StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(name) ? path : name;
            }

            return path;
        }

        public static string FormatEntryDate(DateTime? value)
        {
            return value.HasValue && value.Value != default
                ? value.Value.ToString("yyyy-MM-dd HH:mm")
                : "-";
        }

        public static string FormatEntrySize(decimal? sizeMb)
        {
            return sizeMb.HasValue && sizeMb.Value > 0
                ? ElectronicMediaItemSupport.FormatSizeMb(sizeMb.Value)
                : "-";
        }
    }
}
