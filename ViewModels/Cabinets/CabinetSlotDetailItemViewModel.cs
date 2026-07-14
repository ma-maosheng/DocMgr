using System.Collections.Generic;

namespace DocMgr.ViewModels.Cabinets
{
    /// <summary>
    /// 档口详情列表中的档案盒行。
    /// </summary>
    public sealed class CabinetSlotDetailArchiveBoxRowViewModel
    {
        public CabinetSlotDetailArchiveBoxRowViewModel(ArchiveBoxItemViewModel source)
        {
            ArgumentNullException.ThrowIfNull(source);

            BoxCode = source.BoxCode;
            SequenceDisplay = source.SequenceIndex > 0 ? source.SequenceIndex.ToString("D2") : string.Empty;
            BoxLabel = source.BoxLabel;
            CategoryText = source.CategoryText;
            ArchiveTypeText = source.ArchiveTypeText;
            ArchiveIdentifierText = source.ArchiveIdentifierDetailText;
            CountText = source.CountText;
            PlacementModeText = source.PlacementModeText;
            BoxSpecification = string.IsNullOrWhiteSpace(source.BoxSpecification) ? "未登记" : source.BoxSpecification;
            StatusNote = BuildStatusNote(source);
            CanOpenDetail = !string.IsNullOrWhiteSpace(source.BoxCode);
        }

        public string BoxCode { get; }

        public string SequenceDisplay { get; }

        public string BoxLabel { get; }

        public string CategoryText { get; }

        public string ArchiveTypeText { get; }

        public string ArchiveIdentifierText { get; }

        public string CountText { get; }

        public string PlacementModeText { get; }

        public string BoxSpecification { get; }

        public string StatusNote { get; }

        public bool CanOpenDetail { get; }

        private static string BuildStatusNote(ArchiveBoxItemViewModel source)
        {
            var notes = new List<string>();
            if (source.IsMixedPlacement)
            {
                notes.Add("混放待梳理");
            }

            if (source.HasPendingReturn)
            {
                notes.Add(source.PendingReturnStatusText);
            }

            if (source.PendingSortingRecordCount > 0)
            {
                notes.Add($"关联记录 {source.PendingSortingRecordCount} 条");
            }

            if (!string.IsNullOrWhiteSpace(source.SourceSummaryText))
            {
                notes.Add(source.SourceSummaryText.Trim());
            }

            return notes.Count == 0 ? string.Empty : string.Join("；", notes);
        }
    }

    /// <summary>
    /// 档口详情列表中的硬盘/光盘行。
    /// </summary>
    public sealed class CabinetSlotDetailHardDiskRowViewModel
    {
        public CabinetSlotDetailHardDiskRowViewModel(CabinetHardDiskMediumItemViewModel source, bool isPendingReturn)
        {
            ArgumentNullException.ThrowIfNull(source);

            PresenceKind = isPendingReturn ? "待归还" : "在位";
            SequenceDisplay = source.ArchiveSequenceText;
            DiskCodeText = source.DiskCodeText;
            CapacityText = source.CapacityText;
            StatusText = source.StatusText;
            CurrentLocationText = source.CurrentLocationText;
            ElectronicArchiveNoText = string.IsNullOrWhiteSpace(source.ElectronicArchiveNoText) ? "—" : source.ElectronicArchiveNoText;
            ElectronicArchiveLocationText = string.IsNullOrWhiteSpace(source.ElectronicArchiveLocationText) ? "—" : source.ElectronicArchiveLocationText;
            ElectronicArchiveUnitId = source.ElectronicArchiveUnitId;
            DetailText = string.IsNullOrWhiteSpace(source.CompactDetailText) ? source.SecondaryText : source.CompactDetailText;
            BadgeText = source.BadgeText;
            CanOpenDetail = !source.IsEmpty;
            IsArchiveInfoPreferred = source.CanShowArchiveInfo;
            InfoText = source.InfoText;
            ArchiveInfoText = source.ArchiveInfoText;
        }

        public string PresenceKind { get; }

        public string SequenceDisplay { get; }

        public string DiskCodeText { get; }

        public string CapacityText { get; }

        public string StatusText { get; }

        public string CurrentLocationText { get; }

        public string ElectronicArchiveNoText { get; }

        public string ElectronicArchiveLocationText { get; }

        public int ElectronicArchiveUnitId { get; }

        public string DetailText { get; }

        public string BadgeText { get; }

        public bool CanOpenDetail { get; }

        public bool IsArchiveInfoPreferred { get; }

        public string InfoText { get; }

        public string ArchiveInfoText { get; }
    }
}
