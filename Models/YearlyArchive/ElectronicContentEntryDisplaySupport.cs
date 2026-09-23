using System;

namespace DocMgr.Models.YearlyArchive
{
    public static class ElectronicContentEntryDisplaySupport
    {
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
