using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.HistoryArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using DocMgr.Views.HistoryArchive;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>
/// 历史存档杂项资料列表。
/// </summary>
public sealed class HistoryMiscMaterialPageViewModel : ViewModelBase
{
    public const string AllOption = "全部";

    private readonly IHistoryMiscMaterialService _service;
    private readonly IDialogService _dialogService;
    private readonly IUserContextService _userContextService;
    private readonly PageDbOperationQueue _pageDb = new();
    private string _majorCategory = AllOption;
    private string _subCategory = AllOption;
    private string _containerKind = AllOption;
    private string _lifecycleStatus = HistoryArchiveDisposalDomainValues.LifecycleInStock;
    private string _keyword = string.Empty;
    private HistoryMiscMaterialListItem? _selectedItem;

    public HistoryMiscMaterialPageViewModel(
        IHistoryMiscMaterialService service,
        IDialogService dialogService,
        IUserContextService userContextService)
    {
        _service = service;
        _dialogService = dialogService;
        _userContextService = userContextService;
        SearchCommand = new RelayCommand(async _ => await _pageDb.RunAsync(ReloadAsync));
        RefreshCommand = new RelayCommand(async _ => await _pageDb.RunAsync(ReloadAsync));
        BrowsePhotosCommand = new RelayCommand(async _ => await _pageDb.RunAsync(BrowsePhotosAsync), _ => CanBrowsePhotos);
        NewCommand = new RelayCommand(async _ => await _pageDb.RunAsync(() => EditAsync(null)), _ => CanMaintain);
        OpenCommand = new RelayCommand(async _ => await _pageDb.RunAsync(OpenAsync), _ => SelectedItem != null);
        EditCommand = new RelayCommand(async _ => await _pageDb.RunAsync(() => EditAsync(SelectedItem?.Id)), _ => CanMaintain && SelectedItem != null);
        DeleteCommand = new RelayCommand(async _ => await _pageDb.RunAsync(DeleteAsync), _ => CanDeleteSelected);
        _ = _pageDb.RunAsync(InitializeAsync);
    }

    public ObservableCollection<string> MajorCategoryOptions { get; } = new();

    public ObservableCollection<string> SubCategoryOptions { get; } = new();

    public ObservableCollection<string> ContainerKindOptions { get; } = new();

    public ObservableCollection<string> LifecycleStatusOptions { get; } = new()
    {
        AllOption,
        HistoryArchiveDisposalDomainValues.LifecycleInStock,
        HistoryArchiveDisposalDomainValues.LifecycleDisposed
    };

    public ObservableCollection<HistoryMiscMaterialListItem> Items { get; } = new();

    public string MajorCategory
    {
        get => _majorCategory;
        set => SetProperty(ref _majorCategory, value);
    }

    public string SubCategory
    {
        get => _subCategory;
        set => SetProperty(ref _subCategory, value);
    }

    public string ContainerKind
    {
        get => _containerKind;
        set => SetProperty(ref _containerKind, value);
    }

    public string LifecycleStatus
    {
        get => _lifecycleStatus;
        set => SetProperty(ref _lifecycleStatus, value);
    }

    public string Keyword
    {
        get => _keyword;
        set => SetProperty(ref _keyword, value ?? string.Empty);
    }

    public HistoryMiscMaterialListItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(CanDeleteSelected));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool CanMaintain => HistoryArchiveLedgerPermissionSupport.CanMaintain(_userContextService.CurrentUser);

    public bool CanDeleteSelected =>
        CanMaintain
        && SelectedItem != null
        && HistoryArchiveDisposalDomainValues.IsInStockLifecycle(SelectedItem.LifecycleStatus);

    public bool CanBrowsePhotos => SelectedItem is { PhotoCount: > 0 };

    public string MaintainHint => CanMaintain
        ? "一条杂项对应一个存放容器。离库请到「资料台账与资料处置 → 处置」。"
        : HistoryArchiveLedgerPermissionSupport.MaintainDeniedMessage;

    public RelayCommand BrowsePhotosCommand { get; }

    public RelayCommand SearchCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand NewCommand { get; }

    public RelayCommand OpenCommand { get; }

    public RelayCommand EditCommand { get; }

    public RelayCommand DeleteCommand { get; }

    private async Task InitializeAsync()
    {
        await LoadVocabularyAsync();
        await ReloadAsync();
    }

    private async Task LoadVocabularyAsync()
    {
        HistoryMiscVocabulary vocabulary = await _service.GetVocabularyAsync();
        MajorCategory = ReplaceOptions(MajorCategoryOptions, vocabulary.MajorCategories, MajorCategory);
        SubCategory = ReplaceOptions(
            SubCategoryOptions,
            vocabulary.SubCategories.Select(item => item.SubCategory),
            SubCategory);
        ContainerKind = ReplaceOptions(ContainerKindOptions, vocabulary.ContainerKinds, ContainerKind);
    }

    private async Task ReloadAsync()
    {
        int? selectedId = SelectedItem?.Id;
        IReadOnlyList<HistoryMiscMaterialListItem> rows = await _service.SearchAsync(
            MajorCategory,
            SubCategory,
            ContainerKind,
            LifecycleStatus,
            Keyword);
        Items.Clear();
        foreach (HistoryMiscMaterialListItem row in rows)
        {
            Items.Add(row);
        }

        SelectedItem = Items.FirstOrDefault(item => item.Id == selectedId) ?? Items.FirstOrDefault();
        await LoadVocabularyAsync();
    }

    private Task BrowsePhotosAsync()
    {
        if (SelectedItem is not { PhotoCount: > 0 } item)
        {
            return Task.CompletedTask;
        }

        return HistoryMiscPhotoBrowseDialogViewModel.ShowAsync(
            _service,
            _dialogService,
            item.Id,
            item.ContainerKind,
            item.ContainerCode,
            item.MajorCategory,
            item.SubCategory,
            item.LocationDisplay);
    }

    private Task OpenAsync()
    {
        if (SelectedItem == null)
        {
            return Task.CompletedTask;
        }

        return EditAsync(SelectedItem.Id, viewOnly: true);
    }

    private async Task EditAsync(int? id, bool viewOnly = false)
    {
        var editViewModel = new HistoryMiscMaterialEditDialogViewModel(
            _service,
            _dialogService,
            _userContextService.CurrentUser,
            id,
            viewOnly);
        await editViewModel.LoadAsync();
        var dialog = new HistoryMiscMaterialEditDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = editViewModel
        };
        editViewModel.RequestClose += result => dialog.DialogResult = result;
        if (dialog.ShowDialog() == true)
        {
            await ReloadAsync();
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedItem == null || !CanDeleteSelected)
        {
            return;
        }

        string label = $"{SelectedItem.ContainerKind} {SelectedItem.ContainerCode}".Trim();
        if (!_dialogService.ShowConfirm($"确定删除杂项资料「{label}」吗？", "删除杂项资料"))
        {
            return;
        }

        try
        {
            await _service.DeleteAsync(SelectedItem.Id, _userContextService.CurrentUser!);
            await ReloadAsync();
        }
        catch (InvalidOperationException ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private static string ReplaceOptions(ObservableCollection<string> target, IEnumerable<string> values, string selected)
    {
        var next = new List<string> { AllOption };
        next.AddRange(values
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal));
        if (!next.Contains(selected, StringComparer.Ordinal))
        {
            selected = AllOption;
        }

        target.Clear();
        foreach (string value in next)
        {
            target.Add(value);
        }

        return selected;
    }
}
