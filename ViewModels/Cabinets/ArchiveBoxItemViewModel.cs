using System.Windows;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Cabinets
{
    public class ArchiveBoxItemViewModel : ViewModelBase
    {
        private const double RenderHeightScale = 0.9d;
        private bool _isContextMenuOpen;
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (!SetProperty(ref _isSelected, value))
                {
                    return;
                }

                NotifyVisualStateChanged();
            }
        }

        private bool IsHighlighted => IsSelected || IsContextMenuOpen;

        private void NotifyVisualStateChanged()
        {
            OnPropertyChanged(nameof(BoxBackground));
            OnPropertyChanged(nameof(BoxBorderBrush));
            OnPropertyChanged(nameof(BoxBorderThickness));
            OnPropertyChanged(nameof(AccentBrush));
            OnPropertyChanged(nameof(TitleForeground));
            OnPropertyChanged(nameof(CategoryForeground));
            OnPropertyChanged(nameof(ArchiveTypeForeground));
            OnPropertyChanged(nameof(CountBackground));
            OnPropertyChanged(nameof(CountBorderBrush));
            OnPropertyChanged(nameof(CountForeground));
            OnPropertyChanged(nameof(SpecificationBadgeBackground));
            OnPropertyChanged(nameof(SpecificationBadgeBorderBrush));
            OnPropertyChanged(nameof(SpecificationBadgeForeground));
            OnPropertyChanged(nameof(RenderZIndex));
        }

        private readonly double _layoutDisplayScale;
        private double _canvasLayoutScale;

        public ArchiveBoxItemViewModel(string boxCode, string boxLabel, string categoryText, string archiveTypeText, string archiveIdentifierText, string countText, string slotCode, int sequenceIndex, int itemCount, bool isMixedPlacement, string originalBoxNumberText, string relatedBoxCodesText, int relatedBoxCount, string mixedPlacementHint, string sourceSummaryText, int pendingSortingRecordCount, string boxSpecification, string placementMode, double layoutX, double layoutY, double layoutWidth, double layoutHeight, double displayScale, int yearlyArchiveBoxId = 0, int pendingReturnCopyCount = 0, bool hasOccupationLock = false, string occupationLockToolTipText = "", bool isYearlyArchiveDisplay = false, string archiveSequenceNoShortText = "", string yearText = "", string projectText = "")
        {
            BoxCode = boxCode;
            BoxLabel = boxLabel;
            CategoryText = categoryText;
            ArchiveTypeText = archiveTypeText;
            ArchiveIdentifierText = archiveIdentifierText;
            CountText = countText;
            SlotCode = slotCode;
            SequenceIndex = sequenceIndex;
            ItemCount = itemCount;
            IsMixedPlacement = isMixedPlacement;
            OriginalBoxNumberText = originalBoxNumberText;
            RelatedBoxCodesText = relatedBoxCodesText;
            RelatedBoxCount = relatedBoxCount;
            MixedPlacementHint = mixedPlacementHint;
            SourceSummaryText = sourceSummaryText;
            PendingSortingRecordCount = pendingSortingRecordCount;
            BoxSpecification = boxSpecification;
            PlacementMode = placementMode;
            LayoutX = layoutX;
            LayoutY = layoutY;
            LayoutWidth = layoutWidth;
            LayoutHeight = layoutHeight;
            _layoutDisplayScale = displayScale;
            _canvasLayoutScale = displayScale;
            YearlyArchiveBoxId = yearlyArchiveBoxId;
            PendingReturnCopyCount = Math.Max(0, pendingReturnCopyCount);
            HasPendingReturn = PendingReturnCopyCount > 0;
            HasOccupationLock = hasOccupationLock;
            OccupationLockToolTipText = occupationLockToolTipText?.Trim() ?? string.Empty;
            IsYearlyArchiveDisplay = isYearlyArchiveDisplay;
            ArchiveSequenceNoShortText = archiveSequenceNoShortText?.Trim() ?? string.Empty;
            YearDisplayText = FormatLabelValue("年度", yearText);
            ProjectDisplayText = FormatLabelValue("项目", projectText);
        }

        public int YearlyArchiveBoxId { get; init; }

        public int PendingReturnCopyCount { get; init; }

        public bool HasPendingReturn { get; init; }

        public bool HasOccupationLock { get; init; }

        public string OccupationLockToolTipText { get; init; } = string.Empty;

        public Visibility OccupationLockBadgeVisibility => HasOccupationLock && !IsMixedPlacement ? Visibility.Visible : Visibility.Collapsed;

        public bool CanInteractiveRelocate =>
            YearlyArchiveBoxId > 0 && !IsMixedPlacement && ItemCount > 0;

        public string BoxCode { get; init; } = string.Empty;

        public string BoxLabel { get; init; } = string.Empty;

        public string CategoryText { get; init; } = string.Empty;

        public string ArchiveTypeText { get; init; } = string.Empty;

        public string ArchiveIdentifierText { get; init; } = string.Empty;

        public string ArchiveIdentifierCompactText => string.IsNullOrWhiteSpace(ArchiveIdentifierText)
            ? ArchiveTypeText
            : ArchiveIdentifierText.Replace("\r\n", " /").Replace("\n", " /");

        public string ArchiveIdentifierDetailText => string.IsNullOrWhiteSpace(ArchiveIdentifierText)
            ? ArchiveTypeText
            : ArchiveIdentifierText;

        public bool IsYearlyArchiveDisplay { get; }

        public string ArchiveSequenceNoShortText { get; } = string.Empty;

        public string YearDisplayText { get; } = string.Empty;

        public string ProjectDisplayText { get; } = string.Empty;

        public Visibility YearlyArchiveIdentifierVisibility => IsYearlyArchiveDisplay ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DefaultArchiveIdentifierVisibility => IsYearlyArchiveDisplay ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ArchiveSequenceNoShortVisibility => IsYearlyArchiveDisplay && !string.IsNullOrWhiteSpace(ArchiveSequenceNoShortText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        public bool IsTenCentimeterSpecification => !string.IsNullOrWhiteSpace(BoxSpecification)
            && BoxSpecification.Contains("10cm", System.StringComparison.OrdinalIgnoreCase);

        public double SpineIdentifierHorizontalScale => IsFrontOut || !IsTenCentimeterSpecification
            ? 1d
            : 0.5d;

        public string CountText { get; init; } = string.Empty;

        public int SequenceIndex { get; init; }

        public int ItemCount { get; init; }

        public string SlotCode { get; init; } = string.Empty;

        public bool IsMixedPlacement { get; init; }

        public string OriginalBoxNumberText { get; init; } = string.Empty;

        public string RelatedBoxCodesText { get; init; } = string.Empty;

        public int RelatedBoxCount { get; init; }

        public string MixedPlacementHint { get; init; } = string.Empty;

        public string SourceSummaryText { get; init; } = string.Empty;

        public int PendingSortingRecordCount { get; init; }

        public string BoxSpecification { get; init; } = string.Empty;

        public string PlacementMode { get; init; } = string.Empty;

        public double LayoutX { get; init; }

        public double LayoutY { get; init; }

        public double LayoutWidth { get; init; }

        public double LayoutHeight { get; init; }

        public double DisplayScale => _canvasLayoutScale;

        public double RenderX => LayoutX * DisplayScale;

        public double RenderY => LayoutY * DisplayScale;

        public double RenderWidth => LayoutWidth * DisplayScale;

        public double RenderHeight => LayoutHeight * DisplayScale * RenderHeightScale;

        /// <summary>
        /// 单档口快照：按画布尺寸重算盒位坐标（固定字号，不做图像式缩放）。
        /// </summary>
        public void ApplySnapshotCanvasScale(double absoluteCanvasScale)
        {
            double safeScale = absoluteCanvasScale <= 0d ? _layoutDisplayScale : absoluteCanvasScale;
            if (Math.Abs(_canvasLayoutScale - safeScale) < 0.0001d)
            {
                return;
            }

            _canvasLayoutScale = safeScale;
            OnPropertyChanged(nameof(DisplayScale));
            OnPropertyChanged(nameof(RenderX));
            OnPropertyChanged(nameof(RenderY));
            OnPropertyChanged(nameof(RenderWidth));
            OnPropertyChanged(nameof(RenderHeight));
        }

        public bool IsFrontOut => string.Equals(PlacementMode, "FrontOut", System.StringComparison.OrdinalIgnoreCase);

        public Visibility FrontOutVisibility => IsFrontOut ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SpineOutVisibility => IsFrontOut ? Visibility.Collapsed : Visibility.Visible;

        public string PlacementModeText => string.Equals(PlacementMode, "FrontOut", System.StringComparison.OrdinalIgnoreCase)
            ? "盒面向外"
            : "盒脊向外";

        public string PlacementModeBadgeText => string.Equals(PlacementMode, "FrontOut", System.StringComparison.OrdinalIgnoreCase)
            ? "面外"
            : "脊外";

        public string PlacementModeBadgeBackground => string.Equals(PlacementMode, "FrontOut", System.StringComparison.OrdinalIgnoreCase)
            ? "#DBEAFE"
            : "#DCFCE7";

        public string PlacementModeBadgeBorderBrush => string.Equals(PlacementMode, "FrontOut", System.StringComparison.OrdinalIgnoreCase)
            ? "#93C5FD"
            : "#86EFAC";

        public string PlacementModeBadgeForeground => string.Equals(PlacementMode, "FrontOut", System.StringComparison.OrdinalIgnoreCase)
            ? "#1D4ED8"
            : "#166534";

        public Visibility MixedBadgeVisibility => IsMixedPlacement ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PendingReturnBadgeVisibility => HasPendingReturn && !IsMixedPlacement ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PendingReturnDetailMenuVisibility =>
            HasPendingReturn && YearlyArchiveBoxId > 0 && !IsMixedPlacement
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string PendingReturnBadgeText => PendingReturnCopyCount > 1
            ? $"待还{PendingReturnCopyCount}份"
            : "待还";

        public string PendingReturnStatusText => PendingReturnCopyCount > 1
            ? $"部分提档待还 {PendingReturnCopyCount} 份"
            : "部分提档待还";

        public bool IsNonStandardSpecification => !string.IsNullOrWhiteSpace(BoxSpecification)
            && BoxSpecification.Contains("非标", System.StringComparison.OrdinalIgnoreCase);

        public Visibility NonStandardBadgeVisibility => IsNonStandardSpecification ? Visibility.Visible : Visibility.Collapsed;

        public bool IsContextMenuOpen
        {
            get => _isContextMenuOpen;
            set
            {
                if (!SetProperty(ref _isContextMenuOpen, value))
                {
                    return;
                }

                NotifyVisualStateChanged();
            }
        }

        public string BoxBackground => IsHighlighted ? "#DBEAFE" : IsMixedPlacement ? "#FEF2F2" : HasPendingReturn ? "#FFF7ED" : "#FFF8E1";

        public string BoxBorderBrush => IsHighlighted ? "#2563EB" : IsMixedPlacement ? "#FCA5A5" : HasPendingReturn ? "#FDBA74" : "#D97706";

        public double BoxBorderThickness => IsHighlighted ? 2d : 0.6d;

        public string AccentBrush => IsHighlighted ? "#2563EB" : IsMixedPlacement ? "#DC2626" : HasPendingReturn ? "#F59E0B" : "#C2410C";

        public string TitleForeground => IsHighlighted ? "#1E3A8A" : IsMixedPlacement ? "#991B1B" : HasPendingReturn ? "#9A3412" : "#7C2D12";

        public string CategoryForeground => IsHighlighted ? "#1D4ED8" : IsMixedPlacement ? "#B91C1C" : HasPendingReturn ? "#C2410C" : "#92400E";

        public string ArchiveTypeForeground => IsHighlighted ? "#2563EB" : IsMixedPlacement ? "#DC2626" : HasPendingReturn ? "#EA580C" : "#B45309";

        public string CountBackground => IsHighlighted ? "#EFF6FF" : IsMixedPlacement ? "#FFF1F2" : HasPendingReturn ? "#FFEDD5" : "#FFFBEB";

        public string CountBorderBrush => IsHighlighted ? "#60A5FA" : IsMixedPlacement ? "#FCA5A5" : HasPendingReturn ? "#FDBA74" : "#FCD34D";

        public string CountForeground => IsHighlighted ? "#1D4ED8" : IsMixedPlacement ? "#B91C1C" : HasPendingReturn ? "#C2410C" : "#92400E";

        public string SpecificationBadgeBackground => IsHighlighted ? "#DBEAFE" : "#EDE9FE";

        public string SpecificationBadgeBorderBrush => IsHighlighted ? "#93C5FD" : "#C4B5FD";

        public string SpecificationBadgeForeground => IsHighlighted ? "#1D4ED8" : "#6D28D9";

        public int RenderZIndex => IsHighlighted ? 1000 : SequenceIndex;

        public string ToolTipText => IsMixedPlacement
            ? $"档案盒：{BoxCode}\n盒签：{BoxLabel}\n状态：混放待梳理\n来源：{SourceSummaryText}\n标识信息：\n{ArchiveIdentifierDetailText}\n条目数：{ItemCount}条\n统计：{CountText}\n规格：{(string.IsNullOrWhiteSpace(BoxSpecification) ? "未登记" : BoxSpecification)}\n摆放：{PlacementModeText}\n涉及档案盒：{(string.IsNullOrWhiteSpace(RelatedBoxCodesText) ? "未登记" : RelatedBoxCodesText)}\n原始登记：{(string.IsNullOrWhiteSpace(OriginalBoxNumberText) ? "未登记" : OriginalBoxNumberText)}\n待梳理关联记录：{PendingSortingRecordCount}条\n提示：{MixedPlacementHint}"
            : HasPendingReturn
                ? $"档案盒：{BoxCode}\n盒签：{BoxLabel}\n状态：{PendingReturnStatusText}\n来源：{SourceSummaryText}\n标识信息：\n{ArchiveIdentifierDetailText}\n条目数：{ItemCount}条\n统计：{(string.IsNullOrWhiteSpace(CountText) ? "未登记" : CountText)}\n规格：{(string.IsNullOrWhiteSpace(BoxSpecification) ? "未登记" : BoxSpecification)}\n摆放：{PlacementModeText}\n档口：{SlotCode}\n序位：{SequenceIndex}{BuildOccupationLockToolTipSuffix()}"
                : $"档案盒：{BoxCode}\n盒签：{BoxLabel}\n来源：{SourceSummaryText}\n标识信息：\n{ArchiveIdentifierDetailText}\n条目数：{ItemCount}条\n统计：{(string.IsNullOrWhiteSpace(CountText) ? "未登记" : CountText)}\n规格：{(string.IsNullOrWhiteSpace(BoxSpecification) ? "未登记" : BoxSpecification)}\n摆放：{PlacementModeText}\n档口：{SlotCode}\n序位：{SequenceIndex}{BuildOccupationLockToolTipSuffix()}";

        private string BuildOccupationLockToolTipSuffix()
            => HasOccupationLock && !string.IsNullOrWhiteSpace(OccupationLockToolTipText)
                ? $"\n\n{OccupationLockToolTipText}"
                : string.Empty;

        private static string FormatLabelValue(string label, string? value)
        {
            string resolvedValue = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
            return $"{label} {resolvedValue}";
        }
    }
}
