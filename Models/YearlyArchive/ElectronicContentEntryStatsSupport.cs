using System;
using System.Collections.Generic;
using System.Linq;

namespace DocMgr.Models.YearlyArchive
{
    public static class ElectronicContentEntryStatsSupport
    {
        public static (int DirectoryCount, int FileCount) CountEntryKinds(IEnumerable<string?> entryKinds)
        {
            int directoryCount = 0;
            int fileCount = 0;

            foreach (string? entryKind in entryKinds)
            {
                if (string.Equals(entryKind?.Trim(), ArchiveRegisterDomainValues.ElectronicEntryKindDirectory, StringComparison.Ordinal))
                {
                    directoryCount++;
                    continue;
                }

                if (string.Equals(entryKind?.Trim(), ArchiveRegisterDomainValues.ElectronicEntryKindFile, StringComparison.Ordinal))
                {
                    fileCount++;
                }
            }

            return (directoryCount, fileCount);
        }

        public static string FormatBreakdown(int directoryCount, int fileCount, int totalCount)
        {
            if (totalCount <= 0)
            {
                return "无";
            }

            if (directoryCount <= 0 && fileCount <= 0)
            {
                return $"{totalCount} 条";
            }

            return $"目录 {directoryCount}、文件 {fileCount}（共 {totalCount} 条）";
        }

        public static string FormatBreakdownFromEntries(IEnumerable<string?> entryKinds)
        {
            var kinds = entryKinds.ToList();
            var (directoryCount, fileCount) = CountEntryKinds(kinds);
            return FormatBreakdown(directoryCount, fileCount, kinds.Count);
        }
    }
}
