using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 跨域流转台账页面 ViewModel。
    /// </summary>
    public sealed class ArchiveCrossDomainTransferLedgerViewModel : ViewModelBase
    {
        private const int BusinessNoOptionLimit = 50;
        private const int RecentBusinessNoChipLimit = 8;

        private readonly IArchiveCrossDomainTransferLedgerService _ledgerService;
        private readonly IDialogService _dialogService;

        private DateTime? _operatedFrom;
        private DateTime? _operatedTo;
        private string _selectedTransactionType = string.Empty;
        private string _selectedMediaKind = string.Empty;
        private string _businessNo = string.Empty;
        private string _operatorName = string.Empty;
        private string _keyword = string.Empty;
        private CrossDomainTransferLedgerRow? _selectedRow;
        private string _summaryText = "共 0 条";
        private string _filterScopeHint = "当前显示：全部跨域流转流水";
        private bool _isInitialized;

        public ArchiveCrossDomainTransferLedgerViewModel(
            IArchiveCrossDomainTransferLedgerService ledgerService,
            IDialogService dialogService)
        {
            _ledgerService = ledgerService;
            _dialogService = dialogService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            ResetCommand = new RelayCommand(_ => ResetCriteria());
            NavigateToFilingLedgerCommand = new RelayCommand(
                _ => NavigateToFilingLedger(),
                _ => SelectedRow != null && SelectedRow.FilingFactId > 0);
            UseSelectedRowBusinessNoCommand = new RelayCommand(
                async _ =>
                {
                    ApplyBusinessNoFromSelectedRow();
                    await SearchAsync();
                },
                _ => SelectedRow != null && !string.IsNullOrWhiteSpace(SelectedRow.BusinessNo));
            ClearBusinessNoCommand = new RelayCommand(
                _ => BusinessNo = string.Empty,
                _ => !string.IsNullOrWhiteSpace(BusinessNo));
            SelectRecentBusinessNoCommand = new RelayCommand(async value =>
            {
                if (value is string text && !string.IsNullOrWhiteSpace(text))
                {
                    BusinessNo = text.Trim();
                    await SearchAsync();
                }
            });
            ApplyRecentDaysCommand = new RelayCommand(async param =>
            {
                if (TryResolveRecentDayCount(param, out int days))
                {
                    OperatedTo = DateTime.Today;
                    OperatedFrom = DateTime.Today.AddDays(-days + 1);
                }
                else
                {
                    OperatedFrom = null;
                    OperatedTo = null;
                }

                UpdateFilterScopeHint();
                await SearchAsync();
            });
            ExportCommand = new RelayCommand(async _ => await ExportAsync(), _ => LedgerRows.Count > 0);
        }

        public event Action<int>? NavigateToFilingLedgerRequested;

        public DateTime? OperatedFrom
        {
            get => _operatedFrom;
            set
            {
                if (SetProperty(ref _operatedFrom, value))
                {
                    UpdateFilterScopeHint();
                }
            }
        }

        public DateTime? OperatedTo
        {
            get => _operatedTo;
            set
            {
                if (SetProperty(ref _operatedTo, value))
                {
                    UpdateFilterScopeHint();
                }
            }
        }

        public ObservableCollection<FilterOption> TransactionTypeOptions { get; } =
        [
            new FilterOption { Label = "全部类型", Value = string.Empty },
            new FilterOption
            {
                Label = MaterialTransactionDomainValues.MapTypeDisplay(
                    MaterialTransactionDomainValues.TypeNetworkInboundCopy),
                Value = MaterialTransactionDomainValues.TypeNetworkInboundCopy
            }
        ];

        public string SelectedTransactionType
        {
            get => _selectedTransactionType;
            set
            {
                if (SetProperty(ref _selectedTransactionType, value))
                {
                    UpdateFilterScopeHint();
                }
            }
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
            set
            {
                if (SetProperty(ref _selectedMediaKind, value))
                {
                    UpdateFilterScopeHint();
                }
            }
        }

        public string BusinessNo
        {
            get => _businessNo;
            set
            {
                if (SetProperty(ref _businessNo, value))
                {
                    UpdateFilterScopeHint();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
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

        public ObservableCollection<string> BusinessNoOptions { get; } = new();

        public ObservableCollection<string> RecentBusinessNoOptions { get; } = new();

        public ObservableCollection<CrossDomainTransferLedgerRow> LedgerRows { get; } = new();

        public CrossDomainTransferLedgerRow? SelectedRow
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

        public string FilterScopeHint
        {
            get => _filterScopeHint;
            private set => SetProperty(ref _filterScopeHint, value);
        }

        public RelayCommand SearchCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand NavigateToFilingLedgerCommand { get; }
        public RelayCommand UseSelectedRowBusinessNoCommand { get; }
        public RelayCommand ClearBusinessNoCommand { get; }
        public RelayCommand SelectRecentBusinessNoCommand { get; }
        public RelayCommand ApplyRecentDaysCommand { get; }

        public RelayCommand ExportCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await LoadBusinessNoOptionsAsync();
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

                SummaryText = BuildSummaryText(rows.Count);
                UpdateFilterScopeHint();
                await LoadBusinessNoOptionsAsync();
                RefreshRecentBusinessNoOptions(rows);
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"查询跨域流转台账失败：{ex.Message}");
            }
        }

        private async Task LoadBusinessNoOptionsAsync()
        {
            IReadOnlyList<string> options = await _ledgerService.GetBusinessNoOptionsAsync(BusinessNoOptionLimit);

            BusinessNoOptions.Clear();
            foreach (string option in options)
            {
                if (!string.IsNullOrWhiteSpace(option))
                {
                    BusinessNoOptions.Add(option.Trim());
                }
            }
        }

        private void RefreshRecentBusinessNoOptions(IReadOnlyList<CrossDomainTransferLedgerRow> rows)
        {
            RecentBusinessNoOptions.Clear();

            foreach (string businessNo in rows
                         .Select(row => row.BusinessNo?.Trim() ?? string.Empty)
                         .Where(item => !string.IsNullOrWhiteSpace(item))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .Take(RecentBusinessNoChipLimit))
            {
                RecentBusinessNoOptions.Add(businessNo);
            }

            if (RecentBusinessNoOptions.Count > 0)
            {
                return;
            }

            foreach (string businessNo in BusinessNoOptions.Take(RecentBusinessNoChipLimit))
            {
                RecentBusinessNoOptions.Add(businessNo);
            }
        }

        private CrossDomainTransferLedgerSearchCriteria BuildCriteria() => new()
        {
            OperatedFrom = OperatedFrom,
            OperatedTo = OperatedTo,
            TransactionType = SelectedTransactionType,
            MediaKind = SelectedMediaKind,
            BusinessNo = BusinessNo,
            OperatorName = OperatorName,
            Keyword = Keyword
        };

        private void ResetCriteria()
        {
            OperatedFrom = null;
            OperatedTo = null;
            SelectedTransactionType = string.Empty;
            SelectedMediaKind = string.Empty;
            BusinessNo = string.Empty;
            OperatorName = string.Empty;
            Keyword = string.Empty;
            LedgerRows.Clear();
            SelectedRow = null;
            RecentBusinessNoOptions.Clear();
            SummaryText = "共 0 条";
            FilterScopeHint = "当前显示：全部跨域流转流水";
            CommandManager.InvalidateRequerySuggested();
        }

        private void ApplyBusinessNoFromSelectedRow()
        {
            if (SelectedRow == null || string.IsNullOrWhiteSpace(SelectedRow.BusinessNo))
            {
                return;
            }

            BusinessNo = SelectedRow.BusinessNo.Trim();
        }

        private void NavigateToFilingLedger()
        {
            if (SelectedRow == null || SelectedRow.FilingFactId <= 0)
            {
                return;
            }

            NavigateToFilingLedgerRequested?.Invoke(SelectedRow.FilingFactId);
        }

        private async Task ExportAsync()
        {
            if (LedgerRows.Count == 0)
            {
                _dialogService.ShowError("当前没有可导出的跨域流转流水。");
                return;
            }

            string defaultFileName = $"跨域流转台账_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            string? filePath = _dialogService.SaveFileDialog("Excel Files|*.xlsx", "导出跨域流转台账", defaultFileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _dialogService.SetBusyState(true);
            try
            {
                await _ledgerService.ExportAsync(filePath, LedgerRows.ToList());
                _dialogService.ShowMessage($"跨域流转台账导出完成：\n{filePath}", "完成");
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

        private string BuildSummaryText(int count)
        {
            if (count <= 0)
            {
                return "共 0 条跨域流转流水";
            }

            int distinctBusinessCount = LedgerRows
                .Select(row => row.BusinessNo?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            return distinctBusinessCount > 0
                ? $"共 {count} 条跨域流转流水，涉及 {distinctBusinessCount} 个入网单号"
                : $"共 {count} 条跨域流转流水";
        }

        private void UpdateFilterScopeHint()
        {
            var parts = new List<string>();

            if (OperatedFrom.HasValue || OperatedTo.HasValue)
            {
                string from = OperatedFrom?.ToString("yyyy-MM-dd") ?? "…";
                string to = OperatedTo?.ToString("yyyy-MM-dd") ?? "…";
                parts.Add($"时间 {from} ~ {to}");
            }

            if (!string.IsNullOrWhiteSpace(SelectedTransactionType))
            {
                parts.Add(MaterialTransactionDomainValues.MapTypeDisplay(SelectedTransactionType));
            }

            if (!string.IsNullOrWhiteSpace(SelectedMediaKind))
            {
                parts.Add($"介质 {SelectedMediaKind.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(BusinessNo))
            {
                parts.Add($"入网单号 {BusinessNo.Trim()}");
            }

            FilterScopeHint = parts.Count == 0
                ? "当前显示：全部跨域流转流水"
                : $"当前筛选：{string.Join(" · ", parts)}";
        }

        private static bool TryResolveRecentDayCount(object? parameter, out int days)
        {
            days = 0;
            switch (parameter)
            {
                case null:
                    return false;
                case int value when value > 0:
                    days = value;
                    return true;
                case string text when int.TryParse(text, out int parsed) && parsed > 0:
                    days = parsed;
                    return true;
                default:
                    return false;
            }
        }

        public sealed class FilterOption
        {
            public string Label { get; init; } = string.Empty;

            public string Value { get; init; } = string.Empty;
        }
    }
}
