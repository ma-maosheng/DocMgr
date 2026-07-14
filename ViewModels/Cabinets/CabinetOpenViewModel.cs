using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.ViewModels.Cabinets
{
    public class CabinetOpenViewModel : ViewModelBase
    {
        private const double DefaultCompactViewportWidth = 860d;
        private const double DefaultCompactViewportHeight = 460d;
        private const double CompactSlotGap = 8d;
        private const double StandardSlotGap = 16d;
        private const double StandardSlotHorizontalChrome = 20d;
        private const double StandardMagneticSlotVerticalChrome = 108d * 1.1d;
        private const double StandardMagneticSlotMinWidth = 140d;
        private const double StandardMagneticSlotMinHeight = 48d;
        private const double StandardMagneticSlotHeightScale = 4d;

        private readonly IDialogService _dialogService;
        private readonly ICabinetService _cabinetService;
        private readonly ICabinetOpenLayoutService _cabinetOpenLayoutService;
        private readonly ICabinetArchiveBoxPlacementService _cabinetArchiveBoxPlacementService;
        private readonly IUserContextService _userContextService;
        private readonly IArchiveRelocationService _archiveRelocationService;
        private readonly IBatchSlotRelocationSession _batchSlotRelocationSession;
        private readonly IInteractiveItemRelocationSession _interactiveItemRelocationSession;
        private readonly ICabinetOpenLayoutRefreshNotifier _cabinetOpenLayoutRefreshNotifier;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly string _targetSlotCode;
        private bool _isCompactDisplayMode;
        private double _compactSlotDisplayWidth = 96d;
        private double _compactSlotDisplayHeight = 56d;
        private double _standardMagneticSlotDisplayWidth = 180d;
        private double _standardMagneticSlotDisplayHeight = 360d;
        private double _lastSlotViewportWidth = DefaultCompactViewportWidth;
        private double _lastSlotViewportHeight = DefaultCompactViewportHeight;
        private double _snapshotSlotDisplayWidth;
        private double _snapshotSlotDisplayHeight;
        private const double SnapshotSlotBorderMargin = 16d;
        private const double SnapshotViewportChromePadding = 8d;
        private string? _selectionAnchorSlotCode;
        private ArchiveBoxItemViewModel? _selectedArchiveBox;
        private CabinetHardDiskMediumItemViewModel? _selectedHardDiskMedium;

        public CabinetOpenViewModel(CabinetOpenRequest request, IDialogService dialogService, ICabinetService cabinetService, ICabinetOpenLayoutService cabinetOpenLayoutService, ICabinetArchiveBoxPlacementService cabinetArchiveBoxPlacementService, IUserContextService userContextService, IArchiveRelocationService archiveRelocationService, IBatchSlotRelocationSession batchSlotRelocationSession, IInteractiveItemRelocationSession interactiveItemRelocationSession, ICabinetOpenLayoutRefreshNotifier cabinetOpenLayoutRefreshNotifier, IArchiveRegisterService archiveRegisterService)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(cabinetService);
            ArgumentNullException.ThrowIfNull(cabinetOpenLayoutService);
            ArgumentNullException.ThrowIfNull(cabinetArchiveBoxPlacementService);
            ArgumentNullException.ThrowIfNull(userContextService);
            ArgumentNullException.ThrowIfNull(archiveRelocationService);
            ArgumentNullException.ThrowIfNull(batchSlotRelocationSession);
            ArgumentNullException.ThrowIfNull(interactiveItemRelocationSession);
            ArgumentNullException.ThrowIfNull(cabinetOpenLayoutRefreshNotifier);
            ArgumentNullException.ThrowIfNull(archiveRegisterService);

            _dialogService = dialogService;
            _cabinetService = cabinetService;
            _cabinetOpenLayoutService = cabinetOpenLayoutService;
            _cabinetArchiveBoxPlacementService = cabinetArchiveBoxPlacementService;
            _userContextService = userContextService;
            _archiveRelocationService = archiveRelocationService;
            _batchSlotRelocationSession = batchSlotRelocationSession;
            _interactiveItemRelocationSession = interactiveItemRelocationSession;
            _cabinetOpenLayoutRefreshNotifier = cabinetOpenLayoutRefreshNotifier;
            _archiveRegisterService = archiveRegisterService;
            _batchSlotRelocationSession.SourceChanged += OnBatchSlotRelocationSourceChanged;
            _interactiveItemRelocationSession.SourceChanged += OnInteractiveItemRelocationSourceChanged;
            _cabinetOpenLayoutRefreshNotifier.LayoutRefreshRequested += OnLayoutRefreshRequested;
            Request = request;
            _targetSlotCode = request.TargetSlotCode?.Trim() ?? string.Empty;
            string sideDisplayName = GetSideDisplayName(request.CabinetType, request.Face);
            bool isSingleSlotSnapshot = !string.IsNullOrWhiteSpace(_targetSlotCode);
            CabinetTitle = request.CabinetType == CabinetType.MagneticDisk
                ? $"防磁磁盘柜：{request.CabinetName}"
                : $"档案柜：{request.CabinetName}";
            CabinetSubtitle = isSingleSlotSnapshot
                ? $"{GetCabinetTypeDisplayName(request.CabinetType)} · {sideDisplayName} · 档口 {_targetSlotCode}"
                : $"{GetCabinetTypeDisplayName(request.CabinetType)} · {sideDisplayName}";
            LayoutSummary = isSingleSlotSnapshot
                ? $"档口占用快照 · {_targetSlotCode}"
                : $"{request.LayerCount}层 × {request.ColumnCount}列 · {request.LayerCount * request.ColumnCount}个格口";
            WindowTitle = isSingleSlotSnapshot
                ? $"档口占用快照 - {request.CabinetName}{sideDisplayName} {_targetSlotCode}"
                : request.CabinetType == CabinetType.Standard || request.CabinetType == CabinetType.MagneticDisk
                    ? $"打开档案柜{sideDisplayName} - {request.CabinetName}"
                    : $"打开档案柜 - {request.CabinetName}";
            CurrentFaceDisplayName = sideDisplayName;
            PreviewTitle = isSingleSlotSnapshot ? "档口占用快照" : GetPreviewTitle(request.CabinetType);
            PreviewSubtitle = isSingleSlotSnapshot ? $"正在载入 {sideDisplayName} {_targetSlotCode} 的空间利用情况..." : GetPreviewSubtitle(request.CabinetType);
            DisplayRowCount = isSingleSlotSnapshot ? 1 : request.LayerCount;
            DisplayColumnCount = isSingleSlotSnapshot ? 1 : request.ColumnCount;

            Slots = new ObservableCollection<CabinetSlotViewModel>();
            ReloadSlots();

            ShowSlotInfoCommand = new RelayCommand<CabinetSlotViewModel>(ShowSlotInfo);
            ShowSlotDetailCommand = new RelayCommand<CabinetSlotViewModel>(ShowSlotDetail);
            ShowSlotZoomCommand = new RelayCommand<CabinetSlotViewModel>(ShowSlotZoom, CanShowSlotZoomFromSlot);
            AddArchiveBoxCommand = new RelayCommand<CabinetSlotViewModel>(AddArchiveBox);
            RefreshSlotCommand = new RelayCommand<CabinetSlotViewModel>(RefreshSlot);
            EditSlotPlacementModeCommand = new RelayCommand<CabinetSlotViewModel>(EditSlotPlacementMode);
            ToggleDamagedDiskSlotCommand = new RelayCommand<CabinetSlotViewModel>(ToggleDamagedDiskSlot);
            ToggleDamagedOpticalDiscSlotCommand = new RelayCommand<CabinetSlotViewModel>(ToggleDamagedOpticalDiscSlot);
            ToggleDataDiskSlotCommand = new RelayCommand<CabinetSlotViewModel>(ToggleDataDiskSlot);
            ToggleDataOpticalDiscSlotCommand = new RelayCommand<CabinetSlotViewModel>(ToggleDataOpticalDiscSlot);
            ToggleHistoricalDataDiskSlotCommand = new RelayCommand<CabinetSlotViewModel>(ToggleHistoricalDataDiskSlot);
            ToggleHistoricalDataOpticalDiscSlotCommand = new RelayCommand<CabinetSlotViewModel>(ToggleHistoricalDataOpticalDiscSlot);
            ToggleBlankDiskSlotCommand = new RelayCommand<CabinetSlotViewModel>(ToggleBlankDiskSlot);
            ShowArchiveContentCommand = new RelayCommand<ArchiveBoxItemViewModel>(ShowArchiveContent);
            ShowPendingReturnDetailCommand = new RelayCommand<ArchiveBoxItemViewModel>(ShowPendingReturnDetail, CanShowPendingReturnDetail);
            EditArchiveBoxPlacementModeCommand = new RelayCommand<ArchiveBoxItemViewModel>(EditArchiveBoxPlacementMode);
            ResetArchiveBoxSpecificationCommand = new RelayCommand<ArchiveBoxItemViewModel>(ResetArchiveBoxSpecification);
            ShowHardDiskMediumInfoCommand = new RelayCommand<CabinetHardDiskMediumItemViewModel>(ShowHardDiskMediumInfo);
            ShowHardDiskMediumArchiveInfoCommand = new RelayCommand<CabinetHardDiskMediumItemViewModel>(ShowHardDiskMediumArchiveInfo);
            ApplySelectedSlotsPurposeCommand = new RelayCommand(_ => ApplySelectedSlotsPurpose(), _ => CanApplySelectedSlotsPurpose);
            ClearSlotSelectionCommand = new RelayCommand(_ => ClearSlotSelection(), _ => SelectedSlotCount > 0);
            SelectAllSlotsCommand = new RelayCommand(_ => SelectAllSlots(), _ => CanSelectAllSlots);
            InvertSlotSelectionCommand = new RelayCommand(_ => InvertSlotSelection(), _ => Slots.Count > 0);
            SetBatchRelocationSourceCommand = new RelayCommand<CabinetSlotViewModel>(SetBatchRelocationSource, CanSetBatchRelocationSource);
            RelocateBatchToSlotCommand = new RelayCommand<CabinetSlotViewModel>(slot => _ = RelocateBatchToSlotAsync(slot), CanRelocateBatchToSlot);
            ClearBatchRelocationSourceCommand = new RelayCommand(_ => ClearBatchRelocationSource(), _ => HasBatchRelocationSource);
            ClearContentSelectionCommand = new RelayCommand(_ => ClearContentSelection(), _ => HasContentSelection);
            SetInteractiveItemRelocationFromArchiveBoxCommand = new RelayCommand<ArchiveBoxItemViewModel>(SetInteractiveItemRelocationFromArchiveBox, CanSetInteractiveItemRelocationFromArchiveBox);
            SetInteractiveItemRelocationFromMediumCommand = new RelayCommand<CabinetHardDiskMediumItemViewModel>(SetInteractiveItemRelocationFromMedium, CanSetInteractiveItemRelocationFromMedium);
            SetInteractiveItemRelocationFromSelectionCommand = new RelayCommand(_ => SetInteractiveItemRelocationFromSelection(), _ => CanSetInteractiveItemRelocationFromSelection);
            RelocateInteractiveItemToSlotCommand = new RelayCommand<CabinetSlotViewModel>(slot => _ = RelocateInteractiveItemToSlotAsync(slot), CanRelocateInteractiveItemToSlotFromSession);
            ClearInteractiveItemRelocationSourceCommand = new RelayCommand(_ => ClearInteractiveItemRelocationSource(), _ => HasInteractiveItemRelocationSource);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
            UpdateMagneticDiskSlotDimensions(DefaultCompactViewportWidth, DefaultCompactViewportHeight);
            OnBatchSlotRelocationSourceChanged();
            OnInteractiveItemRelocationSourceChanged();
        }

        public CabinetOpenRequest Request { get; }

        public string WindowTitle { get; }

        public string CabinetTitle { get; }

        public string CabinetSubtitle { get; }

        public string LayoutSummary { get; }

        public string CurrentFaceDisplayName { get; }

        public string PreviewTitle { get; }

        public string PreviewSubtitle { get; }

        public Visibility MagneticDiskLegendVisibility => Request.CabinetType == CabinetType.MagneticDisk && !IsSingleSlotSnapshot
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility MagneticDiskFooterVisibility => Request.CabinetType == CabinetType.MagneticDisk
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility ArchiveFooterVisibility => Request.CabinetType == CabinetType.MagneticDisk
            ? Visibility.Collapsed
            : Visibility.Visible;

        public string CurrentSideLabel => Request.CabinetType == CabinetType.MagneticDisk
            ? "当前门别："
            : "当前面别：";

        public int DisplayRowCount { get; private set; }

        public int DisplayColumnCount { get; private set; }

        public bool IsSingleSlotSnapshot => !string.IsNullOrWhiteSpace(_targetSlotCode);

        public Visibility SlotZoomMenuVisibility => IsSingleSlotSnapshot ? Visibility.Collapsed : Visibility.Visible;

        public int SlotCount => Slots.Count;

        public int ArchiveBoxCount => Slots.Sum(slot => slot.ArchiveBoxes.Count);

        public int MixedArchiveBoxCount => Slots.Sum(slot => slot.MixedArchiveBoxCount);

        public int PendingSortingRecordCount => Slots.Sum(slot => slot.PendingSortingRecordCount);

        public int MagneticDiskMediumCount => Slots.Sum(slot => slot.HardDiskPresentCount);

        public int MagneticDiskPendingReturnCount => Slots.Sum(slot => slot.PendingReturnMediumCount);

        public int MagneticDiskFreeCapacityCount => Slots.Sum(slot => Math.Max(slot.HardDiskCapacity - slot.HardDiskPresentCount, 0));

        public string FooterHintText => Request.CabinetType == CabinetType.MagneticDisk
            ? IsCompactDisplayMode
                ? "提示：简洁模式下单击选中档口，Ctrl 点击增减选择，Shift 点击范围连选，Ctrl+A 全选；双击或右键可查看档口详情；多选后可用工具栏或右键统一设置用途"
                : "提示：可拖拽硬盘/光盘到目标档口迁档（紫/红高亮表示可放/不可放）；双击档口或右键可查看详情/放大布局"
            : "提示：可拖拽档案盒到目标档口迁档（紫/红高亮表示可放/不可放）；双击档口或右键可查看详情/放大布局";

        public bool IsMagneticDiskCabinet => Request.CabinetType == CabinetType.MagneticDisk;

        public Visibility SlotDisplayModeSwitcherVisibility =>
            IsMagneticDiskCabinet && !IsSingleSlotSnapshot ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CompactModeToolbarVisibility =>
            IsMagneticDiskCabinet && IsCompactDisplayMode && !IsSingleSlotSnapshot ? Visibility.Visible : Visibility.Collapsed;

        public Visibility StandardSlotsVisibility => IsCompactDisplayMode ? Visibility.Collapsed : Visibility.Visible;

        public Visibility CompactSlotsVisibility => IsCompactDisplayMode ? Visibility.Visible : Visibility.Collapsed;

        public ScrollBarVisibility SlotsScrollBarVisibility =>
            IsSingleSlotSnapshot ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

        public ScrollBarVisibility SlotsHorizontalScrollBarVisibility =>
            IsSingleSlotSnapshot || IsMagneticDiskCabinet
                ? ScrollBarVisibility.Disabled
                : ScrollBarVisibility.Auto;

        public ScrollBarVisibility SlotsVerticalScrollBarVisibility =>
            IsSingleSlotSnapshot ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

        public Visibility SnapshotFooterVisibility => IsSingleSlotSnapshot ? Visibility.Collapsed : Visibility.Visible;

        public Thickness SnapshotRootMargin => IsSingleSlotSnapshot ? new Thickness(12d) : new Thickness(20d);

        public HorizontalAlignment SnapshotSlotsHorizontalAlignment =>
            IsSingleSlotSnapshot ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;

        public VerticalAlignment SnapshotSlotsVerticalAlignment =>
            IsSingleSlotSnapshot ? VerticalAlignment.Stretch : VerticalAlignment.Top;

        public bool IsStandardDisplayMode
        {
            get => !IsCompactDisplayMode;
            set
            {
                if (value)
                {
                    IsCompactDisplayMode = false;
                }
            }
        }

        public bool IsCompactDisplayMode
        {
            get => _isCompactDisplayMode;
            set
            {
                if (!SetProperty(ref _isCompactDisplayMode, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(IsStandardDisplayMode));
                OnPropertyChanged(nameof(StandardSlotsVisibility));
                OnPropertyChanged(nameof(CompactSlotsVisibility));
                OnPropertyChanged(nameof(SlotsScrollBarVisibility));
                OnPropertyChanged(nameof(SlotsHorizontalScrollBarVisibility));
                OnPropertyChanged(nameof(SlotsVerticalScrollBarVisibility));
                OnPropertyChanged(nameof(CompactModeToolbarVisibility));
                OnPropertyChanged(nameof(SlotDisplayModeSwitcherVisibility));
                OnPropertyChanged(nameof(FooterHintText));
                OnPropertyChanged(nameof(EffectiveSlotDisplayWidth));
                OnPropertyChanged(nameof(EffectiveSlotDisplayHeight));
                OnPropertyChanged(nameof(EffectiveRenderSlotCanvasWidth));
                OnPropertyChanged(nameof(EffectiveRenderSlotCanvasHeight));
                OnPropertyChanged(nameof(EffectiveSlotsSurfaceWidth));
                OnPropertyChanged(nameof(EffectiveSlotsSurfaceHeight));
                OnPropertyChanged(nameof(SlotsSurfaceWidth));
                OnPropertyChanged(nameof(SlotsSurfaceHeight));
                OnPropertyChanged(nameof(CanApplySelectedSlotsPurpose));
                OnPropertyChanged(nameof(CanSelectAllSlots));
                ClearSlotSelection();
                ClearContentSelectionWithoutNotify();
                OnPropertyChanged(nameof(HasContentSelection));
                OnPropertyChanged(nameof(SelectedContentSummaryText));
                OnPropertyChanged(nameof(ContentSelectionToolbarVisibility));
                OnPropertyChanged(nameof(InteractiveItemRelocationMenuVisibility));
                OnPropertyChanged(nameof(CanSetInteractiveItemRelocationFromSelection));
                _interactiveItemRelocationSession.ClearSource();
                if (IsMagneticDiskCabinet)
                {
                    UpdateMagneticDiskSlotDimensions(_lastSlotViewportWidth, _lastSlotViewportHeight);
                }
            }
        }

        public double CompactSlotDisplayWidth => _compactSlotDisplayWidth;

        public double CompactSlotDisplayHeight => _compactSlotDisplayHeight;

        public double EffectiveSlotDisplayWidth => IsSingleSlotSnapshot && _snapshotSlotDisplayWidth > 0d
            ? _snapshotSlotDisplayWidth
            : IsMagneticDiskCabinet
                ? IsCompactDisplayMode ? _compactSlotDisplayWidth : _standardMagneticSlotDisplayWidth
                : SlotDisplayWidth;

        public double EffectiveSlotDisplayHeight => IsSingleSlotSnapshot && _snapshotSlotDisplayHeight > 0d
            ? _snapshotSlotDisplayHeight
            : IsMagneticDiskCabinet
                ? IsCompactDisplayMode ? _compactSlotDisplayHeight : _standardMagneticSlotDisplayHeight
                : SlotDisplayHeight;

        public double EffectiveRenderSlotCanvasWidth => IsSingleSlotSnapshot || IsMagneticDiskCabinet
            ? Math.Max(0d, EffectiveSlotDisplayWidth - StandardSlotHorizontalChrome)
            : Slots.FirstOrDefault()?.RenderSlotCanvasWidth ?? 580d;

        public double EffectiveRenderSlotCanvasHeight => IsSingleSlotSnapshot || IsMagneticDiskCabinet
            ? Math.Max(0d, EffectiveSlotDisplayHeight - ResolveEffectiveSlotVerticalChrome())
            : Slots.FirstOrDefault()?.RenderSlotCanvasHeight ?? 272d;

        public double EffectiveSlotsSurfaceWidth => DisplayColumnCount * (EffectiveSlotDisplayWidth + ResolveSlotSurfaceGap());

        public double EffectiveSlotsSurfaceHeight => DisplayRowCount * (EffectiveSlotDisplayHeight + ResolveSlotSurfaceGap());

        public int SelectedSlotCount => Slots.Count(slot => slot.IsSelected);

        public string SelectedSlotSummaryText => SelectedSlotCount == 0
            ? "未选择档口"
            : $"已选择 {SelectedSlotCount} 个档口";

        public bool HasSelectedSlots => SelectedSlotCount > 0;

        public bool CanApplySelectedSlotsPurpose => IsCompactDisplayMode && HasSelectedSlots && IsArchiveRoomMediaAdmin();

        public bool CanSelectAllSlots => IsCompactDisplayMode && Slots.Count > 0;

        public Visibility CompactBatchPurposeMenuVisibility =>
            IsCompactDisplayMode && HasSelectedSlots && IsArchiveRoomMediaAdmin()
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility CompactPerSlotCategoryMenuVisibility =>
            IsCompactDisplayMode && SelectedSlotCount > 1 ? Visibility.Collapsed : DamagedDiskSlotActionVisibility;

        public string BatchApplyPurposeMenuText => SelectedSlotCount <= 1
            ? "设置档口用途"
            : $"统一设置所选档口用途（{SelectedSlotCount}）";

        public Visibility DamagedDiskSlotActionVisibility => Request.CabinetType == CabinetType.MagneticDisk && IsArchiveRoomMediaAdmin()
            ? Visibility.Visible
            : Visibility.Collapsed;

        public string AverageUtilizationText => Slots.Count == 0
            ? "0%"
            : $"{Math.Round(Slots.Average(slot => slot.UtilizationRatio) * 100d, MidpointRounding.AwayFromZero)}%";

        public double SlotDisplayWidth => Slots.FirstOrDefault()?.SlotDisplayWidth ?? 620d;

        public double SlotDisplayHeight => Slots.FirstOrDefault()?.SlotDisplayHeight ?? 398d;

        public double SlotsSurfaceWidth => IsSingleSlotSnapshot
            ? EffectiveSlotsSurfaceWidth
            : IsMagneticDiskCabinet && !IsCompactDisplayMode
                ? DisplayColumnCount * (EffectiveSlotDisplayWidth + StandardSlotGap)
                : DisplayColumnCount * (SlotDisplayWidth + StandardSlotGap);

        public double SlotsSurfaceHeight => IsSingleSlotSnapshot
            ? EffectiveSlotsSurfaceHeight
            : IsMagneticDiskCabinet && !IsCompactDisplayMode
                ? DisplayRowCount * (EffectiveSlotDisplayHeight + StandardSlotGap)
                : DisplayRowCount * (SlotDisplayHeight + StandardSlotGap);

        public ObservableCollection<CabinetSlotViewModel> Slots { get; }

        public RelayCommand<CabinetSlotViewModel> ShowSlotInfoCommand { get; }

        public RelayCommand<CabinetSlotViewModel> ShowSlotDetailCommand { get; }

        public RelayCommand<CabinetSlotViewModel> ShowSlotZoomCommand { get; }

        public RelayCommand<CabinetSlotViewModel> AddArchiveBoxCommand { get; }

        public RelayCommand<CabinetSlotViewModel> RefreshSlotCommand { get; }

        public RelayCommand<CabinetSlotViewModel> EditSlotPlacementModeCommand { get; }

        public RelayCommand<CabinetSlotViewModel> ToggleDamagedDiskSlotCommand { get; }

        public RelayCommand<CabinetSlotViewModel> ToggleDamagedOpticalDiscSlotCommand { get; }

        public RelayCommand<CabinetSlotViewModel> ToggleDataDiskSlotCommand { get; }

        public RelayCommand<CabinetSlotViewModel> ToggleDataOpticalDiscSlotCommand { get; }

        public RelayCommand<CabinetSlotViewModel> ToggleHistoricalDataDiskSlotCommand { get; }

        public RelayCommand<CabinetSlotViewModel> ToggleHistoricalDataOpticalDiscSlotCommand { get; }

        public RelayCommand<CabinetSlotViewModel> ToggleBlankDiskSlotCommand { get; }

        public RelayCommand<ArchiveBoxItemViewModel> ShowArchiveContentCommand { get; }

        public RelayCommand<ArchiveBoxItemViewModel> ShowPendingReturnDetailCommand { get; }

        public RelayCommand<ArchiveBoxItemViewModel> EditArchiveBoxPlacementModeCommand { get; }

        public RelayCommand<ArchiveBoxItemViewModel> ResetArchiveBoxSpecificationCommand { get; }

        public RelayCommand<CabinetHardDiskMediumItemViewModel> ShowHardDiskMediumInfoCommand { get; }

        public RelayCommand<CabinetHardDiskMediumItemViewModel> ShowHardDiskMediumArchiveInfoCommand { get; }

        public RelayCommand CloseCommand { get; }

        public RelayCommand ApplySelectedSlotsPurposeCommand { get; }

        public RelayCommand ClearSlotSelectionCommand { get; }

        public RelayCommand SelectAllSlotsCommand { get; }

        public RelayCommand InvertSlotSelectionCommand { get; }

        public RelayCommand<CabinetSlotViewModel> SetBatchRelocationSourceCommand { get; }

        public RelayCommand<CabinetSlotViewModel> RelocateBatchToSlotCommand { get; }

        public RelayCommand ClearContentSelectionCommand { get; }

        public bool HasContentSelection => _selectedArchiveBox != null || _selectedHardDiskMedium != null;

        public Visibility ContentSelectionToolbarVisibility =>
            !IsSingleSlotSnapshot && !IsCompactDisplayMode ? Visibility.Visible : Visibility.Collapsed;

        public string SelectedContentSummaryText
        {
            get
            {
                if (_selectedArchiveBox != null)
                {
                    return $"已选档案盒：{_selectedArchiveBox.BoxCode}（{_selectedArchiveBox.BoxLabel}）";
                }

                if (_selectedHardDiskMedium != null)
                {
                    if (_selectedHardDiskMedium.IsOpticalDiscMedia)
                    {
                        return string.IsNullOrWhiteSpace(_selectedHardDiskMedium.ElectronicArchiveNoText)
                            ? $"已选光盘介质：{_selectedHardDiskMedium.DiskCodeText}"
                            : $"已选电子介质袋：{_selectedHardDiskMedium.ElectronicArchiveNoText}（光盘 {_selectedHardDiskMedium.DiskCodeText}）";
                    }

                    return string.IsNullOrWhiteSpace(_selectedHardDiskMedium.ElectronicArchiveNoText)
                        ? $"已选硬盘介质：{_selectedHardDiskMedium.DiskCodeText}"
                        : $"已选电子介质袋：{_selectedHardDiskMedium.ElectronicArchiveNoText}（硬盘 {_selectedHardDiskMedium.DiskCodeText}）";
                }

                return "提示：单击档口内的档案盒、硬盘或光盘可选中；选中后高亮显示";
            }
        }

        public RelayCommand ClearBatchRelocationSourceCommand { get; }

        public bool IsArchiveRelocationCabinet => Request.CabinetType != CabinetType.MagneticDisk;

        public bool IsMagneticDiskRelocationCabinet => Request.CabinetType == CabinetType.MagneticDisk;

        public Visibility BatchSlotRelocationMenuVisibility =>
            SupportsBatchSlotRelocation
                ? Visibility.Visible
                : Visibility.Collapsed;

        private bool SupportsBatchSlotRelocation =>
            (IsArchiveRelocationCabinet || IsMagneticDiskRelocationCabinet)
            && IsArchiveAdmin()
            && !IsSingleSlotSnapshot;

        public bool HasBatchRelocationSource => _batchSlotRelocationSession.Source != null;

        public string BatchRelocationSourceText => _batchSlotRelocationSession.Source == null
            ? string.Empty
            : $"批量搬迁源：{_batchSlotRelocationSession.Source.DisplayText}";

        public Visibility BatchRelocationSourceHintVisibility =>
            HasBatchRelocationSource ? Visibility.Visible : Visibility.Collapsed;

        public RelayCommand<ArchiveBoxItemViewModel> SetInteractiveItemRelocationFromArchiveBoxCommand { get; }

        public RelayCommand<CabinetHardDiskMediumItemViewModel> SetInteractiveItemRelocationFromMediumCommand { get; }

        public RelayCommand SetInteractiveItemRelocationFromSelectionCommand { get; }

        public RelayCommand<CabinetSlotViewModel> RelocateInteractiveItemToSlotCommand { get; }

        public RelayCommand ClearInteractiveItemRelocationSourceCommand { get; }

        public Visibility InteractiveItemRelocationMenuVisibility =>
            SupportsInteractiveItemRelocation ? Visibility.Visible : Visibility.Collapsed;

        private bool SupportsInteractiveItemRelocation =>
            IsArchiveAdmin() && !IsSingleSlotSnapshot && !IsCompactDisplayMode;

        public bool HasInteractiveItemRelocationSource => _interactiveItemRelocationSession.Source != null;

        public string InteractiveItemRelocationSourceText => _interactiveItemRelocationSession.Source == null
            ? string.Empty
            : $"迁档对象：{_interactiveItemRelocationSession.Source.DisplayText}";

        public Visibility InteractiveItemRelocationSourceHintVisibility =>
            HasInteractiveItemRelocationSource ? Visibility.Visible : Visibility.Collapsed;

        public bool CanSetInteractiveItemRelocationFromSelection =>
            SupportsInteractiveItemRelocation
            && ((_selectedArchiveBox?.CanInteractiveRelocate ?? false)
                || (_selectedHardDiskMedium?.CanInteractiveRelocate ?? false));

        public bool SupportsInteractiveItemRelocationDrag => SupportsInteractiveItemRelocation;

        public event Action<bool?>? RequestClose;

        /// <summary>
        /// 窗体关闭时解除跨窗体事件订阅。
        /// </summary>
        public void Detach()
        {
            _batchSlotRelocationSession.SourceChanged -= OnBatchSlotRelocationSourceChanged;
            _interactiveItemRelocationSession.SourceChanged -= OnInteractiveItemRelocationSourceChanged;
            _cabinetOpenLayoutRefreshNotifier.LayoutRefreshRequested -= OnLayoutRefreshRequested;
        }

        public void UpdateMagneticDiskSlotDimensions(double availableWidth, double availableHeight)
        {
            if (!IsMagneticDiskCabinet || DisplayColumnCount <= 0 || DisplayRowCount <= 0)
            {
                return;
            }

            if (IsSingleSlotSnapshot)
            {
                UpdateSingleSlotSnapshotDimensions(availableWidth, availableHeight);
                return;
            }

            _lastSlotViewportWidth = Math.Max(availableWidth, 240d);
            _lastSlotViewportHeight = Math.Max(availableHeight, 180d);

            if (IsCompactDisplayMode)
            {
                double cellWidth = (_lastSlotViewportWidth - CompactSlotGap * DisplayColumnCount) / DisplayColumnCount;
                double cellHeight = (_lastSlotViewportHeight - CompactSlotGap * DisplayRowCount) / DisplayRowCount;
                _compactSlotDisplayWidth = Math.Max(64d, Math.Floor(cellWidth));
                _compactSlotDisplayHeight = Math.Max(40d, Math.Floor(cellHeight));
            }
            else
            {
                double cellWidth = (_lastSlotViewportWidth - StandardSlotGap * DisplayColumnCount) / DisplayColumnCount;
                double cellHeight = (_lastSlotViewportHeight - StandardSlotGap * DisplayRowCount) / DisplayRowCount;
                _standardMagneticSlotDisplayWidth = Math.Max(StandardMagneticSlotMinWidth, Math.Floor(cellWidth));
                _standardMagneticSlotDisplayHeight = Math.Max(StandardMagneticSlotMinHeight, Math.Floor(cellHeight)) * StandardMagneticSlotHeightScale;
            }

            NotifyMagneticDiskSlotDimensionProperties();
        }

        /// <summary>
        /// 单档口快照：按可视区域等比铺满，并随窗体尺寸变化实时缩放。
        /// </summary>
        public void UpdateSingleSlotSnapshotDimensions(double availableWidth, double availableHeight)
        {
            if (!IsSingleSlotSnapshot || Slots.Count == 0 || DisplayColumnCount <= 0 || DisplayRowCount <= 0)
            {
                return;
            }

            _lastSlotViewportWidth = Math.Max(availableWidth, 240d);
            _lastSlotViewportHeight = Math.Max(availableHeight, 180d);

            CabinetSlotViewModel slot = Slots[0];
            double slotGap = ResolveSlotSurfaceGap();
            double naturalWidth = slot.BaseSlotDisplayWidth;
            double naturalHeight = slot.BaseSlotDisplayHeight;
            double targetWidth = _lastSlotViewportWidth - slotGap - SnapshotSlotBorderMargin;
            double targetHeight = _lastSlotViewportHeight - slotGap - SnapshotSlotBorderMargin;
            if (naturalWidth <= 0d || naturalHeight <= 0d || targetWidth <= 0d || targetHeight <= 0d)
            {
                return;
            }

            double fitScale = Math.Min(targetWidth / naturalWidth, targetHeight / naturalHeight);
            fitScale = Math.Max(0.2d, fitScale);

            _snapshotSlotDisplayWidth = Math.Max(120d, Math.Floor(naturalWidth * fitScale));
            _snapshotSlotDisplayHeight = Math.Max(80d, Math.Floor(naturalHeight * fitScale));

            double renderCanvasWidth = Math.Max(0d, _snapshotSlotDisplayWidth - StandardSlotHorizontalChrome);
            double renderCanvasHeight = Math.Max(0d, _snapshotSlotDisplayHeight - (slot.BaseSlotDisplayHeight - slot.SlotCanvasHeight * slot.CanvasDisplayScale));

            if (IsMagneticDiskCabinet)
            {
                _standardMagneticSlotDisplayWidth = _snapshotSlotDisplayWidth;
                _standardMagneticSlotDisplayHeight = _snapshotSlotDisplayHeight;
            }
            else
            {
                slot.UpdateSnapshotCanvasLayout(renderCanvasWidth, renderCanvasHeight);
            }

            NotifyMagneticDiskSlotDimensionProperties();
        }

        private double ResolveSlotSurfaceGap()
            => IsSingleSlotSnapshot || (IsMagneticDiskCabinet && !IsCompactDisplayMode)
                ? StandardSlotGap
                : CompactSlotGap;

        private double ResolveEffectiveSlotVerticalChrome()
        {
            CabinetSlotViewModel? slot = Slots.FirstOrDefault();
            if (slot == null)
            {
                return StandardMagneticSlotVerticalChrome;
            }

            return slot.SlotDisplayHeight - slot.RenderSlotCanvasHeight;
        }

        private void NotifyMagneticDiskSlotDimensionProperties()
        {
            OnPropertyChanged(nameof(CompactSlotDisplayWidth));
            OnPropertyChanged(nameof(CompactSlotDisplayHeight));
            OnPropertyChanged(nameof(EffectiveSlotDisplayWidth));
            OnPropertyChanged(nameof(EffectiveSlotDisplayHeight));
            OnPropertyChanged(nameof(EffectiveRenderSlotCanvasWidth));
            OnPropertyChanged(nameof(EffectiveRenderSlotCanvasHeight));
            OnPropertyChanged(nameof(EffectiveSlotsSurfaceWidth));
            OnPropertyChanged(nameof(EffectiveSlotsSurfaceHeight));
            OnPropertyChanged(nameof(SlotsSurfaceWidth));
            OnPropertyChanged(nameof(SlotsSurfaceHeight));
        }

        /// <summary>
        /// 兼容旧调用；防磁磁盘柜请使用 <see cref="UpdateMagneticDiskSlotDimensions"/>。
        /// </summary>
        public void UpdateCompactSlotDimensions(double availableWidth, double availableHeight)
        {
            UpdateMagneticDiskSlotDimensions(availableWidth, availableHeight);
        }

        public void NotifySlotSelectionChanged()
        {
            OnPropertyChanged(nameof(SelectedSlotCount));
            OnPropertyChanged(nameof(SelectedSlotSummaryText));
            OnPropertyChanged(nameof(HasSelectedSlots));
            OnPropertyChanged(nameof(CanApplySelectedSlotsPurpose));
            OnPropertyChanged(nameof(CanSelectAllSlots));
            OnPropertyChanged(nameof(CompactBatchPurposeMenuVisibility));
            OnPropertyChanged(nameof(CompactPerSlotCategoryMenuVisibility));
            OnPropertyChanged(nameof(BatchApplyPurposeMenuText));
            CommandManager.InvalidateRequerySuggested();
        }

        public void SelectArchiveBox(ArchiveBoxItemViewModel archiveBox)
        {
            ArgumentNullException.ThrowIfNull(archiveBox);

            if (_selectedArchiveBox == archiveBox)
            {
                return;
            }

            ClearContentSelectionWithoutNotify();
            _selectedArchiveBox = archiveBox;
            archiveBox.IsSelected = true;
            NotifyContentSelectionChanged();
        }

        public void SelectHardDiskMedium(CabinetHardDiskMediumItemViewModel medium)
        {
            ArgumentNullException.ThrowIfNull(medium);
            if (medium.IsEmpty)
            {
                return;
            }

            if (_selectedHardDiskMedium == medium)
            {
                return;
            }

            ClearContentSelectionWithoutNotify();
            _selectedHardDiskMedium = medium;
            medium.IsSelected = true;
            NotifyContentSelectionChanged();
        }

        public void ClearContentSelection()
        {
            if (!ClearContentSelectionWithoutNotify())
            {
                return;
            }

            NotifyContentSelectionChanged();
        }

        private bool ClearContentSelectionWithoutNotify()
        {
            bool changed = false;
            if (_selectedArchiveBox != null)
            {
                _selectedArchiveBox.IsSelected = false;
                _selectedArchiveBox = null;
                changed = true;
            }

            if (_selectedHardDiskMedium != null)
            {
                _selectedHardDiskMedium.IsSelected = false;
                _selectedHardDiskMedium = null;
                changed = true;
            }

            return changed;
        }

        private void NotifyContentSelectionChanged()
        {
            OnPropertyChanged(nameof(HasContentSelection));
            OnPropertyChanged(nameof(SelectedContentSummaryText));
            OnPropertyChanged(nameof(CanSetInteractiveItemRelocationFromSelection));
            CommandManager.InvalidateRequerySuggested();
        }

        public void PrepareCompactSlotContextMenu(CabinetSlotViewModel clickedSlot)
        {
            if (!IsCompactDisplayMode)
            {
                return;
            }

            if (!clickedSlot.IsSelected)
            {
                ClearSlotSelectionWithoutNotify();
                clickedSlot.IsSelected = true;
                _selectionAnchorSlotCode = clickedSlot.SlotCode;
                NotifySlotSelectionChanged();
            }
        }

        public void HandleCompactSlotSelection(CabinetSlotViewModel slot, bool ctrlPressed, bool shiftPressed)
        {
            if (!IsCompactDisplayMode || Slots.Count == 0)
            {
                return;
            }

            if (shiftPressed)
            {
                var anchorSlot = ResolveSelectionAnchorSlot() ?? slot;
                SelectSlotRange(anchorSlot, slot, additive: ctrlPressed);
                if (!ctrlPressed)
                {
                    _selectionAnchorSlotCode = anchorSlot.SlotCode;
                }
            }
            else if (ctrlPressed)
            {
                slot.IsSelected = !slot.IsSelected;
                _selectionAnchorSlotCode = slot.SlotCode;
            }
            else
            {
                ClearSlotSelectionWithoutNotify();
                slot.IsSelected = true;
                _selectionAnchorSlotCode = slot.SlotCode;
            }

            NotifySlotSelectionChanged();
        }

        public void SelectAllSlots()
        {
            if (Slots.Count == 0)
            {
                return;
            }

            foreach (var slot in Slots)
            {
                slot.IsSelected = true;
            }

            _selectionAnchorSlotCode = Slots[0].SlotCode;
            NotifySlotSelectionChanged();
        }

        public void InvertSlotSelection()
        {
            if (Slots.Count == 0)
            {
                return;
            }

            foreach (var slot in Slots)
            {
                slot.IsSelected = !slot.IsSelected;
            }

            _selectionAnchorSlotCode = Slots.FirstOrDefault(slot => slot.IsSelected)?.SlotCode;
            NotifySlotSelectionChanged();
        }

        public void ClearSlotSelection()
        {
            if (!ClearSlotSelectionWithoutNotify())
            {
                return;
            }

            _selectionAnchorSlotCode = null;
            NotifySlotSelectionChanged();
        }

        private bool ClearSlotSelectionWithoutNotify()
        {
            bool changed = false;
            foreach (var slot in Slots)
            {
                if (!slot.IsSelected)
                {
                    continue;
                }

                slot.IsSelected = false;
                changed = true;
            }

            return changed;
        }

        private CabinetSlotViewModel? ResolveSelectionAnchorSlot()
        {
            if (string.IsNullOrWhiteSpace(_selectionAnchorSlotCode))
            {
                return null;
            }

            return Slots.FirstOrDefault(slot =>
                string.Equals(slot.SlotCode, _selectionAnchorSlotCode, StringComparison.OrdinalIgnoreCase));
        }

        private void SelectSlotRange(CabinetSlotViewModel anchorSlot, CabinetSlotViewModel targetSlot, bool additive)
        {
            int anchorKey = GetSlotSortKey(anchorSlot);
            int targetKey = GetSlotSortKey(targetSlot);
            int minKey = Math.Min(anchorKey, targetKey);
            int maxKey = Math.Max(anchorKey, targetKey);

            if (!additive)
            {
                ClearSlotSelectionWithoutNotify();
            }

            foreach (var slot in Slots)
            {
                int key = GetSlotSortKey(slot);
                if (key >= minKey && key <= maxKey)
                {
                    slot.IsSelected = true;
                }
            }
        }

        private static int GetSlotSortKey(CabinetSlotViewModel slot)
            => slot.VisualRowIndex * 10000 + slot.VisualColumnIndex;

        private void ShowSlotInfo(CabinetSlotViewModel? slot)
        {
            ShowSlotDetail(slot);
        }

        public void ShowSlotDetail(CabinetSlotViewModel? slot)
        {
            if (slot == null)
            {
                return;
            }

            _dialogService.ShowCabinetSlotDetailDialog(Request, slot, CanShowSlotZoomFromSlot(slot));
        }

        private void ShowSlotZoom(CabinetSlotViewModel? slot)
        {
            if (slot == null || !CanShowSlotZoomFromSlot(slot))
            {
                return;
            }

            _dialogService.ShowCabinetOpenDialog(CabinetSlotDetailViewModel.BuildSlotZoomRequest(Request, slot.SlotCode));
        }

        private bool CanShowSlotZoomFromSlot(CabinetSlotViewModel? slot)
            => slot != null && !IsSingleSlotSnapshot;

        private void AddArchiveBox(CabinetSlotViewModel? slot)
        {
            if (slot == null)
            {
                return;
            }

            _dialogService.ShowMessage($"格口 {slot.SlotCode} 的新增档案盒功能将在后续步骤实现。", "提示");
        }

        private void RefreshSlot(CabinetSlotViewModel? slot)
        {
            if (slot == null)
            {
                return;
            }

            ReloadSlotsAndBroadcast(slot.SlotCode);
        }

        private void ReloadSlotsAndBroadcast(string? slotCode = null)
        {
            _cabinetOpenLayoutRefreshNotifier.RequestRefresh(new CabinetOpenLayoutRefreshScope
            {
                CabinetId = Request.CabinetId,
                Face = Request.Face,
                SlotCode = slotCode?.Trim() ?? string.Empty
            });
        }

        private void OnLayoutRefreshRequested(CabinetOpenLayoutRefreshScope scope)
        {
            if (!MatchesLayoutRefreshScope(scope))
            {
                return;
            }

            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                Application.Current.Dispatcher.Invoke(ReloadSlots);
                return;
            }

            ReloadSlots();
        }

        private bool MatchesLayoutRefreshScope(CabinetOpenLayoutRefreshScope scope)
        {
            if (scope.CabinetId != Request.CabinetId || scope.Face != Request.Face)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(scope.SlotCode))
            {
                return true;
            }

            if (IsSingleSlotSnapshot)
            {
                return string.Equals(scope.SlotCode, _targetSlotCode, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private void ToggleDamagedDiskSlot(CabinetSlotViewModel? slot)
        {
            ToggleDedicatedSlotCategory(slot, CabinetHardDiskSlotCategoryAssignment.CategoryDamaged, slot?.IsDamagedDiskDedicatedSlot == true, true);
        }

        private void ToggleDamagedOpticalDiscSlot(CabinetSlotViewModel? slot)
        {
            ToggleDedicatedSlotCategory(slot, CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc, slot?.IsDamagedOpticalDiscDedicatedSlot == true, false);
        }

        private void ToggleDataDiskSlot(CabinetSlotViewModel? slot)
        {
            ToggleDedicatedSlotCategory(slot, CabinetHardDiskSlotCategoryAssignment.CategoryData, slot?.IsDataDiskDedicatedSlot == true, false);
        }

        private void ToggleBlankDiskSlot(CabinetSlotViewModel? slot)
        {
            ToggleDedicatedSlotCategory(slot, CabinetHardDiskSlotCategoryAssignment.CategoryBlank, slot?.IsBlankDiskDedicatedSlot == true, false);
        }

        private void ToggleDataOpticalDiscSlot(CabinetSlotViewModel? slot)
        {
            ToggleDedicatedSlotCategory(slot, CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc, slot?.IsDataOpticalDiscDedicatedSlot == true, false);
        }

        private void ToggleHistoricalDataDiskSlot(CabinetSlotViewModel? slot)
        {
            ToggleDedicatedSlotCategory(slot, CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataHardDisk, slot?.IsHistoricalDataDiskDedicatedSlot == true, false);
        }

        private void ToggleHistoricalDataOpticalDiscSlot(CabinetSlotViewModel? slot)
        {
            ToggleDedicatedSlotCategory(slot, CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataOpticalDisc, slot?.IsHistoricalDataOpticalDiscDedicatedSlot == true, false);
        }

        private void ToggleDedicatedSlotCategory(CabinetSlotViewModel? slot, string categoryName, bool isCurrentCategory, bool showReturnHint)
        {
            if (slot == null || !slot.IsMagneticDiskSlot || !IsArchiveRoomMediaAdmin())
            {
                return;
            }

            try
            {
                if (isCurrentCategory)
                {
                    if (!_dialogService.ShowConfirm($"确定取消 {CurrentFaceDisplayName} {slot.SlotCode} 的{categoryName}设置吗？", "确认"))
                    {
                        return;
                    }

                    _cabinetService.ClearHardDiskDedicatedSlotCategory(Request.CabinetId, Request.Face.ToString(), slot.SlotCode);
                    _dialogService.ShowMessage($"已取消 {CurrentFaceDisplayName} {slot.SlotCode} 的{categoryName}设置。", "提示");
                }
                else
                {
                    string suffix = showReturnHint ? "\n后续损坏硬盘归还登记将自动回柜到该档口。" : string.Empty;
                    if (!_dialogService.ShowConfirm($"确定将 {CurrentFaceDisplayName} {slot.SlotCode} 设置为{categoryName}吗？{suffix}", "确认"))
                    {
                        return;
                    }

                    _cabinetService.SetHardDiskDedicatedSlotCategory(Request.CabinetId, Request.Face.ToString(), slot.SlotCode, categoryName);
                    _dialogService.ShowMessage($"已将 {CurrentFaceDisplayName} {slot.SlotCode} 设置为{categoryName}。", "提示");
                }

                ReloadSlotsAndBroadcast(slot.SlotCode);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void EditSlotPlacementMode(CabinetSlotViewModel? slot)
        {
            if (slot == null || slot.ArchiveBoxes.Count == 0)
            {
                return;
            }

            var initialMode = _cabinetArchiveBoxPlacementService.GetPlacementMode(slot.ArchiveBoxes[0].BoxCode);
            var selectedMode = _dialogService.ShowCabinetArchiveBoxPlacementEditDialog(
                $"设置格口 {slot.SlotCode} 放置方式",
                $"将 {CurrentFaceDisplayName} {slot.SlotCode} 内全部档案盒统一设置为所选放置方式。",
                initialMode);

            if (selectedMode == null)
            {
                return;
            }

            int updatedCount = _cabinetArchiveBoxPlacementService.UpdateSlotPlacementMode(Request.CabinetName, Request.Face.ToString(), slot.SlotCode, selectedMode.Value, GetUpdatedBy());
            if (updatedCount <= 0)
            {
                updatedCount = slot.ArchiveBoxes.Count(box =>
                    _cabinetArchiveBoxPlacementService.UpdateBoxPlacementMode(box.BoxCode, selectedMode.Value, GetUpdatedBy()));
            }

            if (updatedCount <= 0)
            {
                _dialogService.ShowMessage($"格口 {slot.SlotCode} 当前没有可更新的档案盒。", "提示");
                return;
            }

            ReloadSlotsAndBroadcast(slot.SlotCode);
        }

        private void EditArchiveBoxPlacementMode(ArchiveBoxItemViewModel? archiveBox)
        {
            if (archiveBox == null)
            {
                return;
            }

            var initialMode = _cabinetArchiveBoxPlacementService.GetPlacementMode(archiveBox.BoxCode);
            var selectedMode = _dialogService.ShowCabinetArchiveBoxPlacementEditDialog(
                $"设置档案盒 {archiveBox.BoxCode} 放置方式",
                $"将档案盒 {archiveBox.BoxCode} 设置为所选放置方式。",
                initialMode);

            if (selectedMode == null)
            {
                return;
            }

            if (!_cabinetArchiveBoxPlacementService.UpdateBoxPlacementMode(archiveBox.BoxCode, selectedMode.Value, GetUpdatedBy()))
            {
                _dialogService.ShowMessage($"档案盒 {archiveBox.BoxCode} 当前没有可更新的摆放记录。", "提示");
                return;
            }

            ReloadSlotsAndBroadcast(archiveBox.SlotCode);
        }

        private void ShowArchiveContent(ArchiveBoxItemViewModel? archiveBox)
        {
            if (archiveBox == null)
            {
                return;
            }

            _dialogService.ShowCabinetArchiveBoxContentDialog(archiveBox.BoxCode);
        }

        private static bool CanShowPendingReturnDetail(ArchiveBoxItemViewModel? archiveBox) =>
            archiveBox != null
            && archiveBox.HasPendingReturn
            && archiveBox.YearlyArchiveBoxId > 0
            && !archiveBox.IsMixedPlacement;

        private void ShowPendingReturnDetail(ArchiveBoxItemViewModel? archiveBox)
        {
            if (archiveBox == null)
            {
                return;
            }

            _dialogService.ShowCabinetArchiveBoxPendingReturnDetailDialog(
                archiveBox.BoxCode,
                archiveBox.BoxLabel,
                archiveBox.PendingReturnCopyCount);
        }

        private void ResetArchiveBoxSpecification(ArchiveBoxItemViewModel? archiveBox)
        {
            if (archiveBox == null)
            {
                return;
            }

            IReadOnlyList<string> specifications = _cabinetArchiveBoxPlacementService.GetAvailableBoxSpecifications();
            if (specifications.Count == 0)
            {
                _dialogService.ShowMessage("未找到可用的档案盒规格。", "提示");
                return;
            }

            string? selectedSpecification = _dialogService.ShowSheetSelectionDialog(specifications.ToList(), "设置档案盒规格");
            if (string.IsNullOrWhiteSpace(selectedSpecification))
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确定将档案盒 {archiveBox.BoxCode} 的规格设置为“{selectedSpecification}”吗？", "确认"))
            {
                return;
            }

            if (!_cabinetArchiveBoxPlacementService.ResetBoxSpecification(archiveBox.BoxCode, selectedSpecification, GetUpdatedBy()))
            {
                _dialogService.ShowMessage($"档案盒 {archiveBox.BoxCode} 当前没有可设置的规格记录。", "提示");
                return;
            }

            ReloadSlotsAndBroadcast(archiveBox.SlotCode);
        }

        private void ShowHardDiskMediumInfo(CabinetHardDiskMediumItemViewModel? hardDiskMedium)
        {
            if (hardDiskMedium == null || !hardDiskMedium.CanShowInfo)
            {
                return;
            }

            _dialogService.ShowMessage(hardDiskMedium.InfoText, "硬盘介质信息");
        }

        private void ShowHardDiskMediumArchiveInfo(CabinetHardDiskMediumItemViewModel? hardDiskMedium)
        {
            if (hardDiskMedium == null || !hardDiskMedium.CanShowArchiveInfo)
            {
                return;
            }

            if (hardDiskMedium.ElectronicArchiveUnitId > 0)
            {
                _dialogService.ShowCabinetElectronicBagContentDialog(hardDiskMedium.ElectronicArchiveUnitId);
                return;
            }

            if (!string.IsNullOrWhiteSpace(hardDiskMedium.ElectronicArchiveLocationText))
            {
                _dialogService.ShowCabinetElectronicBagContentDialogByLocation(hardDiskMedium.ElectronicArchiveLocationText);
                return;
            }

            _dialogService.ShowMessage(hardDiskMedium.ArchiveInfoText, "资料信息");
        }

        private string GetUpdatedBy()
        {
            return _userContextService.CurrentUser?.RealName ?? "System";
        }

        private bool IsArchiveRoomMediaAdmin()
        {
            string dept = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty;
            string role = _userContextService.CurrentUser?.Role?.Trim() ?? string.Empty;

            return (string.Equals(dept, "资料室", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(role, "部门资料管理员", StringComparison.OrdinalIgnoreCase)) ||
                   string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsArchiveAdmin()
        {
            return _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser);
        }

        private bool CanSetBatchRelocationSource(CabinetSlotViewModel? slot)
        {
            if (slot == null || !IsArchiveAdmin() || IsSingleSlotSnapshot)
            {
                return false;
            }

            if (IsMagneticDiskRelocationCabinet)
            {
                return slot.IsYearlyDataMagneticDiskSourceSlot;
            }

            return IsArchiveRelocationCabinet && slot.IsYearlySimulatedOnlyArchiveSlot;
        }

        private void SetBatchRelocationSource(CabinetSlotViewModel? slot)
        {
            if (!CanSetBatchRelocationSource(slot) || slot == null)
            {
                return;
            }

            bool isElectronic = IsMagneticDiskRelocationCabinet;
            int itemCount = isElectronic ? slot.HardDiskPresentCount : slot.ArchiveBoxes.Count;
            string itemLabel = isElectronic ? "袋" : "盒";

            _interactiveItemRelocationSession.ClearSource();
            _batchSlotRelocationSession.SetSource(new BatchSlotRelocationEndpoint
            {
                CabinetName = Request.CabinetName,
                FaceCode = ResolveFaceCode(slot.Face),
                Row = slot.LayerIndex,
                Column = slot.ColumnIndex,
                SlotCode = slot.SlotCode,
                MediaKind = isElectronic
                    ? ArchiveRegisterDomainValues.MediaKindElectronic
                    : ArchiveRegisterDomainValues.MediaKindSimulated,
                DedicatedSlotCategoryName = isElectronic ? slot.DedicatedSlotCategoryName : string.Empty,
                ItemCount = itemCount
            });

            _dialogService.ShowMessage(
                $"已将 [{Request.CabinetName}{ResolveFaceCode(slot.Face)}-{slot.LayerIndex}-{slot.ColumnIndex}] 设为批量搬迁源（{itemCount} {itemLabel}）。请在全空目标档口右键选择「搬迁到此档口」。",
                "批量搬迁");
        }

        private bool CanRelocateBatchToSlot(CabinetSlotViewModel? slot)
        {
            if (slot == null || !IsArchiveAdmin() || IsSingleSlotSnapshot || !HasBatchRelocationSource)
            {
                return false;
            }

            var source = _batchSlotRelocationSession.Source;
            if (source == null)
            {
                return false;
            }

            if (string.Equals(source.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
            {
                return IsMagneticDiskRelocationCabinet
                    && slot.IsFullyEmptyMagneticDiskSlot
                    && slot.CanAcceptElectronicBatchRelocationTarget(source.DedicatedSlotCategoryName);
            }

            return IsArchiveRelocationCabinet && slot.IsFullyEmptyArchiveSlot;
        }

        private async Task RelocateBatchToSlotAsync(CabinetSlotViewModel? slot)
        {
            if (!CanRelocateBatchToSlot(slot) || slot == null || _batchSlotRelocationSession.Source == null)
            {
                return;
            }

            var source = _batchSlotRelocationSession.Source;
            var request = new BatchSimulatedSlotPhysicalMoveRequest
            {
                SourceCabinetName = source.CabinetName,
                SourceFace = source.FaceCode,
                SourceRow = source.Row,
                SourceColumn = source.Column,
                TargetCabinetName = Request.CabinetName,
                TargetFace = ResolveFaceCode(slot.Face),
                TargetRow = slot.LayerIndex,
                TargetColumn = slot.ColumnIndex
            };

            bool isElectronic = string.Equals(
                source.MediaKind,
                ArchiveRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal);

            try
            {
                var preview = isElectronic
                    ? await _archiveRelocationService.PreviewBatchElectronicSlotPhysicalMoveAsync(request)
                    : await _archiveRelocationService.PreviewBatchSimulatedSlotPhysicalMoveAsync(request);
                if (!preview.CanExecute)
                {
                    _dialogService.ShowMessage(preview.BlockReason, "无法批量搬迁");
                    return;
                }

                if (!_dialogService.ShowConfirm($"{preview.SummaryText}\n\n确认执行档口批量搬迁？", "确认批量搬迁"))
                {
                    return;
                }

                if (!isElectronic)
                {
                    string? pendingReturnWarning = await _archiveRelocationService.GetBatchSimulatedPendingReturnConfirmMessageAsync(
                        request,
                        "实施批量搬迁");
                    if (!string.IsNullOrWhiteSpace(pendingReturnWarning)
                        && !_dialogService.ShowConfirm(pendingReturnWarning, "待归还提醒"))
                    {
                        return;
                    }
                }

                _dialogService.SetBusyState(true);
                var result = isElectronic
                    ? await _archiveRelocationService.ExecuteBatchElectronicSlotPhysicalMoveAsync(request)
                    : await _archiveRelocationService.ExecuteBatchSimulatedSlotPhysicalMoveAsync(request);
                if (result.Success)
                {
                    _batchSlotRelocationSession.ClearSource();
                    _dialogService.ShowMessage($"{result.Message}\n迁档单号：{result.RelocationNo}", "批量搬迁完成");
                    ReloadSlotsAndBroadcast();
                }
                else
                {
                    _dialogService.ShowError(result.Message, "批量搬迁失败");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "批量搬迁失败");
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private void ClearBatchRelocationSource()
        {
            _batchSlotRelocationSession.ClearSource();
        }

        private bool CanSetInteractiveItemRelocationFromArchiveBox(ArchiveBoxItemViewModel? box)
        {
            return SupportsInteractiveItemRelocation
                && IsArchiveRelocationCabinet
                && box != null
                && box.CanInteractiveRelocate;
        }

        private bool CanSetInteractiveItemRelocationFromMedium(CabinetHardDiskMediumItemViewModel? medium)
        {
            return SupportsInteractiveItemRelocation
                && IsMagneticDiskRelocationCabinet
                && medium != null
                && medium.CanInteractiveRelocate;
        }

        private void SetInteractiveItemRelocationFromArchiveBox(ArchiveBoxItemViewModel? box)
        {
            if (!CanSetInteractiveItemRelocationFromArchiveBox(box) || box == null)
            {
                return;
            }

            _batchSlotRelocationSession.ClearSource();
            _interactiveItemRelocationSession.SetSource(new InteractiveItemRelocationSource
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindSimulated,
                SourceBoxId = box.YearlyArchiveBoxId,
                DisplayText = $"{box.BoxCode}（{box.BoxLabel}）",
                BoxSpecification = box.BoxSpecification,
                SourceStorageLocation = box.BoxCode
            });

            _dialogService.ShowMessage(
                $"已将档案盒 [{box.BoxCode}] 设为迁档对象。请在目标档口右键选择「迁档到此档口」。",
                "交互式迁档");
        }

        private void SetInteractiveItemRelocationFromMedium(CabinetHardDiskMediumItemViewModel? medium)
        {
            if (!CanSetInteractiveItemRelocationFromMedium(medium) || medium == null)
            {
                return;
            }

            var slot = FindSlotForMedium(medium);
            if (slot == null)
            {
                _dialogService.ShowMessage("未找到该介质所在档口，无法设为迁档对象。", "交互式迁档");
                return;
            }

            _batchSlotRelocationSession.ClearSource();
            string displayText = string.IsNullOrWhiteSpace(medium.ElectronicArchiveNoText)
                ? medium.DiskCodeText
                : medium.ElectronicArchiveNoText;
            _interactiveItemRelocationSession.SetSource(new InteractiveItemRelocationSource
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
                SourceUnitId = medium.ElectronicArchiveUnitId,
                DisplayText = displayText,
                SourceDedicatedSlotCategoryName = slot.DedicatedSlotCategoryName,
                SourceStorageLocation = medium.CurrentLocationText,
                IsOpticalDiscMedia = medium.IsOpticalDiscMedia
            });

            string mediumLabel = medium.IsOpticalDiscMedia ? "光盘" : "硬盘";
            _dialogService.ShowMessage(
                $"已将{mediumLabel}介质袋 [{displayText}] 设为迁档对象。请在用途一致且有空余盘位的目标档口右键选择「迁档到此档口」。",
                "交互式迁档");
        }

        private void SetInteractiveItemRelocationFromSelection()
        {
            if (_selectedArchiveBox?.CanInteractiveRelocate == true && IsArchiveRelocationCabinet)
            {
                SetInteractiveItemRelocationFromArchiveBox(_selectedArchiveBox);
                return;
            }

            if (_selectedHardDiskMedium?.CanInteractiveRelocate == true && IsMagneticDiskRelocationCabinet)
            {
                SetInteractiveItemRelocationFromMedium(_selectedHardDiskMedium);
            }
        }

        private bool CanRelocateInteractiveItemToSlotFromSession(CabinetSlotViewModel? slot)
        {
            return HasInteractiveItemRelocationSource
                && CanRelocateInteractiveItemToSlot(slot, _interactiveItemRelocationSession.Source);
        }

        private bool CanRelocateInteractiveItemToSlot(CabinetSlotViewModel? slot, InteractiveItemRelocationSource? source = null)
        {
            source ??= _interactiveItemRelocationSession.Source;
            if (slot == null || !SupportsInteractiveItemRelocation || source == null)
            {
                return false;
            }

            if (string.Equals(source.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal))
            {
                return IsArchiveRelocationCabinet
                    && slot.CanAcceptInteractiveItemRelocationTarget(source.MediaKind, source.SourceDedicatedSlotCategoryName);
            }

            return IsMagneticDiskRelocationCabinet
                && slot.CanAcceptInteractiveItemRelocationTarget(source.MediaKind, source.SourceDedicatedSlotCategoryName);
        }

        public InteractiveItemRelocationDragPayload? TryCreateDragPayloadFromArchiveBox(ArchiveBoxItemViewModel archiveBox)
        {
            if (!CanSetInteractiveItemRelocationFromArchiveBox(archiveBox))
            {
                return null;
            }

            return new InteractiveItemRelocationDragPayload
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindSimulated,
                SourceBoxId = archiveBox.YearlyArchiveBoxId,
                DisplayText = $"{archiveBox.BoxCode}（{archiveBox.BoxLabel}）",
                BoxSpecification = archiveBox.BoxSpecification,
                SourceStorageLocation = archiveBox.BoxCode
            };
        }

        public InteractiveItemRelocationDragPayload? TryCreateDragPayloadFromMedium(CabinetHardDiskMediumItemViewModel medium)
        {
            if (!CanSetInteractiveItemRelocationFromMedium(medium))
            {
                return null;
            }

            var slot = FindSlotForMedium(medium);
            if (slot == null)
            {
                return null;
            }

            string displayText = string.IsNullOrWhiteSpace(medium.ElectronicArchiveNoText)
                ? medium.DiskCodeText
                : medium.ElectronicArchiveNoText;

            return new InteractiveItemRelocationDragPayload
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
                SourceUnitId = medium.ElectronicArchiveUnitId,
                DisplayText = displayText,
                SourceDedicatedSlotCategoryName = slot.DedicatedSlotCategoryName,
                SourceStorageLocation = medium.CurrentLocationText,
                IsOpticalDiscMedia = medium.IsOpticalDiscMedia
            };
        }

        public bool CanAcceptInteractiveItemDragOnSlot(CabinetSlotViewModel slot, InteractiveItemRelocationDragPayload payload)
        {
            ArgumentNullException.ThrowIfNull(slot);
            ArgumentNullException.ThrowIfNull(payload);
            return CanRelocateInteractiveItemToSlot(slot, payload.ToRelocationSource());
        }

        public void SetInteractiveItemDragHover(CabinetSlotViewModel? slot, InteractiveItemRelocationDragPayload? payload)
        {
            ClearInteractiveItemDragHover();
            if (slot == null || payload == null)
            {
                return;
            }

            slot.InteractiveRelocationDropHighlight = CanAcceptInteractiveItemDragOnSlot(slot, payload)
                ? CabinetSlotViewModel.InteractiveRelocationDropHighlightKind.Allowed
                : CabinetSlotViewModel.InteractiveRelocationDropHighlightKind.Denied;
        }

        public void ClearInteractiveItemDragHover()
        {
            foreach (var slot in Slots)
            {
                slot.ClearInteractiveRelocationDropHighlight();
            }
        }

        public async Task HandleInteractiveItemDropAsync(CabinetSlotViewModel slot, InteractiveItemRelocationDragPayload payload)
        {
            ArgumentNullException.ThrowIfNull(slot);
            ArgumentNullException.ThrowIfNull(payload);

            var source = payload.ToRelocationSource();
            if (!CanRelocateInteractiveItemToSlot(slot, source))
            {
                return;
            }

            _batchSlotRelocationSession.ClearSource();
            _interactiveItemRelocationSession.SetSource(source);
            await ExecuteInteractiveItemRelocationAsync(slot, source);
        }

        private async Task RelocateInteractiveItemToSlotAsync(CabinetSlotViewModel? slot)
        {
            if (!CanRelocateInteractiveItemToSlot(slot) || slot == null)
            {
                return;
            }

            var source = _interactiveItemRelocationSession.Source;
            if (source == null)
            {
                return;
            }

            await ExecuteInteractiveItemRelocationAsync(slot, source);
        }

        private async Task ExecuteInteractiveItemRelocationAsync(CabinetSlotViewModel slot, InteractiveItemRelocationSource source)
        {
            var request = new InteractiveItemPhysicalMoveRequest
            {
                MediaKind = source.MediaKind,
                SourceBoxId = source.SourceBoxId,
                SourceUnitId = source.SourceUnitId,
                TargetCabinetName = Request.CabinetName,
                TargetFace = ResolveFaceCode(slot.Face),
                TargetRow = slot.LayerIndex,
                TargetColumn = slot.ColumnIndex
            };

            try
            {
                var preview = await _archiveRelocationService.PreviewInteractiveItemPhysicalMoveAsync(request);
                if (!preview.CanExecute)
                {
                    _dialogService.ShowMessage(preview.BlockReason, "无法迁档");
                    return;
                }

                if (!_dialogService.ShowConfirm($"{preview.SummaryText}\n\n确认执行迁档？", "确认迁档"))
                {
                    return;
                }

                if (string.Equals(source.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal)
                    && source.SourceBoxId > 0)
                {
                    string? pendingReturnWarning = await _archiveRelocationService.GetSimulatedPendingReturnConfirmMessageAsync(
                        source.SourceBoxId,
                        "实施迁档");
                    if (!string.IsNullOrWhiteSpace(pendingReturnWarning)
                        && !_dialogService.ShowConfirm(pendingReturnWarning, "待归还提醒"))
                    {
                        return;
                    }
                }

                _dialogService.SetBusyState(true);
                var result = await _archiveRelocationService.ExecuteInteractiveItemPhysicalMoveAsync(request);
                if (result.Success)
                {
                    _interactiveItemRelocationSession.ClearSource();
                    _dialogService.ShowMessage($"{result.Message}\n迁档单号：{result.RelocationNo}", "迁档完成");
                    ReloadSlotsAndBroadcast();
                }
                else
                {
                    _dialogService.ShowError(result.Message, "迁档失败");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "迁档失败");
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private void ClearInteractiveItemRelocationSource()
        {
            _interactiveItemRelocationSession.ClearSource();
        }

        private CabinetSlotViewModel? FindSlotForMedium(CabinetHardDiskMediumItemViewModel medium)
        {
            return Slots.FirstOrDefault(slot => slot.HardDiskMediaItems.Contains(medium));
        }

        private void OnInteractiveItemRelocationSourceChanged()
        {
            OnPropertyChanged(nameof(HasInteractiveItemRelocationSource));
            OnPropertyChanged(nameof(InteractiveItemRelocationSourceText));
            OnPropertyChanged(nameof(InteractiveItemRelocationSourceHintVisibility));
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnBatchSlotRelocationSourceChanged()
        {
            OnPropertyChanged(nameof(HasBatchRelocationSource));
            OnPropertyChanged(nameof(BatchRelocationSourceText));
            OnPropertyChanged(nameof(BatchRelocationSourceHintVisibility));
            CommandManager.InvalidateRequerySuggested();
        }

        private static string ResolveFaceCode(CabinetFace face)
        {
            return face == CabinetFace.B ? "B" : "A";
        }

        private void ApplySelectedSlotsPurpose()
        {
            if (!CanApplySelectedSlotsPurpose)
            {
                return;
            }

            var selectedSlots = Slots.Where(slot => slot.IsSelected).ToList();
            if (selectedSlots.Count == 0)
            {
                return;
            }

            string? sharedCategory = ResolveSharedCategoryName(selectedSlots);
            var result = _dialogService.ShowCabinetHardDiskSlotCategoryEditDialog(
                "统一设置档口用途",
                $"将为 {CurrentFaceDisplayName} 已选的 {selectedSlots.Count} 个档口设置相同专用用途。",
                sharedCategory);

            if (result == null)
            {
                return;
            }

            try
            {
                string faceCode = Request.Face.ToString();
                foreach (var slot in selectedSlots)
                {
                    if (string.IsNullOrWhiteSpace(result.CategoryName))
                    {
                        _cabinetService.ClearHardDiskDedicatedSlotCategory(Request.CabinetId, faceCode, slot.SlotCode);
                    }
                    else
                    {
                        _cabinetService.SetHardDiskDedicatedSlotCategory(Request.CabinetId, faceCode, slot.SlotCode, result.CategoryName);
                    }
                }

                _dialogService.ShowMessage($"已更新 {selectedSlots.Count} 个档口的用途设置。", "提示");
                ClearSlotSelection();
                ReloadSlotsAndBroadcast();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private static string? ResolveSharedCategoryName(IReadOnlyList<CabinetSlotViewModel> selectedSlots)
        {
            if (selectedSlots.Count == 0)
            {
                return null;
            }

            string first = selectedSlots[0].DedicatedSlotCategoryName;
            bool allSame = selectedSlots.All(slot =>
                string.Equals(slot.DedicatedSlotCategoryName, first, StringComparison.OrdinalIgnoreCase));
            return allSame ? first : null;
        }

        private void ReloadSlots()
        {
            ClearSlotSelection();
            ClearContentSelectionWithoutNotify();
            _selectedArchiveBox = null;
            _selectedHardDiskMedium = null;
            _interactiveItemRelocationSession.ClearSource();
            NotifyContentSelectionChanged();
            Slots.Clear();
            if (IsSingleSlotSnapshot)
            {
                _snapshotSlotDisplayWidth = 0d;
                _snapshotSlotDisplayHeight = 0d;
            }

            var descriptors = _cabinetOpenLayoutService.BuildSlots(Request);
            if (IsSingleSlotSnapshot)
            {
                descriptors = descriptors
                    .Where(descriptor => string.Equals(descriptor.SlotCode, _targetSlotCode, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                DisplayRowCount = 1;
                DisplayColumnCount = 1;
                OnPropertyChanged(nameof(DisplayRowCount));
                OnPropertyChanged(nameof(DisplayColumnCount));
            }

            foreach (var descriptor in descriptors)
            {
                Slots.Add(new CabinetSlotViewModel(descriptor));
            }

            OnPropertyChanged(nameof(ArchiveBoxCount));
            OnPropertyChanged(nameof(MixedArchiveBoxCount));
            OnPropertyChanged(nameof(PendingSortingRecordCount));
            OnPropertyChanged(nameof(MagneticDiskMediumCount));
            OnPropertyChanged(nameof(MagneticDiskPendingReturnCount));
            OnPropertyChanged(nameof(MagneticDiskFreeCapacityCount));
            OnPropertyChanged(nameof(AverageUtilizationText));
            OnPropertyChanged(nameof(SlotDisplayWidth));
            OnPropertyChanged(nameof(SlotDisplayHeight));
            OnPropertyChanged(nameof(SlotsSurfaceWidth));
            OnPropertyChanged(nameof(SlotsSurfaceHeight));
            OnPropertyChanged(nameof(EffectiveSlotDisplayWidth));
            OnPropertyChanged(nameof(EffectiveSlotDisplayHeight));
            OnPropertyChanged(nameof(EffectiveRenderSlotCanvasWidth));
            OnPropertyChanged(nameof(EffectiveRenderSlotCanvasHeight));
            OnPropertyChanged(nameof(EffectiveSlotsSurfaceWidth));
            OnPropertyChanged(nameof(EffectiveSlotsSurfaceHeight));
            if (IsMagneticDiskCabinet)
            {
                UpdateMagneticDiskSlotDimensions(_lastSlotViewportWidth, _lastSlotViewportHeight);
            }
            else if (IsSingleSlotSnapshot)
            {
                UpdateSingleSlotSnapshotDimensions(_lastSlotViewportWidth, _lastSlotViewportHeight);
            }
            OnPropertyChanged(nameof(CanSelectAllSlots));
            OnPropertyChanged(nameof(CompactBatchPurposeMenuVisibility));
            OnPropertyChanged(nameof(CompactPerSlotCategoryMenuVisibility));
            OnPropertyChanged(nameof(BatchApplyPurposeMenuText));
            CommandManager.InvalidateRequerySuggested();
        }

        private static string GetCabinetTypeDisplayName(CabinetType cabinetType)
        {
            return cabinetType switch
            {
                CabinetType.Standard => "标准滑道式档案柜",
                CabinetType.Vertical => "立式文件柜",
                CabinetType.Horizontal => "卧式文件柜",
                CabinetType.MagneticDisk => "防磁磁盘柜",
                _ => "档案柜"
            };
        }

        private static string GetSideDisplayName(CabinetType cabinetType, CabinetFace face)
        {
            if (cabinetType == CabinetType.MagneticDisk)
            {
                return face switch
                {
                    CabinetFace.A => "左门",
                    CabinetFace.B => "右门",
                    _ => "左门"
                };
            }

            return face switch
            {
                CabinetFace.A => "A面",
                CabinetFace.B => "B面",
                _ => "A面"
            };
        }

        private static string GetPreviewTitle(CabinetType cabinetType)
        {
            return cabinetType switch
            {
                CabinetType.Standard => "标准柜双门展开中",
                CabinetType.Vertical => "立式柜侧开中",
                CabinetType.Horizontal => "卧式柜下翻开启中",
                CabinetType.MagneticDisk => "防磁磁盘柜抽屉展开中",
                _ => "档案柜开柜中"
            };
        }

        private static string GetPreviewSubtitle(CabinetType cabinetType)
        {
            return cabinetType switch
            {
                CabinetType.Standard => "正在展开当前面格口布局...",
                CabinetType.Vertical => "正在展开立式柜当前面格口布局...",
                CabinetType.Horizontal => "正在展开卧式柜当前面格口布局...",
                CabinetType.MagneticDisk => "正在展开防磁磁盘柜当前面抽屉格口布局...",
                _ => "正在展开当前面格口布局..."
            };
        }
    }
}
