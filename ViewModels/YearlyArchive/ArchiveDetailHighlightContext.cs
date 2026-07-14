using DocMgr.Models.YearlyArchive;
using System.Collections.Generic;
using System.Linq;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class ArchiveDetailHighlightContext
    {
        public string MediaKind { get; init; } = string.Empty;

        public int RegisterMediaId { get; init; }

        public int MediaItemId { get; init; }

        public string ItemType { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public string ContainerCode { get; init; } = string.Empty;

        public string ContentEntryKeyword { get; init; } = string.Empty;

        public string ContentEntryKindFilter { get; init; } = string.Empty;

        public IReadOnlyList<int> MatchedContentEntryIds { get; init; } = [];

        public bool HasContentEntryHighlight =>
            MatchedContentEntryIds.Count > 0
            || !string.IsNullOrWhiteSpace(ContentEntryKeyword);

        public RegisterDirectionSearchCriteria ToContentSearchCriteria()
        {
            return ContentEntrySearchSupport.CreateCriteria(
                ContentEntryKeyword,
                ContentEntryKindFilter);
        }

        public static ArchiveDetailHighlightContext FromHit(FiledArchiveSearchHit hit)
        {
            return new ArchiveDetailHighlightContext
            {
                MediaKind = hit.MediaKind,
                RegisterMediaId = hit.RegisterMediaId,
                MediaItemId = hit.MediaItemId,
                ItemType = hit.ItemType,
                ItemName = hit.ItemName,
                ContainerCode = hit.ContainerCode,
                ContentEntryKeyword = hit.ContentSearchKeyword,
                ContentEntryKindFilter = hit.ContentSearchKindFilter,
                MatchedContentEntryIds = hit.MatchedContentEntries
                    .Select(entry => entry.EntryId)
                    .ToList()
            };
        }
    }

    public sealed record ArchiveDetailOpenRequest(
        int RegisterRecordId,
        ArchiveDetailHighlightContext? SearchHighlight,
        string? FilterPoolMediaKind = null,
        int? FilingFactId = null);
}
