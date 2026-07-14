using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    public sealed class ArchiveFilingSearchPoolSession : IArchiveFilingSearchPoolSession
    {
        private readonly Dictionary<string, List<ArchiveSearchPoolItemRow>> _pools = new(StringComparer.Ordinal);

        public event Action<string>? PoolChanged;

        public IReadOnlyList<ArchiveSearchPoolItemRow> GetPool(string mediaKind)
        {
            return _pools.TryGetValue(mediaKind, out var pool)
                ? pool
                : Array.Empty<ArchiveSearchPoolItemRow>();
        }

        public ArchiveSearchPoolSupport.MergeResult Merge(
            string mediaKind,
            IReadOnlyList<ArchiveSearchPoolSelection> incoming,
            IReadOnlyDictionary<int, FiledArchiveSearchHit> hitsByFactId,
            IReadOnlyDictionary<int, MatchedContentEntryInfo> entriesById)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mediaKind);
            ArgumentNullException.ThrowIfNull(incoming);
            ArgumentNullException.ThrowIfNull(hitsByFactId);
            ArgumentNullException.ThrowIfNull(entriesById);

            if (!_pools.TryGetValue(mediaKind, out var pool))
            {
                pool = new List<ArchiveSearchPoolItemRow>();
                _pools[mediaKind] = pool;
            }

            var hitByFactId = new Dictionary<int, FiledArchiveSearchHit>();
            foreach (var row in pool)
            {
                hitByFactId[row.FilingFactId] = row.Hit;
            }

            foreach (var pair in hitsByFactId)
            {
                hitByFactId[pair.Key] = pair.Value;
            }

            var selections = pool.Select(row => row.Selection).ToList();
            var mergeResult = ArchiveSearchPoolSupport.MergeSelections(selections, incoming);

            pool.Clear();
            foreach (var selection in selections)
            {
                if (!hitByFactId.TryGetValue(selection.FilingFactId, out var hit))
                {
                    continue;
                }

                MatchedContentEntryInfo? contentEntry = null;
                if (selection.IsContentEntry && selection.ContentEntryId is int entryId)
                {
                    if (!entriesById.TryGetValue(entryId, out contentEntry))
                    {
                        contentEntry = hit.MatchedContentEntries.FirstOrDefault(entry => entry.EntryId == entryId);
                    }
                }

                pool.Add(new ArchiveSearchPoolItemRow(hit, selection, contentEntry));
            }

            PoolChanged?.Invoke(mediaKind);
            return mergeResult;
        }

        public void Replace(string mediaKind, IEnumerable<ArchiveSearchPoolItemRow> items)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mediaKind);
            ArgumentNullException.ThrowIfNull(items);

            _pools[mediaKind] = items.ToList();
            PoolChanged?.Invoke(mediaKind);
        }

        public void Clear(string mediaKind)
        {
            if (_pools.Remove(mediaKind))
            {
                PoolChanged?.Invoke(mediaKind);
            }
        }
    }
}
