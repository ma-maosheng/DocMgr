using System;
using System.Collections.Generic;
using System.Linq;

namespace DocMgr.Models.YearlyArchive
{
    public sealed class MatchedContentEntryInfo
    {
        public int EntryId { get; init; }

        public string EntryKind { get; init; } = string.Empty;

        public string EntryName { get; init; } = string.Empty;

        public string RelativePath { get; init; } = string.Empty;

        /// <summary>立档时写入目标介质的条目路径。</summary>
        public string FilingPath { get; init; } = string.Empty;

        public string CreatedDateText { get; init; } = string.Empty;

        public string ModifiedDateText { get; init; } = string.Empty;

        public string SizeText { get; init; } = string.Empty;
    }

    public static class ContentEntrySearchSupport
    {
        public static bool HasActiveFilter(RegisterDirectionSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            return !string.IsNullOrWhiteSpace(criteria.ContentEntryKeyword);
        }

        public static bool MatchesEntryKind(string entryKind, string? kindFilter)
        {
            if (string.IsNullOrWhiteSpace(kindFilter))
            {
                return true;
            }

            return string.Equals(entryKind?.Trim(), kindFilter.Trim(), StringComparison.Ordinal);
        }

        public static bool MatchesEntry(
            YearlyArchiveRegisterElectronicMediaItemEntry entry,
            RegisterDirectionSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(criteria);

            if (!HasActiveFilter(criteria))
            {
                return false;
            }

            if (!MatchesEntryKind(entry.EntryKind, criteria.ContentEntryKindFilter))
            {
                return false;
            }

            string likePattern = SearchWildcardPatternSupport.ToSqlLikePattern(criteria.ContentEntryKeyword);
            return MatchesField(entry.EntryName, likePattern);
        }

        public static bool MatchesEntry(
            string entryKind,
            string entryName,
            RegisterDirectionSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            if (!HasActiveFilter(criteria))
            {
                return false;
            }

            if (!MatchesEntryKind(entryKind, criteria.ContentEntryKindFilter))
            {
                return false;
            }

            string likePattern = SearchWildcardPatternSupport.ToSqlLikePattern(criteria.ContentEntryKeyword);
            return MatchesField(entryName, likePattern);
        }

        public static MatchedContentEntryInfo ToMatchedInfo(
            YearlyArchiveRegisterElectronicMediaItemEntry entry,
            string? filingStoragePath = null)
        {
            ArgumentNullException.ThrowIfNull(entry);

            return new MatchedContentEntryInfo
            {
                EntryId = entry.Id,
                EntryKind = entry.EntryKind?.Trim() ?? string.Empty,
                EntryName = entry.EntryName?.Trim() ?? string.Empty,
                RelativePath = entry.RelativePath?.Trim() ?? string.Empty,
                FilingPath = ElectronicFilingStoragePathSupport.BuildEntryFilingPath(
                    filingStoragePath,
                    entry.RelativePath),
                CreatedDateText = ElectronicContentEntryDisplaySupport.FormatEntryDate(entry.CreatedAt),
                ModifiedDateText = ElectronicContentEntryDisplaySupport.FormatEntryDate(entry.ModifiedAt),
                SizeText = ElectronicContentEntryDisplaySupport.FormatEntrySize(entry.SizeMb)
            };
        }

        public static FilingLedgerContentEntryInfo ToLedgerInfo(
            YearlyArchiveRegisterElectronicMediaItemEntry entry,
            string? filingStoragePath)
        {
            var matched = ToMatchedInfo(entry, filingStoragePath);
            return new FilingLedgerContentEntryInfo
            {
                EntryKind = matched.EntryKind,
                EntryName = matched.EntryName,
                FilingPath = matched.FilingPath,
                CreatedDateText = matched.CreatedDateText,
                ModifiedDateText = matched.ModifiedDateText,
                SizeText = matched.SizeText
            };
        }

        public static string FormatMatchedSummary(IReadOnlyList<MatchedContentEntryInfo> entries, int maxDisplay = 3)
        {
            if (entries == null || entries.Count == 0)
            {
                return string.Empty;
            }

            var labels = entries
                .Take(maxDisplay)
                .Select(FormatEntryLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label));

            string joined = string.Join("、", labels);
            if (entries.Count > maxDisplay)
            {
                joined = string.IsNullOrWhiteSpace(joined)
                    ? $"等 {entries.Count} 条"
                    : $"{joined} 等 {entries.Count} 条";
            }

            return joined;
        }

        /// <summary>
        /// 相对路径与条目名称相同时不重复展示（常见于根目录文件）。
        /// </summary>
        public static string BuildRelativePathSuffix(string entryName, string? relativePath)
        {
            string name = entryName?.Trim() ?? string.Empty;
            string path = relativePath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path) || string.Equals(name, path, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return $"（{path}）";
        }

        public static RegisterDirectionSearchCriteria CreateCriteria(
            string contentEntryKeyword,
            string? contentEntryKindFilter)
        {
            return new RegisterDirectionSearchCriteria
            {
                ContentEntryKeyword = contentEntryKeyword?.Trim() ?? string.Empty,
                ContentEntryKindFilter = contentEntryKindFilter?.Trim() ?? string.Empty
            };
        }

        public static string FormatEntryLabel(MatchedContentEntryInfo entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            string name = entry.EntryName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return $"{name}{BuildRelativePathSuffix(name, entry.RelativePath)}";
        }

        private static bool MatchesField(string? value, string sqlLikePattern)
        {
            return MatchesSqlLike(value ?? string.Empty, sqlLikePattern, SearchWildcardPatternSupport.EscapeCharacter);
        }

        public static bool MatchesSqlLike(string input, string pattern, char escapeCharacter = '\\')
        {
            return MatchesSqlLikeRecursive(input, pattern, 0, 0, escapeCharacter);
        }

        private static bool MatchesSqlLikeRecursive(
            string input,
            string pattern,
            int inputIndex,
            int patternIndex,
            char escapeCharacter)
        {
            while (patternIndex < pattern.Length)
            {
                char patternChar = pattern[patternIndex];
                if (patternChar == escapeCharacter && patternIndex + 1 < pattern.Length)
                {
                    char literal = pattern[patternIndex + 1];
                    if (inputIndex >= input.Length || input[inputIndex] != literal)
                    {
                        return false;
                    }

                    inputIndex++;
                    patternIndex += 2;
                    continue;
                }

                if (patternChar == '%')
                {
                    patternIndex++;
                    if (patternIndex >= pattern.Length)
                    {
                        return true;
                    }

                    for (int candidate = inputIndex; candidate <= input.Length; candidate++)
                    {
                        if (MatchesSqlLikeRecursive(input, pattern, candidate, patternIndex, escapeCharacter))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                if (patternChar == '_')
                {
                    if (inputIndex >= input.Length)
                    {
                        return false;
                    }

                    inputIndex++;
                    patternIndex++;
                    continue;
                }

                if (inputIndex >= input.Length || input[inputIndex] != patternChar)
                {
                    return false;
                }

                inputIndex++;
                patternIndex++;
            }

            return inputIndex == input.Length;
        }
    }
}
