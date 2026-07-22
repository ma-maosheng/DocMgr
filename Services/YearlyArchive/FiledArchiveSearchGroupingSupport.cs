using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    internal static class FiledArchiveSearchGroupingSupport
    {
        internal static List<FiledArchiveSearchBoxGroupHit> GroupRegisterHitsByArchiveBox(
            IReadOnlyList<FiledArchiveSearchGroupHit> itemGroups,
            IReadOnlyDictionary<string, YearlyArchiveBox> boxesBySequenceNo)
        {
            if (itemGroups.Count == 0)
            {
                return [];
            }

            var groups = itemGroups
                .GroupBy(group => ResolveArchiveBoxKey(group.PrimaryHit))
                .OrderBy(bucket => bucket.Key, StringComparer.Ordinal)
                .Select(bucket =>
                {
                    var orderedItemGroups = bucket
                        .OrderBy(group => group.PrimaryHit.FormNo, StringComparer.Ordinal)
                        .ThenBy(group => group.PrimaryHit.MaterialName, StringComparer.Ordinal)
                        .ThenBy(group => group.PrimaryHit.ItemName, StringComparer.Ordinal)
                        .ThenBy(group => group.PrimaryHit.FilingFactId)
                        .ToList();

                    var referenceHit = orderedItemGroups[0].PrimaryHit;
                    boxesBySequenceNo.TryGetValue(bucket.Key, out var box);

                    return new FiledArchiveSearchBoxGroupHit
                    {
                        ArchiveSequenceNo = bucket.Key,
                        ProjectName = !string.IsNullOrWhiteSpace(box?.ProjectName)
                            ? box.ProjectName.Trim()
                            : referenceHit.ProjectName,
                        Year = NormalizeText(box?.Year),
                        StorageLocation = ResolveBoxStorageLocation(box, referenceHit),
                        CurrentStorageLocation = referenceHit.CurrentStorageLocation,
                        Specifications = NormalizeText(box?.Specs),
                        PlacementMode = NormalizeText(box?.PlacementMode),
                        ArchivedBy = NormalizeText(box?.ArchivedBy),
                        ArchivedDate = box?.ArchivedDate,
                        Remarks = NormalizeText(box?.Remarks),
                        ContainerLifecycleStatus = NormalizeText(box?.ContainerLifecycleStatus),
                        ItemGroups = orderedItemGroups
                    };
                })
                .ToList();

            return groups;
        }

        internal static string ResolveArchiveBoxKey(FiledArchiveSearchHit hit)
        {
            if (!string.IsNullOrWhiteSpace(hit.CurrentContainerCode))
            {
                return hit.CurrentContainerCode.Trim();
            }

            return hit.ContainerCode.Trim();
        }

        private static string ResolveBoxStorageLocation(YearlyArchiveBox? box, FiledArchiveSearchHit referenceHit)
        {
            if (!string.IsNullOrWhiteSpace(box?.BoxLocationCode))
            {
                return box.BoxLocationCode.Trim();
            }

            if (!string.IsNullOrWhiteSpace(box?.LastStorageLocation))
            {
                return box.LastStorageLocation.Trim();
            }

            return referenceHit.StorageLocation;
        }

        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        internal static List<FiledArchiveSearchGroupHit> GroupRegisterHits(
            IReadOnlyList<FiledArchiveSearchHit> matchedHits,
            IReadOnlyList<YearlyArchiveFilingFact> primaryFacts,
            IReadOnlyList<YearlyArchiveFilingFact> backupFacts,
            Func<YearlyArchiveFilingFact, FiledArchiveSearchHit?> mapFactToHit)
        {
            if (matchedHits.Count == 0)
            {
                return [];
            }

            var matchedByFactId = matchedHits.ToDictionary(hit => hit.FilingFactId);
            var primaryFactById = primaryFacts.ToDictionary(fact => fact.Id);
            var matchedBackupFactIdsByRoot = matchedHits
                .Where(hit => matchedByFactId.ContainsKey(hit.FilingFactId))
                .GroupBy(hit => hit.PrimaryFilingFactId ?? hit.FilingFactId)
                .ToDictionary(
                    bucket => bucket.Key,
                    bucket => bucket.Select(hit => hit.FilingFactId).ToHashSet());
            var rootIds = matchedHits
                .Select(hit => hit.PrimaryFilingFactId ?? hit.FilingFactId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            var groups = new List<FiledArchiveSearchGroupHit>();
            foreach (int rootId in rootIds)
            {
                if (!primaryFactById.TryGetValue(rootId, out var primaryFact))
                {
                    continue;
                }

                FiledArchiveSearchHit primaryHit = matchedByFactId.TryGetValue(rootId, out var matchedPrimary)
                    ? matchedPrimary
                    : mapFactToHit(primaryFact)
                      ?? throw new InvalidOperationException($"无法映射原件立档事实 [{rootId}]。");

                var backupHits = backupFacts
                    .Where(fact => fact.PrimaryFilingFactId == rootId)
                    .OrderBy(fact => fact.FiledAt)
                    .ThenBy(fact => fact.Id)
                    .Select(fact => matchedByFactId.TryGetValue(fact.Id, out var matchedBackup)
                        ? matchedBackup
                        : mapFactToHit(fact))
                    .Where(hit => hit != null)
                    .Cast<FiledArchiveSearchHit>()
                    .ToList();

                bool hasMatchingBackup = matchedBackupFactIdsByRoot.TryGetValue(rootId, out var matchedIdsInRoot)
                    && matchedIdsInRoot.Any(id => id != rootId);

                groups.Add(new FiledArchiveSearchGroupHit
                {
                    PrimaryHit = primaryHit,
                    BackupHits = backupHits,
                    HasMatchingBackup = hasMatchingBackup,
                    ExpandByDefault = hasMatchingBackup && !matchedByFactId.ContainsKey(rootId)
                });
            }

            return groups;
        }
    }
}
