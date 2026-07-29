using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘台账列表 ViewModel（基于当前台账数据，而非流转记录）。
    /// </summary>
    public class HardDiskMediaTransactionPageViewModel : ViewModelBase
    {
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IDialogService _dialogService;
        private readonly List<HardDiskMediaTransaction> _transactionCache = new();

        private bool _isInitialized;
        private string _searchKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private string _selectedNature = "全部";
        private string _selectedRegisterLockFilter = HardDiskRegisterLockFilterSupport.All;
        private HardDiskLedgerQuickFilter _quickFilter = HardDiskLedgerQuickFilter.None;
        private HardDiskMedium? _selectedMedium;

        public HardDiskMediaTransactionPageViewModel(IHardDiskMediaService hardDiskMediaService, IDialogService dialogService)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
        }

        public ObservableCollection<HardDiskMedium> MediaItems { get; } = new();
        public ObservableCollection<string> StatusOptions { get; } = new();
        public ObservableCollection<string> NatureOptions { get; } = new();
        public ObservableCollection<string> RegisterLockFilterOptions { get; } = new();

        /// <summary>
        /// 当前选中硬盘的历史流转记录（右下 DataGrid）。
        /// </summary>
        public ObservableCollection<HardDiskMediaTransaction> SelectedMediumTransactions { get; } = new();

        public string SearchKeyword
        {
            get => _searchKeyword;
            set => SetProperty(ref _searchKeyword, value);
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value) && _isInitialized)
                {
                    _quickFilter = HardDiskLedgerQuickFilter.None;
                    RefreshByFilterChange();
                }
            }
        }

        public string SelectedNature
        {
            get => _selectedNature;
            set
            {
                if (SetProperty(ref _selectedNature, value) && _isInitialized)
                {
                    _quickFilter = HardDiskLedgerQuickFilter.None;
                    RefreshByFilterChange();
                }
            }
        }

        public string SelectedRegisterLockFilter
        {
            get => _selectedRegisterLockFilter;
            set
            {
                if (SetProperty(ref _selectedRegisterLockFilter, value) && _isInitialized)
                {
                    RefreshByFilterChange();
                }
            }
        }

        public HardDiskMedium? SelectedMedium
        {
            get => _selectedMedium;
            set
            {
                if (SetProperty(ref _selectedMedium, value))
                {
                    OnPropertyChanged(nameof(SelectedDiskCodeText));
                    OnPropertyChanged(nameof(SelectedSerialNumberText));
                    OnPropertyChanged(nameof(SelectedDiskTypeText));
                    OnPropertyChanged(nameof(SelectedBrandText));
                    OnPropertyChanged(nameof(SelectedCapacityText));
                    OnPropertyChanged(nameof(SelectedInterfaceTypeText));
                    OnPropertyChanged(nameof(SelectedRegistrationMethodText));
                    OnPropertyChanged(nameof(SelectedRegisterPersonText));
                    OnPropertyChanged(nameof(SelectedRegisterDateText));
                    OnPropertyChanged(nameof(SelectedFactoryDateText));
                    OnPropertyChanged(nameof(SelectedInitialRemarkText));
                    OnPropertyChanged(nameof(HasRegisterLock));
                    OnPropertyChanged(nameof(RegisterLockBusinessTypeText));
                    OnPropertyChanged(nameof(RegisterLockBusinessNoText));
                    OnPropertyChanged(nameof(RegisterLockBusinessRecordIdText));
                    OnPropertyChanged(nameof(RegisterLockPreviousStatusText));
                    OnPropertyChanged(nameof(RegisterLockLockedTimeText));

                    // 左侧切换选中项时，同步刷新右下历史流转记录
                    ApplySelectedMediumTransactions();
                }
            }
        }

        // 初始登记信息（HardDiskMedium）
        public string SelectedDiskCodeText => EmptyAsPlaceholder(SelectedMedium?.DiskCode);
        public string SelectedSerialNumberText => EmptyAsPlaceholder(SelectedMedium?.SerialNumber);
        public string SelectedDiskTypeText => EmptyAsPlaceholder(SelectedMedium?.DiskType);
        public string SelectedBrandText => EmptyAsPlaceholder(SelectedMedium?.Brand);
        public string SelectedCapacityText => EmptyAsPlaceholder(SelectedMedium?.Capacity);
        public string SelectedInterfaceTypeText => EmptyAsPlaceholder(SelectedMedium?.InterfaceType);
        public string SelectedRegistrationMethodText => EmptyAsPlaceholder(SelectedMedium?.RegistrationMethod);
        public string SelectedRegisterPersonText => EmptyAsPlaceholder(SelectedMedium?.RegisterPerson);
        public string SelectedRegisterDateText => FormatDate(SelectedMedium?.RegisterDate);
        public string SelectedFactoryDateText => FormatDate(SelectedMedium?.FactoryDate);
        public string SelectedInitialRemarkText => EmptyAsPlaceholder(SelectedMedium?.Remark);

        public bool HasRegisterLock => SelectedMedium?.RegisterLock != null;
        public string RegisterLockBusinessTypeText => EmptyAsPlaceholder(
            HardDiskRegisterLockFilterSupport.GetDisplayLabel(SelectedMedium?.RegisterLock?.BusinessType));
        public string RegisterLockBusinessNoText => EmptyAsPlaceholder(SelectedMedium?.RegisterLock?.BusinessNo);
        public string RegisterLockBusinessRecordIdText => SelectedMedium?.RegisterLock?.BusinessRecordId?.ToString() ?? "(无)";
        public string RegisterLockPreviousStatusText => EmptyAsPlaceholder(SelectedMedium?.RegisterLock?.PreviousStatus);
        public string RegisterLockLockedTimeText => FormatDateTime(SelectedMedium?.RegisterLock?.LockedTime);

        public RelayCommand SearchCommand { get; }
        public RelayCommand RefreshCommand { get; }

        public async Task InitializeAsync(
            string? initialStatus = null,
            string? initialLockFilter = null,
            HardDiskLedgerQuickFilter quickFilter = HardDiskLedgerQuickFilter.None)
        {
            if (_isInitialized)
            {
                ApplyInitialFilters(initialStatus, initialLockFilter, quickFilter);
                await SearchAsync();
                return;
            }

            await LoadFilterOptionsAsync();
            ApplyInitialFilters(initialStatus, initialLockFilter, quickFilter);
            await SearchAsync();
            _isInitialized = true;
        }

        private void ApplyInitialFilters(
            string? initialStatus,
            string? initialLockFilter,
            HardDiskLedgerQuickFilter quickFilter)
        {
            _quickFilter = quickFilter;

            if (!string.IsNullOrWhiteSpace(initialStatus)
                && StatusOptions.Contains(initialStatus))
            {
                _selectedStatus = initialStatus.Trim();
                OnPropertyChanged(nameof(SelectedStatus));
            }

            if (!string.IsNullOrWhiteSpace(initialLockFilter)
                && RegisterLockFilterOptions.Contains(initialLockFilter))
            {
                _selectedRegisterLockFilter = initialLockFilter.Trim();
                OnPropertyChanged(nameof(SelectedRegisterLockFilter));
            }
        }

        private async Task LoadFilterOptionsAsync()
        {
            var statusOptions = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskLedger), nameof(HardDiskLedger.MediaStatus));
            var natureOptions = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskLedger), nameof(HardDiskLedger.MediaNature));

            StatusOptions.Clear();
            StatusOptions.Add("全部");
            foreach (var item in statusOptions)
            {
                StatusOptions.Add(item);
            }

            NatureOptions.Clear();
            NatureOptions.Add("全部");
            foreach (var item in natureOptions)
            {
                NatureOptions.Add(item);
            }

            RegisterLockFilterOptions.Clear();
            foreach (var item in HardDiskRegisterLockFilterSupport.FilterOptions)
            {
                RegisterLockFilterOptions.Add(item);
            }

            SelectedRegisterLockFilter = HardDiskRegisterLockFilterSupport.All;
        }

        private async Task SearchAsync()
        {
            try
            {
                int? selectedId = SelectedMedium?.Id;
                string? status = SelectedStatus == "全部" || _quickFilter != HardDiskLedgerQuickFilter.None
                    ? null
                    : SelectedStatus;
                string? nature = SelectedNature == "全部" ? null : SelectedNature;

                var items = HardDiskRegisterLockFilterSupport.ApplyFilter(
                    await _hardDiskMediaService.SearchMediaAsync(SearchKeyword, status, nature),
                    SelectedRegisterLockFilter);

                items = ApplyQuickFilter(items, _quickFilter);

                MediaItems.Clear();
                foreach (var item in items)
                {
                    MediaItems.Add(item);
                }

                await LoadTransactionCacheAsync();

                SelectedMedium = selectedId.HasValue
                    ? MediaItems.FirstOrDefault(item => item.Id == selectedId.Value)
                    : MediaItems.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载硬盘台账失败：{ex.Message}");
            }
        }

        private static IEnumerable<HardDiskMedium> ApplyQuickFilter(
            IEnumerable<HardDiskMedium> items,
            HardDiskLedgerQuickFilter quickFilter)
        {
            return quickFilter switch
            {
                HardDiskLedgerQuickFilter.BorrowedTempOrLong => items.Where(item =>
                    item.Ledger != null
                    && (item.Ledger.MediaStatus == HardDiskMedium.StatusOutTemporary
                        || item.Ledger.MediaStatus == HardDiskMedium.StatusOutLongTerm)),
                HardDiskLedgerQuickFilter.NeedReturn => items.Where(item => item.Ledger?.NeedReturn == true),
                HardDiskLedgerQuickFilter.MissingLocationInStock => items.Where(item =>
                    item.Ledger != null
                    && IsLocatableInStockStatus(item.Ledger.MediaStatus)
                    && string.IsNullOrWhiteSpace(item.Ledger.StorageLocation)),
                HardDiskLedgerQuickFilter.MissingLedger => items.Where(item => item.Ledger == null),
                HardDiskLedgerQuickFilter.OutboundWithoutKeeper => items.Where(item =>
                    item.Ledger != null
                    && IsActiveOutboundStatus(item.Ledger.MediaStatus)
                    && string.IsNullOrWhiteSpace(item.Ledger.HolderOrOrganization)),
                _ => items
            };
        }

        private static bool IsLocatableInStockStatus(string? status)
        {
            return status == HardDiskMedium.StatusInStockBlank
                || status == HardDiskMedium.StatusInStockData
                || status == HardDiskMedium.StatusInStockDamaged;
        }

        private static bool IsActiveOutboundStatus(string? status)
        {
            return status == HardDiskMedium.StatusOutTemporary
                || status == HardDiskMedium.StatusOutLongTerm
                || status == HardDiskMedium.StatusOutPermanent
                || status == HardDiskMedium.StatusOutLost;
        }

        private async Task LoadTransactionCacheAsync()
        {
            _transactionCache.Clear();

            var transactions = await _hardDiskMediaService.SearchTransactionsAsync(null, null);
            _transactionCache.AddRange(transactions);

            ApplySelectedMediumTransactions();
        }

        private void ApplySelectedMediumTransactions()
        {
            SelectedMediumTransactions.Clear();

            if (SelectedMedium == null)
            {
                return;
            }

            var rows = _transactionCache
                .Where(item => item.MediumId == SelectedMedium.Id)
                .OrderByDescending(item => item.OperateTime)
                .ThenByDescending(item => item.Id);

            foreach (var item in rows)
            {
                SelectedMediumTransactions.Add(item);
            }
        }

        private async Task RefreshAsync()
        {
            SearchKeyword = string.Empty;
            _quickFilter = HardDiskLedgerQuickFilter.None;
            SelectedStatus = "全部";
            SelectedNature = "全部";
            SelectedRegisterLockFilter = HardDiskRegisterLockFilterSupport.All;
            await SearchAsync();
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "(无)";
        }

        private static string FormatDateTime(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm:ss") : "(无)";
        }

        private static string EmptyAsPlaceholder(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(无)" : value.Trim();
        }

        private async void RefreshByFilterChange()
        {
            try
            {
                await SearchAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"刷新硬盘台账失败：{ex.Message}");
            }
        }
    }
}