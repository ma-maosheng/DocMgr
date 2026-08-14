using System;
using System.Collections.Generic;

namespace DocMgr.Models.YearlyArchive
{
    public sealed class SearchPoolListCriteria
    {
        public string MediaKind { get; set; } = string.Empty;

        /// <summary>空或「全部年度」表示不限；否则为立档年度字符串。</summary>
        public string? Year { get; set; }

        public string Keyword { get; set; } = string.Empty;

        public string? Status { get; set; }

        public bool OnlyMine { get; set; }
    }

    public sealed class SearchPoolListItem
    {
        public int Id { get; init; }

        public string ResultSetNo { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string MediaKind { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string StatusDisplay { get; init; } = string.Empty;

        public string CreatedByName { get; init; } = string.Empty;

        public DateTime CreatedAt { get; init; }

        public DateTime? UpdatedAt { get; init; }

        public string Remarks { get; init; } = string.Empty;

        public int ItemCount { get; init; }

        public string UpdatedAtDisplay => (UpdatedAt ?? CreatedAt).ToString("yyyy-MM-dd HH:mm");
    }

    public sealed class UpdateSearchPoolRequest
    {
        public int ResultSetId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Remarks { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public IReadOnlyList<int> RemainingResultSetItemIds { get; set; } = Array.Empty<int>();
    }

    public sealed class SearchResultSetSaveResult
    {
        public YearlyArchiveSearchResultSet ResultSet { get; init; } = null!;
    }
}
