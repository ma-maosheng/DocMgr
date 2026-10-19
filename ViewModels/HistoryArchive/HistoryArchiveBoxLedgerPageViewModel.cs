using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.Shared;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;
using NPOI.XSSF.UserModel;
using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>
/// 历史存档盒台账页：按资料类别与在库/已离库查询，支持分页/全局浏览与导出。
/// </summary>
public sealed class HistoryArchiveBoxLedgerPageViewModel : ViewModelBase
{
    public const string AllOption = "全部";

    private readonly IHistoryArchiveDisposalService _service;
    private readonly IDialogService _dialogService;
    private readonly List<HistoryArchiveBoxLedgerRow> _allRows = new();
    private readonly List<HistoryArchiveBoxLedgerRow> _filteredRows = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly PageDbOperationQueue _pageDb = new();
    private int _refreshGeneration;
    private bool _isInitialized;
    private bool _isGlobalBrowse;
    private bool _isSwitchingBrowseMode;
    private int _pageSize = 50;
    private int _currentPage = 1;
    private string _boxKeyword = string.Empty;
    private string _materialKindDisplay = AllOption;
    private string _lifecycleStatus = HistoryArchiveDisposalDomainValues.LifecycleInStock;
    private string _cabinetName = AllOption;
    private HistoryArchiveBoxLedgerRow? _selectedRow;

    public HistoryArchiveBoxLedgerPageViewModel(
        IHistoryArchiveDisposalService service,
        IDialogService dialogService)
    {
        _service = service;
        _dialogService = dialogService;

        RefreshCommand = new RelayCommand(async _ => await _pageDb.RunAsync(RefreshAsync));
        SearchCommand = new RelayCommand(_ => ApplyFilters(resetPage: true));
        ShowIntroCommand = new RelayCommand(_ => _dialogService.ShowLedgerIntro(LedgerIntroKind.HistoryArchiveBox));
        ExportCommand = new RelayCommand(async _ => await ExportAsync(), _ => _filteredRows.Count > 0);
        FirstPageCommand = new RelayCommand(_ => GoToPage(1), _ => CanGoPrevious);
        PreviousPageCommand = new RelayCommand(_ => GoToPage(CurrentPage - 1), _ => CanGoPrevious);
        NextPageCommand = new RelayCommand(_ => GoToPage(CurrentPage + 1), _ => CanGoNext);
        LastPageCommand = new RelayCommand(_ => GoToPage(TotalPages), _ => CanGoNext);
    }

    public ObservableCollection<HistoryArchiveBoxLedgerRow> Rows { get; } = new();

    public ObservableCollection<string> MaterialKindOptions { get; } = new();

    public ObservableCollection<string> LifecycleStatusOptions { get; } = new();

    public ObservableCollection<string> CabinetNameOptions { get; } = new() { AllOption };

    public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 50, 100, 200, 500 };

    public string BoxKeyword
    {
        get => _boxKeyword;
        set
        {
            if (SetProperty(ref _boxKeyword, value) && _isInitialized)
            {
                ApplyFilters(resetPage: true);
            }
        }
    }

    public string MaterialKindDisplay
    {
        get => _materialKindDisplay;
        set
        {
            if (SetProperty(ref _materialKindDisplay, value) && _isInitialized)
            {
                _pageDb.RunInBackground(RefreshAsync);
            }
        }
    }

    public string LifecycleStatus
    {
        get => _lifecycleStatus;
        set
        {
            if (SetProperty(ref _lifecycleStatus, value) && _isInitialized)
            {
                _pageDb.RunInBackground(RefreshAsync);
            }
        }
    }

    public string CabinetName
    {
        get => _cabinetName;
        set
        {
            if (SetProperty(ref _cabinetName, value) && _isInitialized)
            {
                ApplyFilters(resetPage: true);
            }
        }
    }

    /// <summary>分页浏览：列表按页展示。</summary>
    public bool IsPagedBrowse
    {
        get => !_isGlobalBrowse;
        set
        {
            if (value)
            {
                SwitchBrowseMode(isGlobal: false);
            }
        }
    }

    /// <summary>全局浏览：一次展示全部筛选结果，不分页。</summary>
    public bool IsGlobalBrowse
    {
        get => _isGlobalBrowse;
        set
        {
            if (value)
            {
                SwitchBrowseMode(isGlobal: true);
            }
        }
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (value <= 0 || _pageSize == value)
            {
                return;
            }

            _pageSize = value;
            OnPropertyChanged();
            CurrentPage = 1;
            RefreshDisplayedRows();
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                NotifyPaginationChanged();
            }
        }
    }

    public int TotalCount => _filteredRows.Count;

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public string PageInfo => TotalCount == 0
        ? "暂无记录"
        : IsGlobalBrowse
            ? $"共 {TotalCount} 条"
            : $"第 {CurrentPage} / {TotalPages} 页，本页 {Rows.Count} 条，共 {TotalCount} 条";

    public bool CanGoPrevious => IsPagedBrowse && CurrentPage > 1;

    public bool CanGoNext => IsPagedBrowse && CurrentPage < TotalPages;

    public bool ShowPaginationBar => IsPagedBrowse && _filteredRows.Count > 0;

    public string CurrentBrowseCaption
    {
        get
        {
            string modeName = IsGlobalBrowse ? "全局浏览" : "分页浏览";
            string kind = string.IsNullOrWhiteSpace(MaterialKindDisplay) ? AllOption : MaterialKindDisplay.Trim();
            string lifecycle = string.IsNullOrWhiteSpace(LifecycleStatus) ? AllOption : LifecycleStatus.Trim();
            return $"{modeName}  ·  {kind}  ·  {lifecycle}  ·  {PageInfo}";
        }
    }

    public HistoryArchiveBoxLedgerRow? SelectedRow
    {
        get => _selectedRow;
        set => SetProperty(ref _selectedRow, value);
    }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand SearchCommand { get; }

    public RelayCommand ShowIntroCommand { get; }

    public RelayCommand ExportCommand { get; }

    public RelayCommand FirstPageCommand { get; }

    public RelayCommand PreviousPageCommand { get; }

    public RelayCommand NextPageCommand { get; }

    public RelayCommand LastPageCommand { get; }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            ApplyFilters(resetPage: false);
            return;
        }

        MaterialKindOptions.Clear();
        MaterialKindOptions.Add(AllOption);
        foreach (string display in HistoryArchiveDisposalDomainValues.MaterialKindDisplayOptions)
        {
            MaterialKindOptions.Add(display);
        }

        LifecycleStatusOptions.Clear();
        LifecycleStatusOptions.Add(AllOption);
        LifecycleStatusOptions.Add(HistoryArchiveDisposalDomainValues.LifecycleInStock);
        LifecycleStatusOptions.Add(HistoryArchiveDisposalDomainValues.LifecycleDisposed);

        await RefreshAsync();
        _isInitialized = true;
    }

    private async Task RefreshAsync()
    {
        int generation = Interlocked.Increment(ref _refreshGeneration);
        await _refreshGate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (generation != Volatile.Read(ref _refreshGeneration))
            {
                return;
            }

            try
            {
                IReadOnlyList<HistoryArchiveBoxLedgerEntry> list =
                    await _service.SearchBoxLedgerAsync(MaterialKindDisplay, LifecycleStatus);
                _allRows.Clear();
                foreach (var entry in list)
                {
                    _allRows.Add(new HistoryArchiveBoxLedgerRow(entry));
                }

                CabinetNameOptions.Clear();
                CabinetNameOptions.Add(AllOption);
                foreach (string name in _allRows
                             .Select(item => item.CabinetName?.Trim() ?? string.Empty)
                             .Where(item => !string.IsNullOrWhiteSpace(item))
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
                {
                    CabinetNameOptions.Add(name);
                }

                if (!CabinetNameOptions.Contains(CabinetName, StringComparer.OrdinalIgnoreCase))
                {
                    _cabinetName = AllOption;
                    OnPropertyChanged(nameof(CabinetName));
                }

                ApplyFilters(resetPage: true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void SwitchBrowseMode(bool isGlobal)
    {
        if (_isSwitchingBrowseMode || _isGlobalBrowse == isGlobal)
        {
            return;
        }

        _isSwitchingBrowseMode = true;
        try
        {
            _isGlobalBrowse = isGlobal;
            OnPropertyChanged(nameof(IsGlobalBrowse));
            OnPropertyChanged(nameof(IsPagedBrowse));
            CurrentPage = 1;
            RefreshDisplayedRows();
        }
        finally
        {
            _isSwitchingBrowseMode = false;
        }
    }

    private void ApplyFilters(bool resetPage)
    {
        IEnumerable<HistoryArchiveBoxLedgerRow> query = _allRows;
        if (!string.Equals(CabinetName, AllOption, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(CabinetName))
        {
            query = query.Where(item =>
                string.Equals(item.CabinetName?.Trim(), CabinetName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        string keyword = BoxKeyword?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item =>
                Contains(item.BoxCode, keyword)
                || Contains(item.ContentSummary, keyword)
                || Contains(item.RelatedBoxCodesText, keyword)
                || Contains(item.MaterialKindDisplay, keyword)
                || Contains(item.CabinetName, keyword)
                || Contains(item.LifecycleStatus, keyword));
        }

        _filteredRows.Clear();
        _filteredRows.AddRange(query
            .OrderBy(row => row.MaterialKindDisplay, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.CabinetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.BoxCode, StringComparer.OrdinalIgnoreCase));

        if (resetPage)
        {
            _currentPage = 1;
            OnPropertyChanged(nameof(CurrentPage));
        }
        else if (CurrentPage > TotalPages)
        {
            _currentPage = TotalPages;
            OnPropertyChanged(nameof(CurrentPage));
        }

        RefreshDisplayedRows();
    }

    private void RefreshDisplayedRows()
    {
        string? selectedCode = SelectedRow?.BoxCode;
        string? selectedKind = SelectedRow?.MaterialKindDisplay;

        IEnumerable<HistoryArchiveBoxLedgerRow> displayQuery = _filteredRows;
        if (IsPagedBrowse && _filteredRows.Count > 0)
        {
            int page = Math.Clamp(CurrentPage, 1, TotalPages);
            if (page != CurrentPage)
            {
                _currentPage = page;
                OnPropertyChanged(nameof(CurrentPage));
            }

            displayQuery = _filteredRows
                .Skip((page - 1) * PageSize)
                .Take(PageSize);
        }

        Rows.Clear();
        foreach (var item in displayQuery)
        {
            Rows.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(selectedCode))
        {
            SelectedRow = Rows.FirstOrDefault(item =>
                string.Equals(item.BoxCode, selectedCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.MaterialKindDisplay, selectedKind, StringComparison.Ordinal));
        }

        NotifyPaginationChanged();
        CommandManager.InvalidateRequerySuggested();
    }

    private void GoToPage(int page)
    {
        CurrentPage = Math.Clamp(page, 1, TotalPages);
        RefreshDisplayedRows();
    }

    private void NotifyPaginationChanged()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowPaginationBar));
        OnPropertyChanged(nameof(CurrentBrowseCaption));
    }

    private async Task ExportAsync()
    {
        if (_filteredRows.Count == 0)
        {
            _dialogService.ShowMessage("当前没有可导出的记录。");
            return;
        }

        string defaultFileName = BuildDefaultExportFileName();
        string? filePath = _dialogService.SaveFileDialog("Excel Files|*.xlsx", "导出历史存档盒台账", defaultFileName);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            var exportRows = _filteredRows.ToList();
            await Task.Run(() => ExportToExcel(filePath, exportRows));
            _dialogService.ShowMessage($"导出完成：{filePath}", "完成");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"导出失败: {ex.Message}");
        }
    }

    private string BuildDefaultExportFileName()
    {
        string kind = string.Equals(MaterialKindDisplay, AllOption, StringComparison.Ordinal)
            ? "全部类别"
            : MaterialKindDisplay.Trim();
        string lifecycle = string.Equals(LifecycleStatus, AllOption, StringComparison.Ordinal)
            ? "全部状态"
            : LifecycleStatus.Trim();
        string baseName = $"历史存档盒台账_{kind}_{lifecycle}";
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(invalidChar, '_');
        }

        return $"{baseName}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
    }

    private static void ExportToExcel(string filePath, List<HistoryArchiveBoxLedgerRow> list)
    {
        using var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet(ExcelSheetNameSupport.Sanitize("历史存档盒台账", "盒台账"));

        string[] headers =
        [
            "资料类别",
            "状态",
            "盒号",
            "规格",
            "柜体",
            "盒内摘要",
            "台账条数",
            "混放",
            "关联盒数",
            "关联混放盒号",
            "可处置"
        ];
        var headerRow = sheet.CreateRow(0);
        for (int i = 0; i < headers.Length; i++)
        {
            headerRow.CreateCell(i).SetCellValue(headers[i]);
        }

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            var row = sheet.CreateRow(i + 1);
            row.CreateCell(0).SetCellValue(item.MaterialKindDisplay ?? string.Empty);
            row.CreateCell(1).SetCellValue(item.LifecycleStatus ?? string.Empty);
            row.CreateCell(2).SetCellValue(item.BoxCode ?? string.Empty);
            row.CreateCell(3).SetCellValue(item.BoxSpecification ?? string.Empty);
            row.CreateCell(4).SetCellValue(item.CabinetName ?? string.Empty);
            row.CreateCell(5).SetCellValue(item.ContentSummary ?? string.Empty);
            row.CreateCell(6).SetCellValue(item.LedgerRecordCount);
            row.CreateCell(7).SetCellValue(item.MixedPlacementText ?? string.Empty);
            row.CreateCell(8).SetCellValue(item.RelatedBoxCount);
            row.CreateCell(9).SetCellValue(item.RelatedBoxCodesText ?? string.Empty);
            row.CreateCell(10).SetCellValue(item.AvailabilityText ?? string.Empty);
        }

        for (int i = 0; i < headers.Length; i++)
        {
            sheet.AutoSizeColumn(i);
        }

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        workbook.Write(fs, leaveOpen: false);
    }

    private static bool Contains(string? value, string keyword) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
