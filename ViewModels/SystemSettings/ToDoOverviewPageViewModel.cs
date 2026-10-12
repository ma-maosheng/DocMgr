using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings;

/// <summary>系统设置：待办事项总览（全员可查看）。</summary>
public sealed class ToDoOverviewPageViewModel : ViewModelBase
{
    private readonly IToDoService _toDoService;
    private readonly IDialogService _dialogService;
    private readonly IUserContextService _userContextService;
    private readonly List<ToDoItem> _allItems = new();

    public ToDoOverviewPageViewModel(
        IToDoService toDoService,
        IDialogService dialogService,
        IUserContextService userContextService)
    {
        _toDoService = toDoService;
        _dialogService = dialogService;
        _userContextService = userContextService;

        CanView = true;

        Items = new ObservableCollection<ToDoItem>();
        RefreshCommand = new RelayCommand(async _ => await RefreshAsync(), _ => CanView && !IsBusy);
        OpenCommand = new RelayCommand(async _ => await OpenSelectedAsync(), _ => CanView && SelectedItem != null && !IsBusy);
    }

    public bool CanView { get; }

    public string AccessDeniedText { get; } = string.Empty;

    public ObservableCollection<ToDoItem> Items { get; }

    public ICommand RefreshCommand { get; }

    public ICommand OpenCommand { get; }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private string _keyword = string.Empty;
    public string Keyword
    {
        get => _keyword;
        set
        {
            if (SetProperty(ref _keyword, value ?? string.Empty))
            {
                ApplyFilter();
            }
        }
    }

    private ToDoItem? _selectedItem;
    public ToDoItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public Func<ToDoItem, Task>? OpenItemHandler { get; set; }

    public async Task InitializeAsync()
    {
        if (!CanView)
        {
            StatusText = AccessDeniedText;
            return;
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (!CanView)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var user = _userContextService.CurrentUser;
            var items = await _toDoService.GetOverviewToDosAsync(user!, topN: 500);
            _allItems.Clear();
            _allItems.AddRange(items);
            ApplyFilter();
            StatusText = $"共 {_allItems.Count} 条未办结待办（最多展示 500 条）。";
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage($"刷新待办总览失败：{ex.Message}", "错误");
            StatusText = "刷新失败。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        string keyword = Keyword.Trim();
        IEnumerable<ToDoItem> query = _allItems;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item =>
                (item.Title?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.BizNo?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.Stage?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.BizType?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Items.Clear();
        foreach (var item in query)
        {
            Items.Add(item);
        }
    }

    private async Task OpenSelectedAsync()
    {
        if (SelectedItem == null || OpenItemHandler == null)
        {
            return;
        }

        await OpenItemHandler(SelectedItem);
    }
}
