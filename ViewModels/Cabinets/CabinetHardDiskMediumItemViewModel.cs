using System;
using System.Windows;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.Shared;
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
        private readonly CabinetOpenStatusBadgeSupport.MediaCornerLayout _corners;
        private bool _isSelected;

        public CabinetHardDiskMediumItemViewModel(CabinetHardDiskMediumDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            InventoryMarkBadgeText = descriptor.InventoryMarkBadgeText?.Trim() ?? string.Empty;
            IsPendingReturn = descriptor.IsPendingReturn;
            HasOccupationLock = descriptor.HasOccupationLock;
            OccupationLockToolTipText = descriptor.OccupationLockToolTipText ?? string.Empty;
            OccupationLockBadgeText = CabinetOpenStatusBadgeSupport.NormalizeReservationDisplayText(
                descriptor.HasOccupationLock,
                descriptor.OccupationLockBadgeText);
            ArchiveSequenceNumber = descriptor.ArchiveSequenceNumber;
            ArchiveSequenceText = string.IsNullOrWhiteSpace(descriptor.ArchiveSequenceText)
                ? (ArchiveSequenceNumber > 0 ? ArchiveSequenceNumber.ToString("D2") : string.Empty)
                : descriptor.ArchiveSequenceText.Trim();

            _corners = CabinetOpenStatusBadgeSupport.ResolveMedia(
                ArchiveSequenceText,
                ArchiveSequenceNumber,
                InventoryMarkBadgeText,
                IsPendingReturn,
                HasOccupationLock,
                OccupationLockToolTipText);

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
            YearDisplayText = FormatLabelValue("年度", descriptor.YearText);
            ProjectDisplayText = FormatLabelValue("项目", descriptor.ProjectText);
            // 数据光盘不展示盘内已用/剩余容量（仅硬盘年度袋标容量）。
            UsedCapacityDisplayText = descriptor.IsOpticalDiscMedia
                ? string.Empty
                : FormatLabelValue("已用", descriptor.UsedCapacityDisplayText);
            RemainingCapacityDisplayText = descriptor.IsOpticalDiscMedia
                ? string.Empty
                : FormatLabelValue("剩余", descriptor.RemainingCapacityDisplayText);
            YearlyHardDiskCapacityLineText = BuildYearlyCapacityLine(descriptor);
            SecondaryText = descriptor.IsPendingReturn
                ? BuildPendingReturnText(descriptor.CurrentLocationText, descriptor.CurrentHolderText)
                : BuildElectronicArchiveText(ElectronicArchiveNoText, ElectronicArchiveLocationText);
            CompactHeaderText = IsYearlyArchiveDisplay
                ? BuildYearlyCompactHeader(descriptor)
                : BuildCompactHeaderText(DiskCodeText, CapacityText);
            StatusTextForeground = MediumStatusDisplayColorSupport.ResolveForeground(StatusText);
            if (IsYearlyArchiveDisplay)
            {
                CompactDetailText = BuildYearlyCompactDetail(descriptor);
                CompactDetailStatusText = string.Empty;
                CompactDetailSuffixText = CompactDetailText;
            }
            else
            {
                CompactDetailStatusText = StatusText;
                CompactDetailSuffixText = BuildCompactDetailSuffixText(
                    CurrentLocationText,
                    ElectronicArchiveNoText,
                    ElectronicArchiveLocationText);
                CompactDetailText = string.IsNullOrEmpty(CompactDetailSuffixText)
                    ? CompactDetailStatusText
                    : $"{CompactDetailStatusText}{CompactDetailSuffixText}";
            }

            // 类型文案仅供档口详情等列表使用；卡片中部 pill 已由状态文案替代，不再展示。
            BadgeText = ResolveTypeBadgeLabel(descriptor);
            ToolTipText = descriptor.ToolTipText;
            ElectronicArchiveUnitId = descriptor.ElectronicArchiveUnitId;
            MediumId = descriptor.MediumId;
            IsBlankInStock = descriptor.IsBlankInStock;
            IsScrapInStock = descriptor.IsScrapInStock;
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

        /// <summary>状态描述前景色（与 HD-TXN 状态列共用映射）。</summary>
        public string StatusTextForeground { get; } = MediumStatusDisplayColorSupport.DefaultForeground;

        public string CurrentLocationText { get; } = string.Empty;

        public string SecondaryText { get; } = string.Empty;

        public string ElectronicArchiveNoText { get; } = string.Empty;

        public string ElectronicArchiveNoShortText { get; } = string.Empty;

        public string ElectronicArchiveLocationText { get; } = string.Empty;

        public string ArchiveInfoText { get; } = string.Empty;

        public string MediumInfoText { get; } = string.Empty;

        public bool IsBlankInStock { get; }

        /// <summary>在库(拟销)：空白档口内继续占位展示。</summary>
        public bool IsScrapInStock { get; }

        /// <summary>空白硬盘专用档口物理占用（空盘或拟销）。</summary>
        public bool IsBlankSlotOccupancy => IsBlankInStock || IsScrapInStock;

        public bool HasArchiveInfo { get; }

        public int ArchiveSequenceNumber { get; }

        public string ArchiveSequenceText { get; } = string.Empty;

        /// <summary>档内序号是否存在（标准模式由左上 NW 角标展示；本属性供兼容/详情引用）。</summary>
        public Visibility ArchiveSequenceVisibility => ArchiveSequenceNumber > 0 ? Visibility.Visible : Visibility.Collapsed;

        public string NwBadgeText => _corners.Nw.Text;
        public string NwBadgeBackground => _corners.Nw.Background;
        public string NwBadgeBorderBrush => _corners.Nw.BorderBrush;
        public string NwBadgeForeground => _corners.Nw.Foreground;
        public Visibility NwBadgeVisibility => _corners.Nw.Visibility;

        public string NeBadgeText => _corners.Ne.Text;
        public string NeBadgeBackground => _corners.Ne.Background;
        public string NeBadgeBorderBrush => _corners.Ne.BorderBrush;
        public string NeBadgeForeground => _corners.Ne.Foreground;
        public Visibility NeBadgeVisibility => _corners.Ne.Visibility;

        public string NeSecondaryBadgeText => _corners.NeSecondary.Text;
        public string NeSecondaryBadgeBackground => _corners.NeSecondary.Background;
        public string NeSecondaryBadgeBorderBrush => _corners.NeSecondary.BorderBrush;
        public string NeSecondaryBadgeForeground => _corners.NeSecondary.Foreground;
        public Visibility NeSecondaryBadgeVisibility => _corners.NeSecondary.Visibility;

        public string NeTertiaryBadgeText => _corners.NeTertiary.Text;
        public string NeTertiaryBadgeBackground => _corners.NeTertiary.Background;
        public string NeTertiaryBadgeBorderBrush => _corners.NeTertiary.BorderBrush;
        public string NeTertiaryBadgeForeground => _corners.NeTertiary.Foreground;
        public Visibility NeTertiaryBadgeVisibility => _corners.NeTertiary.Visibility;

        public string SeBadgeText => _corners.Se.Text;
        public string SeBadgeBackground => _corners.Se.Background;
        public string SeBadgeBorderBrush => _corners.Se.BorderBrush;
        public string SeBadgeForeground => _corners.Se.Foreground;
        public Visibility SeBadgeVisibility => _corners.Se.Visibility;

        public string SwBadgeText => _corners.Sw.Text;
        public string SwBadgeBackground => _corners.Sw.Background;
        public string SwBadgeBorderBrush => _corners.Sw.BorderBrush;
        public string SwBadgeForeground => _corners.Sw.Foreground;
        public string SwBadgeToolTip => string.IsNullOrWhiteSpace(_corners.Sw.ToolTip) ? SwBadgeText : _corners.Sw.ToolTip;
        public Visibility SwBadgeVisibility => _corners.Sw.Visibility;

        public string YearDisplayText { get; } = string.Empty;

        public string ProjectDisplayText { get; } = string.Empty;

        public string UsedCapacityDisplayText { get; } = string.Empty;

        public string RemainingCapacityDisplayText { get; } = string.Empty;

        public string YearlyHardDiskCapacityLineText { get; } = string.Empty;

        public string CompactHeaderText { get; } = string.Empty;

        public string CompactDetailText { get; } = string.Empty;

        /// <summary>紧凑详情中的状态段（年度袋布局为空，避免与后缀重复）。</summary>
        public string CompactDetailStatusText { get; } = string.Empty;

        /// <summary>紧凑详情中状态之后的后缀（如「、现位：…」），供状态赋色分段绑定。</summary>
        public string CompactDetailSuffixText { get; } = string.Empty;

        public string BadgeText { get; } = string.Empty;

        /// <summary>中部类型 pill 已由状态文案替代，统一隐藏。</summary>
        public Visibility CenterTypeBadgeVisibility => Visibility.Collapsed;

        public string ToolTipText { get; } = string.Empty;

        public string InfoText => MediumInfoText;

        public bool IsEmpty { get; }

        public int ElectronicArchiveUnitId { get; }

        public bool IsPendingReturn { get; }

        public bool HasOccupationLock { get; }

        public string OccupationLockToolTipText { get; } = string.Empty;

        public string OccupationLockBadgeText { get; } = string.Empty;

        public string InventoryMarkBadgeText { get; } = string.Empty;

        public Visibility OccupationLockBadgeVisibility => SwBadgeVisibility;

        public Visibility InventoryMarkBadgeVisibility =>
            NeBadgeVisibility == Visibility.Visible
            || NeSecondaryBadgeVisibility == Visibility.Visible
            || NeTertiaryBadgeVisibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;

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

        private bool IsInventoryLostMark =>
            InventoryMarkBadgeText.Contains(CabinetOpenStatusBadgeSupport.InventoryLostMarkText, StringComparison.Ordinal);

        private bool IsInventoryScrapMark =>
            InventoryMarkBadgeText.Contains(CabinetOpenStatusBadgeSupport.InventoryScrapMarkText, StringComparison.Ordinal);

        /// <summary>电子介质袋在库占用（含征用/预订；用于目标容量与源档口类型判断）。失/销不可作迁档源；X（损坏）仍可迁袋。</summary>
        public bool IsElectronicInStockOccupancy =>
            !IsEmpty
            && !IsPendingReturn
            && !IsBlankInStock
            && ElectronicArchiveUnitId > 0
            && !IsInventoryLostMark
            && !IsInventoryScrapMark;

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

        public int MediumId { get; }

        public Visibility YearlyArchiveLayoutVisibility => IsYearlyArchiveDisplay ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DefaultLayoutVisibility => IsYearlyArchiveDisplay ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ElectronicArchiveNoShortVisibility => IsYearlyArchiveDisplay && !string.IsNullOrWhiteSpace(ElectronicArchiveNoShortText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        /// <summary>年度硬盘袋「已用」行；光盘不展示盘内容量。</summary>
        public Visibility YearlyHardDiskUsedCapacityVisibility =>
            IsYearlyArchiveDisplay
            && !IsOpticalDiscMedia
            && !string.IsNullOrWhiteSpace(UsedCapacityDisplayText)
                ? Visibility.Visible
                : Visibility.Collapsed;

        /// <summary>年度硬盘袋「剩余」行；光盘不展示盘内容量。</summary>
        public Visibility YearlyHardDiskCapacityLineVisibility =>
            IsYearlyArchiveDisplay
            && !IsOpticalDiscMedia
            && !string.IsNullOrWhiteSpace(RemainingCapacityDisplayText)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility YearlyHardDiskCodeVisibility => IsYearlyArchiveDisplay && !IsOpticalDiscMedia
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility OpticalDiscIconVisibility => IsOpticalDiscMedia ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HardDiskIconVisibility => IsOpticalDiscMedia ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>右键「介质信息」及弹窗标题：光盘 / 硬盘。</summary>
        public string MediumInfoMenuHeader => IsOpticalDiscMedia ? "光盘介质信息" : "硬盘介质信息";

        public string CardBackground => IsSelected ? "#DBEAFE" : _baseCardBackground;

        public string CardBorderBrush => IsSelected ? "#2563EB" : _baseCardBorderBrush;

        public double CardBorderThickness => IsSelected ? 2d : 1d;

        /// <summary>图标色固定表达空盘/数据盘，选中态不改色以免冲掉语义。</summary>
        public string IconBodyBrush => _baseIconBodyBrush;

        /// <summary>图标描边/强调色；选中态不改色。</summary>
        public string IconAccentBrush => _baseIconAccentBrush;

        public string StatusBadgeBackground => IsSelected ? "#EFF6FF" : _baseStatusBadgeBackground;

        public string StatusBadgeForeground => IsSelected ? "#1D4ED8" : _baseStatusBadgeForeground;

        public string TitleForeground => IsSelected ? "#1E3A8A" : _baseTitleForeground;

        public string DetailForeground => IsSelected ? "#1D4ED8" : _baseDetailForeground;

        public Visibility SecondaryTextVisibility => string.IsNullOrWhiteSpace(SecondaryText) ? Visibility.Collapsed : Visibility.Visible;

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
                return $"{FormatLabelValue("年度", descriptor.YearText)} · {FormatLabelValue("项目", descriptor.ProjectText)}";
            }

            string bagNo = ArchiveContainerCodeDisplaySupport.ToShortDisplayCode(descriptor.ElectronicArchiveNoText);
            string bagSegment = string.IsNullOrWhiteSpace(bagNo) ? string.Empty : $"{bagNo} · ";
            return $"{bagSegment}{FormatLabelValue("硬盘", descriptor.DiskCode)} · {FormatLabelValue("年度", descriptor.YearText)} · {FormatLabelValue("项目", descriptor.ProjectText)} · {BuildYearlyCapacityLine(descriptor)}";
        }

        private static string BuildYearlyCapacityLine(CabinetHardDiskMediumDescriptor descriptor)
        {
            if (descriptor.IsOpticalDiscMedia)
            {
                return string.Empty;
            }

            string usedToken = ToCompactCapacityToken(descriptor.UsedCapacityDisplayText);
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

        private static string BuildCompactDetailSuffixText(
            string currentLocationText,
            string electronicArchiveNoText,
            string electronicArchiveLocationText)
        {
            string location = string.IsNullOrWhiteSpace(currentLocationText) ? "位置未登记" : currentLocationText.Trim();
            string suffix = $"、现位：{location}";
            if (string.IsNullOrWhiteSpace(electronicArchiveNoText) && string.IsNullOrWhiteSpace(electronicArchiveLocationText))
            {
                return suffix;
            }

            string archiveNo = string.IsNullOrWhiteSpace(electronicArchiveNoText) ? "未登记" : electronicArchiveNoText.Trim();
            string archiveLocation = string.IsNullOrWhiteSpace(electronicArchiveLocationText) ? "未登记" : electronicArchiveLocationText.Trim();
            return $"{suffix}、袋号：{archiveNo}、袋位：{archiveLocation}";
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
            // 图标色仅表达空盘/数据盘；形状由 HardDisk/OpticalDisc 图标可见性区分。
            var (iconBody, iconAccent) = ResolveIconColors(descriptor);

            if (descriptor.IsPendingReturn)
            {
                // 待还：橙底；右下角标「待还」；图标色仍按空盘/数据盘。
                return CreateTypeVisual(
                    cardBackground: "#FFF7ED",
                    cardBorder: "#FDBA74",
                    iconBody: iconBody,
                    iconAccent: iconAccent,
                    statusBadgeBackground: "#FFEDD5",
                    statusBadgeForeground: "#C2410C",
                    titleForeground: "#9A3412",
                    detailForeground: "#C2410C");
            }

            if (IsScrapType(descriptor) || IsBlankType(descriptor))
            {
                // 空盘/拟销：灰底；状态文案已展示，中部不再标「空盘」「拟销」。
                return CreateTypeVisual(
                    cardBackground: "#F8FAFC",
                    cardBorder: "#CBD5E1",
                    iconBody: iconBody,
                    iconAccent: iconAccent,
                    statusBadgeBackground: "#E2E8F0",
                    statusBadgeForeground: "#475569",
                    titleForeground: "#334155",
                    detailForeground: "#64748B");
            }

            // 资料：含年度袋、在库资料、盘库失/销/X、损坏专用档口等；异常靠右上角标，底色统一资料蓝。
            return CreateTypeVisual(
                cardBackground: "#EFF6FF",
                cardBorder: "#93C5FD",
                iconBody: iconBody,
                iconAccent: iconAccent,
                statusBadgeBackground: "#DBEAFE",
                statusBadgeForeground: "#1D4ED8",
                titleForeground: "#1E3A8A",
                detailForeground: "#1D4ED8");
        }

        /// <summary>
        /// 介质图标颜色：空盘（含空白档口拟销裸盘）灰色，其余数据盘蓝色。
        /// 光/硬盘由图标形状区分，不在此处理。
        /// </summary>
        private static (string IconBody, string IconAccent) ResolveIconColors(CabinetHardDiskMediumDescriptor descriptor)
        {
            if (IsBlankType(descriptor) || IsScrapType(descriptor))
            {
                return ("#64748B", "#334155");
            }

            return ("#3B82F6", "#1D4ED8");
        }

        /// <summary>档口详情等列表用的类型短文案（卡片中部不再展示）。</summary>
        private static string ResolveTypeBadgeLabel(CabinetHardDiskMediumDescriptor descriptor)
        {
            if (descriptor.IsPendingReturn)
            {
                return "待还";
            }

            if (IsScrapType(descriptor))
            {
                return "拟销";
            }

            if (IsBlankType(descriptor))
            {
                return "空盘";
            }

            return ResolveDataTypeBadge(descriptor);
        }

        private static bool IsScrapType(CabinetHardDiskMediumDescriptor descriptor)
        {
            // 仅空白档口裸盘拟销用「拟销」标识；电子袋拟销仍走资料卡 + 右上「销」。
            if (descriptor.IsYearlyArchiveDisplay || descriptor.ElectronicArchiveUnitId > 0)
            {
                return false;
            }

            if (descriptor.IsScrapInStock)
            {
                return true;
            }

            string normalizedStatus = MediumStatusTextNormalizer.Normalize(descriptor.StatusText);
            return string.Equals(normalizedStatus, HardDiskMedium.StatusInStockScrap, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBlankType(CabinetHardDiskMediumDescriptor descriptor)
        {
            if (descriptor.IsBlankInStock)
            {
                return true;
            }

            string normalizedStatus = MediumStatusTextNormalizer.Normalize(descriptor.StatusText);
            return string.Equals(normalizedStatus, HardDiskMedium.StatusInStockBlank, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDataTypeBadge(CabinetHardDiskMediumDescriptor descriptor)
        {
            if (!string.IsNullOrWhiteSpace(descriptor.InventoryMarkBadgeText))
            {
                return ResolveTypeBadgeWithoutInventory(descriptor);
            }

            if (descriptor.IsYearlyArchiveDisplay)
            {
                return descriptor.IsOpticalDiscMedia ? "年度光盘" : "年度资料";
            }

            string normalizedStatus = MediumStatusTextNormalizer.Normalize(descriptor.StatusText);
            if (string.Equals(normalizedStatus, HardDiskMedium.StatusInStockDamaged, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, OpticalDiscMedium.StatusDamaged, StringComparison.OrdinalIgnoreCase))
            {
                return "损坏";
            }

            return "资料";
        }

        private static string ResolveTypeBadgeWithoutInventory(CabinetHardDiskMediumDescriptor descriptor)
        {
            if (descriptor.IsYearlyArchiveDisplay)
            {
                return descriptor.IsOpticalDiscMedia ? "年度光盘" : "年度资料";
            }

            return "资料";
        }

        private static StatusVisual CreateTypeVisual(
            string cardBackground,
            string cardBorder,
            string iconBody,
            string iconAccent,
            string statusBadgeBackground,
            string statusBadgeForeground,
            string titleForeground,
            string detailForeground)
            => new(
                cardBackground,
                cardBorder,
                iconBody,
                iconAccent,
                statusBadgeBackground,
                statusBadgeForeground,
                titleForeground,
                detailForeground);

        private sealed record StatusVisual(string CardBackground, string CardBorderBrush, string IconBodyBrush, string IconAccentBrush, string StatusBadgeBackground, string StatusBadgeForeground, string TitleForeground, string DetailForeground);
    }
}
