using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;
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
        private readonly IHardDiskDisposalService _hardDiskDisposalService;
        private readonly IHardDiskInventoryRegisterService _hardDiskInventoryRegisterService;
        private readonly IArchiveOutboundService _archiveOutboundService;
        private readonly IArchiveDisposalService _archiveDisposalService;
        private readonly IArchiveInventoryRegisterService _archiveInventoryRegisterService;
        private readonly INetworkTransferService _networkTransferService;
        private readonly IDialogService _dialogService;
        private readonly List<HardDiskMediaTransaction> _transactionCache = new();

        private bool _isInitialized;
        private string _searchKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private string _selectedNature = "全部";
        private string _selectedRegisterLockFilter = HardDiskRegisterLockFilterSupport.All;
        private HardDiskLedgerQuickFilter _quickFilter = HardDiskLedgerQuickFilter.None;
        private HardDiskMedium? _selectedMedium;
        private readonly SemaphoreSlim _searchGate = new(1, 1);
        private int _searchGeneration;

        public HardDiskMediaTransactionPageViewModel(
            IHardDiskMediaService hardDiskMediaService,
            IHardDiskDisposalService hardDiskDisposalService,
            IHardDiskInventoryRegisterService hardDiskInventoryRegisterService,
            IArchiveOutboundService archiveOutboundService,
            IArchiveDisposalService archiveDisposalService,
            IArchiveInventoryRegisterService archiveInventoryRegisterService,
            INetworkTransferService networkTransferService,
            IDialogService dialogService)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _hardDiskDisposalService = hardDiskDisposalService;
            _hardDiskInventoryRegisterService = hardDiskInventoryRegisterService;
            _archiveOutboundService = archiveOutboundService;
            _archiveDisposalService = archiveDisposalService;
            _archiveInventoryRegisterService = archiveInventoryRegisterService;
            _networkTransferService = networkTransferService;
            _dialogService = dialogService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            ShowIntroCommand = new RelayCommand(_ => _dialogService.ShowLedgerIntro(LedgerIntroKind.HardDisk));
            // 无真实业务单号时仍可点击，以便提示不可查看的原因。
            ViewBusinessDetailCommand = new RelayCommand(
                async param => await ViewBusinessDetailAsync(param as HardDiskMediaTransaction),
                param => param is HardDiskMediaTransaction);
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
        public RelayCommand ShowIntroCommand { get; }
        public RelayCommand ViewBusinessDetailCommand { get; }

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
            int generation = Interlocked.Increment(ref _searchGeneration);
            await _searchGate.WaitAsync().ConfigureAwait(true);
            try
            {
                if (generation != Volatile.Read(ref _searchGeneration))
                {
                    return;
                }

                int? selectedId = SelectedMedium?.Id;
                string? status = SelectedStatus == "全部" || _quickFilter != HardDiskLedgerQuickFilter.None
                    ? null
                    : SelectedStatus;
                string? nature = SelectedNature == "全部" ? null : SelectedNature;

                var items = HardDiskRegisterLockFilterSupport.ApplyFilter(
                    await _hardDiskMediaService.SearchMediaAsync(SearchKeyword, status, nature)
                        .ConfigureAwait(true),
                    SelectedRegisterLockFilter);

                items = ApplyQuickFilter(items, _quickFilter);

                if (generation != Volatile.Read(ref _searchGeneration))
                {
                    return;
                }

                MediaItems.Clear();
                foreach (var item in items)
                {
                    MediaItems.Add(item);
                }

                await LoadTransactionCacheAsync().ConfigureAwait(true);

                if (generation != Volatile.Read(ref _searchGeneration))
                {
                    return;
                }

                SelectedMedium = selectedId.HasValue
                    ? MediaItems.FirstOrDefault(item => item.Id == selectedId.Value)
                    : MediaItems.FirstOrDefault();
            }
            catch (Exception ex)
            {
                if (generation == Volatile.Read(ref _searchGeneration))
                {
                    _dialogService.ShowError($"加载硬盘台账失败：{ex.GetBaseException().Message}");
                }
            }
            finally
            {
                _searchGate.Release();
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

        private async Task ViewBusinessDetailAsync(HardDiskMediaTransaction? transaction)
        {
            if (transaction == null)
            {
                return;
            }

            string? businessNo = transaction.ResolveViewableBusinessNo();
            if (string.IsNullOrWhiteSpace(businessNo))
            {
                _dialogService.ShowMessage(BuildNoViewableBusinessNoMessage(transaction));
                return;
            }

            try
            {
                if (transaction.Application != null)
                {
                    _dialogService.ShowHardDiskMediaApplicationViewDialog(transaction.Application);
                    return;
                }

                if (await TryOpenHardDiskApplicationAsync(businessNo))
                {
                    return;
                }

                if (businessNo.StartsWith("盘离-申", StringComparison.Ordinal)
                    || string.Equals(transaction.TransactionType, HardDiskMediaTransaction.TypeDisposal, StringComparison.Ordinal))
                {
                    if (await TryOpenHardDiskDisposalAsync(businessNo))
                    {
                        return;
                    }
                }

                if (businessNo.StartsWith("盘库-登", StringComparison.Ordinal)
                    || IsHardDiskInventoryTransactionType(transaction.TransactionType))
                {
                    if (await TryOpenHardDiskInventoryAsync(businessNo))
                    {
                        return;
                    }
                }

                if (businessNo.StartsWith("资-出-申", StringComparison.Ordinal))
                {
                    var outbound = await _archiveOutboundService.GetRecordByOutboundNoAsync(businessNo);
                    if (outbound != null)
                    {
                        _dialogService.ShowArchiveOutboundApplicationViewDialog(outbound);
                        return;
                    }
                }

                if (businessNo.StartsWith("资离-处", StringComparison.Ordinal)
                    && await TryOpenArchiveDisposalAsync(businessNo))
                {
                    return;
                }

                if (businessNo.StartsWith("资盘-登", StringComparison.Ordinal)
                    && await TryOpenArchiveInventoryAsync(businessNo))
                {
                    return;
                }

                if (businessNo.StartsWith("网-入-申", StringComparison.Ordinal)
                    && await TryOpenNetworkInboundAsync(businessNo))
                {
                    return;
                }

                if (businessNo.StartsWith("资-还-登", StringComparison.Ordinal))
                {
                    _dialogService.ShowMessage($"业务单号 [{businessNo}] 为资料归还登记，请到「资料归还」工作台查看。");
                    return;
                }

                if (await TryOpenHardDiskDisposalAsync(businessNo)
                    || await TryOpenHardDiskInventoryAsync(businessNo)
                    || await TryOpenArchiveDisposalAsync(businessNo)
                    || await TryOpenArchiveInventoryAsync(businessNo))
                {
                    return;
                }

                _dialogService.ShowMessage($"未找到业务单号 [{businessNo}] 对应的业务详情。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"打开业务详情失败：{ex.Message}");
            }
        }

        private static string BuildNoViewableBusinessNoMessage(HardDiskMediaTransaction transaction)
        {
            if (string.Equals(transaction.TransactionType, HardDiskMediaTransaction.TypeRelocate, StringComparison.Ordinal)
                && !transaction.ApplicationId.HasValue)
            {
                return "该流转由开柜交互式迁档或档口批量搬迁产生，无独立业务单号，无法查看业务详情。";
            }

            return "该流转记录没有关联业务单号，无法查看业务详情。";
        }

        private async Task<bool> TryOpenHardDiskApplicationAsync(string businessNo)
        {
            var applications = await _hardDiskMediaService.SearchApplicationsAsync(businessNo, null, null);
            var match = applications.FirstOrDefault(item =>
                string.Equals(item.ApplicationNo?.Trim(), businessNo, StringComparison.Ordinal));
            if (match == null)
            {
                return false;
            }

            _dialogService.ShowHardDiskMediaApplicationViewDialog(match);
            return true;
        }

        private async Task<bool> TryOpenHardDiskDisposalAsync(string businessNo)
        {
            var records = await _hardDiskDisposalService.SearchRecordsAsync(businessNo, null, null);
            var match = records.FirstOrDefault(item =>
                string.Equals(item.DisposalNo?.Trim(), businessNo, StringComparison.Ordinal));
            if (match == null)
            {
                return false;
            }

            var full = await _hardDiskDisposalService.GetRecordByIdAsync(match.Id) ?? match;
            _dialogService.ShowHardDiskDisposalEditDialog(full);
            return true;
        }

        private async Task<bool> TryOpenHardDiskInventoryAsync(string businessNo)
        {
            var records = await _hardDiskInventoryRegisterService.SearchRecordsAsync(businessNo, null, null);
            var match = records.FirstOrDefault(item =>
                string.Equals(item.RegisterNo?.Trim(), businessNo, StringComparison.Ordinal));
            if (match == null)
            {
                return false;
            }

            var full = await _hardDiskInventoryRegisterService.GetRecordByIdAsync(match.Id) ?? match;
            _dialogService.ShowHardDiskInventoryRegisterEditDialog(full);
            return true;
        }

        private async Task<bool> TryOpenArchiveDisposalAsync(string businessNo)
        {
            foreach (string mediaKind in new[]
                     {
                         ArchiveRegisterDomainValues.MediaKindElectronic,
                         ArchiveRegisterDomainValues.MediaKindSimulated
                     })
            {
                var records = await _archiveDisposalService.SearchRecordsAsync(businessNo, null, null, mediaKind);
                var match = records.FirstOrDefault(item =>
                    string.Equals(item.DisposalNo?.Trim(), businessNo, StringComparison.Ordinal));
                if (match == null)
                {
                    continue;
                }

                var full = await _archiveDisposalService.GetRecordByIdAsync(match.Id) ?? match;
                _dialogService.ShowArchiveDisposalEditDialog(full);
                return true;
            }

            return false;
        }

        private async Task<bool> TryOpenArchiveInventoryAsync(string businessNo)
        {
            var records = await _archiveInventoryRegisterService.SearchRecordsAsync(null, businessNo, null, null);
            var match = records.FirstOrDefault(item =>
                string.Equals(item.RegisterNo?.Trim(), businessNo, StringComparison.Ordinal));
            if (match == null)
            {
                return false;
            }

            var full = await _archiveInventoryRegisterService.GetRecordByIdAsync(match.Id) ?? match;
            if (string.Equals(full.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal))
            {
                _dialogService.ShowSimulatedArchiveInventoryRegisterEditDialog(full);
            }
            else
            {
                _dialogService.ShowElectronicArchiveInventoryRegisterEditDialog(full);
            }

            return true;
        }

        private async Task<bool> TryOpenNetworkInboundAsync(string businessNo)
        {
            var records = await _networkTransferService.SearchInboundRecordsAsync(businessNo, null, null);
            var match = records.FirstOrDefault(item =>
                string.Equals(item.InboundNo?.Trim(), businessNo, StringComparison.Ordinal));
            if (match == null)
            {
                return false;
            }

            var full = await _networkTransferService.GetInboundByIdAsync(match.Id) ?? match;
            _dialogService.ShowNetworkInboundEditDialog(full, NetworkTransferWorkspaceMode.Application);
            return true;
        }

        private static bool IsHardDiskInventoryTransactionType(string? transactionType)
        {
            return string.Equals(transactionType, HardDiskMediaTransaction.TypeInventoryRegisterDamage, StringComparison.Ordinal)
                || string.Equals(transactionType, HardDiskMediaTransaction.TypeInventoryRegisterLost, StringComparison.Ordinal)
                || string.Equals(transactionType, HardDiskMediaTransaction.TypeInventoryRegisterScrap, StringComparison.Ordinal)
                || string.Equals(transactionType, HardDiskMediaTransaction.TypeInventoryRegisterRelocate, StringComparison.Ordinal)
                || string.Equals(transactionType, HardDiskMediaTransaction.TypeInventoryLost, StringComparison.Ordinal);
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