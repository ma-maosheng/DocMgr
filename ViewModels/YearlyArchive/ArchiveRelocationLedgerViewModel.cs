using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 迁档台账页面 ViewModel。
    /// </summary>
    public sealed class ArchiveRelocationLedgerViewModel : ViewModelBase
    {
        private readonly IArchiveRelocationLedgerService _ledgerService;
        private readonly IDialogService _dialogService;

        private DateTime? _operatedFrom;
        private DateTime? _operatedTo;
        private string _selectedRelocationMode = string.Empty;
        private string _selectedMediaKind = string.Empty;
        private string _businessNo = string.Empty;
        private string _operatorName = string.Empty;
        private string _keyword = string.Empty;
        private MaterialTransactionLedgerRow? _selectedRow;
        private string _summaryText = "共 0 条";
        private bool _isInitialized;

        public ArchiveRelocationLedgerViewModel(
            IArchiveRelocationLedgerService ledgerService,
            IDialogService dialogService)
        {
            _ledgerService = ledgerService;
            _dialogService = dialogService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            ResetCommand = new RelayCommand(_ => ResetCriteria());
            NavigateToFilingLedgerCommand = new RelayCommand(
                _ => NavigateToFilingLedger(),
                _ => SelectedRow != null && SelectedRow.FilingFactId > 0);
        }

        public event Action<int>? NavigateToFilingLedgerRequested;

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

        public ObservableCollection<FilterOption> RelocationModeOptions { get; } =
        [
            new FilterOption { Label = "全部模式", Value = string.Empty },
            new FilterOption { Label = "物理位置迁移", Value = ArchiveRelocationMode.PhysicalMove },
            new FilterOption { Label = "迁入空盘/空袋", Value = ArchiveRelocationMode.MoveToEmpty },
            new FilterOption { Label = "并入已有容器", Value = ArchiveRelocationMode.MergeToExisting },
            new FilterOption { Label = "档口批量搬迁", Value = ArchiveRelocationMode.BatchPhysicalMove }
        ];

        public string SelectedRelocationMode
        {
            get => _selectedRelocationMode;
            set => SetProperty(ref _selectedRelocationMode, value);
        }

        public ObservableCollection<FilterOption> MediaKindOptions { get; } =
        [
            new FilterOption { Label = "全部介质", Value = string.Empty },
            new FilterOption { Label = ArchiveRegisterDomainValues.MediaKindSimulated, Value = ArchiveRegisterDomainValues.MediaKindSimulated },
            new FilterOption { Label = ArchiveRegisterDomainValues.MediaKindElectronic, Value = ArchiveRegisterDomainValues.MediaKindElectronic }
        ];

        public string SelectedMediaKind
        {
            get => _selectedMediaKind;
            set => SetProperty(ref _selectedMediaKind, value);
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

        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        public ObservableCollection<MaterialTransactionLedgerRow> LedgerRows { get; } = new();

        public MaterialTransactionLedgerRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetProperty(ref _selectedRow, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string SummaryText
        {
            get => _summaryText;
            private set => SetProperty(ref _summaryText, value);
        }

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
                int? selectedId = SelectedRow?.FilingFactId;
                var rows = await _ledgerService.SearchAsync(BuildCriteria());

                LedgerRows.Clear();
                foreach (var row in rows)
                {
                    LedgerRows.Add(row);
                }

                SelectedRow = selectedId.HasValue
                    ? LedgerRows.FirstOrDefault(row => row.FilingFactId == selectedId.Value)
                    : LedgerRows.FirstOrDefault();

                SummaryText = $"共 {rows.Count} 条迁档流水";
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"查询迁档台账失败：{ex.Message}");
            }
        }

        private RelocationLedgerSearchCriteria BuildCriteria() => new()
        {
            OperatedFrom = OperatedFrom,
            OperatedTo = OperatedTo,
            RelocationMode = SelectedRelocationMode,
            MediaKind = SelectedMediaKind,
            BusinessNo = BusinessNo,
            OperatorName = OperatorName,
            Keyword = Keyword
        };

        private void ResetCriteria()
        {
            OperatedFrom = null;
            OperatedTo = null;
            SelectedRelocationMode = string.Empty;
            SelectedMediaKind = string.Empty;
            BusinessNo = string.Empty;
            OperatorName = string.Empty;
            Keyword = string.Empty;
            LedgerRows.Clear();
            SelectedRow = null;
            SummaryText = "共 0 条";
            CommandManager.InvalidateRequerySuggested();
        }

        private void NavigateToFilingLedger()
        {
            if (SelectedRow == null || SelectedRow.FilingFactId <= 0)
            {
                return;
            }

            NavigateToFilingLedgerRequested?.Invoke(SelectedRow.FilingFactId);
        }

        public sealed class FilterOption
        {
            public string Label { get; init; } = string.Empty;

            public string Value { get; init; } = string.Empty;
        }
    }
}
