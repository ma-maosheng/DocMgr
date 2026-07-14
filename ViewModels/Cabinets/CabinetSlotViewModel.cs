using System.Windows;
using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Linq;

namespace DocMgr.ViewModels.Cabinets
{
    public class CabinetSlotViewModel : ViewModelBase
    {
        public enum InteractiveRelocationDropHighlightKind
        {
            None,
            Allowed,
            Denied
        }

        private const double TargetCanvasDisplayWidth = 800d;
        private const double TargetCanvasDisplayHeight = 300d;
        private const double MagneticDiskDisplayHeightScale = 1.1d;
        private const double SlotHorizontalChrome = 20d;
        private const double SlotVerticalChrome = 108d;
        private const int DataOpticalDiscSlotCapacity = 20;
        private bool _isContextMenuOpen;
        private bool _isSelected;
        private InteractiveRelocationDropHighlightKind _interactiveRelocationDropHighlight;

        public CabinetSlotViewModel(CabinetSlotDescriptor descriptor)
        {
            VisualRowIndex = descriptor.VisualRowIndex;
            VisualColumnIndex = descriptor.VisualColumnIndex;
            LayerIndex = descriptor.LayerIndex;
            ColumnIndex = descriptor.ColumnIndex;
            SlotCode = descriptor.SlotCode;
            Face = descriptor.Face;
            SlotCanvasWidth = descriptor.SlotCanvasWidth;
            SlotCanvasHeight = descriptor.SlotCanvasHeight;
            CanvasDisplayScale = ResolveCanvasDisplayScale(SlotCanvasWidth, SlotCanvasHeight, descriptor.IsMagneticDiskSlot);
            IsMagneticDiskSlot = descriptor.IsMagneticDiskSlot;
            HardDiskCapacity = descriptor.HardDiskCapacity <= 0 ? 10 : descriptor.HardDiskCapacity;
            ArchiveBoxes = new ObservableCollection<ArchiveBoxItemViewModel>(descriptor.ArchiveBoxes.Select(box => new ArchiveBoxItemViewModel(box.BoxCode, box.BoxLabel, box.CategoryText, box.ArchiveTypeText, box.ArchiveIdentifierText, box.CountText, box.SlotCode, box.SequenceIndex, box.ItemCount, box.IsMixedPlacement, box.OriginalBoxNumberText, box.RelatedBoxCodesText, box.RelatedBoxCount, box.MixedPlacementHint, box.SourceSummaryText, box.PendingSortingRecordCount, box.BoxSpecification, box.PlacementMode, box.LayoutX, box.LayoutY, box.LayoutWidth, box.LayoutHeight, CanvasDisplayScale, box.YearlyArchiveBoxId, box.PendingReturnCopyCount, box.HasOccupationLock, box.OccupationLockToolTipText, box.IsYearlyArchiveDisplay, box.ArchiveSequenceNoShortText, box.YearText, box.ProjectText)));
            HardDiskMediaItems = new ObservableCollection<CabinetHardDiskMediumItemViewModel>(BuildHardDiskMediaItems(descriptor));
            PendingReturnMediaItems = new ObservableCollection<CabinetHardDiskMediumItemViewModel>(descriptor.PendingReturnMedia.Select(item => new CabinetHardDiskMediumItemViewModel(item)));
            UtilizationRatio = descriptor.UtilizationRatio;
            UtilizationText = descriptor.UtilizationText;
            CapacitySummaryText = descriptor.CapacitySummaryText;
            RemainingSummaryText = descriptor.RemainingSummaryText;
            LayoutModeText = descriptor.LayoutModeText;
            SlotToolTipText = descriptor.SlotToolTipText;
            IsCrossFaceLinked = descriptor.IsCrossFaceLinked;
            IsSpecialRule = descriptor.IsSpecialRule;
            SpecialRuleText = descriptor.SpecialRuleText;
            IsDamagedDiskDedicatedSlot = descriptor.IsDamagedDiskDedicatedSlot;
            IsDamagedOpticalDiscDedicatedSlot = descriptor.IsDamagedOpticalDiscDedicatedSlot;
            IsDataDiskDedicatedSlot = descriptor.IsDataDiskDedicatedSlot;
            IsDataOpticalDiscDedicatedSlot = descriptor.IsDataOpticalDiscDedicatedSlot;
            IsHistoricalDataDiskDedicatedSlot = descriptor.IsHistoricalDataDiskDedicatedSlot;
            IsHistoricalDataOpticalDiscDedicatedSlot = descriptor.IsHistoricalDataOpticalDiscDedicatedSlot;
            IsBlankDiskDedicatedSlot = descriptor.IsBlankDiskDedicatedSlot;
            DedicatedSlotCategoryName = descriptor.DedicatedSlotCategoryName;
        }

        public int VisualRowIndex { get; }

        public int VisualColumnIndex { get; }

        public int LayerIndex { get; }

        public int ColumnIndex { get; }

        public string SlotCode { get; }

        public CabinetFace Face { get; }

        public ObservableCollection<ArchiveBoxItemViewModel> ArchiveBoxes { get; }

        public ObservableCollection<CabinetHardDiskMediumItemViewModel> HardDiskMediaItems { get; }

        public ObservableCollection<CabinetHardDiskMediumItemViewModel> PendingReturnMediaItems { get; }

        public bool IsMagneticDiskSlot { get; }

        public int HardDiskCapacity { get; }

        public int HardDiskGridColumns => 5;

        public int HardDiskGridRows => UsesOpticalDiscDedicatedLayout ? 4 : 2;

        public double SlotCanvasWidth { get; }

        public double SlotCanvasHeight { get; }

        public double CanvasDisplayScale { get; }

        public double RenderSlotCanvasWidth => SlotCanvasWidth * CanvasDisplayScale;

        public double RenderSlotCanvasHeight => SlotCanvasHeight * CanvasDisplayScale;

        public double SlotDisplayWidth => RenderSlotCanvasWidth + SlotHorizontalChrome;

        public double SlotDisplayHeight => RenderSlotCanvasHeight + EffectiveSlotVerticalChrome;

        /// <summary>
        /// 未应用视口缩放时的档口设计显示宽度（快照铺满计算基准）。
        /// </summary>
        public double BaseSlotDisplayWidth => SlotCanvasWidth * CanvasDisplayScale + SlotHorizontalChrome;

        /// <summary>
        /// 未应用视口缩放时的档口设计显示高度（快照铺满计算基准）。
        /// </summary>
        public double BaseSlotDisplayHeight => SlotCanvasHeight * CanvasDisplayScale + EffectiveSlotVerticalChrome;

        /// <summary>
        /// 单档口快照：按画布像素尺寸重算档案盒布局坐标（与防磁柜容器放大机制一致）。
        /// </summary>
        public void UpdateSnapshotCanvasLayout(double renderCanvasWidth, double renderCanvasHeight)
        {
            if (IsMagneticDiskSlot || SlotCanvasWidth <= 0d || SlotCanvasHeight <= 0d)
            {
                return;
            }

            double scaleX = renderCanvasWidth / SlotCanvasWidth;
            double scaleY = renderCanvasHeight / SlotCanvasHeight;
            double absoluteCanvasScale = Math.Min(scaleX, scaleY);
            foreach (ArchiveBoxItemViewModel archiveBox in ArchiveBoxes)
            {
                archiveBox.ApplySnapshotCanvasScale(absoluteCanvasScale);
            }
        }

        private double EffectiveSlotVerticalChrome => IsMagneticDiskSlot
            ? SlotVerticalChrome * MagneticDiskDisplayHeightScale
            : SlotVerticalChrome;

        public double UtilizationRatio { get; }

        public double UtilizationPercent => UtilizationRatio * 100d;

        public string UtilizationText { get; }

        public string CapacitySummaryText { get; }

        public string RemainingSummaryText { get; }

        public string LayoutModeText { get; }

        public string SlotToolTipText { get; }

        public bool IsCrossFaceLinked { get; }

        public bool IsSpecialRule { get; }

        public string SpecialRuleText { get; }

        public bool IsDamagedDiskDedicatedSlot { get; }

        public bool IsDamagedOpticalDiscDedicatedSlot { get; }

        public bool IsDataDiskDedicatedSlot { get; }

        public bool IsDataOpticalDiscDedicatedSlot { get; }

        public bool IsHistoricalDataDiskDedicatedSlot { get; }

        public bool IsHistoricalDataOpticalDiscDedicatedSlot { get; }

        public bool IsBlankDiskDedicatedSlot { get; }

        public string DedicatedSlotCategoryName { get; }

        private bool UsesOpticalDiscDedicatedLayout =>
            IsDataOpticalDiscDedicatedSlot
            || IsHistoricalDataOpticalDiscDedicatedSlot
            || IsDamagedOpticalDiscDedicatedSlot;

        public string PurposeDisplayText => string.IsNullOrWhiteSpace(DedicatedSlotCategoryName)
            ? "通用"
            : ResolveShortPurposeDisplayText(DedicatedSlotCategoryName);

        public InteractiveRelocationDropHighlightKind InteractiveRelocationDropHighlight
        {
            get => _interactiveRelocationDropHighlight;
            set
            {
                if (!SetProperty(ref _interactiveRelocationDropHighlight, value))
                {
                    return;
                }

                NotifyInteractiveRelocationDropHighlightChanged();
            }
        }

        public void ClearInteractiveRelocationDropHighlight()
        {
            InteractiveRelocationDropHighlight = InteractiveRelocationDropHighlightKind.None;
        }

        private void NotifyInteractiveRelocationDropHighlightChanged()
        {
            OnPropertyChanged(nameof(SlotBackground));
            OnPropertyChanged(nameof(SlotBorderBrush));
            OnPropertyChanged(nameof(SlotBorderThickness));
            OnPropertyChanged(nameof(SlotMatrixBackground));
            OnPropertyChanged(nameof(SlotMatrixBorderBrush));
            OnPropertyChanged(nameof(SlotAccentForeground));
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (!SetProperty(ref _isSelected, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(CompactBorderBrush));
                OnPropertyChanged(nameof(CompactBorderThickness));
                OnPropertyChanged(nameof(CompactSlotBackground));
            }
        }

        public string CompactSlotBackground => IsSelected ? "#DBEAFE" : SlotBackground;

        public string CompactBorderBrush => IsSelected ? "#2563EB" : SlotBorderBrush;

        public double CompactBorderThickness => IsSelected ? 2d : SlotBorderThickness;

        public Visibility ArchiveBoxesVisibility => IsMagneticDiskSlot ? Visibility.Collapsed : Visibility.Visible;

        public Visibility HardDiskMatrixVisibility => IsMagneticDiskSlot ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PendingReturnVisibility => PendingReturnMediaItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ArchiveBoxActionsVisibility => IsMagneticDiskSlot ? Visibility.Collapsed : Visibility.Visible;

        public Visibility SpecialRuleVisibility => IsSpecialRule ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DamagedDiskSlotVisibility => IsDamagedDiskDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public string DamagedDiskSlotMenuText => IsDamagedDiskDedicatedSlot ? "取消损坏硬盘专用档口" : "设为损坏硬盘专用档口";

        public Visibility DamagedOpticalDiscSlotVisibility => IsDamagedOpticalDiscDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public string DamagedOpticalDiscSlotMenuText => IsDamagedOpticalDiscDedicatedSlot
            ? $"取消{CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc}"
            : $"设为{CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc}";

        public Visibility DataDiskSlotVisibility => IsDataDiskDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public string DataDiskSlotMenuText => IsDataDiskDedicatedSlot
            ? $"取消{CabinetHardDiskSlotCategoryAssignment.CategoryData}"
            : $"设为{CabinetHardDiskSlotCategoryAssignment.CategoryData}";

        public Visibility DataOpticalDiscSlotVisibility => IsDataOpticalDiscDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public string DataOpticalDiscSlotMenuText => IsDataOpticalDiscDedicatedSlot
            ? $"取消{CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc}"
            : $"设为{CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc}";

        public Visibility HistoricalDataDiskSlotVisibility => IsHistoricalDataDiskDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public string HistoricalDataDiskSlotMenuText => IsHistoricalDataDiskDedicatedSlot
            ? $"取消{CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataHardDisk}"
            : $"设为{CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataHardDisk}";

        public Visibility HistoricalDataOpticalDiscSlotVisibility => IsHistoricalDataOpticalDiscDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public string HistoricalDataOpticalDiscSlotMenuText => IsHistoricalDataOpticalDiscDedicatedSlot
            ? $"取消{CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataOpticalDisc}"
            : $"设为{CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataOpticalDisc}";

        public Visibility BlankDiskSlotVisibility => IsBlankDiskDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public string BlankDiskSlotMenuText => IsBlankDiskDedicatedSlot ? "取消空白硬盘专用档口" : "设为空白硬盘专用档口";

        public bool IsContextMenuOpen
        {
            get => _isContextMenuOpen;
            set
            {
                if (!SetProperty(ref _isContextMenuOpen, value))
                {
                    return;
                }

                NotifyInteractiveRelocationDropHighlightChanged();
            }
        }

        public string SlotBackground
        {
            get
            {
                if (InteractiveRelocationDropHighlight == InteractiveRelocationDropHighlightKind.Allowed)
                {
                    return "#F5F3FF";
                }

                if (InteractiveRelocationDropHighlight == InteractiveRelocationDropHighlightKind.Denied)
                {
                    return "#FEF2F2";
                }

                return IsContextMenuOpen
                    ? "#DBEAFE"
                    : ResolveDedicatedSlotBackground() ?? ResolveSlotBackground(UtilizationRatio);
            }
        }

        public string SlotBorderBrush
        {
            get
            {
                if (InteractiveRelocationDropHighlight == InteractiveRelocationDropHighlightKind.Allowed)
                {
                    return "#7C3AED";
                }

                if (InteractiveRelocationDropHighlight == InteractiveRelocationDropHighlightKind.Denied)
                {
                    return "#DC2626";
                }

                return IsContextMenuOpen
                    ? "#2563EB"
                    : ResolveDedicatedSlotBorderBrush() ?? ResolveSlotBorderBrush(UtilizationRatio);
            }
        }

        public string SlotMatrixBackground => InteractiveRelocationDropHighlight == InteractiveRelocationDropHighlightKind.Allowed
            ? "#EDE9FE"
            : InteractiveRelocationDropHighlight == InteractiveRelocationDropHighlightKind.Denied
                ? "#FEE2E2"
                : ResolveDedicatedSlotMatrixBackground() ?? "#F8FAFC";

        public string SlotMatrixBorderBrush => InteractiveRelocationDropHighlight == InteractiveRelocationDropHighlightKind.Allowed
            ? "#C4B5FD"
            : InteractiveRelocationDropHighlight == InteractiveRelocationDropHighlightKind.Denied
                ? "#FCA5A5"
                : ResolveDedicatedSlotMatrixBorderBrush() ?? "#CBD5E1";

        public double SlotBorderThickness => InteractiveRelocationDropHighlight != InteractiveRelocationDropHighlightKind.None || IsContextMenuOpen
            ? 2.5d
            : 1d;

        public string SlotAccentForeground
        {
            get
            {
                if (InteractiveRelocationDropHighlight == InteractiveRelocationDropHighlightKind.Allowed)
                {
                    return "#6D28D9";
                }

                if (InteractiveRelocationDropHighlight == InteractiveRelocationDropHighlightKind.Denied)
                {
                    return "#B91C1C";
                }

                return IsContextMenuOpen ? "#1D4ED8" : ResolveSlotAccentForeground(UtilizationRatio);
            }
        }

        public int MixedArchiveBoxCount => ArchiveBoxes.Count(box => box.IsMixedPlacement);

        public int PendingSortingRecordCount => ArchiveBoxes.Where(box => box.IsMixedPlacement).Sum(box => box.PendingSortingRecordCount);

        public int HardDiskPresentCount => HardDiskMediaItems.Count(item => !item.IsEmpty);

        public int PendingReturnMediumCount => PendingReturnMediaItems.Count;

        public bool IsFullyEmptyArchiveSlot => !IsMagneticDiskSlot && ArchiveBoxes.Count == 0;

        public bool IsFullyEmptyMagneticDiskSlot =>
            IsMagneticDiskSlot && HardDiskPresentCount == 0;

        public bool IsYearlyDataMagneticDiskSourceSlot =>
            IsMagneticDiskSlot
            && (IsDataDiskDedicatedSlot || IsDataOpticalDiscDedicatedSlot)
            && HardDiskPresentCount > 0
            && HardDiskMediaItems.Where(item => !item.IsEmpty).All(item => item.IsYearlyArchiveDisplay);

        public bool CanAcceptElectronicBatchRelocationTarget(string? sourceDedicatedCategoryName)
        {
            if (!IsMagneticDiskSlot)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourceDedicatedCategoryName))
            {
                return false;
            }

            return CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                DedicatedSlotCategoryName,
                sourceDedicatedCategoryName);
        }

        public bool CanAcceptInteractiveItemRelocationTarget(string mediaKind, string? sourceDedicatedCategoryName)
        {
            if (string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
            {
                return IsMagneticDiskSlot
                    && CanAcceptElectronicBatchRelocationTarget(sourceDedicatedCategoryName);
            }

            return !IsMagneticDiskSlot;
        }

        public bool IsYearlySimulatedOnlyArchiveSlot =>
            !IsMagneticDiskSlot
            && ArchiveBoxes.Count > 0
            && ArchiveBoxes.All(box => IsYearlySimulatedArchiveBox(box))
            && MixedArchiveBoxCount == 0;

        private static bool IsYearlySimulatedArchiveBox(ArchiveBoxItemViewModel box)
        {
            return string.Equals(box.SourceSummaryText, "年度资料", StringComparison.Ordinal)
                || string.Equals(box.ArchiveTypeText, "年度资料", StringComparison.Ordinal);
        }

        private IEnumerable<CabinetHardDiskMediumItemViewModel> BuildHardDiskMediaItems(CabinetSlotDescriptor descriptor)
        {
            var items = descriptor.HardDiskMedia
                .Select(item => new CabinetHardDiskMediumItemViewModel(item))
                .ToList();

            if (!descriptor.IsMagneticDiskSlot)
            {
                return items;
            }

            int slotCapacity = ResolveSlotCapacity(descriptor);
            int cellCount = System.Math.Max(slotCapacity, items.Count);
            while (items.Count < cellCount)
            {
                items.Add(CabinetHardDiskMediumItemViewModel.CreateEmpty());
            }

            int columns = 5;
            int rows = DescriptorUsesOpticalDiscDedicatedLayout(descriptor) ? 4 : 2;
            return ReorderForBottomLeftToTopRight(items, columns, rows);
        }

        private static bool DescriptorUsesOpticalDiscDedicatedLayout(CabinetSlotDescriptor descriptor) =>
            descriptor.IsDataOpticalDiscDedicatedSlot
            || descriptor.IsHistoricalDataOpticalDiscDedicatedSlot
            || descriptor.IsDamagedOpticalDiscDedicatedSlot;

        private static List<CabinetHardDiskMediumItemViewModel> ReorderForBottomLeftToTopRight(
            IReadOnlyList<CabinetHardDiskMediumItemViewModel> items,
            int columns,
            int rows)
        {
            if (items.Count == 0 || columns <= 0 || rows <= 0)
            {
                return items.ToList();
            }

            var ordered = new List<CabinetHardDiskMediumItemViewModel>(items.Count);
            for (int visualRow = 0; visualRow < rows; visualRow++)
            {
                int sourceRow = rows - 1 - visualRow;
                for (int column = 0; column < columns; column++)
                {
                    int sourceIndex = sourceRow * columns + column;
                    if (sourceIndex >= 0 && sourceIndex < items.Count)
                    {
                        ordered.Add(items[sourceIndex]);
                    }
                }
            }

            return ordered;
        }

        private static int ResolveSlotCapacity(CabinetSlotDescriptor descriptor)
        {
            if (DescriptorUsesOpticalDiscDedicatedLayout(descriptor))
            {
                return DataOpticalDiscSlotCapacity;
            }

            return descriptor.HardDiskCapacity <= 0 ? 10 : descriptor.HardDiskCapacity;
        }

        private static double ResolveCanvasDisplayScale(double slotCanvasWidth, double slotCanvasHeight, bool isMagneticDiskSlot)
        {
            if (slotCanvasWidth <= 0d || slotCanvasHeight <= 0d)
            {
                return 1d;
            }

            double targetDisplayHeight = isMagneticDiskSlot
                ? TargetCanvasDisplayHeight * MagneticDiskDisplayHeightScale
                : TargetCanvasDisplayHeight;
            double widthScale = TargetCanvasDisplayWidth / slotCanvasWidth;
            double heightScale = targetDisplayHeight / slotCanvasHeight;
            return System.Math.Max(1d, System.Math.Min(widthScale, heightScale));
        }

        private static string ResolveSlotBackground(double ratio)
        {
            return ratio switch
            {
                <= 0 => "#F8FAFC",
                <= 0.5 => "#F0FDF4",
                <= 0.8 => "#FEFCE8",
                <= 1.0 => "#FFF7ED",
                _ => "#FEF2F2"
            };
        }

        private static string ResolveSlotBorderBrush(double ratio)
        {
            return ratio switch
            {
                <= 0 => "#CBD5E1",
                <= 0.5 => "#86EFAC",
                <= 0.8 => "#FDE68A",
                <= 1.0 => "#FDBA74",
                _ => "#FCA5A5"
            };
        }

        private static string ResolveSlotAccentForeground(double ratio)
        {
            return ratio switch
            {
                <= 0 => "#475569",
                <= 0.5 => "#166534",
                <= 0.8 => "#A16207",
                <= 1.0 => "#C2410C",
                _ => "#B91C1C"
            };
        }

        private string? ResolveDedicatedSlotBackground()
        {
            if (IsHistoricalDataDiskDedicatedSlot)
            {
                return "#EEF2FF";
            }

            if (IsHistoricalDataOpticalDiscDedicatedSlot)
            {
                return "#FAF5FF";
            }

            if (IsDataDiskDedicatedSlot)
            {
                return "#EFF6FF";
            }

            if (IsDataOpticalDiscDedicatedSlot)
            {
                return "#FDF2F8";
            }

            if (IsDamagedDiskDedicatedSlot)
            {
                return "#FEF2F2";
            }

            if (IsDamagedOpticalDiscDedicatedSlot)
            {
                return "#FFE4E6";
            }

            if (IsBlankDiskDedicatedSlot)
            {
                return "#F7FEE7";
            }

            return null;
        }

        private string? ResolveDedicatedSlotBorderBrush()
        {
            if (IsHistoricalDataDiskDedicatedSlot)
            {
                return "#A5B4FC";
            }

            if (IsHistoricalDataOpticalDiscDedicatedSlot)
            {
                return "#E879F9";
            }

            if (IsDataDiskDedicatedSlot)
            {
                return "#93C5FD";
            }

            if (IsDataOpticalDiscDedicatedSlot)
            {
                return "#F9A8D4";
            }

            if (IsDamagedDiskDedicatedSlot)
            {
                return "#FCA5A5";
            }

            if (IsDamagedOpticalDiscDedicatedSlot)
            {
                return "#FB7185";
            }

            if (IsBlankDiskDedicatedSlot)
            {
                return "#BEF264";
            }

            return null;
        }

        private string? ResolveDedicatedSlotMatrixBackground()
        {
            if (IsHistoricalDataDiskDedicatedSlot)
            {
                return "#E0E7FF";
            }

            if (IsHistoricalDataOpticalDiscDedicatedSlot)
            {
                return "#F5D0FE";
            }

            if (IsDataDiskDedicatedSlot)
            {
                return "#DBEAFE";
            }

            if (IsDataOpticalDiscDedicatedSlot)
            {
                return "#FCE7F3";
            }

            if (IsDamagedDiskDedicatedSlot)
            {
                return "#FEE2E2";
            }

            if (IsDamagedOpticalDiscDedicatedSlot)
            {
                return "#FECDD3";
            }

            if (IsBlankDiskDedicatedSlot)
            {
                return "#ECFCCB";
            }

            return null;
        }

        private string? ResolveDedicatedSlotMatrixBorderBrush()
        {
            if (IsHistoricalDataDiskDedicatedSlot)
            {
                return "#C7D2FE";
            }

            if (IsHistoricalDataOpticalDiscDedicatedSlot)
            {
                return "#F0ABFC";
            }

            if (IsDataDiskDedicatedSlot)
            {
                return "#BFDBFE";
            }

            if (IsDataOpticalDiscDedicatedSlot)
            {
                return "#FBCFE8";
            }

            if (IsDamagedDiskDedicatedSlot)
            {
                return "#FECACA";
            }

            if (IsDamagedOpticalDiscDedicatedSlot)
            {
                return "#FDA4AF";
            }

            if (IsBlankDiskDedicatedSlot)
            {
                return "#D9F99D";
            }

            return null;
        }

        private static string ResolveShortPurposeDisplayText(string categoryName)
        {
            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(categoryName, CabinetHardDiskSlotCategoryAssignment.CategoryDamaged))
            {
                return "损坏硬盘";
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(categoryName, CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc))
            {
                return "损坏光盘";
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(categoryName, CabinetHardDiskSlotCategoryAssignment.CategoryData))
            {
                return "年度硬盘";
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(categoryName, CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc))
            {
                return "年度光盘";
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(categoryName, CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataHardDisk))
            {
                return "历史硬盘";
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(categoryName, CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataOpticalDisc))
            {
                return "历史光盘";
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(categoryName, CabinetHardDiskSlotCategoryAssignment.CategoryBlank))
            {
                return "空白专用";
            }

            return categoryName;
        }
    }
}
