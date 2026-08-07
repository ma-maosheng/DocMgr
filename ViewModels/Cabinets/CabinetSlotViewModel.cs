using System;
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
            ArchiveBoxes = new ObservableCollection<ArchiveBoxItemViewModel>(descriptor.ArchiveBoxes.Select(box => new ArchiveBoxItemViewModel(box.BoxCode, box.BoxLabel, box.CategoryText, box.ArchiveTypeText, box.ArchiveIdentifierText, box.CountText, box.SlotCode, box.SequenceIndex, box.ItemCount, box.IsMixedPlacement, box.OriginalBoxNumberText, box.RelatedBoxCodesText, box.RelatedBoxCount, box.MixedPlacementHint, box.SourceSummaryText, box.PendingSortingRecordCount, box.BoxSpecification, box.PlacementMode, box.LayoutX, box.LayoutY, box.LayoutWidth, box.LayoutHeight, CanvasDisplayScale, box.YearlyArchiveBoxId, box.PendingReturnCopyCount, box.HasOccupationLock, box.OccupationLockToolTipText, box.OccupationLockBadgeText, box.IsYearlyArchiveDisplay, box.ArchiveSequenceNoShortText, box.YearText, box.ProjectText, box.InventoryMarkBadgeText)));
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
            IsYearlyMaterialsDedicatedSlot = descriptor.IsYearlyMaterialsDedicatedSlot;
            IsHistoricalMaterialsDedicatedSlot = descriptor.IsHistoricalMaterialsDedicatedSlot;
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

        public bool IsYearlyMaterialsDedicatedSlot { get; }

        public bool IsHistoricalMaterialsDedicatedSlot { get; }

        public string DedicatedSlotCategoryName { get; }

        private bool UsesOpticalDiscDedicatedLayout =>
            IsDataOpticalDiscDedicatedSlot
            || IsHistoricalDataOpticalDiscDedicatedSlot
            || IsDamagedOpticalDiscDedicatedSlot;

        public string PurposeDisplayText
        {
            get
            {
                if (!IsMagneticDiskSlot)
                {
                    if (string.IsNullOrWhiteSpace(DedicatedSlotCategoryName))
                    {
                        return string.Empty;
                    }

                    if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                            DedicatedSlotCategoryName,
                            CabinetArchiveSlotCategoryAssignment.CategoryUnset))
                    {
                        return CabinetArchiveSlotCategoryAssignment.CategoryUnset;
                    }

                    return ResolveShortPurposeDisplayText(DedicatedSlotCategoryName);
                }

                return string.IsNullOrWhiteSpace(DedicatedSlotCategoryName)
                    ? "通用"
                    : ResolveShortPurposeDisplayText(DedicatedSlotCategoryName);
            }
        }

        public Visibility ArchiveSlotPurposeVisibility =>
            !IsMagneticDiskSlot && !string.IsNullOrWhiteSpace(PurposeDisplayText)
                ? Visibility.Visible
                : Visibility.Collapsed;

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
                NotifyInteractiveRelocationDropHighlightChanged();
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

        public Visibility DamagedOpticalDiscSlotVisibility => IsDamagedOpticalDiscDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DataDiskSlotVisibility => IsDataDiskDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DataOpticalDiscSlotVisibility => IsDataOpticalDiscDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HistoricalDataDiskSlotVisibility => IsHistoricalDataDiskDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HistoricalDataOpticalDiscSlotVisibility => IsHistoricalDataOpticalDiscDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public Visibility BlankDiskSlotVisibility => IsBlankDiskDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public bool IsGeneralHardDiskSlotCategory =>
            IsMagneticDiskSlot && string.IsNullOrWhiteSpace(DedicatedSlotCategoryName);

        public Visibility YearlyMaterialsSlotVisibility => IsYearlyMaterialsDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HistoricalMaterialsSlotVisibility => IsHistoricalMaterialsDedicatedSlot ? Visibility.Visible : Visibility.Collapsed;

        public bool IsUnsetArchiveSlotCategory =>
            !IsMagneticDiskSlot
            && (string.IsNullOrWhiteSpace(DedicatedSlotCategoryName)
                || CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                    DedicatedSlotCategoryName,
                    CabinetArchiveSlotCategoryAssignment.CategoryUnset));

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

                return IsContextMenuOpen || IsSelected
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

                return IsContextMenuOpen || IsSelected
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

        public double SlotBorderThickness =>
            InteractiveRelocationDropHighlight != InteractiveRelocationDropHighlightKind.None
            || IsContextMenuOpen
            || IsSelected
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

                return IsContextMenuOpen || IsSelected ? "#1D4ED8" : ResolveSlotAccentForeground(UtilizationRatio);
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
            IsElectronicMediaMagneticDiskSourceSlot;

        public bool IsRelocatableDedicatedMagneticDiskSlot =>
            IsMagneticDiskSlot
            && CabinetHardDiskSlotCategoryAssignment.IsRelocatableDedicatedSlotCategory(DedicatedSlotCategoryName);

        public bool IsElectronicMediaMagneticDiskSourceSlot =>
            IsRelocatableDedicatedMagneticDiskSlot
            && HardDiskPresentCount > 0
            && HardDiskMediaItems.Where(item => !item.IsEmpty).All(item => item.IsElectronicInStockOccupancy)
            && ElectronicMediaRelocationCandidateCount > 0;

        public int ElectronicMediaRelocationCandidateCount =>
            HardDiskMediaItems.Count(item => item.IsElectronicMediaRelocationCandidate);

        public bool IsBlankHardDiskMagneticDiskSourceSlot =>
            IsMagneticDiskSlot
            && IsBlankDiskDedicatedSlot
            && HardDiskPresentCount > 0
            && HardDiskMediaItems.Where(item => !item.IsEmpty).All(item => item.IsBlankInStock)
            && BlankHardDiskRelocationCandidateCount > 0;

        public int BlankHardDiskRelocationCandidateCount =>
            HardDiskMediaItems.Count(item => item.IsBlankHardDiskRelocationCandidate);

        public bool IsDamagedHardDiskMagneticDiskSourceSlot =>
            IsMagneticDiskSlot
            && IsDamagedDiskDedicatedSlot
            && HardDiskPresentCount > 0
            && HardDiskMediaItems.Where(item => !item.IsEmpty).All(item => item.IsDamagedInStockOccupancy)
            && DamagedHardDiskRelocationCandidateCount > 0;

        public int DamagedHardDiskRelocationCandidateCount =>
            HardDiskMediaItems.Count(item => item.IsDamagedHardDiskRelocationCandidate);

        public bool IsDamagedOpticalDiscMagneticDiskSourceSlot =>
            IsMagneticDiskSlot
            && IsDamagedOpticalDiscDedicatedSlot
            && HardDiskPresentCount > 0
            && HardDiskMediaItems.Where(item => !item.IsEmpty).All(item => item.IsDamagedOpticalDiscInStockOccupancy)
            && DamagedOpticalDiscRelocationCandidateCount > 0;

        public int DamagedOpticalDiscRelocationCandidateCount =>
            HardDiskMediaItems.Count(item => item.IsDamagedOpticalDiscRelocationCandidate);

        /// <summary>可整档口批量迁出的模拟盒数量（排除征用/预订）。</summary>
        public int RelocatableSimulatedArchiveBoxCount =>
            ArchiveBoxes.Count(box => box.CanInteractiveRelocate);

        public bool IsYearlySimulatedBatchRelocationSourceSlot =>
            IsYearlySimulatedOnlyArchiveSlot
            && RelocatableSimulatedArchiveBoxCount > 0;

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

        public bool CanAcceptBlankHardDiskBatchRelocationTarget(int incomingCount)
        {
            if (!IsMagneticDiskSlot || !IsBlankDiskDedicatedSlot || incomingCount <= 0)
            {
                return false;
            }

            // 物理占用按「在库空盘」判断（含征用锁）；征用锁只禁止该盘作迁档源，不禁止整口作目标。
            if (HardDiskMediaItems.Any(item => !item.IsEmpty && !item.IsBlankInStock))
            {
                return false;
            }

            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryBlank);
            int occupiedCount = HardDiskMediaItems.Count(item => item.IsBlankInStock);
            return occupiedCount + incomingCount <= slotCapacity;
        }

        public bool CanAcceptDamagedHardDiskBatchRelocationTarget(int incomingCount)
        {
            if (!IsMagneticDiskSlot || !IsDamagedDiskDedicatedSlot || incomingCount <= 0)
            {
                return false;
            }

            if (HardDiskMediaItems.Any(item => !item.IsEmpty && !item.IsDamagedInStockOccupancy))
            {
                return false;
            }

            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryDamaged);
            int occupiedCount = HardDiskMediaItems.Count(item => item.IsDamagedInStockOccupancy);
            return occupiedCount + incomingCount <= slotCapacity;
        }

        public bool CanAcceptDamagedOpticalDiscBatchRelocationTarget(int incomingCount)
        {
            if (!IsMagneticDiskSlot || !IsDamagedOpticalDiscDedicatedSlot || incomingCount <= 0)
            {
                return false;
            }

            if (HardDiskMediaItems.Any(item => !item.IsEmpty && !item.IsDamagedOpticalDiscInStockOccupancy))
            {
                return false;
            }

            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc);
            int occupiedCount = HardDiskMediaItems.Count(item => item.IsDamagedOpticalDiscInStockOccupancy);
            return occupiedCount + incomingCount <= slotCapacity;
        }

        public bool CanAcceptBlankHardDiskInteractiveRelocationTarget(int incomingCount = 1)
            => CanAcceptBlankHardDiskBatchRelocationTarget(incomingCount);

        public bool CanAcceptInteractiveItemRelocationTarget(string mediaKind, string? sourceDedicatedCategoryName)
        {
            if (string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindBlankHardDisk, StringComparison.Ordinal))
            {
                return IsMagneticDiskSlot
                    && CanAcceptBlankHardDiskInteractiveRelocationTarget();
            }

            if (string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindDamagedHardDisk, StringComparison.Ordinal))
            {
                return IsMagneticDiskSlot
                    && CanAcceptDamagedHardDiskBatchRelocationTarget(1);
            }

            if (string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindDamagedOpticalDisc, StringComparison.Ordinal))
            {
                return IsMagneticDiskSlot
                    && CanAcceptDamagedOpticalDiscBatchRelocationTarget(1);
            }

            if (string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
            {
                return IsMagneticDiskSlot
                    && CanAcceptElectronicBatchRelocationTarget(sourceDedicatedCategoryName);
            }

            if (IsMagneticDiskSlot)
            {
                return false;
            }

            // 年度模拟档案盒：标准滑道式须为年度资料专用档口；立式/卧式不限制用途标记。
            if (IsYearlyMaterialsDedicatedSlot)
            {
                return true;
            }

            if (IsHistoricalMaterialsDedicatedSlot
                || CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                    DedicatedSlotCategoryName,
                    CabinetArchiveSlotCategoryAssignment.CategoryUnset))
            {
                return false;
            }

            // 无档口用途记录（如立式/卧式）仍允许迁入。
            return string.IsNullOrWhiteSpace(DedicatedSlotCategoryName);
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
            var presentItems = descriptor.HardDiskMedia
                .Select(item => new CabinetHardDiskMediumItemViewModel(item))
                .Where(item => !item.IsEmpty)
                .ToList();

            if (!descriptor.IsMagneticDiskSlot)
            {
                return presentItems;
            }

            // 仅展示已占用盘位；不再用「空位」占位卡填满矩阵（序号仍由卡片左上角标示）。
            bool placeBySequence = presentItems.Any(item => item.ArchiveSequenceNumber > 0);
            if (placeBySequence)
            {
                return presentItems
                    .OrderBy(item => item.ArchiveSequenceNumber <= 0 ? int.MaxValue : item.ArchiveSequenceNumber)
                    .ThenBy(item => item.DiskCodeText, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return presentItems
                .OrderBy(item => item.DiskCodeText, StringComparer.OrdinalIgnoreCase)
                .ToList();
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

            if (IsYearlyMaterialsDedicatedSlot)
            {
                return "#EFF6FF";
            }

            if (IsHistoricalMaterialsDedicatedSlot)
            {
                return "#F5F3FF";
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

            if (IsYearlyMaterialsDedicatedSlot)
            {
                return "#93C5FD";
            }

            if (IsHistoricalMaterialsDedicatedSlot)
            {
                return "#C4B5FD";
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

            if (IsYearlyMaterialsDedicatedSlot)
            {
                return "#DBEAFE";
            }

            if (IsHistoricalMaterialsDedicatedSlot)
            {
                return "#EDE9FE";
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

            if (IsYearlyMaterialsDedicatedSlot)
            {
                return "#BFDBFE";
            }

            if (IsHistoricalMaterialsDedicatedSlot)
            {
                return "#DDD6FE";
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

            if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(categoryName, CabinetArchiveSlotCategoryAssignment.CategoryYearlyMaterials))
            {
                return "年度资料";
            }

            if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(categoryName, CabinetArchiveSlotCategoryAssignment.CategoryHistoricalMaterials))
            {
                return "历史资料";
            }

            if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(categoryName, CabinetArchiveSlotCategoryAssignment.CategoryUnset))
            {
                return CabinetArchiveSlotCategoryAssignment.CategoryUnset;
            }

            return categoryName;
        }
    }
}
