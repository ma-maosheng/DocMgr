using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings;

/// <summary>系统设置：当前在线用户与登录记录（系统管理员 / 资料管理员）。</summary>
public class UserSessionMonitorViewModel : ViewModelBase
{
    private readonly IUserService _userService;
    private readonly IDialogService _dialogService;
    private readonly IUserContextService _userContextService;

    public UserSessionMonitorViewModel(
        IUserService userService,
        IDialogService dialogService,
        IUserContextService userContextService)
    {
        _userService = userService;
        _dialogService = dialogService;
        _userContextService = userContextService;

        CanView = ArchiveRegisterBusinessRules.IsSystemAdministrator(_userContextService.CurrentUser)
                  || ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        OnlineUsers = new ObservableCollection<UserSessionMonitorItem>();
        LoginRecords = new ObservableCollection<UserSessionMonitorItem>();

        EndDate = DateTime.Today;
        StartDate = DateTime.Today.AddDays(-7);

        RefreshOnlineCommand = new RelayCommand(async _ => await RefreshOnlineAsync(), _ => CanView && !IsBusy);
        RefreshLoginRecordsCommand = new RelayCommand(async _ => await RefreshLoginRecordsAsync(), _ => CanView && !IsBusy);
    }

    /// <summary>系统管理员或资料管理员可查看。</summary>
    public bool CanView { get; }

    public ObservableCollection<UserSessionMonitorItem> OnlineUsers { get; }

    public ObservableCollection<UserSessionMonitorItem> LoginRecords { get; }

    public ICommand RefreshOnlineCommand { get; }

    public ICommand RefreshLoginRecordsCommand { get; }

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

    private DateTime? _startDate;
    public DateTime? StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    private DateTime? _endDate;
    public DateTime? EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    private string _keyword = string.Empty;
    public string Keyword
    {
        get => _keyword;
        set => SetProperty(ref _keyword, value ?? string.Empty);
    }

    private string _onlineStatusText = string.Empty;
    public string OnlineStatusText
    {
        get => _onlineStatusText;
        private set => SetProperty(ref _onlineStatusText, value);
    }

    private string _loginRecordStatusText = string.Empty;
    public string LoginRecordStatusText
    {
        get => _loginRecordStatusText;
        private set => SetProperty(ref _loginRecordStatusText, value);
    }

    private string _accessDeniedText = "仅系统管理员或资料管理员可查看在线用户与登录记录。";
    public string AccessDeniedText
    {
        get => _accessDeniedText;
        private set => SetProperty(ref _accessDeniedText, value);
    }

    /// <summary>页面显示时加载默认数据。</summary>
    public async Task LoadOnPageDisplayedAsync()
    {
        if (!CanView)
        {
            OnlineStatusText = AccessDeniedText;
            LoginRecordStatusText = AccessDeniedText;
            return;
        }

        await RefreshOnlineAsync();
        await RefreshLoginRecordsAsync();
    }

    private async Task RefreshOnlineAsync()
    {
        if (!EnsureCanView())
        {
            return;
        }

        IsBusy = true;
        try
        {
            User operatorUser = _userContextService.CurrentUser
                ?? throw new InvalidOperationException("当前用户未登录。");

            IReadOnlyList<UserSessionMonitorItem> items = await Task.Run(
                () => _userService.GetOnlineUsers(operatorUser));

            OnlineUsers.Clear();
            foreach (UserSessionMonitorItem item in items)
            {
                OnlineUsers.Add(item);
            }

            OnlineStatusText = $"当前在线 {OnlineUsers.Count} 人（会话心跳超时约 2 分钟视为离线）";
        }
        catch (Exception ex)
        {
            OnlineStatusText = "加载失败";
            _dialogService.ShowError($"加载在线用户失败：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshLoginRecordsAsync()
    {
        if (!EnsureCanView())
        {
            return;
        }

        IsBusy = true;
        try
        {
            User operatorUser = _userContextService.CurrentUser
                ?? throw new InvalidOperationException("当前用户未登录。");

            DateTime? start = StartDate;
            DateTime? end = EndDate;
            string keyword = Keyword;

            IReadOnlyList<UserSessionMonitorItem> items = await Task.Run(
                () => _userService.SearchLoginRecords(operatorUser, start, end, keyword));

            LoginRecords.Clear();
            foreach (UserSessionMonitorItem item in items)
            {
                LoginRecords.Add(item);
            }

            LoginRecordStatusText = $"共 {LoginRecords.Count} 条登录记录";
        }
        catch (Exception ex)
        {
            LoginRecordStatusText = "加载失败";
            _dialogService.ShowError($"加载登录记录失败：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool EnsureCanView()
    {
        if (CanView)
        {
            return true;
        }

        _dialogService.ShowError(AccessDeniedText);
        return false;
    }
}
