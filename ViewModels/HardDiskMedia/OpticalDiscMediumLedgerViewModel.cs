using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 光盘流转台账列表 ViewModel。
    /// </summary>
    public class OpticalDiscMediumLedgerViewModel : ViewModelBase
    {
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IDialogService _dialogService;
        private bool _isInitialized;
        private string _searchKeyword = string.Empty;
        private string _transactionDiscCodeKeyword = string.Empty;
        private string _transactionBusinessNoKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private OpticalDiscMedium? _selectedMedium;
        private OpticalDiscMediumTransactionRecord? _selectedTransaction;

        public OpticalDiscMediumLedgerViewModel(IHardDiskMediaService hardDiskMediaService, IDialogService dialogService)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            ExportCommand = new RelayCommand(async _ => await ExportAsync());
        }

        public ObservableCollection<OpticalDiscMedium> MediaItems { get; } = new();
        public ObservableCollection<string> StatusOptions { get; } = new();
        public ObservableCollection<OpticalDiscMediumTransactionRecord> Transactions { get; } = new();

        public string SearchKeyword
        {
            get => _searchKeyword;
            set => SetProperty(ref _searchKeyword, value);
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set => SetProperty(ref _selectedStatus, value);
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

        public OpticalDiscMedium? SelectedMedium
        {
            get => _selectedMedium;
            set => SetProperty(ref _selectedMedium, value);
        }

        public OpticalDiscMediumTransactionRecord? SelectedTransaction
        {
            get => _selectedTransaction;
            set => SetProperty(ref _selectedTransaction, value);
        }

        public RelayCommand SearchCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ExportCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            LoadOptions();
            await SearchAsync();
            await LoadTransactionsAsync();
            _isInitialized = true;
        }

        private void LoadOptions()
        {
            StatusOptions.Clear();
            StatusOptions.Add("全部");
            StatusOptions.Add(OpticalDiscMedium.StatusInStock);
            StatusOptions.Add(OpticalDiscMedium.StatusOut);
            StatusOptions.Add(OpticalDiscMedium.StatusDamaged);
            StatusOptions.Add(OpticalDiscMedium.StatusDestroyed);
        }

        private async Task SearchAsync()
        {
            try
            {
                int? selectedId = SelectedMedium?.Id;
                string? status = SelectedStatus == "全部" ? null : SelectedStatus;
                var items = await _hardDiskMediaService.SearchOpticalDiscMediaAsync(SearchKeyword, status);

                MediaItems.Clear();
                foreach (var item in items)
                {
                    MediaItems.Add(item);
                }

                SelectedMedium = selectedId.HasValue
                    ? MediaItems.FirstOrDefault(item => item.Id == selectedId.Value)
                    : MediaItems.FirstOrDefault();

                await LoadTransactionsAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载光盘流转台账失败：{ex.Message}");
            }
        }

        private async Task RefreshAsync()
        {
            SearchKeyword = string.Empty;
            TransactionDiscCodeKeyword = string.Empty;
            TransactionBusinessNoKeyword = string.Empty;
            SelectedStatus = "全部";
            await SearchAsync();
        }

        public async Task SearchTransactionsAsync()
        {
            await LoadTransactionsAsync();
        }

        private async Task LoadTransactionsAsync()
        {
            try
            {
                int? selectedId = SelectedTransaction == null ? null : SelectedTransaction.OperateTime.GetHashCode();
                var records = await _hardDiskMediaService.SearchOpticalDiscTransactionsAsync(TransactionDiscCodeKeyword, TransactionBusinessNoKeyword);
                Transactions.Clear();
                foreach (var record in records)
                {
                    Transactions.Add(record);
                }

                SelectedTransaction = selectedId.HasValue
                    ? Transactions.FirstOrDefault(item => item.OperateTime.GetHashCode() == selectedId.Value)
                    : Transactions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载光盘流转台账失败：{ex.Message}");
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
    }
}
