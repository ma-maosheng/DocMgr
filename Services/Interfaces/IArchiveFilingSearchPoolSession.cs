using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 立档检索池会话契约：维护当前检索池的状态与选中项。
    /// </summary>
    public interface IArchiveFilingSearchPoolSession
    {
        event Action<string>? PoolChanged;

        IReadOnlyList<ArchiveSearchPoolItemRow> GetPool(string mediaKind);

        ArchiveSearchPoolSupport.MergeResult Merge(
            string mediaKind,
            IReadOnlyList<ArchiveSearchPoolSelection> incoming,
            IReadOnlyDictionary<int, FiledArchiveSearchHit> hitsByFactId,
            IReadOnlyDictionary<int, MatchedContentEntryInfo> entriesById);

        void Replace(string mediaKind, IEnumerable<ArchiveSearchPoolItemRow> items);

        void Clear(string mediaKind);
    }
}
