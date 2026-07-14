using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 流转台账页面 ViewModel（容器 → 业务单 → 子流程/明细三级展示）。
    /// </summary>
    public sealed class ArchiveCirculationLedgerViewModel : ViewModelBase
    {
        private readonly IArchiveCirculationLedgerService _ledgerService;
        private readonly IDialogService _dialogService;

        private int _selectedSubTabIndex;
        private DateTime? _operatedFrom;
        private DateTime? _operatedTo;
        private string _selectedTransactionType = string.Empty;
        private string _selectedNodeCategory = string.Empty;
        private string _businessNo = string.Empty;
        private string _operatorName = string.Empty;
        private string _applicantName = string.Empty;
        private string _keyword = string.Empty;
        private string _listingMode = CirculationLedgerListingMode.CirculationOnly;
        private CirculationContainerMasterRow? _selectedContainer;
        private CirculationLedgerBusinessRow? _selectedBusiness;
        private string _summaryText = "共 0 条";
        private string _businessDetailHeader = "业务单";
        private string _subItemDetailHeader = "子流程 / 明细";
        private bool _isInitialized;

        private List<MaterialTransactionLedgerRow> _allCirculationRows = new();
        private List<MaterialOutboundProcessNodeSearchRow> _allProcessNodeRows = new();
        private List<CirculationContainerMasterRow> _neverCirculatedMasters = new();

        public ArchiveCirculationLedgerViewModel(
            IArchiveCirculationLedgerService ledgerService,
            IDialogService dialogService)
        {
            _ledgerService = ledgerService;
            _dialogService = dialogService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            ResetCommand = new RelayCommand(_ => ResetCriteria());
            NavigateToFilingLedgerCommand = new RelayCommand(
                _ => NavigateToFilingLedger(),
                _ => CurrentFilingFactId > 0);
        }

        public event Action<int>? NavigateToFilingLedgerRequested;

        public int SelectedSubTabIndex
        {
            get => _selectedSubTabIndex;
            set
            {
                if (SetProperty(ref _selectedSubTabIndex, value))
                {
                    OnPropertyChanged(nameof(IsSimulatedSubTab));
                    OnPropertyChanged(nameof(IsElectronicSubTab));
                    SelectedContainer = null;
                    RefreshPresentation();
                    SelectedContainer = ContainerMasters.FirstOrDefault();
                    UpdateSummaryText();
                }
            }
        }

        public bool IsSimulatedSubTab => SelectedSubTabIndex == 0;

        public bool IsElectronicSubTab => SelectedSubTabIndex == 1;

        public DateTime? OperatedFrom
        {
            get => _operatedFrom;
            set => SetProperty(ref _operatedFrom, value);
        }

        public DateTime? OperatedTo
        {
            get => _operatedTo;
            set => SetProperty(ref _operatedTo, value);
        }

        public ObservableCollection<FilterOption> TransactionTypeOptions { get; } =
        [
            new FilterOption { Label = "全部业务", Value = string.Empty },
            new FilterOption { Label = "资料出库", Value = MaterialTransactionDomainValues.TypeOutbound },
            new FilterOption { Label = "资料归还", Value = MaterialTransactionDomainValues.TypeReturn }
        ];

        public string SelectedTransactionType
        {
            get => _selectedTransactionType;
            set => SetProperty(ref _selectedTransactionType, value);
        }

        public ObservableCollection<FilterOption> NodeCategoryOptions { get; } =
        [
            new FilterOption { Label = "全部节点", Value = string.Empty },
            new FilterOption { Label = "流程预订", Value = OutboundProcessNodeCategoryFilter.Reservation },
            new FilterOption { Label = "流程撤销", Value = OutboundProcessNodeCategoryFilter.Cancelled },
            new FilterOption { Label = "办结同步", Value = OutboundProcessNodeCategoryFilter.Confirmed }
        ];

        public string SelectedNodeCategory
        {
            get => _selectedNodeCategory;
            set => SetProperty(ref _selectedNodeCategory, value);
        }

        public string BusinessNo
        {
            get => _businessNo;
            set => SetProperty(ref _businessNo, value);
        }

        public string OperatorName
        {
            get => _operatorName;
            set => SetProperty(ref _operatorName, value);
        }

        public string ApplicantName
        {
            get => _applicantName;
            set => SetProperty(ref _applicantName, value);
        }

        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        public string ListingMode
        {
            get => _listingMode;
            set
            {
                if (SetProperty(ref _listingMode, value))
                {
                    OnPropertyChanged(nameof(IsCirculationOnlyListingMode));
                    OnPropertyChanged(nameof(IsIncludeNeverCirculatedListingMode));
                    OnPropertyChanged(nameof(ShowListingModeLimitedHint));
                    if (_isInitialized)
                    {
                        _ = SearchAsync();
                    }
                }
            }
        }

        public bool IsCirculationOnlyListingMode
        {
            get => string.Equals(ListingMode, CirculationLedgerListingMode.CirculationOnly, StringComparison.Ordinal);
            set
            {
                if (value)
                {
                    ListingMode = CirculationLedgerListingMode.CirculationOnly;
                }
            }
        }

        public bool IsIncludeNeverCirculatedListingMode
        {
            get => string.Equals(ListingMode, CirculationLedgerListingMode.IncludeNeverCirculated, StringComparison.Ordinal);
            set
            {
                if (value)
                {
                    ListingMode = CirculationLedgerListingMode.IncludeNeverCirculated;
                }
            }
        }

        public bool ShowListingModeLimitedHint =>
            string.Equals(ListingMode, CirculationLedgerListingMode.IncludeNeverCirculated, StringComparison.Ordinal)
            && !CirculationLedgerNeverCirculatedSupport.CanIncludeNeverCirculated(
                BuildCirculationCriteria(),
                BuildProcessNodeCriteria());

        public string ListingModeLimitedHint =>
            "已选「含未流转在库容器」，但当前业务/单号/节点/人员筛选不适用于未流转容器；请清空后重新查询。";

        public ObservableCollection<CirculationContainerMasterRow> ContainerMasters { get; } = new();

        public ObservableCollection<CirculationLedgerBusinessRow> BusinessRows { get; } = new();

        public ObservableCollection<CirculationLedgerSubItemRow> SubItemRows { get; } = new();

        public CirculationContainerMasterRow? SelectedContainer
        {
            get => _selectedContainer;
            set
            {
                if (SetProperty(ref _selectedContainer, value))
                {
                    SelectedBusiness = null;
                    RefreshBusinessRows();
                    SelectedBusiness = BusinessRows.FirstOrDefault();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public CirculationLedgerBusinessRow? SelectedBusiness
        {
            get => _selectedBusiness;
            set
            {
                if (SetProperty(ref _selectedBusiness, value))
                {
                    RefreshSubItemRows();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string SummaryText
        {
            get => _summaryText;
            private set => SetProperty(ref _summaryText, value);
        }

        public string BusinessDetailHeader
        {
            get => _businessDetailHeader;
            private set => SetProperty(ref _businessDetailHeader, value);
        }

        public string SubItemDetailHeader
        {
            get => _subItemDetailHeader;
            private set => SetProperty(ref _subItemDetailHeader, value);
        }

        private int CurrentFilingFactId =>
            SelectedBusiness?.RepresentativeFilingFactId
            ?? SelectedContainer?.RepresentativeFilingFactId
            ?? 0;

        public RelayCommand SearchCommand { get; }

        public RelayCommand ResetCommand { get; }

        public RelayCommand NavigateToFilingLedgerCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await SearchAsync();
            _isInitialized = true;
        }

        private async Task SearchAsync()
        {
            try
            {
                string? selectedContainerCode = SelectedContainer?.ContainerCode;
                ArchiveContainerKind selectedKind = SelectedContainer?.ContainerKind ?? CurrentContainerKind;
                string? selectedBusinessNo = SelectedBusiness?.BusinessNo;
                string? selectedBusinessKind = SelectedBusiness?.BusinessKind;

                var circulationCriteria = BuildCirculationCriteria();
                var processNodeCriteria = BuildProcessNodeCriteria();

                _allCirculationRows = (await _ledgerService.SearchCirculationAsync(circulationCriteria)).ToList();

                _allProcessNodeRows = ShouldLoadProcessNodes()
                    ? (await _ledgerService.SearchOutboundProcessNodesAsync(processNodeCriteria)).ToList()
                    : new List<MaterialOutboundProcessNodeSearchRow>();

                _neverCirculatedMasters = CirculationLedgerNeverCirculatedSupport.CanIncludeNeverCirculated(
                        circulationCriteria,
                        processNodeCriteria)
                    ? (await _ledgerService.SearchNeverCirculatedContainersAsync(circulationCriteria)).ToList()
                    : new List<CirculationContainerMasterRow>();

                RefreshPresentation();

                SelectedContainer = !string.IsNullOrWhiteSpace(selectedContainerCode)
                    ? ContainerMasters.FirstOrDefault(row =>
                        row.ContainerKind == selectedKind
                        && string.Equals(row.ContainerCode, selectedContainerCode, StringComparison.OrdinalIgnoreCase))
                    : ContainerMasters.FirstOrDefault();

                if (SelectedContainer != null
                    && !string.IsNullOrWhiteSpace(selectedBusinessNo)
                    && !string.IsNullOrWhiteSpace(selectedBusinessKind))
                {
                    SelectedBusiness = BusinessRows.FirstOrDefault(row =>
                        string.Equals(row.BusinessKind, selectedBusinessKind, StringComparison.Ordinal)
                        && string.Equals(row.BusinessNo, selectedBusinessNo, StringComparison.OrdinalIgnoreCase))
                        ?? BusinessRows.FirstOrDefault();
                }
                else
                {
                    SelectedBusiness = BusinessRows.FirstOrDefault();
                }

                UpdateSummaryText();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"查询流转台账失败：{ex.Message}");
            }
        }

        private bool ShouldLoadProcessNodes()
        {
            if (string.Equals(SelectedTransactionType, MaterialTransactionDomainValues.TypeReturn, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private void RefreshPresentation()
        {
            ContainerMasters.Clear();
            foreach (var row in CirculationLedgerHierarchySupport.BuildContainerMasters(
                         _allCirculationRows,
                         _allProcessNodeRows,
                         _neverCirculatedMasters,
                         CurrentContainerKind))
            {
                ContainerMasters.Add(row);
            }

            if (SelectedContainer != null
                && !ContainerMasters.Any(row =>
                    row.ContainerKind == SelectedContainer.ContainerKind
                    && string.Equals(row.ContainerCode, SelectedContainer.ContainerCode, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedContainer = ContainerMasters.FirstOrDefault();
            }
            else
            {
                RefreshBusinessRows();
            }
        }

        private void RefreshBusinessRows()
        {
            BusinessRows.Clear();
            foreach (var row in CirculationLedgerHierarchySupport.BuildBusinessRows(
                         SelectedContainer,
                         _allCirculationRows,
                         _allProcessNodeRows))
            {
                BusinessRows.Add(row);
            }

            BusinessDetailHeader = SelectedContainer == null
                ? "业务单"
                : $"业务单 · {SelectedContainer.ContainerKindDisplay} {SelectedContainer.ContainerCode}";

            if (SelectedBusiness != null
                && !BusinessRows.Any(row =>
                    string.Equals(row.BusinessKind, SelectedBusiness.BusinessKind, StringComparison.Ordinal)
                    && string.Equals(row.BusinessNo, SelectedBusiness.BusinessNo, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedBusiness = BusinessRows.FirstOrDefault();
            }
            else
            {
                RefreshSubItemRows();
            }
        }

        private void RefreshSubItemRows()
        {
            SubItemRows.Clear();
            foreach (var row in CirculationLedgerHierarchySupport.BuildSubItemRows(
                         SelectedContainer,
                         SelectedBusiness,
                         _allCirculationRows,
                         _allProcessNodeRows))
            {
                SubItemRows.Add(row);
            }

            SubItemDetailHeader = SelectedBusiness == null
                ? "子流程 / 明细"
                : $"子流程 / 明细 · {SelectedBusiness.DisplayTitle}";
        }

        private ArchiveContainerKind CurrentContainerKind =>
            IsSimulatedSubTab ? ArchiveContainerKind.ArchiveBox : ArchiveContainerKind.ElectronicBag;

        private CirculationLedgerSearchCriteria BuildCirculationCriteria() => new()
        {
            OperatedFrom = OperatedFrom,
            OperatedTo = OperatedTo,
            TransactionType = SelectedTransactionType,
            BusinessNo = BusinessNo,
            OperatorName = OperatorName,
            Keyword = Keyword,
            ListingMode = ListingMode
        };

        private OutboundProcessNodeLedgerSearchCriteria BuildProcessNodeCriteria() => new()
        {
            OperatedFrom = OperatedFrom,
            OperatedTo = OperatedTo,
            OutboundNo = BusinessNo,
            NodeCategory = SelectedNodeCategory,
            OperatorName = OperatorName,
            ApplicantName = ApplicantName,
            Keyword = Keyword
        };

        private void UpdateSummaryText()
        {
            int archiveBoxCount = CountMastersForKind(ArchiveContainerKind.ArchiveBox);
            int electronicCount = CountMastersForKind(ArchiveContainerKind.ElectronicBag);
            int archiveBoxNeverCount = _neverCirculatedMasters.Count(row => row.ContainerKind == ArchiveContainerKind.ArchiveBox);
            int electronicNeverCount = _neverCirculatedMasters.Count(row => row.ContainerKind == ArchiveContainerKind.ElectronicBag);
            string currentLabel = IsSimulatedSubTab ? "档案盒" : "电子介质袋";
            int currentNeverCount = IsSimulatedSubTab ? archiveBoxNeverCount : electronicNeverCount;

            SummaryText =
                $"档案盒 {archiveBoxCount} 个 · 电子介质袋 {electronicCount} 个 · 实物流水 {_allCirculationRows.Count} 条 · 流程节点 {_allProcessNodeRows.Count} 条；当前 {currentLabel} {ContainerMasters.Count} 个"
                + (currentNeverCount > 0 ? $"（含未流转 {currentNeverCount} 个）" : string.Empty)
                + (ShowListingModeLimitedHint ? "；未流转容器需清空业务/单号/节点/人员筛选" : string.Empty);
        }

        private int CountMastersForKind(ArchiveContainerKind containerKind)
        {
            return CirculationLedgerHierarchySupport.BuildContainerMasters(
                _allCirculationRows,
                _allProcessNodeRows,
                CirculationLedgerNeverCirculatedSupport.CanIncludeNeverCirculated(
                    BuildCirculationCriteria(),
                    BuildProcessNodeCriteria())
                    ? _neverCirculatedMasters
                    : Array.Empty<CirculationContainerMasterRow>(),
                containerKind).Count;
        }

        private void ResetCriteria()
        {
            OperatedFrom = null;
            OperatedTo = null;
            SelectedTransactionType = string.Empty;
            SelectedNodeCategory = string.Empty;
            BusinessNo = string.Empty;
            OperatorName = string.Empty;
            ApplicantName = string.Empty;
            Keyword = string.Empty;
            ListingMode = CirculationLedgerListingMode.CirculationOnly;

            _allCirculationRows.Clear();
            _allProcessNodeRows.Clear();
            _neverCirculatedMasters.Clear();
            ContainerMasters.Clear();
            BusinessRows.Clear();
            SubItemRows.Clear();
            SelectedContainer = null;
            SelectedBusiness = null;
            BusinessDetailHeader = "业务单";
            SubItemDetailHeader = "子流程 / 明细";
            SummaryText = "共 0 条";
            CommandManager.InvalidateRequerySuggested();
        }

        private void NavigateToFilingLedger()
        {
            if (CurrentFilingFactId <= 0)
            {
                return;
            }

            NavigateToFilingLedgerRequested?.Invoke(CurrentFilingFactId);
        }

        public sealed class FilterOption
        {
            public string Label { get; init; } = string.Empty;

            public string Value { get; init; } = string.Empty;
        }
    }
}
