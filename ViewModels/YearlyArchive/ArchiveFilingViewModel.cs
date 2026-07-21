using DocMgr.Models.ArchiveContainers;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DocMgr.ViewModels.YearlyArchive
{
    public partial class ArchiveFilingViewModel : ViewModelBase
    {
        private readonly IArchiveFilingService _filingService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IServiceScopeFactory _scopeFactory;
        private int _selectedRecordsChangedGeneration;
        private int _simulatedBoxIndexCalculationGeneration;
        private bool _suppressSimulatedLocationRecalc;
        private bool _suppressSimulatedLocationOptionSync;
        private int _simulatedTargetLocationOptionsGeneration;
        private bool _suppressElectronicLocationRecalc;
        private bool _suppressElectronicLocationOptionSync;
        private int _electronicTargetLocationOptionsGeneration;
        private readonly SemaphoreSlim _selectedRecordsChangedGate = new(1, 1);
        private bool _isInitialized;
        private int _currentCellBoxCount;
        private int _resolvedBoxSequenceIndex = 1;
        private int _currentElectronicCellMediumCount;
        private int _resolvedElectronicSequenceIndex = 1;
        private string _draftNewArchiveSequenceNo = string.Empty;
        private string _draftNewElectronicArchiveNo = string.Empty;
        private IReadOnlyList<ArchiveContainerSummary> _existingContainerSummaries = Array.Empty<ArchiveContainerSummary>();
        private PendingExternalHardDiskRegistration? _registeredExternalHardDisk;
        private HardDiskMediaReturnCandidate? _borrowedHardDiskReturnCandidate;
        private ElectronicArchiveUiDecision _electronicDecision = ArchiveFilingBusinessRules.ResolveUiDecision(new ElectronicArchiveScenarioInput());
        private ElectronicMediaFormListItem? _selectedElectronicMediaForm;
        private bool _isRefreshingElectronicScenario;
        private bool _suppressElectronicScenarioRefresh;
        private bool _suppressElectronicSubmissionModeChange;
        private string _externalHardDiskFormattedBlankTargetLocation = string.Empty;

        private bool UsesOpticalDiscCarrierForLabels =>
            SelectedElectronicSubmissionMode is ElectronicArchiveSubmissionMode.CopyNewOpticalDisc
                or ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew
                or ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc;

        public ArchiveFilingViewModel(
            IArchiveFilingService filingService,
            IHardDiskMediaService hardDiskMediaService,
            IDialogService dialogService,
            IUserContextService userContextService,
            IServiceScopeFactory scopeFactory)
        {
            _filingService = filingService;
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _scopeFactory = scopeFactory;

            PendingYears = new ObservableCollection<string>();
            SimulatedPendingRecords = new ObservableCollection<YearlyArchiveRegisterRecord>();
            ElectronicPendingRecords = new ObservableCollection<YearlyArchiveRegisterRecord>();
            Cabinets = new ObservableCollection<Cabinet>();
            Sides = new ObservableCollection<string>();
            Rows = new ObservableCollection<string>();
            Columns = new ObservableCollection<string>();
            SimulatedTargetLocationOptions = new ObservableCollection<ArchiveBoxTargetLocationOption>();
            ElectronicCabinets = new ObservableCollection<Cabinet>();
            ElectronicSides = new ObservableCollection<string>();
            ElectronicRows = new ObservableCollection<string>();
            ElectronicColumns = new ObservableCollection<string>();
            ElectronicTargetLocationOptions = new ObservableCollection<HardDiskMediaReturnTargetLocationOption>();
            ExistingBoxes = new ObservableCollection<YearlyArchiveBox>();
            ExistingElectronicUnits = new ObservableCollection<ExistingElectronicArchiveUnitListItem>();
            SimulatedRecordItems = new ObservableCollection<SelectableSimulatedArchiveItemViewModel>();
            ElectronicRecordItems = new ObservableCollection<SelectableElectronicArchiveMediaViewModel>();
            ElectronicRecordItemsStepTwo = new ObservableCollection<SelectableElectronicArchiveMediaViewModel>();
            ElectronicMediaFormOptions = new ObservableCollection<ElectronicMediaFormListItem>();
            AvailableElectronicSubmissionModes = new ObservableCollection<ElectronicArchiveSubmissionModeOption>();
            Specs = new ObservableCollection<string> { "标准(10cm)", "标准(5cm)", "标准(3cm)", "标准(2cm)", "非标(10cm)" };

            SimulatedRecordItemsPanel = new ItemDetailsListPresenter<SelectableSimulatedArchiveItemViewModel>(
                "资料子项",
                summaryBuilder: items => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    items,
                    item => item.ContentDesc,
                    "暂无资料子项"));
            SimulatedRecordItemsPanel.RefreshItems(SimulatedRecordItems);

            ElectronicRecordItemsStepTwoPanel = new ItemDetailsListPresenter<SelectableElectronicArchiveMediaViewModel>(
                "资料明细",
                summaryBuilder: items => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    items,
                    item => item.ItemName,
                    "暂无资料明细"));
            ElectronicRecordItemsStepTwoPanel.RefreshItems(ElectronicRecordItemsStepTwo);

            ElectronicFilingExistingDetailRowsPanel = new ItemDetailsListPresenter<ElectronicFilingDetailRowViewModel>(
                "已立档明细",
                summaryBuilder: items => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    items,
                    item => item.ItemName,
                    "暂无已立档明细"));
            ElectronicFilingExistingDetailRowsPanel.RefreshItems(ElectronicFilingExistingDetailRows);

            ElectronicFilingPendingDetailRowsPanel = new ItemDetailsListPresenter<ElectronicFilingDetailRowViewModel>(
                "待立档明细",
                summaryBuilder: items => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    items,
                    item => item.ItemName,
                    "暂无待立档明细"));
            ElectronicFilingPendingDetailRowsPanel.RefreshItems(ElectronicFilingPendingDetailRows);

            RefreshPendingCommand = new RelayCommand(async _ => await RefreshPendingList());
            GenSeqCommand = new RelayCommand(async _ => await GenerateSequence());
            SubmitCommand = new RelayCommand(async _ => await Submit());
            PreviewElectronicSubmissionCommand = new RelayCommand(async _ => await PreviewElectronicSubmissionAsync());
            SuggestSimulatedLocationCommand = new RelayCommand(async _ => await SuggestSimulatedLocationAsync());
            ShowSimulatedSlotSnapshotCommand = new RelayCommand(_ => ShowSimulatedSlotSnapshot());
            SuggestElectronicLocationCommand = new RelayCommand(async _ => await SuggestElectronicLocationAsync());
            ShowElectronicSlotSnapshotCommand = new RelayCommand(_ => ShowElectronicSlotSnapshot());
            SelectElectronicMediaCommand = new RelayCommand(_ => SelectElectronicMedia());
            RegisterExternalHardDiskCommand = new RelayCommand(async _ => await RegisterExternalHardDiskAsync());
            RecommendExternalHardDiskBlankTargetLocationCommand = new RelayCommand(async _ => await RecommendExternalHardDiskBlankTargetLocationAsync());
            ShowExternalHardDiskBlankTargetSlotSnapshotCommand = new RelayCommand(_ => ShowExternalHardDiskBlankTargetSlotSnapshot());

            InitializePendingYears();
            IsNewBoxMode = true;
            SelectedSpec = "标准(5cm)";
            SelectedTrackIndex = 0;
            ResetPanelState();
        }

        public ObservableCollection<string> PendingYears { get; }
        public ObservableCollection<YearlyArchiveRegisterRecord> SimulatedPendingRecords { get; }
        public ObservableCollection<YearlyArchiveRegisterRecord> ElectronicPendingRecords { get; }
        public ObservableCollection<Cabinet> Cabinets { get; }
        public ObservableCollection<string> Sides { get; }
        public ObservableCollection<string> Rows { get; }
        public ObservableCollection<string> Columns { get; }
        public ObservableCollection<ArchiveBoxTargetLocationOption> SimulatedTargetLocationOptions { get; }
        public ObservableCollection<Cabinet> ElectronicCabinets { get; }
        public ObservableCollection<string> ElectronicSides { get; }
        public ObservableCollection<string> ElectronicRows { get; }
        public ObservableCollection<string> ElectronicColumns { get; }
        public ObservableCollection<HardDiskMediaReturnTargetLocationOption> ElectronicTargetLocationOptions { get; }
        public ObservableCollection<YearlyArchiveBox> ExistingBoxes { get; }
        public ObservableCollection<ExistingElectronicArchiveUnitListItem> ExistingElectronicUnits { get; }
        public ObservableCollection<SelectableSimulatedArchiveItemViewModel> SimulatedRecordItems { get; }
        public ItemDetailsListPresenter<SelectableSimulatedArchiveItemViewModel> SimulatedRecordItemsPanel { get; }
        public ObservableCollection<SelectableElectronicArchiveMediaViewModel> ElectronicRecordItems { get; }
        public ObservableCollection<SelectableElectronicArchiveMediaViewModel> ElectronicRecordItemsStepTwo { get; }
        public ItemDetailsListPresenter<SelectableElectronicArchiveMediaViewModel> ElectronicRecordItemsStepTwoPanel { get; }
        public ObservableCollection<ElectronicMediaFormListItem> ElectronicMediaFormOptions { get; }
        public ObservableCollection<ElectronicArchiveSubmissionModeOption> AvailableElectronicSubmissionModes { get; }
        public ObservableCollection<string> Specs { get; }

        public ItemDetailsListPresenter<ElectronicFilingDetailRowViewModel> ElectronicFilingExistingDetailRowsPanel { get; }

        public ItemDetailsListPresenter<ElectronicFilingDetailRowViewModel> ElectronicFilingPendingDetailRowsPanel { get; }

        public event Action<IReadOnlyList<int>>? SimulatedPendingSelectionRestoreRequested;

        /// <summary>
        /// 在批量替换待立档池列表期间为 true，此时忽略 ListView 的 SelectionChanged，避免触发数据库加载并与当前 DbContext 访问交错导致死锁。
        /// </summary>
        public bool SuppressPendingListSelectionSync { get; private set; }

        /// <summary>
        /// 请求视图清空两侧待立档 ListView 的选中项（在非恢复选中路径下调用）。
        /// </summary>
        public event Action? RequestClearPendingListSelections;

        private string _selectedPendingYear = string.Empty;
        public string SelectedPendingYear
        {
            get => _selectedPendingYear;
            set
            {
                if (SetProperty(ref _selectedPendingYear, value) && _isInitialized)
                {
                    _ = RefreshPendingList();
                }
            }
        }

        private int _simulatedPendingCount;
        public int SimulatedPendingCount
        {
            get => _simulatedPendingCount;
            private set
            {
                if (SetProperty(ref _simulatedPendingCount, value))
                {
                    OnPropertyChanged(nameof(SimulatedTrackTabHeader));
                }
            }
        }

        private int _simulatedFiledCount;
        public int SimulatedFiledCount
        {
            get => _simulatedFiledCount;
            private set
            {
                if (SetProperty(ref _simulatedFiledCount, value))
                {
                    OnPropertyChanged(nameof(SimulatedTrackTabHeader));
                }
            }
        }

        private int _electronicPendingCount;
        public int ElectronicPendingCount
        {
            get => _electronicPendingCount;
            private set
            {
                if (SetProperty(ref _electronicPendingCount, value))
                {
                    OnPropertyChanged(nameof(ElectronicTrackTabHeader));
                }
            }
        }

        private int _electronicFiledCount;
        public int ElectronicFiledCount
        {
            get => _electronicFiledCount;
            private set
            {
                if (SetProperty(ref _electronicFiledCount, value))
                {
                    OnPropertyChanged(nameof(ElectronicTrackTabHeader));
                }
            }
        }

        public string SimulatedTrackTabHeader => $"模拟介质立档（{SimulatedPendingCount}/{SimulatedFiledCount}）";

        public string ElectronicTrackTabHeader => $"电子介质立档（{ElectronicPendingCount}/{ElectronicFiledCount}）";

        private int _selectedTrackIndex;
        public int SelectedTrackIndex
        {
            get => _selectedTrackIndex;
            set
            {
                if (SetProperty(ref _selectedTrackIndex, value))
                {
                    OnPropertyChanged(nameof(IsSimulatedTrack));
                    OnPropertyChanged(nameof(IsElectronicTrack));
                    OnPropertyChanged(nameof(IsSimulatedLocationEditable));
                    OnPropertyChanged(nameof(IsSimulatedArchiveFieldsEditable));
                    OnPropertyChanged(nameof(IsSimulatedRemarksEditable));
                    OnPropertyChanged(nameof(IsElectronicLocationEditable));
                    OnPropertyChanged(nameof(IsElectronicArchiveFieldsEditable));
                    OnPropertyChanged(nameof(IsElectronicUsbScenario));
                    OnPropertyChanged(nameof(IsElectronicInnerNetworkScenario));
                    OnPropertyChanged(nameof(IsElectronicOpticalDiscScenario));
                    OnPropertyChanged(nameof(IsElectronicHardDiskScenario));
                    OnPropertyChanged(nameof(IsElectronicHardDiskReturnScenario));
                    OnPropertyChanged(nameof(IsElectronicHardDiskRetainedScenario));
                    OnPropertyChanged(nameof(IsOpticalDiscArchiveScenario));
                    OnPropertyChanged(nameof(IsElectronicCopyScenario));
                    OnPropertyChanged(nameof(IsElectronicDirectBagScenario));
                    OnPropertyChanged(nameof(IsElectronicMediumSelectionButtonVisible));
                    OnPropertyChanged(nameof(ElectronicStepSevenTitle));
                    OnPropertyChanged(nameof(ElectronicStepEightTitle));
                    OnPropertyChanged(nameof(ElectronicStoragePathLabel));
                    OnPropertyChanged(nameof(ElectronicMediaCountLabel));
                    OnPropertyChanged(nameof(ElectronicLocationActionHintText));
                    RaiseElectronicStepFourPresentationChanged();
                    ResetSelection();
                    ResetPanelState();
                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        public bool IsSimulatedTrack => SelectedTrackIndex == 0;
        public bool IsElectronicTrack => SelectedTrackIndex == 1;

        private List<YearlyArchiveRegisterRecord> _selectedRecords = new();
        public List<YearlyArchiveRegisterRecord> SelectedRecords
        {
            get => _selectedRecords;
            set
            {
                _selectedRecords = value ?? new List<YearlyArchiveRegisterRecord>();
                OnPropertyChanged(nameof(SelectedRecords));
                OnPropertyChanged(nameof(ElectronicApplicationFormNosText));
                _ = HandleSelectedRecordsChangedAsync();
            }
        }

        private Brush _electronicLocationSuggestionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B45309"));
        public Brush ElectronicLocationSuggestionBrush
        {
            get => _electronicLocationSuggestionBrush;
            set => SetProperty(ref _electronicLocationSuggestionBrush, value);
        }

        private string _targetProject = string.Empty;
        public string TargetProject
        {
            get => _targetProject;
            set => SetProperty(ref _targetProject, value);
        }

        private string _targetYear = string.Empty;
        public string TargetYear
        {
            get => _targetYear;
            set => SetProperty(ref _targetYear, value);
        }

        /// <summary>
        /// 左侧勾选批次涉及的申请单编号（去重并列示）。
        /// </summary>
        public string ElectronicApplicationFormNosText =>
            SelectedRecords.Count == 0
                ? string.Empty
                : string.Join("、", SelectedRecords
                    .Select(r => r.FormNo)
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Distinct(StringComparer.OrdinalIgnoreCase));

        public ElectronicMediaFormListItem? SelectedElectronicMediaForm
        {
            get => _selectedElectronicMediaForm;
            set
            {
                if (value != null && !value.CanSelectAsCurrent)
                {
                    return;
                }

                if (SetProperty(ref _selectedElectronicMediaForm, value))
                {
                    SyncElectronicStepTwoRows();
                    OnPropertyChanged(nameof(ElectronicStepOneSelectedDisposition));
                    OnPropertyChanged(nameof(ElectronicStepOneSelectedMediumCode));
                    OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingStatus));
                    OnPropertyChanged(nameof(ElectronicStepOneSelectedFilingProgress));
                    RefreshElectronicScenario();
                    OnCurrentElectronicMediaFormChanged();
                }
            }
        }

        public string ElectronicStepOneSelectedDisposition =>
            string.IsNullOrWhiteSpace(SelectedElectronicMediaForm?.Disposition)
                ? "—"
                : SelectedElectronicMediaForm!.Disposition;

        public string ElectronicStepOneSelectedMediumCode =>
            SelectedElectronicMediaForm?.MediumCode ?? "—";

        public string ElectronicStepOneSelectedFilingStatus =>
            SelectedElectronicMediaForm == null
                ? "—"
                : SelectedElectronicMediaForm.FilingStatus;

        public string ElectronicStepOneSelectedFilingProgress =>
            SelectedElectronicMediaForm == null
                ? "0/0"
                : SelectedElectronicMediaForm.FilingProgressText;

        public Visibility ElectronicStepFourAppendModeNoticeVisibility =>
            IsElectronicTrack && IsAppendMode && !RequiresRetainedHardDiskAppendProcessing
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility ElectronicStepFourNewBoxSectionVisibility =>
            IsElectronicTrack && (IsNewBoxMode || RequiresRetainedHardDiskAppendProcessing)
                ? Visibility.Visible
                : Visibility.Collapsed;

        private bool RequiresRetainedHardDiskAppendProcessing =>
            IsAppendMode && SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk;

        public Visibility ElectronicStepFourBlankHardDiskSectionVisibility =>
            IsNewBoxMode && _electronicDecision.StepFourLayout.ShowBlankInventoryHardDiskSelection
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility ExternalHardDiskRegistrationVisibility =>
            (IsNewBoxMode || RequiresRetainedHardDiskAppendProcessing) && _electronicDecision.StepFourLayout.ShowExternalHardDiskRegistration
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility ExternalHardDiskFormattedBlankLocationVisibility =>
            (IsNewBoxMode || RequiresRetainedHardDiskAppendProcessing) && _electronicDecision.StepFourLayout.ShowExternalHardDiskFormattedBlankLocation
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility ElectronicStepFourBorrowedDirectCompleteVisibility =>
            ComputeElectronicStepFourSummaryOnly() ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ElectronicStepFourGenericIdleVisibility
        {
            get
            {
                if (!IsElectronicTrack || !IsNewBoxMode)
                {
                    return IsElectronicTrack && RequiresRetainedHardDiskAppendProcessing
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }

                if (ElectronicStepFourBlankHardDiskSectionVisibility == Visibility.Visible
                    || ExternalHardDiskRegistrationVisibility == Visibility.Visible
                    || ElectronicStepFourBorrowedDirectCompleteVisibility == Visibility.Visible)
                {
                    return Visibility.Collapsed;
                }

                return Visibility.Visible;
            }
        }

        private YearlyArchiveBox? _selectedExistingBox;
        public YearlyArchiveBox? SelectedExistingBox
        {
            get => _selectedExistingBox;
            set
            {
                if (SetProperty(ref _selectedExistingBox, value))
                {
                    OnSelectedExistingBoxChanged();
                }
            }
        }

        private ExistingElectronicArchiveUnitListItem? _selectedExistingElectronicUnitItem;
        public ExistingElectronicArchiveUnitListItem? SelectedExistingElectronicUnitItem
        {
            get => _selectedExistingElectronicUnitItem;
            set
            {
                if (value != null && !value.CanSelectForAppend)
                {
                    return;
                }

                if (SetProperty(ref _selectedExistingElectronicUnitItem, value))
                {
                    OnPropertyChanged(nameof(SelectedExistingElectronicUnit));
                    OnSelectedExistingElectronicUnitChanged();
                }
            }
        }

        public YearlyElectronicArchiveUnit? SelectedExistingElectronicUnit => SelectedExistingElectronicUnitItem?.Unit;

        public bool IsAppendMode => !IsNewBoxMode;

        private bool _isNewBoxMode;
        public bool IsNewBoxMode
        {
            get => _isNewBoxMode;
            set
            {
                if (SetProperty(ref _isNewBoxMode, value))
                {
                    OnPropertyChanged(nameof(IsAppendMode));
                    OnPropertyChanged(nameof(IsSimulatedLocationEditable));
                    OnPropertyChanged(nameof(IsSimulatedArchiveFieldsEditable));
                    OnPropertyChanged(nameof(IsSimulatedRemarksEditable));
                    OnPropertyChanged(nameof(SimulatedStepTwoLocationSelectorVisibility));
                    OnPropertyChanged(nameof(SimulatedStepTwoAppendNoticeVisibility));
                    OnPropertyChanged(nameof(IsElectronicLocationEditable));
                    OnPropertyChanged(nameof(IsElectronicArchiveFieldsEditable));
                    OnPropertyChanged(nameof(ExternalHardDiskRegistrationVisibility));
                    OnPropertyChanged(nameof(IsElectronicHardDiskReturnScenario));
                    OnPropertyChanged(nameof(IsElectronicHardDiskRetainedScenario));
                    OnPropertyChanged(nameof(IsOpticalDiscArchiveScenario));
                    OnPropertyChanged(nameof(IsElectronicCopyScenario));
                    OnPropertyChanged(nameof(IsElectronicDirectBagScenario));
                    OnPropertyChanged(nameof(IsElectronicMediumSelectionButtonVisible));
                    OnPropertyChanged(nameof(CanUseElectronicAppendMode));
                    OnPropertyChanged(nameof(ElectronicStepSevenTitle));
                    OnPropertyChanged(nameof(ElectronicStepEightTitle));
                    OnPropertyChanged(nameof(ElectronicStepSevenLocationSelectorVisibility));
                    OnPropertyChanged(nameof(ElectronicStepSevenAppendNoticeVisibility));
                    OnPropertyChanged(nameof(IsElectronicStepSevenSuggestLocationEnabled));
                    OnPropertyChanged(nameof(ElectronicStoragePathLabel));
                    OnPropertyChanged(nameof(ElectronicMediaCountLabel));
                    OnPropertyChanged(nameof(ElectronicLocationActionHintText));
                    RaiseElectronicStepFourPresentationChanged();
                    RaiseSlotSnapshotAvailabilityChanged();
                    OnModeChanged();
                }
            }
        }

        private Cabinet? _selectedCabinet;
        public Cabinet? SelectedCabinet
        {
            get => _selectedCabinet;
            set
            {
                if (SetProperty(ref _selectedCabinet, value))
                {
                    bool wasSuppressed = _suppressSimulatedLocationRecalc;
                    _suppressSimulatedLocationRecalc = true;
                    try
                    {
                        UpdateSides();
                        UpdateRowsAndCols();
                    }
                    finally
                    {
                        _suppressSimulatedLocationRecalc = wasSuppressed;
                    }

                    if (!_suppressSimulatedLocationRecalc)
                    {
                        CalculateBoxIndex();
                    }

                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        private string _selectedSide = string.Empty;
        public string SelectedSide
        {
            get => _selectedSide;
            set
            {
                if (SetProperty(ref _selectedSide, value))
                {
                    if (!_suppressSimulatedLocationRecalc)
                    {
                        CalculateBoxIndex();
                    }

                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        private string _selectedRow = string.Empty;
        public string SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetProperty(ref _selectedRow, value))
                {
                    if (!_suppressSimulatedLocationRecalc)
                    {
                        CalculateBoxIndex();
                    }

                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        private string _selectedColumn = string.Empty;
        public string SelectedColumn
        {
            get => _selectedColumn;
            set
            {
                if (SetProperty(ref _selectedColumn, value))
                {
                    if (!_suppressSimulatedLocationRecalc)
                    {
                        CalculateBoxIndex();
                    }

                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        private ArchiveBoxTargetLocationOption? _selectedSimulatedTargetLocationOption;
        public ArchiveBoxTargetLocationOption? SelectedSimulatedTargetLocationOption
        {
            get => _selectedSimulatedTargetLocationOption;
            set
            {
                if (!SetProperty(ref _selectedSimulatedTargetLocationOption, value)
                    || _suppressSimulatedLocationOptionSync)
                {
                    return;
                }

                if (value == null)
                {
                    ClearSimulatedLocationSelectionCore();
                    return;
                }

                if (!TryApplySimulatedSlotOption(value))
                {
                    ClearSimulatedLocationSelectionCore();
                }
            }
        }

        private string _archiveSequenceNo = string.Empty;
        public string ArchiveSequenceNo
        {
            get => _archiveSequenceNo;
            set
            {
                if (SetProperty(ref _archiveSequenceNo, value)
                    && IsSimulatedTrack
                    && IsNewBoxMode)
                {
                    _draftNewArchiveSequenceNo = value;
                }
            }
        }

        private string _physicalCodeResult = "请先选择位置";
        public string PhysicalCodeResult
        {
            get => _physicalCodeResult;
            set
            {
                if (SetProperty(ref _physicalCodeResult, value))
                {
                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        private bool _isPhysicalCodeWarning = true;
        public bool IsPhysicalCodeWarning
        {
            get => _isPhysicalCodeWarning;
            set => SetProperty(ref _isPhysicalCodeWarning, value);
        }

        private string _selectedSpec = "标准(5cm)";
        public string SelectedSpec
        {
            get => _selectedSpec;
            set
            {
                if (SetProperty(ref _selectedSpec, value)
                    && IsSimulatedTrack
                    && IsNewBoxMode)
                {
                    _ = LoadSimulatedTargetLocationOptionsAsync();
                }
            }
        }

        private string _cellCountText = "-";
        public string CellCountText
        {
            get => _cellCountText;
            set => SetProperty(ref _cellCountText, value);
        }

        private string _electronicArchiveNo = string.Empty;
        public string ElectronicArchiveNo
        {
            get => _electronicArchiveNo;
            set
            {
                if (SetProperty(ref _electronicArchiveNo, value)
                    && IsElectronicTrack
                    && IsNewBoxMode)
                {
                    _draftNewElectronicArchiveNo = value;
                }
            }
        }

        private string _electronicStorageCarrierType = string.Empty;
        public string ElectronicStorageCarrierType
        {
            get => _electronicStorageCarrierType;
            set => SetProperty(ref _electronicStorageCarrierType, value);
        }

        private string _electronicSourceCarrierSummary = string.Empty;
        public string ElectronicSourceCarrierSummary
        {
            get => _electronicSourceCarrierSummary;
            set => SetProperty(ref _electronicSourceCarrierSummary, value);
        }

        private string _electronicSourceStoragePathSummary = string.Empty;
        public string ElectronicSourceStoragePathSummary
        {
            get => _electronicSourceStoragePathSummary;
            set => SetProperty(ref _electronicSourceStoragePathSummary, value);
        }

        private string _electronicStoragePath = string.Empty;
        public string ElectronicStoragePath
        {
            get => _electronicStoragePath;
            set => SetProperty(ref _electronicStoragePath, value);
        }

        private string _electronicStorageLocation = string.Empty;
        public string ElectronicStorageLocation
        {
            get => _electronicStorageLocation;
            set
            {
                if (SetProperty(ref _electronicStorageLocation, value))
                {
                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        private string _electronicLinkedMediumCodes = string.Empty;
        public string ElectronicLinkedMediumCodes
        {
            get => _electronicLinkedMediumCodes;
            set
            {
                if (SetProperty(ref _electronicLinkedMediumCodes, value))
                {
                    _ = RebuildElectronicFilingDetailRowsAsync();
                }
            }
        }

        private string _electronicOriginalStorageLocation = string.Empty;
        public string ElectronicOriginalStorageLocation
        {
            get => _electronicOriginalStorageLocation;
            set
            {
                if (SetProperty(ref _electronicOriginalStorageLocation, value))
                {
                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        private string _electronicSelectedMediumStatus = string.Empty;
        public string ElectronicSelectedMediumStatus
        {
            get => _electronicSelectedMediumStatus;
            set
            {
                if (SetProperty(ref _electronicSelectedMediumStatus, value)
                    && IsElectronicTrack
                    && ElectronicStepSevenLocationSelectorVisibility == Visibility.Visible)
                {
                    _ = LoadElectronicTargetLocationOptionsAsync();
                }
            }
        }

        private HardDiskMediaReturnTargetLocationOption? _selectedElectronicTargetLocationOption;
        public HardDiskMediaReturnTargetLocationOption? SelectedElectronicTargetLocationOption
        {
            get => _selectedElectronicTargetLocationOption;
            set
            {
                if (!SetProperty(ref _selectedElectronicTargetLocationOption, value)
                    || _suppressElectronicLocationOptionSync)
                {
                    return;
                }

                if (value == null)
                {
                    ClearElectronicLocationSelectionCore();
                    return;
                }

                if (!TryApplyElectronicSlotCode(value.Location))
                {
                    ClearElectronicLocationSelectionCore();
                }
            }
        }

        private Cabinet? _selectedElectronicCabinet;
        public Cabinet? SelectedElectronicCabinet
        {
            get => _selectedElectronicCabinet;
            set
            {
                if (SetProperty(ref _selectedElectronicCabinet, value))
                {
                    UpdateElectronicSides();
                    UpdateElectronicRowsAndCols();
                    if (!_suppressElectronicLocationRecalc)
                    {
                        CalculateElectronicLocation();
                    }

                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        private string _selectedElectronicSide = string.Empty;
        public string SelectedElectronicSide
        {
            get => _selectedElectronicSide;
            set
            {
                if (SetProperty(ref _selectedElectronicSide, value))
                {
                    if (!_suppressElectronicLocationRecalc)
                    {
                        CalculateElectronicLocation();
                    }

                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        private string _selectedElectronicRow = string.Empty;
        public string SelectedElectronicRow
        {
            get => _selectedElectronicRow;
            set
            {
                if (SetProperty(ref _selectedElectronicRow, value))
                {
                    if (!_suppressElectronicLocationRecalc)
                    {
                        CalculateElectronicLocation();
                    }

                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        private string _selectedElectronicColumn = string.Empty;
        public string SelectedElectronicColumn
        {
            get => _selectedElectronicColumn;
            set
            {
                if (SetProperty(ref _selectedElectronicColumn, value))
                {
                    if (!_suppressElectronicLocationRecalc)
                    {
                        CalculateElectronicLocation();
                    }

                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        private string _electronicCellCountText = "-";
        public string ElectronicCellCountText
        {
            get => _electronicCellCountText;
            set => SetProperty(ref _electronicCellCountText, value);
        }

        private string _electronicDisposition = string.Empty;
        public string ElectronicDisposition
        {
            get => _electronicDisposition;
            set
            {
                if (SetProperty(ref _electronicDisposition, value))
                {
                    RefreshElectronicScenario();
                    OnPropertyChanged(nameof(IsElectronicHardDiskReturnScenario));
                    OnPropertyChanged(nameof(IsElectronicHardDiskRetainedScenario));
                    OnPropertyChanged(nameof(IsOpticalDiscArchiveScenario));
                    OnPropertyChanged(nameof(IsElectronicCopyScenario));
                    OnPropertyChanged(nameof(IsElectronicDirectBagScenario));
                    OnPropertyChanged(nameof(CanUseElectronicAppendMode));
                    OnPropertyChanged(nameof(ElectronicStepSevenTitle));
                }
            }
        }

        private int _electronicMediaCount;
        public int ElectronicMediaCount
        {
            get => _electronicMediaCount;
            set => SetProperty(ref _electronicMediaCount, value);
        }

        private string _electronicContentSummary = string.Empty;
        public string ElectronicContentSummary
        {
            get => _electronicContentSummary;
            set => SetProperty(ref _electronicContentSummary, value);
        }

        private string _selectedHardDiskCopyTargetMode = string.Empty;
        public string SelectedHardDiskCopyTargetMode
        {
            get => _selectedHardDiskCopyTargetMode;
            set
            {
                if (SetProperty(ref _selectedHardDiskCopyTargetMode, value))
                {
                    RefreshElectronicScenario();
                    ApplyHardDiskCopyTargetSelection();
                }
            }
        }

        private bool _isRetainedHardDiskScenario;
        public bool IsRetainedHardDiskScenario
        {
            get => _isRetainedHardDiskScenario;
            set
            {
                if (SetProperty(ref _isRetainedHardDiskScenario, value))
                {
                    RefreshElectronicScenario();
                    OnPropertyChanged(nameof(ExternalHardDiskRegistrationVisibility));
                    OnPropertyChanged(nameof(CanUseElectronicAppendMode));
                }
            }
        }

        private string _selectedRetainedHardDiskSource = string.Empty;
        public string SelectedRetainedHardDiskSource
        {
            get => _selectedRetainedHardDiskSource;
            set
            {
                if (SetProperty(ref _selectedRetainedHardDiskSource, value))
                {
                    RefreshElectronicScenario();
                    OnPropertyChanged(nameof(ExternalHardDiskRegistrationVisibility));
                    OnPropertyChanged(nameof(CanUseElectronicAppendMode));
                    ApplyRetainedHardDiskSourceSelection();
                }
            }
        }

        private ElectronicArchiveSubmissionMode? _selectedElectronicSubmissionMode;
        public ElectronicArchiveSubmissionMode? SelectedElectronicSubmissionMode
        {
            get => _selectedElectronicSubmissionMode;
            set
            {
                ElectronicArchiveSubmissionMode? previousMode = _selectedElectronicSubmissionMode;
                if (!SetProperty(ref _selectedElectronicSubmissionMode, value))
                {
                    return;
                }

                if (_suppressElectronicSubmissionModeChange)
                {
                    return;
                }

                bool shouldUseNewMode = value is not ElectronicArchiveSubmissionMode.CopyAppendExistingHardDisk
                    and not ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk;
                if (_isNewBoxMode != shouldUseNewMode)
                {
                    _isNewBoxMode = shouldUseNewMode;
                    OnPropertyChanged(nameof(IsNewBoxMode));
                    OnPropertyChanged(nameof(IsAppendMode));
                    OnPropertyChanged(nameof(ElectronicStepSevenLocationSelectorVisibility));
                    OnPropertyChanged(nameof(ElectronicStepSevenAppendNoticeVisibility));
                    OnPropertyChanged(nameof(IsElectronicStepSevenSuggestLocationEnabled));
                }

                if (IsElectronicTrack && previousMode is not null && previousMode != value)
                {
                    ResetElectronicFilingStepsFourThroughSixForModeChange();
                }

                RefreshElectronicScenario();
                OnPropertyChanged(nameof(IsElectronicCopyScenario));
                OnPropertyChanged(nameof(IsElectronicDirectBagScenario));
                OnPropertyChanged(nameof(IsOpticalDiscArchiveScenario));
                OnPropertyChanged(nameof(IsElectronicHardDiskRetainedScenario));
                OnPropertyChanged(nameof(IsElectronicHardDiskReturnScenario));
                OnPropertyChanged(nameof(ElectronicStepSevenLocationSelectorVisibility));
                OnPropertyChanged(nameof(ElectronicStepSevenAppendNoticeVisibility));
                OnPropertyChanged(nameof(IsElectronicStepSevenSuggestLocationEnabled));
                OnModeChanged();
            }
        }

        public bool CanUseElectronicAppendMode => _electronicDecision.CanAppend;

        public string ElectronicStepSevenTitle => "第七步：资料介质物理存放位置";

        public string ElectronicStepEightTitle => "第八步：赋码与确认";

        public ElectronicArchiveStepFourLayoutDescriptor ElectronicStepFourLayout => _electronicDecision.StepFourLayout;

        public string ElectronicStoragePathLabel => UsesOpticalDiscCarrierForLabels ? "原始目录/盘面信息：" : "归档路径：";

        public string ElectronicMediaCountLabel => UsesOpticalDiscCarrierForLabels ? "袋内光盘数量：" : "袋内硬盘数量：";

        public string ElectronicLocationActionHintText => _electronicDecision.StorageCarrierType.Contains("光盘", StringComparison.OrdinalIgnoreCase)
            ? "光盘介质袋应放入防磁磁盘柜“年度数据光盘专用档口”。"
            : "电子资料拷贝/入袋后的硬盘介质袋应放入“年度数据硬盘专用档口”。";

        public Visibility ElectronicStepSevenLocationSelectorVisibility => IsAppendMode
            ? (RequiresRetainedHardDiskAppendProcessing ? Visibility.Visible : Visibility.Collapsed)
            : Visibility.Visible;

        public Visibility ElectronicStepEightSlotSnapshotVisibility =>
            IsElectronicTrack
            && IsAppendMode
            && ElectronicStepSevenLocationSelectorVisibility == Visibility.Collapsed
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility ElectronicStepSevenAppendNoticeVisibility => IsAppendMode
            ? (RequiresRetainedHardDiskAppendProcessing ? Visibility.Collapsed : Visibility.Visible)
            : Visibility.Collapsed;

        public string ElectronicStepSevenAppendNoticeText => "当前场景为并档操作，无需选择新的数据硬盘物理存放位置。";

        public bool IsElectronicStepSevenSuggestLocationEnabled => !RequiresRetainedHardDiskAppendProcessing;

        public bool IsElectronicUsbScenario => SelectedElectronicMediaTypes.Count == 1 && string.Equals(SelectedElectronicMediaTypes[0], ArchiveRegisterDomainValues.ElectronicMediaTypeUsbDrive, StringComparison.Ordinal);

        public bool IsElectronicInnerNetworkScenario => SelectedElectronicMediaTypes.Count == 1 && string.Equals(SelectedElectronicMediaTypes[0], ArchiveRegisterDomainValues.ElectronicMediaTypeInnerNetwork, StringComparison.Ordinal);

        public bool IsElectronicOpticalDiscScenario => SelectedElectronicMediaTypes.Count == 1 && string.Equals(SelectedElectronicMediaTypes[0], ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc, StringComparison.Ordinal);

        public bool IsElectronicHardDiskScenario => SelectedElectronicMediaTypes.Count == 1 && string.Equals(SelectedElectronicMediaTypes[0], ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.Ordinal);

        public bool IsElectronicHardDiskReturnScenario => IsElectronicHardDiskScenario && string.Equals(ElectronicDisposition?.Trim(), ArchiveRegisterDomainValues.ElectronicDispositionReturn, StringComparison.Ordinal);

        public bool IsElectronicHardDiskRetainedScenario => IsElectronicHardDiskScenario && string.Equals(ElectronicDisposition?.Trim(), ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.Ordinal);

        public bool IsOpticalDiscArchiveScenario => SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew;

        public bool IsElectronicCopyScenario => SelectedElectronicSubmissionMode is ElectronicArchiveSubmissionMode.CopyNewHardDisk or ElectronicArchiveSubmissionMode.CopyNewOpticalDisc or ElectronicArchiveSubmissionMode.CopyAppendExistingHardDisk;

        public bool IsElectronicDirectBagScenario => SelectedElectronicSubmissionMode is ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew or ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew;

        public bool IsElectronicMediumSelectionButtonVisible =>
            ElectronicStepFourBlankHardDiskSectionVisibility == Visibility.Visible;

        private IReadOnlyList<string> SelectedElectronicMediaTypes => EnumerateSelectedElectronicMediaEntryRows()
            .Select(item => item.MediaType?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();

        public string ExternalHardDiskRegistrationTooltip => BuildExternalHardDiskRegistrationTooltip();

        public string RegisteredExternalHardDiskCodeDisplay => _registeredExternalHardDisk?.DiskCode ?? string.Empty;

        /// <summary>
        /// 外来硬盘格式化为空盘后拟入库位置。
        /// </summary>
        public string ExternalHardDiskFormattedBlankTargetLocation
        {
            get => _externalHardDiskFormattedBlankTargetLocation;
            set
            {
                if (SetProperty(ref _externalHardDiskFormattedBlankTargetLocation, value))
                {
                    RaiseSlotSnapshotAvailabilityChanged();
                }
            }
        }

        public string ExternalHardDiskFormattedBlankTargetHintText =>
            "外来留存硬盘在完成新增硬盘登记并用于当前电子立档后，如需格式化为空盘入库，应进入空白硬盘专用档口。";

        private string _electronicLocationSuggestionHint = string.Empty;
        public string ElectronicLocationSuggestionHint
        {
            get => _electronicLocationSuggestionHint;
            set
            {
                if (SetProperty(ref _electronicLocationSuggestionHint, value))
                {
                    OnPropertyChanged(nameof(ElectronicLocationSuggestionHintVisibility));
                }
            }
        }

        public Visibility ElectronicLocationSuggestionHintVisibility => string.IsNullOrWhiteSpace(ElectronicLocationSuggestionHint)
            ? Visibility.Collapsed
            : Visibility.Visible;

        private string _remarks = string.Empty;
        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        private string _summaryText = "已选择 0 份资料，准备新建立档容器";
        public string SummaryText
        {
            get => _summaryText;
            set => SetProperty(ref _summaryText, value);
        }

        public bool IsSimulatedLocationEditable => IsSimulatedTrack && IsNewBoxMode;

        public Visibility SimulatedStepTwoLocationSelectorVisibility => IsNewBoxMode
            ? Visibility.Visible
            : Visibility.Collapsed;

        public Visibility SimulatedStepTwoAppendNoticeVisibility => IsNewBoxMode
            ? Visibility.Collapsed
            : Visibility.Visible;

        public bool IsSimulatedArchiveFieldsEditable => IsSimulatedTrack && IsNewBoxMode;

        public bool IsSimulatedRemarksEditable => IsSimulatedTrack && IsNewBoxMode;

        public bool IsElectronicLocationEditable => IsElectronicTrack && IsNewBoxMode;

        public bool IsElectronicArchiveFieldsEditable => IsElectronicTrack && IsNewBoxMode;

        public RelayCommand RefreshPendingCommand { get; }
        public RelayCommand GenSeqCommand { get; }
        public RelayCommand SubmitCommand { get; }
        public RelayCommand PreviewElectronicSubmissionCommand { get; }
        public RelayCommand SuggestSimulatedLocationCommand { get; }
        public RelayCommand ShowSimulatedSlotSnapshotCommand { get; }
        public RelayCommand SuggestElectronicLocationCommand { get; }
        public RelayCommand ShowElectronicSlotSnapshotCommand { get; }
        public RelayCommand SelectElectronicMediaCommand { get; }
        public RelayCommand RegisterExternalHardDiskCommand { get; }
        public RelayCommand RecommendExternalHardDiskBlankTargetLocationCommand { get; }
        public RelayCommand ShowExternalHardDiskBlankTargetSlotSnapshotCommand { get; }

        public string SelectedSimulatedAppendTargetHintText
        {
            get
            {
                if (IsNewBoxMode)
                {
                    return "当前选中档案盒：新建模式（未启用并入）";
                }

                return SelectedExistingBox == null
                    ? "当前选中档案盒：未选择"
                    : $"当前选中档案盒：{SelectedExistingBox.ArchiveSequenceNo}";
            }
        }


        private void ResetSelection()
        {
            _selectedRecords = new List<YearlyArchiveRegisterRecord>();
            ReplaceItems(SimulatedRecordItems, Array.Empty<SelectableSimulatedArchiveItemViewModel>());
            RefreshSimulatedRecordItemsPanel();
            ReplaceItems(ElectronicRecordItems, Array.Empty<SelectableElectronicArchiveMediaViewModel>());
            ReplaceItems(ElectronicRecordItemsStepTwo, Array.Empty<SelectableElectronicArchiveMediaViewModel>());
            RefreshElectronicRecordItemsStepTwoPanel();
            ReplaceItems(ElectronicMediaFormOptions, Array.Empty<ElectronicMediaFormListItem>());
            _selectedElectronicMediaForm = null;
            OnPropertyChanged(nameof(SelectedElectronicMediaForm));
            OnPropertyChanged(nameof(SelectedRecords));
            OnPropertyChanged(nameof(ElectronicApplicationFormNosText));
            OnPropertyChanged(nameof(ElectronicStepOneSelectedDisposition));
            OnPropertyChanged(nameof(ElectronicStepOneSelectedMediumCode));
            RaiseElectronicStepFourPresentationChanged();
        }

        private void ResetPanelState()
        {
            TargetProject = string.Empty;
            TargetYear = string.Empty;
            _draftNewArchiveSequenceNo = string.Empty;
            _draftNewElectronicArchiveNo = string.Empty;
            _existingContainerSummaries = Array.Empty<ArchiveContainerSummary>();
            ReplaceItems(ExistingBoxes, Array.Empty<YearlyArchiveBox>());
            ReplaceItems(ExistingElectronicUnits, Array.Empty<ExistingElectronicArchiveUnitListItem>());
            ReplaceItems(SimulatedRecordItems, Array.Empty<SelectableSimulatedArchiveItemViewModel>());
            RefreshSimulatedRecordItemsPanel();
            ReplaceItems(ElectronicRecordItems, Array.Empty<SelectableElectronicArchiveMediaViewModel>());
            ReplaceItems(ElectronicRecordItemsStepTwo, Array.Empty<SelectableElectronicArchiveMediaViewModel>());
            RefreshElectronicRecordItemsStepTwoPanel();
            ReplaceItems(ElectronicMediaFormOptions, Array.Empty<ElectronicMediaFormListItem>());
            ClearElectronicFilingDetailState();
            _selectedElectronicMediaForm = null;
            OnPropertyChanged(nameof(SelectedElectronicMediaForm));
            SelectedExistingBox = null;
            SelectedExistingElectronicUnitItem = null;
            ArchiveSequenceNo = string.Empty;
            ResetSimulatedLocationSelection();
            ResetElectronicFields();
            ResetElectronicLocationSelection();
            IsNewBoxMode = true;
            UpdateSummaryText();
            OnPropertyChanged(nameof(ElectronicApplicationFormNosText));
            OnPropertyChanged(nameof(ElectronicStepOneSelectedDisposition));
            OnPropertyChanged(nameof(ElectronicStepOneSelectedMediumCode));
            RaiseElectronicStepFourPresentationChanged();
        }

        /// <summary>
        /// 电子介质立档提交成功后刷新与第三步相关的可并入状态提示，避免残留与上一轮操作无关的缓存。
        /// </summary>
        private void ResetElectronicStepThreeSummaryAfterSuccessfulFiling()
        {
            if (!IsElectronicTrack)
            {
                return;
            }

            OnPropertyChanged(nameof(CanUseElectronicAppendMode));
        }

        private void OnSelectedExistingBoxChanged()
        {
            OnPropertyChanged(nameof(SelectedSimulatedAppendTargetHintText));

            if (IsSimulatedTrack && !IsNewBoxMode && SelectedExistingBox != null)
            {
                ArchiveSequenceNo = SelectedExistingBox.ArchiveSequenceNo;
                PhysicalCodeResult = SelectedExistingBox.BoxLocationCode;
                _suppressSimulatedLocationRecalc = true;
                try
                {
                    SelectedCabinet = Cabinets.FirstOrDefault(item => string.Equals(item.Name, SelectedExistingBox.CabinetName, StringComparison.OrdinalIgnoreCase));
                    SelectedSide = SelectedExistingBox.Side;
                    SelectedRow = SelectedExistingBox.Row.ToString();
                    SelectedColumn = SelectedExistingBox.Column.ToString();
                }
                finally
                {
                    _suppressSimulatedLocationRecalc = false;
                }

                SelectedSpec = string.IsNullOrWhiteSpace(SelectedExistingBox.Specs) ? SelectedSpec : SelectedExistingBox.Specs;
                Remarks = SelectedExistingBox.Remarks;
                _currentCellBoxCount = Math.Max(SelectedExistingBox.BoxIndex, 1);
                CellCountText = $"{_currentCellBoxCount} 盒";
                IsPhysicalCodeWarning = false;
            }

            RaiseSlotSnapshotAvailabilityChanged();
        }

        private void OnSelectedExistingElectronicUnitChanged()
        {
            if (IsElectronicTrack && !IsNewBoxMode && SelectedExistingElectronicUnit != null)
            {
                ElectronicArchiveNo = SelectedExistingElectronicUnit.ElectronicArchiveNo;
                ElectronicStorageCarrierType = SelectedExistingElectronicUnit.StorageCarrierType;
                ElectronicStoragePath = SelectedExistingElectronicUnit.StoragePath;
                ElectronicStorageLocation = SelectedExistingElectronicUnit.StorageLocation;
                ElectronicOriginalStorageLocation = SelectedExistingElectronicUnit.StorageLocation;
                ElectronicLinkedMediumCodes = SelectedExistingElectronicUnit.LinkedMediumCodes;
                ElectronicDisposition = SelectedExistingElectronicUnit.Disposition;
                ElectronicMediaCount = ResolveElectronicMediaCount(SelectedExistingElectronicUnit.LinkedMediumCodes, SelectedExistingElectronicUnit.MediaCount);
                ElectronicContentSummary = SelectedExistingElectronicUnit.ContentSummary;
                ElectronicCellCountText = "-";
                Remarks = SelectedExistingElectronicUnit.Remarks;

                if (RequiresRetainedHardDiskAppendProcessing)
                {
                    _ = LoadElectronicTargetLocationOptionsAsync(SelectedExistingElectronicUnit.StorageLocation);
                }

                RaiseSlotSnapshotAvailabilityChanged();
                return;
            }

            if (IsElectronicTrack && !IsNewBoxMode)
            {
                ClearElectronicAppendTargetFields();
                if (ElectronicStepSevenLocationSelectorVisibility == Visibility.Visible)
                {
                    _ = LoadElectronicTargetLocationOptionsAsync();
                }
            }

            RaiseElectronicStepFourPresentationChanged();
            RaiseSlotSnapshotAvailabilityChanged();
        }

        private void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> source)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }

        private void RefreshSimulatedRecordItemsPanel() =>
            SimulatedRecordItemsPanel.RefreshItems(SimulatedRecordItems, preserveExpanded: SimulatedRecordItemsPanel.IsExpanded);

        private void RefreshElectronicRecordItemsStepTwoPanel() =>
            ElectronicRecordItemsStepTwoPanel.RefreshItems(ElectronicRecordItemsStepTwo, preserveExpanded: ElectronicRecordItemsStepTwoPanel.IsExpanded);

        private void RefreshElectronicFilingExistingDetailRowsPanel() =>
            ElectronicFilingExistingDetailRowsPanel.RefreshItems(ElectronicFilingExistingDetailRows, preserveExpanded: ElectronicFilingExistingDetailRowsPanel.IsExpanded);

        private void RefreshElectronicFilingPendingDetailRowsPanel() =>
            ElectronicFilingPendingDetailRowsPanel.RefreshItems(ElectronicFilingPendingDetailRows, preserveExpanded: ElectronicFilingPendingDetailRowsPanel.IsExpanded);
    }
}