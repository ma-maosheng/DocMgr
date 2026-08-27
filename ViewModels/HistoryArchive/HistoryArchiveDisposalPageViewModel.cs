using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>
/// 历史存档资料离库处置页：在柜盒候选 + 处置单。
/// </summary>
public sealed class HistoryArchiveDisposalPageViewModel : ViewModelBase
{
    public const string PendingInProgressStatus = "进行中（待办结前）";
    public const string AllOption = "全部";

    private readonly IHistoryArchiveDisposalService _service;
    private readonly IDialogService _dialogService;
    private readonly IUserContextService _userContextService;
    private readonly List<HistoryArchiveDisposalRecord> _allRecords = new();
    private readonly List<HistoryArchiveDisposalBoxCandidateRow> _allCandidates = new();
    private bool _isInitialized;
    private int _applyYear = DateTime.Today.Year;
    private string _searchKeyword = string.Empty;
    private string _selectedStatus = AllOption;
    private string _boxKeyword = string.Empty;
    private string _materialKindDisplay = HistoryArchiveDisposalDomainValues.MaterialKindDisplayTopoMap;
    private string _cabinetName = AllOption;
    private HistoryArchiveDisposalRecord? _selectedRecord;
    private HistoryArchiveDisposalBoxCandidateRow? _selectedCandidate;

    public HistoryArchiveDisposalPageViewModel(
        IHistoryArchiveDisposalService service,
        IDialogService dialogService,
        IUserContextService userContextService)
    {
        _service = service;
        _dialogService = dialogService;
        _userContextService = userContextService;

        RefreshCommand = new RelayCommand(async _ => await RefreshAllAsync());
        SearchCommand = new RelayCommand(async _ => await RefreshDisposalsAsync());
        SearchBoxesCommand = new RelayCommand(_ => ApplyBoxFilters());
        AddDisposalCommand = new RelayCommand(async _ => await AddDisposalAsync(), _ => CanOperate);
        OpenDisposalCommand = new RelayCommand(async _ => await OpenDisposalAsync(), _ => SelectedRecord != null && CanOperate);
        WithdrawDisposalCommand = new RelayCommand(async _ => await WithdrawDisposalAsync(), _ => CanWithdrawSelected);
    }

    public ObservableCollection<HistoryArchiveDisposalBoxCandidateRow> Candidates { get; } = new();
    public ObservableCollection<HistoryArchiveDisposalRecord> Records { get; } = new();
    public ObservableCollection<int> ApplyYears { get; } = new();
    public ObservableCollection<string> StatusOptions { get; } = new();
    public ObservableCollection<string> MaterialKindOptions { get; } = new();
    public ObservableCollection<string> CabinetNameOptions { get; } = new() { AllOption };

    public string SearchKeyword { get => _searchKeyword; set => SetProperty(ref _searchKeyword, value); }
    public string BoxKeyword
    {
        get => _boxKeyword;
        set
        {
            if (SetProperty(ref _boxKeyword, value) && _isInitialized)
            {
                ApplyBoxFilters();
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
                _ = RefreshCandidatesAsync();
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
                ApplyBoxFilters();
            }
        }
    }

    public int ApplyYear
    {
        get => _applyYear;
        set
        {
            if (SetProperty(ref _applyYear, value) && _isInitialized)
            {
                ApplyRecordFilters();
            }
        }
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value) && _isInitialized)
            {
                ApplyRecordFilters();
            }
        }
    }

    public HistoryArchiveDisposalRecord? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            if (SetProperty(ref _selectedRecord, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public HistoryArchiveDisposalBoxCandidateRow? SelectedCandidate
    {
        get => _selectedCandidate;
        set => SetProperty(ref _selectedCandidate, value);
    }

    public string MixedGroupPreview
    {
        get
        {
            var related = Candidates
                .Where(item => item.IsSelected && item.IsMixedPlacement)
                .SelectMany(item => HistoryArchiveBoxCodeSupport.SplitBoxCodes(item.RelatedBoxCodesText))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return related.Count == 0
                ? string.Empty
                : "勾选混放盒将整组纳入：" + string.Join("、", related);
        }
    }

    private bool CanOperate => ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
    private bool CanWithdrawSelected =>
        CanOperate
        && SelectedRecord != null
        && SelectedRecord.Status is HistoryArchiveDisposalRecord.StatusDraft
            or HistoryArchiveDisposalRecord.StatusSubmitted;

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand SearchBoxesCommand { get; }
    public RelayCommand AddDisposalCommand { get; }
    public RelayCommand OpenDisposalCommand { get; }
    public RelayCommand WithdrawDisposalCommand { get; }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            ApplyRecordFilters();
            ApplyBoxFilters();
            return;
        }

        StatusOptions.Clear();
        StatusOptions.Add(AllOption);
        StatusOptions.Add(PendingInProgressStatus);
        foreach (var option in ApplicationWorkflowStatus.AllOptions)
        {
            StatusOptions.Add(option.Label);
        }

        MaterialKindOptions.Clear();
        foreach (string display in HistoryArchiveDisposalDomainValues.MaterialKindDisplayOptions)
        {
            MaterialKindOptions.Add(display);
        }

        ApplyYears.Clear();
        int currentYear = DateTime.Today.Year;
        for (int year = currentYear; year >= currentYear - 5; year--)
        {
            ApplyYears.Add(year);
        }

        await RefreshAllAsync();
        _isInitialized = true;
    }

    private async Task RefreshAllAsync()
    {
        await RefreshCandidatesAsync();
        await RefreshDisposalsAsync();
    }

    private async Task RefreshCandidatesAsync()
    {
        try
        {
            string kind = HistoryArchiveDisposalDomainValues.NormalizeMaterialKind(MaterialKindDisplay);
            IReadOnlyList<HistoryArchiveDisposalBoxCandidate> list =
                await _service.GetSelectableBoxesAsync(kind);
            ClearCandidates();
            foreach (var item in list.Where(candidate => candidate.IsSelectable))
            {
                AttachCandidate(new HistoryArchiveDisposalBoxCandidateRow(item));
            }

            CabinetNameOptions.Clear();
            CabinetNameOptions.Add(AllOption);
            foreach (string name in _allCandidates
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

            ApplyBoxFilters();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private async Task RefreshDisposalsAsync()
    {
        try
        {
            int? selectedId = SelectedRecord?.Id;
            string? keyword = string.IsNullOrWhiteSpace(SearchKeyword) ? null : SearchKeyword.Trim();
            var list = await _service.SearchRecordsAsync(keyword, null, null);
            _allRecords.Clear();
            _allRecords.AddRange(list);
            ApplyRecordFilters();
            if (selectedId.HasValue)
            {
                SelectedRecord = Records.FirstOrDefault(item => item.Id == selectedId.Value);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private void ApplyBoxFilters()
    {
        IEnumerable<HistoryArchiveDisposalBoxCandidateRow> query = _allCandidates;
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
                || Contains(item.StorageLocation, keyword)
                || Contains(item.RelatedBoxCodesText, keyword));
        }

        Candidates.Clear();
        foreach (var item in query)
        {
            Candidates.Add(item);
        }

        OnPropertyChanged(nameof(MixedGroupPreview));
        CommandManager.InvalidateRequerySuggested();
    }

    private void ApplyRecordFilters()
    {
        IEnumerable<HistoryArchiveDisposalRecord> query = _allRecords.Where(item => item.ApplyTime.Year == ApplyYear);
        if (string.Equals(SelectedStatus, PendingInProgressStatus, StringComparison.Ordinal))
        {
            query = query.Where(item =>
                item.Status is HistoryArchiveDisposalRecord.StatusSubmitted
                    or HistoryArchiveDisposalRecord.StatusApproved
                    or HistoryArchiveDisposalRecord.StatusSignedUploaded);
        }
        else if (!string.Equals(SelectedStatus, AllOption, StringComparison.Ordinal)
                 && !string.IsNullOrWhiteSpace(SelectedStatus))
        {
            var matched = ApplicationWorkflowStatus.AllOptions
                .FirstOrDefault(item => string.Equals(item.Label, SelectedStatus, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(matched.Label))
            {
                query = query.Where(item => item.Status == matched.Value);
            }
        }

        Records.Clear();
        foreach (var item in query.OrderByDescending(record => record.ApplyTime).ThenByDescending(record => record.Id))
        {
            Records.Add(item);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private async Task AddDisposalAsync()
    {
        var draft = new HistoryArchiveDisposalRecord
        {
            ApplyTime = DateTime.Now,
            ApplicantName = _userContextService.CurrentUser?.RealName?.Trim() ?? string.Empty,
            ApplicantDept = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty,
            Status = HistoryArchiveDisposalRecord.StatusDraft,
            MaterialKind = HistoryArchiveDisposalDomainValues.NormalizeMaterialKind(MaterialKindDisplay)
        };

        List<HistoryArchiveDisposalBoxCandidate> selected = Candidates
            .Where(item => item.IsSelected)
            .Select(item => item.Candidate)
            .ToList();
        foreach (var candidate in selected)
        {
            draft.Items.Add(new HistoryArchiveDisposalItemRow(candidate).ToItem(draft.Items.Count + 1));
        }

        if (_dialogService.ShowHistoryArchiveDisposalEditDialog(draft))
        {
            await RefreshAllAsync();
        }
    }

    private async Task OpenDisposalAsync()
    {
        if (SelectedRecord == null)
        {
            return;
        }

        var latest = await _service.GetRecordByIdAsync(SelectedRecord.Id);
        if (latest == null)
        {
            _dialogService.ShowError("未找到处置单。");
            await RefreshDisposalsAsync();
            return;
        }

        if (_dialogService.ShowHistoryArchiveDisposalEditDialog(latest))
        {
            await RefreshAllAsync();
        }
    }

    private async Task WithdrawDisposalAsync()
    {
        if (SelectedRecord == null)
        {
            return;
        }

        try
        {
            if (!_dialogService.ShowConfirm($"确认撤回作废处置单【{SelectedRecord.DisposalNo}】？"))
            {
                return;
            }

            await _service.WithdrawAsync(
                SelectedRecord.Id,
                null,
                _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。"));
            await RefreshAllAsync();
            _dialogService.ShowMessage("已撤回作废。");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private void AttachCandidate(HistoryArchiveDisposalBoxCandidateRow row)
    {
        row.SelectionChanged += OnCandidateSelectionChanged;
        _allCandidates.Add(row);
    }

    private void ClearCandidates()
    {
        foreach (var row in _allCandidates)
        {
            row.SelectionChanged -= OnCandidateSelectionChanged;
        }

        _allCandidates.Clear();
        Candidates.Clear();
    }

    private void OnCandidateSelectionChanged() => OnPropertyChanged(nameof(MixedGroupPreview));

    private static bool Contains(string? value, string keyword) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
