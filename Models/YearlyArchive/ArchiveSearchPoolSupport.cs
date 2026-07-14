using System;
using System.Collections.Generic;
using System.Linq;

namespace DocMgr.Models.YearlyArchive
{
    public static class ArchiveSearchPoolSupport
    {
        public static string BuildPoolKey(ArchiveSearchPoolSelection selection)
        {
            ArgumentNullException.ThrowIfNull(selection);

            if (selection.IsContentEntry)
            {
                return $"{selection.FilingFactId}:E:{selection.ContentEntryId}";
            }

            return $"{selection.FilingFactId}:W";
        }

        public static ArchiveSearchPoolSelection CreateWholeMediaItem(int filingFactId)
        {
            return new ArchiveSearchPoolSelection
            {
                FilingFactId = filingFactId,
                SelectionScopeKind = ArchiveSearchSelectionScopeKind.WholeMediaItem,
                RequestedCopyCount = 1
            };
        }

        public static ArchiveSearchPoolSelection CreateContentEntry(int filingFactId, int contentEntryId)
        {
            return new ArchiveSearchPoolSelection
            {
                FilingFactId = filingFactId,
                SelectionScopeKind = ArchiveSearchSelectionScopeKind.ContentEntry,
                ContentEntryId = contentEntryId,
                RequestedCopyCount = 1
            };
        }

        public static string FormatScopeDisplay(
            string selectionScopeKind,
            string contentEntryKind,
            string contentEntryName,
            string contentEntryRelativePath)
        {
            if (string.Equals(
                    selectionScopeKind,
                    ArchiveSearchSelectionScopeKind.WholeMediaItem,
                    StringComparison.Ordinal))
            {
                return "整子项";
            }

            string kindLabel = string.IsNullOrWhiteSpace(contentEntryKind)
                ? "条目"
                : contentEntryKind.Trim();

            string name = contentEntryName?.Trim() ?? string.Empty;
            return $"{kindLabel}：{name}{ContentEntrySearchSupport.BuildRelativePathSuffix(name, contentEntryRelativePath)}";
        }

        public static string FormatScopeDisplay(MatchedContentEntryInfo entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            return FormatScopeDisplay(
                ArchiveSearchSelectionScopeKind.ContentEntry,
                entry.EntryKind,
                entry.EntryName,
                entry.RelativePath);
        }

        public sealed class MergeResult
        {
            public int AddedCount { get; init; }

            public int SkippedDuplicateCount { get; init; }

            public int SkippedWholeExistsCount { get; init; }

            public int ReplacedPartialCount { get; init; }
        }

        public static MergeResult MergeSelections(
            ICollection<ArchiveSearchPoolSelection> target,
            IReadOnlyCollection<ArchiveSearchPoolSelection> incoming)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(incoming);

            var existingKeys = target.Select(BuildPoolKey).ToHashSet();
            var wholeFactIds = target
                .Where(item => item.IsWholeMediaItem)
                .Select(item => item.FilingFactId)
                .ToHashSet();

            int added = 0;
            int skippedDuplicate = 0;
            int skippedWholeExists = 0;
            int replacedPartial = 0;

            foreach (var selection in incoming)
            {
                string key = BuildPoolKey(selection);

                if (selection.IsWholeMediaItem)
                {
                    if (existingKeys.Contains(key))
                    {
                        skippedDuplicate++;
                        continue;
                    }

                    var partialKeys = target
                        .Where(item => item.FilingFactId == selection.FilingFactId && item.IsContentEntry)
                        .Select(BuildPoolKey)
                        .ToList();

                    if (partialKeys.Count > 0)
                    {
                        foreach (string partialKey in partialKeys)
                        {
                            var partial = target.FirstOrDefault(item => BuildPoolKey(item) == partialKey);
                            if (partial != null)
                            {
                                target.Remove(partial);
                                existingKeys.Remove(partialKey);
                                replacedPartial++;
                            }
                        }
                    }

                    target.Add(selection);
                    existingKeys.Add(key);
                    wholeFactIds.Add(selection.FilingFactId);
                    added++;
                    continue;
                }

                if (wholeFactIds.Contains(selection.FilingFactId))
                {
                    skippedWholeExists++;
                    continue;
                }

                if (existingKeys.Contains(key))
                {
                    skippedDuplicate++;
                    continue;
                }

                target.Add(selection);
                existingKeys.Add(key);
                added++;
            }

            return new MergeResult
            {
                AddedCount = added,
                SkippedDuplicateCount = skippedDuplicate,
                SkippedWholeExistsCount = skippedWholeExists,
                ReplacedPartialCount = replacedPartial
            };
        }
    }
}
