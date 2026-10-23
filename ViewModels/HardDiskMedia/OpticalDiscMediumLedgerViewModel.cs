using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.Shared;
using DocMgr.Services.Cabinets;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 数据光盘台账列表 ViewModel。
    /// </summary>
    public class OpticalDiscMediumLedgerViewModel : ViewModelBase
    {
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly PageDbOperationQueue _pageDb = new();
        private readonly IDialogService _dialogService;
        private readonly LedgerStoredMaterialOpener _materialOpener;
        private int _linkedBagUnitId;
        private bool _isInitialized;
        private bool _suppressSelectionReload;
        private readonly SemaphoreSlim _transactionLoadGate = new(1, 1);
        private int _transactionLoadGeneration;
        private string _searchKeyword = string.Empty;
        private string _transactionDiscCodeKeyword = string.Empty;
        private string _transactionBusinessNoKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private string _selectedTransactionType = "全部";
        private bool _followSelectedMedium = true;
        private OpticalDiscLedgerQuickFilter _quickFilter = OpticalDiscLedgerQuickFilter.None;
        private bool _recentTransactionsOnly;
        private OpticalDiscMedium? _selectedMedium;
        private OpticalDiscMediumTransactionRecord? _selectedTransaction;

        public OpticalDiscMediumLedgerViewModel(
            IHardDiskMediaService hardDiskMediaService,
            IDialogService dialogService,
            LedgerStoredMaterialOpener materialOpener)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;
            _materialOpener = materialOpener;

            SearchCommand = new RelayCommand(async _ => await _pageDb.RunAsync(SearchAsync));
            RefreshCommand = new RelayCommand(async _ => await _pageDb.RunAsync(RefreshAsync));
            ExportCommand = new RelayCommand(async _ => await _pageDb.RunAsync(ExportAsync));
            SearchTransactionsCommand = new RelayCommand(async _ => await _pageDb.RunAsync(SearchTransactionsAsync));
            ShowAllTransactionsCommand = new RelayCommand(async _ => await _pageDb.RunAsync(ShowAllTransactionsAsync));
            ShowIntroCommand = new RelayCommand(_ => _dialogService.ShowLedgerIntro(LedgerIntroKind.OpticalDisc));
            ViewStoredMaterialCommand = new RelayCommand(
                _ => ViewStoredMaterial(),
                _ => _linkedBagUnitId > 0);
        }

        public RelayCommand ViewStoredMaterialCommand { get; }

        public ObservableCollection<OpticalDiscMedium> MediaItems { get; } = new();
        public ObservableCollection<string> StatusOptions { get; } = new();
        public ObservableCollection<string> TransactionTypeOptions { get; } = new();
        public ObservableCollection<OpticalDiscMediumTransactionRecord> Transactions { get; } = new();

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
                    _quickFilter = OpticalDiscLedgerQuickFilter.None;
                }
            }
        }

        public string TransactionDiscCodeKeyword
        {
            get => _transactionDiscCodeKeyword;
            set => SetProperty(ref _transactionDiscCodeKeyword, value);
        }

        public string TransactionBusinessNoKeyword
        {
            get => _transactionBusinessNoKeyword;
            set => SetProperty(ref _transactionBusinessNoKeyword, value);
        }

        public string SelectedTransactionType
        {
            get => _selectedTransactionType;
            set
            {
                if (!SetProperty(ref _selectedTransactionType, value) || !_isInitialized || _suppressSelectionReload)
                {
                    return;
                }

                _recentTransactionsOnly = false;
                _pageDb.RunInBackground(LoadTransactionsAsync);
            }
        }

        public bool FollowSelectedMedium
        {
            get => _followSelectedMedium;
            set
            {
                if (!SetProperty(ref _followSelectedMedium, value))
                {
                    return;
                }

                if (!_isInitialized || _suppressSelectionReload)
                {
                    OnPropertyChanged(nameof(TransactionScopeHint));
                    return;
                }

                if (value && SelectedMedium != null)
                {
                    TransactionDiscCodeKeyword = SelectedMedium.DiscCode;
                }

                _pageDb.RunInBackground(LoadTransactionsAsync);
            }
        }

        public OpticalDiscMedium? SelectedMedium
        {
            get => _selectedMedium;
            set
            {
                if (!SetProperty(ref _selectedMedium, value))
                {
                    return;
                }

                NotifySelectedRegistrationDisplay();

                if (_suppressSelectionReload)
                {
                    return;
                }

                _linkedBagUnitId = 0;
                CommandManager.InvalidateRequerySuggested();

                if (_followSelectedMedium && value != null)
                {
                    TransactionDiscCodeKeyword = value.DiscCode;
                }

                _pageDb.RunInBackground(LoadTransactionsAsync);
            }
        }

        /// <summary>初始登记信息（OpticalDiscMedium）。</summary>
        public string SelectedDiscCodeText => EmptyAsPlaceholder(SelectedMedium?.DiscCode);

        public string SelectedDiscTypeText => EmptyAsPlaceholder(SelectedMedium?.DiscType);

        public string SelectedCapacityText => EmptyAsPlaceholder(SelectedMedium?.Capacity);

        public string SelectedRegistrationMethodText => EmptyAsPlaceholder(SelectedMedium?.RegistrationMethod);

        public string SelectedRegisterPersonText => EmptyAsPlaceholder(SelectedMedium?.RegisterPerson);

        public string SelectedRegisterDateText => FormatDate(SelectedMedium?.RegisterDate);

        public string SelectedSourceTypeText => EmptyAsPlaceholder(SelectedMedium?.SourceType);

        public string SelectedSourceRecordKeyText => EmptyAsPlaceholder(SelectedMedium?.SourceRecordKey);

        public string SelectedInitialRemarkText => EmptyAsPlaceholder(SelectedMedium?.Remarks);

        public OpticalDiscMediumTransactionRecord? SelectedTransaction
        {
            get => _selectedTransaction;
            set => SetProperty(ref _selectedTransaction, value);
        }

        public string TransactionScopeHint
        {
            get
            {
                if (_recentTransactionsOnly)
                {
                    return "当前显示：近90天流转";
                }

                return FollowSelectedMedium && SelectedMedium != null
                    ? $"当前聚焦：{SelectedMedium.DiscCode}"
                    : "当前显示：全部匹配流转";
            }
        }

        public RelayCommand SearchCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand ShowIntroCommand { get; }
        public RelayCommand SearchTransactionsCommand { get; }
        public RelayCommand ShowAllTransactionsCommand { get; }

        public async Task InitializeAsync(
            string? initialStatus = null,
            OpticalDiscLedgerQuickFilter quickFilter = OpticalDiscLedgerQuickFilter.None,
            bool recentTransactionsOnly = false)
        {
            if (_isInitialized)
            {
                ApplyInitialFilters(initialStatus, quickFilter, recentTransactionsOnly);
                await SearchAsync();
                return;
            }

            LoadOptions();
            ApplyInitialFilters(initialStatus, quickFilter, recentTransactionsOnly);
            await SearchAsync();
            _isInitialized = true;
        }

        private void ApplyInitialFilters(
            string? initialStatus,
            OpticalDiscLedgerQuickFilter quickFilter,
            bool recentTransactionsOnly)
        {
            _quickFilter = quickFilter;
            _recentTransactionsOnly = recentTransactionsOnly;

            if (!string.IsNullOrWhiteSpace(initialStatus)
                && StatusOptions.Contains(initialStatus))
            {
                _selectedStatus = initialStatus.Trim();
                OnPropertyChanged(nameof(SelectedStatus));
            }

            if (recentTransactionsOnly)
            {
                _followSelectedMedium = false;
                OnPropertyChanged(nameof(FollowSelectedMedium));
            }

            OnPropertyChanged(nameof(TransactionScopeHint));
        }

        private void LoadOptions()
        {
            StatusOptions.Clear();
            StatusOptions.Add("全部");
            StatusOptions.Add(OpticalDiscMedium.StatusInStock);
            StatusOptions.Add(OpticalDiscMedium.StatusOut);
            StatusOptions.Add(OpticalDiscMedium.StatusDamaged);
            StatusOptions.Add(OpticalDiscMedium.StatusLost);
            StatusOptions.Add(OpticalDiscMedium.StatusScrap);
            StatusOptions.Add(OpticalDiscMedium.StatusDestroyed);

            TransactionTypeOptions.Clear();
            TransactionTypeOptions.Add("全部");
            TransactionTypeOptions.Add(OpticalDiscMediaTransaction.TypeArchiveInbound);
            TransactionTypeOptions.Add(OpticalDiscMediaTransaction.TypeOutboundTemporary);
            TransactionTypeOptions.Add(OpticalDiscMediaTransaction.TypeReturnRegistration);
            TransactionTypeOptions.Add(OpticalDiscMediaTransaction.TypeDamagedRegistration);
            TransactionTypeOptions.Add(OpticalDiscMediaTransaction.TypeInventoryRegisterDamage);
            TransactionTypeOptions.Add(OpticalDiscMediaTransaction.TypeInventoryRegisterLost);
            TransactionTypeOptions.Add(OpticalDiscMediaTransaction.TypeInventoryRegisterScrap);
            TransactionTypeOptions.Add(OpticalDiscMediaTransaction.TypeDestroy);
            TransactionTypeOptions.Add(OpticalDiscMediaTransaction.TypeRelocate);
            TransactionTypeOptions.Add(OpticalDiscMediaTransaction.TypeRegister);
        }

        private async Task SearchAsync()
        {
            try
            {
                int? selectedId = SelectedMedium?.Id;
                string? status = SelectedStatus == "全部" || _quickFilter != OpticalDiscLedgerQuickFilter.None
                    ? null
                    : SelectedStatus;
                var items = ApplyQuickFilter(
                    await _hardDiskMediaService.SearchOpticalDiscMediaAsync(SearchKeyword, status),
                    _quickFilter);

                _suppressSelectionReload = true;
                try
                {
                    MediaItems.Clear();
                    foreach (var item in items)
                    {
                        MediaItems.Add(item);
                    }

                    SelectedMedium = selectedId.HasValue
                        ? MediaItems.FirstOrDefault(item => item.Id == selectedId.Value)
                        : MediaItems.FirstOrDefault();
                }
                finally
                {
                    _suppressSelectionReload = false;
                }

                if (FollowSelectedMedium && SelectedMedium != null)
                {
                    TransactionDiscCodeKeyword = SelectedMedium.DiscCode;
                }

                await LoadTransactionsAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载光盘台账失败：{ex.Message}");
            }
        }

        private static IEnumerable<OpticalDiscMedium> ApplyQuickFilter(
            IEnumerable<OpticalDiscMedium> items,
            OpticalDiscLedgerQuickFilter quickFilter)
        {
            return quickFilter switch
            {
                OpticalDiscLedgerQuickFilter.NeedReturn => items.Where(item => item.Ledger?.NeedReturn == true),
                OpticalDiscLedgerQuickFilter.MissingLocation => items.Where(item =>
                    item.Ledger != null && string.IsNullOrWhiteSpace(item.Ledger.StorageLocation)),
                OpticalDiscLedgerQuickFilter.OutboundWithoutKeeper => items.Where(item =>
                    item.Ledger != null
                    && IsOutboundStatus(item.Ledger.MediaStatus)
                    && string.IsNullOrWhiteSpace(item.Ledger.HolderOrOrganization)),
                _ => items
            };
        }

        private static bool IsOutboundStatus(string? status)
        {
            return string.Equals(status, OpticalDiscMedium.StatusOut, StringComparison.Ordinal)
                || string.Equals(status, OpticalDiscMedium.StatusDestroyed, StringComparison.Ordinal);
        }

        private async Task RefreshAsync()
        {
            _suppressSelectionReload = true;
            try
            {
                SearchKeyword = string.Empty;
                TransactionDiscCodeKeyword = string.Empty;
                TransactionBusinessNoKeyword = string.Empty;
                _quickFilter = OpticalDiscLedgerQuickFilter.None;
                _recentTransactionsOnly = false;
                SelectedStatus = "全部";
                SelectedTransactionType = "全部";
                FollowSelectedMedium = true;
            }
            finally
            {
                _suppressSelectionReload = false;
            }

            await SearchAsync();
        }

        public async Task SearchTransactionsAsync()
        {
            _suppressSelectionReload = true;
            try
            {
                FollowSelectedMedium = false;
                _recentTransactionsOnly = false;
            }
            finally
            {
                _suppressSelectionReload = false;
            }

            await LoadTransactionsAsync();
        }

        private async Task ShowAllTransactionsAsync()
        {
            _suppressSelectionReload = true;
            try
            {
                FollowSelectedMedium = false;
                _recentTransactionsOnly = false;
                TransactionDiscCodeKeyword = string.Empty;
                TransactionBusinessNoKeyword = string.Empty;
                SelectedTransactionType = "全部";
            }
            finally
            {
                _suppressSelectionReload = false;
            }

            await LoadTransactionsAsync();
        }

        private void RefreshLinkedBag()
        {
            int mediumId = _selectedMedium?.Id ?? 0;
            _linkedBagUnitId = mediumId > 0
                ? _materialOpener.FindInUseElectronicUnitIdByOpticalDisc(mediumId) ?? 0
                : 0;
            CommandManager.InvalidateRequerySuggested();
        }

        private void ViewStoredMaterial()
        {
            int mediumId = SelectedMedium?.Id ?? 0;
            if (mediumId <= 0)
            {
                return;
            }

            _materialOpener.OpenElectronicBagByOpticalDisc(mediumId);
        }

        private async Task LoadTransactionsAsync()
        {
            int generation = Interlocked.Increment(ref _transactionLoadGeneration);
            await _transactionLoadGate.WaitAsync().ConfigureAwait(true);
            try
            {
                if (generation != Volatile.Read(ref _transactionLoadGeneration))
                {
                    return;
                }

                RefreshLinkedBag();

                int? selectedTxnId = SelectedTransaction?.Id;
                int? mediumId = FollowSelectedMedium ? SelectedMedium?.Id : null;
                string? discCodeKeyword = string.IsNullOrWhiteSpace(TransactionDiscCodeKeyword)
                    ? null
                    : TransactionDiscCodeKeyword;
                string? businessNoKeyword = string.IsNullOrWhiteSpace(TransactionBusinessNoKeyword)
                    ? null
                    : TransactionBusinessNoKeyword;
                string? transactionType = SelectedTransactionType == "全部" ? null : SelectedTransactionType;

                // 聚焦选中介质时以 MediumId 为准，避免编号关键词误伤其它盘。
                if (mediumId.HasValue)
                {
                    discCodeKeyword = null;
                }

                IReadOnlyList<OpticalDiscMediumTransactionRecord> records =
                    await _hardDiskMediaService.SearchOpticalDiscTransactionsAsync(
                        discCodeKeyword,
                        businessNoKeyword,
                        mediumId,
                        transactionType).ConfigureAwait(true);

                if (generation != Volatile.Read(ref _transactionLoadGeneration))
                {
                    return;
                }

                if (_recentTransactionsOnly)
                {
                    DateTime cutoff = DateTime.Now.AddDays(-90);
                    records = records
                        .Where(item => item.OperateTime >= cutoff)
                        .ToList();
                }

                Transactions.Clear();
                foreach (var record in records)
                {
                    Transactions.Add(record);
                }

                SelectedTransaction = selectedTxnId.HasValue
                    ? Transactions.FirstOrDefault(item => item.Id == selectedTxnId.Value)
                    : Transactions.FirstOrDefault();

                OnPropertyChanged(nameof(TransactionScopeHint));
            }
            catch (Exception ex)
            {
                if (generation == Volatile.Read(ref _transactionLoadGeneration))
                {
                    _dialogService.ShowError($"加载光盘台账失败：{ex.GetBaseException().Message}");
                }
            }
            finally
            {
                _transactionLoadGate.Release();
            }
        }

        private async Task ExportAsync()
        {
            string defaultFileName = $"光盘台账_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            string? filePath = _dialogService.SaveFileDialog("Excel Files|*.xlsx", "导出光盘台账", defaultFileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _dialogService.SetBusyState(true);
            try
            {
                await _hardDiskMediaService.ExportOpticalDiscMediaLedgerAsync(filePath);
                _dialogService.ShowMessage($"光盘台账导出完成：\n{filePath}", "完成");
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _dialogService.ShowError($"没有权限写入目标文件：{ex.Message}");
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"写入导出文件失败：{ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private void NotifySelectedRegistrationDisplay()
        {
            OnPropertyChanged(nameof(SelectedDiscCodeText));
            OnPropertyChanged(nameof(SelectedDiscTypeText));
            OnPropertyChanged(nameof(SelectedCapacityText));
            OnPropertyChanged(nameof(SelectedRegistrationMethodText));
            OnPropertyChanged(nameof(SelectedRegisterPersonText));
            OnPropertyChanged(nameof(SelectedRegisterDateText));
            OnPropertyChanged(nameof(SelectedSourceTypeText));
            OnPropertyChanged(nameof(SelectedSourceRecordKeyText));
            OnPropertyChanged(nameof(SelectedInitialRemarkText));
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "(无)";
        }

        private static string EmptyAsPlaceholder(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(无)" : value.Trim();
        }
    }
}
