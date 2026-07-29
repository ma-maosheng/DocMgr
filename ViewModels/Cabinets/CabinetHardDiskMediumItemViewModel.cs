using System;
using System.Windows;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Cabinets
{
    public sealed class CabinetHardDiskMediumItemViewModel : ViewModelBase
    {
        private readonly string _baseCardBackground;
        private readonly string _baseCardBorderBrush;
        private readonly string _baseIconBodyBrush;
        private readonly string _baseIconAccentBrush;
        private readonly string _baseStatusBadgeBackground;
        private readonly string _baseStatusBadgeForeground;
        private readonly string _baseTitleForeground;
        private readonly string _baseDetailForeground;
        private bool _isSelected;

        private CabinetHardDiskMediumItemViewModel(string diskCodeText, string capacityText, string statusText, string secondaryText, string badgeText, string toolTipText, bool isEmpty, bool isPendingReturn, string cardBackground, string cardBorderBrush, string iconBodyBrush, string iconAccentBrush, string statusBadgeBackground, string statusBadgeForeground, string titleForeground, string detailForeground)
        {
            _baseCardBackground = cardBackground;
            _baseCardBorderBrush = cardBorderBrush;
            _baseIconBodyBrush = iconBodyBrush;
            _baseIconAccentBrush = iconAccentBrush;
            _baseStatusBadgeBackground = statusBadgeBackground;
            _baseStatusBadgeForeground = statusBadgeForeground;
            _baseTitleForeground = titleForeground;
            _baseDetailForeground = detailForeground;
            DiskCodeText = diskCodeText;
            CapacityText = capacityText;
            StatusText = statusText;
            SecondaryText = secondaryText;
            BadgeText = badgeText;
            ToolTipText = toolTipText;
            IsEmpty = isEmpty;
            IsPendingReturn = isPendingReturn;
            ElectronicArchiveUnitId = 0;
            MediumId = 0;
            IsBlankInStock = false;
        }

        public CabinetHardDiskMediumItemViewModel(CabinetHardDiskMediumDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            var visual = ResolveVisual(descriptor);
            _baseCardBackground = visual.CardBackground;
            _baseCardBorderBrush = visual.CardBorderBrush;
            _baseIconBodyBrush = visual.IconBodyBrush;
            _baseIconAccentBrush = visual.IconAccentBrush;
            _baseStatusBadgeBackground = visual.StatusBadgeBackground;
            _baseStatusBadgeForeground = visual.StatusBadgeForeground;
            _baseTitleForeground = visual.TitleForeground;
            _baseDetailForeground = visual.DetailForeground;
            IsYearlyArchiveDisplay = descriptor.IsYearlyArchiveDisplay;
            IsOpticalDiscMedia = descriptor.IsOpticalDiscMedia;
            DiskCodeText = string.IsNullOrWhiteSpace(descriptor.DiskCode) ? "未编号" : descriptor.DiskCode;
            CapacityText = string.IsNullOrWhiteSpace(descriptor.CapacityText) ? "容量未登记" : descriptor.CapacityText;
            StatusText = string.IsNullOrWhiteSpace(descriptor.StatusText) ? "状态未登记" : descriptor.StatusText;
            CurrentLocationText = string.IsNullOrWhiteSpace(descriptor.CurrentLocationText) ? "位置未登记" : descriptor.CurrentLocationText.Trim();
            ElectronicArchiveNoText = string.IsNullOrWhiteSpace(descriptor.ElectronicArchiveNoText) ? string.Empty : descriptor.ElectronicArchiveNoText.Trim();
            ElectronicArchiveNoShortText = ArchiveContainerCodeDisplaySupport.ToShortDisplayCode(ElectronicArchiveNoText);
            ElectronicArchiveLocationText = string.IsNullOrWhiteSpace(descriptor.ElectronicArchiveLocationText) ? string.Empty : descriptor.ElectronicArchiveLocationText.Trim();
            ArchiveInfoText = string.IsNullOrWhiteSpace(descriptor.ArchiveInfoText)
                ? $"硬盘：{DiskCodeText}\n尚未关联电子介质袋资料信息。"
                : descriptor.ArchiveInfoText;
            MediumInfoText = string.IsNullOrWhiteSpace(descriptor.MediumInfoText)
                ? descriptor.ToolTipText
                : descriptor.MediumInfoText;
            HasArchiveInfo = descriptor.HasArchiveInfo;
            ArchiveSequenceNumber = descriptor.ArchiveSequenceNumber;
            ArchiveSequenceText = string.IsNullOrWhiteSpace(descriptor.ArchiveSequenceText)
                ? (ArchiveSequenceNumber > 0 ? ArchiveSequenceNumber.ToString("D2") : string.Empty)
                : descriptor.ArchiveSequenceText.Trim();
            YearDisplayText = FormatLabelValue("年度", descriptor.YearText);
            ProjectDisplayText = FormatLabelValue("项目", descriptor.ProjectText);
            UsedCapacityDisplayText = FormatLabelValue("已用", descriptor.UsedCapacityDisplayText);
            RemainingCapacityDisplayText = FormatLabelValue("剩余", descriptor.RemainingCapacityDisplayText);
            YearlyHardDiskCapacityLineText = BuildYearlyCapacityLine(descriptor);
            SecondaryText = descriptor.IsPendingReturn
                ? BuildPendingReturnText(descriptor.CurrentLocationText, descriptor.CurrentHolderText)
                : BuildElectronicArchiveText(ElectronicArchiveNoText, ElectronicArchiveLocationText);
            CompactHeaderText = IsYearlyArchiveDisplay
                ? BuildYearlyCompactHeader(descriptor)
                : BuildCompactHeaderText(DiskCodeText, CapacityText);
            CompactDetailText = IsYearlyArchiveDisplay
                ? BuildYearlyCompactDetail(descriptor)
                : BuildCompactDetailText(StatusText, CurrentLocationText, ElectronicArchiveNoText, ElectronicArchiveLocationText);
            BadgeText = visual.BadgeText;
            ToolTipText = descriptor.ToolTipText;
            IsPendingReturn = descriptor.IsPendingReturn;
            ElectronicArchiveUnitId = descriptor.ElectronicArchiveUnitId;
            MediumId = descriptor.MediumId;
            IsBlankInStock = descriptor.IsBlankInStock;
            HasOccupationLock = descriptor.HasOccupationLock;
            OccupationLockToolTipText = descriptor.OccupationLockToolTipText ?? string.Empty;
            OccupationLockBadgeText = string.IsNullOrWhiteSpace(descriptor.OccupationLockBadgeText)
                ? (descriptor.HasOccupationLock ? "占用" : string.Empty)
                : descriptor.OccupationLockBadgeText.Trim();
            InventoryMarkBadgeText = descriptor.InventoryMarkBadgeText?.Trim() ?? string.Empty;
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

                NotifySelectionVisualChanged();
            }
        }

        private void NotifySelectionVisualChanged()
        {
            OnPropertyChanged(nameof(CardBackground));
            OnPropertyChanged(nameof(CardBorderBrush));
            OnPropertyChanged(nameof(CardBorderThickness));
            OnPropertyChanged(nameof(IconBodyBrush));
            OnPropertyChanged(nameof(IconAccentBrush));
            OnPropertyChanged(nameof(StatusBadgeBackground));
            OnPropertyChanged(nameof(StatusBadgeForeground));
            OnPropertyChanged(nameof(TitleForeground));
            OnPropertyChanged(nameof(DetailForeground));
        }

        public bool IsYearlyArchiveDisplay { get; }

        public bool IsOpticalDiscMedia { get; }

        public string DiskCodeText { get; } = string.Empty;

        public string CapacityText { get; } = string.Empty;

        public string StatusText { get; } = string.Empty;

        public string CurrentLocationText { get; } = string.Empty;

        public string SecondaryText { get; } = string.Empty;

        public string ElectronicArchiveNoText { get; } = string.Empty;

        public string ElectronicArchiveNoShortText { get; } = string.Empty;

        public string ElectronicArchiveLocationText { get; } = string.Empty;

        public string ArchiveInfoText { get; } = string.Empty;

        public string MediumInfoText { get; } = string.Empty;

        public bool IsBlankInStock { get; }

        public bool HasArchiveInfo { get; }

        public int ArchiveSequenceNumber { get; }

        public string ArchiveSequenceText { get; } = string.Empty;

        public Visibility ArchiveSequenceVisibility => ArchiveSequenceNumber > 0 ? Visibility.Visible : Visibility.Collapsed;

        public string YearDisplayText { get; } = string.Empty;

        public string ProjectDisplayText { get; } = string.Empty;

        public string UsedCapacityDisplayText { get; } = string.Empty;

        public string RemainingCapacityDisplayText { get; } = string.Empty;

        public string YearlyHardDiskCapacityLineText { get; } = string.Empty;

        public string CompactHeaderText { get; } = string.Empty;

        public string CompactDetailText { get; } = string.Empty;

        public string BadgeText { get; } = string.Empty;

        public string ToolTipText { get; } = string.Empty;

        public string InfoText => MediumInfoText;

        public bool IsEmpty { get; }

        public int ElectronicArchiveUnitId { get; }

        public bool IsPendingReturn { get; }

        public bool HasOccupationLock { get; }

        public string OccupationLockToolTipText { get; } = string.Empty;

        public string OccupationLockBadgeText { get; } = string.Empty;

        public string InventoryMarkBadgeText { get; } = string.Empty;

        public Visibility OccupationLockBadgeVisibility => HasOccupationLock ? Visibility.Visible : Visibility.Collapsed;

        public Visibility InventoryMarkBadgeVisibility =>
            !string.IsNullOrWhiteSpace(InventoryMarkBadgeText) ? Visibility.Visible : Visibility.Collapsed;

        public bool CanShowInfo => !IsEmpty;

        public bool CanShowArchiveInfo => !IsEmpty && HasArchiveInfo;

        public bool CanInteractiveRelocate =>
            !IsEmpty
            && !IsPendingReturn
            && !HasOccupationLock
            && (IsElectronicMediaRelocationCandidate
                || IsBlankHardDiskRelocationCandidate
                || IsDamagedHardDiskRelocationCandidate
                || IsDamagedOpticalDiscRelocationCandidate);

        public bool IsElectronicMediaRelocationCandidate =>
            IsElectronicInStockOccupancy
            && !HasOccupationLock;

        /// <summary>电子介质袋在库占用（含征用/预订；用于目标容量与源档口类型判断）。</summary>
        public bool IsElectronicInStockOccupancy =>
            !IsEmpty
            && !IsPendingReturn
            && !IsBlankInStock
            && ElectronicArchiveUnitId > 0
            && !IsInventoryEmptyMark
            && !IsInventoryLostMark;

        public bool IsBlankHardDiskRelocationCandidate =>
            !IsEmpty
            && !IsPendingReturn
            && !IsOpticalDiscMedia
            && !IsYearlyArchiveDisplay
            && IsBlankInStock
            && !HasOccupationLock;

        /// <summary>损坏硬盘在库占用（含征用锁；用于目标档口容量，不等于可迁档源）。</summary>
        public bool IsDamagedInStockOccupancy =>
            !IsEmpty
            && !IsPendingReturn
            && !IsOpticalDiscMedia
            && !IsYearlyArchiveDisplay
            && MediumId > 0
            && string.Equals(StatusText, HardDiskMedium.StatusInStockDamaged, StringComparison.OrdinalIgnoreCase);

        /// <summary>损坏硬盘专用档口内的裸损坏硬盘（非电子袋展示）。</summary>
        public bool IsDamagedHardDiskRelocationCandidate =>
            IsDamagedInStockOccupancy
            && !HasOccupationLock;

        /// <summary>损坏光盘在库占用（含占用角标；用于目标容量）。</summary>
        public bool IsDamagedOpticalDiscInStockOccupancy =>
            !IsEmpty
            && !IsPendingReturn
            && IsOpticalDiscMedia
            && !IsYearlyArchiveDisplay
            && MediumId > 0
            && string.Equals(StatusText, OpticalDiscMedium.StatusDamaged, StringComparison.OrdinalIgnoreCase);

        /// <summary>损坏光盘专用档口内的裸损坏数据光盘（非电子袋展示）。</summary>
        public bool IsDamagedOpticalDiscRelocationCandidate =>
            IsDamagedOpticalDiscInStockOccupancy
            && !HasOccupationLock;

        private bool IsInventoryEmptyMark =>
            string.Equals(InventoryMarkBadgeText, "空", StringComparison.Ordinal);

        private bool IsInventoryLostMark =>
            string.Equals(InventoryMarkBadgeText, "失", StringComparison.Ordinal);

        public int MediumId { get; }

        public Visibility YearlyArchiveLayoutVisibility => IsYearlyArchiveDisplay ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DefaultLayoutVisibility => IsYearlyArchiveDisplay ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ElectronicArchiveNoShortVisibility => IsYearlyArchiveDisplay && !string.IsNullOrWhiteSpace(ElectronicArchiveNoShortText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility YearlyHardDiskCapacityLineVisibility => IsYearlyArchiveDisplay && !string.IsNullOrWhiteSpace(YearlyHardDiskCapacityLineText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility YearlyHardDiskCodeVisibility => IsYearlyArchiveDisplay && !IsOpticalDiscMedia
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility OpticalDiscIconVisibility => IsOpticalDiscMedia ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HardDiskIconVisibility => IsOpticalDiscMedia ? Visibility.Collapsed : Visibility.Visible;

        public string CardBackground => IsSelected ? "#DBEAFE" : _baseCardBackground;

        public string CardBorderBrush => IsSelected ? "#2563EB" : _baseCardBorderBrush;

        public double CardBorderThickness => IsSelected ? 2d : 1d;

        public string IconBodyBrush => IsSelected ? "#BFDBFE" : _baseIconBodyBrush;

        public string IconAccentBrush => IsSelected ? "#2563EB" : _baseIconAccentBrush;

        public string StatusBadgeBackground => IsSelected ? "#EFF6FF" : _baseStatusBadgeBackground;

        public string StatusBadgeForeground => IsSelected ? "#1D4ED8" : _baseStatusBadgeForeground;

        public string TitleForeground => IsSelected ? "#1E3A8A" : _baseTitleForeground;

        public string DetailForeground => IsSelected ? "#1D4ED8" : _baseDetailForeground;

        public Visibility SecondaryTextVisibility => string.IsNullOrWhiteSpace(SecondaryText) ? Visibility.Collapsed : Visibility.Visible;

        public static CabinetHardDiskMediumItemViewModel CreateEmpty()
        {
            return new CabinetHardDiskMediumItemViewModel(
                "空位",
                string.Empty,
                "可放置",
                string.Empty,
                "空",
                "当前档口空闲，可继续放置硬盘介质。",
                true,
                false,
                "#F8FAFC",
                "#CBD5E1",
                "#E2E8F0",
                "#94A3B8",
                "#E2E8F0",
                "#475569",
                "#475569",
                "#64748B");
        }

        private static string BuildCompactHeaderText(string diskCodeText, string capacityText)
        {
            if (string.IsNullOrWhiteSpace(diskCodeText))
            {
                return string.IsNullOrWhiteSpace(capacityText) ? string.Empty : capacityText;
            }

            return string.IsNullOrWhiteSpace(capacityText)
                ? diskCodeText.Trim()
                : $"{diskCodeText.Trim()} {capacityText.Trim()}";
        }

        private static string BuildYearlyCompactHeader(CabinetHardDiskMediumDescriptor descriptor)
        {
            if (descriptor.IsOpticalDiscMedia)
            {
                return $"{FormatLabelValue("年度", descriptor.YearText)} · {FormatLabelValue("项目", descriptor.ProjectText)}";
            }

            return FormatLabelValue("硬盘", descriptor.DiskCode);
        }

        private static string BuildYearlyCompactDetail(CabinetHardDiskMediumDescriptor descriptor)
        {
            if (descriptor.IsOpticalDiscMedia)
            {
                return $"{FormatLabelValue("年度", descriptor.YearText)} · {FormatLabelValue("项目", descriptor.ProjectText)} · {BuildYearlyCapacityLine(descriptor)}";
            }

            string bagNo = ArchiveContainerCodeDisplaySupport.ToShortDisplayCode(descriptor.ElectronicArchiveNoText);
            string bagSegment = string.IsNullOrWhiteSpace(bagNo) ? string.Empty : $"{bagNo} · ";
            return $"{bagSegment}{FormatLabelValue("硬盘", descriptor.DiskCode)} · {FormatLabelValue("年度", descriptor.YearText)} · {FormatLabelValue("项目", descriptor.ProjectText)} · {BuildYearlyCapacityLine(descriptor)}";
        }

        private static string BuildYearlyCapacityLine(CabinetHardDiskMediumDescriptor descriptor)
        {
            string usedToken = ToCompactCapacityToken(descriptor.UsedCapacityDisplayText);
            if (descriptor.IsOpticalDiscMedia)
            {
                return usedToken == "—" ? string.Empty : $"用{usedToken}";
            }

            string remainingToken = ToCompactCapacityToken(descriptor.RemainingCapacityDisplayText);
            return $"用{usedToken}、余{remainingToken}";
        }

        private static string ToCompactCapacityToken(string? capacityText)
        {
            if (string.IsNullOrWhiteSpace(capacityText) || string.Equals(capacityText.Trim(), "—", StringComparison.Ordinal))
            {
                return "—";
            }

            return capacityText.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        }

        private static string FormatLabelValue(string label, string? value)
        {
            string resolvedValue = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
            return $"{label} {resolvedValue}";
        }

        private static string BuildCompactDetailText(string statusText, string currentLocationText, string electronicArchiveNoText, string electronicArchiveLocationText)
        {
            string status = string.IsNullOrWhiteSpace(statusText) ? "状态未登记" : statusText.Trim();
            string location = string.IsNullOrWhiteSpace(currentLocationText) ? "位置未登记" : currentLocationText.Trim();
            string detail = $"{status}、现位：{location}";
            if (string.IsNullOrWhiteSpace(electronicArchiveNoText) && string.IsNullOrWhiteSpace(electronicArchiveLocationText))
            {
                return detail;
            }

            string archiveNo = string.IsNullOrWhiteSpace(electronicArchiveNoText) ? "未登记" : electronicArchiveNoText.Trim();
            string archiveLocation = string.IsNullOrWhiteSpace(electronicArchiveLocationText) ? "未登记" : electronicArchiveLocationText.Trim();
            return $"{detail}、袋号：{archiveNo}、袋位：{archiveLocation}";
        }

        private static string BuildPendingReturnText(string currentLocationText, string currentHolderText)
        {
            string location = string.IsNullOrWhiteSpace(currentLocationText) ? "位置未登记" : currentLocationText.Trim();
            if (string.IsNullOrWhiteSpace(currentHolderText))
            {
                return $"现位：{location}";
            }

            return $"现位：{location} · 保管：{currentHolderText.Trim()}";
        }

        private static string BuildElectronicArchiveText(string electronicArchiveNoText, string electronicArchiveLocationText)
        {
            if (string.IsNullOrWhiteSpace(electronicArchiveNoText) && string.IsNullOrWhiteSpace(electronicArchiveLocationText))
            {
                return string.Empty;
            }

            string archiveNo = string.IsNullOrWhiteSpace(electronicArchiveNoText) ? "未登记" : electronicArchiveNoText.Trim();
            string archiveLocation = string.IsNullOrWhiteSpace(electronicArchiveLocationText) ? "未登记" : electronicArchiveLocationText.Trim();
            return $"袋号：{archiveNo} · 袋位：{archiveLocation}";
        }

        private static StatusVisual ResolveVisual(CabinetHardDiskMediumDescriptor descriptor)
        {
            if (descriptor.IsPendingReturn)
            {
                return new StatusVisual("待归还", "#FFF7ED", "#FDBA74", "#F59E0B", "#B45309", "#FFEDD5", "#9A3412", "#9A3412", "#C2410C");
            }

            string inventoryMark = descriptor.InventoryMarkBadgeText?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(inventoryMark))
            {
                return new StatusVisual(inventoryMark, "#FEF2F2", "#FCA5A5", "#EF4444", "#B91C1C", "#FEE2E2", "#991B1B", "#991B1B", "#B91C1C");
            }

            if (descriptor.IsYearlyArchiveDisplay)
            {
                return descriptor.IsOpticalDiscMedia
                    ? new StatusVisual("年度光盘", "#FDF2F8", "#F9A8D4", "#DB2777", "#9D174D", "#FCE7F3", "#9D174D", "#831843", "#BE185D")
                    : new StatusVisual("年度资料", "#EFF6FF", "#93C5FD", "#3B82F6", "#1D4ED8", "#DBEAFE", "#1E3A8A", "#1E3A8A", "#1D4ED8");
            }

            return descriptor.StatusText switch
            {
                HardDiskMedium.StatusInStockData => new StatusVisual("资料", "#EFF6FF", "#93C5FD", "#3B82F6", "#1D4ED8", "#DBEAFE", "#1E3A8A", "#1E3A8A", "#1D4ED8"),
                HardDiskMedium.StatusInStockDamaged => new StatusVisual("损坏", "#FEF2F2", "#FCA5A5", "#EF4444", "#B91C1C", "#FEE2E2", "#991B1B", "#991B1B", "#B91C1C"),
                HardDiskMedium.StatusInStockLost => new StatusVisual("失", "#FEF2F2", "#FCA5A5", "#EF4444", "#B91C1C", "#FEE2E2", "#991B1B", "#991B1B", "#B91C1C"),
                _ => new StatusVisual("空盘", "#F8FAFC", "#CBD5E1", "#64748B", "#334155", "#E2E8F0", "#334155", "#1F2937", "#475569")
            };
        }

        private sealed record StatusVisual(string BadgeText, string CardBackground, string CardBorderBrush, string IconBodyBrush, string IconAccentBrush, string StatusBadgeBackground, string StatusBadgeForeground, string TitleForeground, string DetailForeground);
    }
}
