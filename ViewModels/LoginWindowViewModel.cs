using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Infrastructure;
using DocMgr.Infrastructure.Startup;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels
{
    public class LoginWindowViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly AppInitializationState _initializationState;
        private readonly IArchiveFilingSearchPoolSession _searchPoolSession;
        private readonly IAppReleaseService _appReleaseService;

        private string _username = string.Empty;
        private string _password = string.Empty;
        private bool _isBusy;
        private string _upgradeStatusText = string.Empty;

        public LoginWindowViewModel(
            IUserService userService,
            IUserContextService userContextService,
            IDialogService dialogService,
            AppInitializationState initializationState,
            IArchiveFilingSearchPoolSession searchPoolSession,
            IAppReleaseService appReleaseService)
        {
            _userService = userService;
            _userContextService = userContextService;
            _dialogService = dialogService;
            _initializationState = initializationState;
            _searchPoolSession = searchPoolSession;
            _appReleaseService = appReleaseService;
            _initializationState.PropertyChanged += InitializationState_PropertyChanged;

            LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => CanSubmitLogin);
            CancelCommand = new RelayCommand(_ => RequestShutdown?.Invoke(), _ => !IsBusy);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanSubmitLogin));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string InitializationStatus => _initializationState.StatusMessage;

        /// <summary>登录页展示的程序版本，例如「版本 1.0.0」。</summary>
        public string VersionDisplay => $"版本 {AppVersionInfo.DisplayVersion}";

        /// <summary>初始化或升级进度。升级下载时优先显示升级说明。</summary>
        public string LoginStatusText =>
            string.IsNullOrWhiteSpace(_upgradeStatusText) ? InitializationStatus : _upgradeStatusText;

        public bool CanSubmitLogin => !IsBusy;

        public ICommand LoginCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<User>? LoginSucceeded;
        public event Action? RequestShutdown;

        private void InitializationState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(InitializationStatus));
            OnPropertyChanged(nameof(LoginStatusText));
            OnPropertyChanged(nameof(CanSubmitLogin));
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task LoginAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (!_initializationState.IsLoginEnabled)
            {
                if (_initializationState.HasFailed)
                {
                    _dialogService.ShowError(
                        _initializationState.ErrorMessage ?? "系统初始化失败。",
                        "无法登录");
                }
                else
                {
                    _dialogService.ShowMessage(_initializationState.StatusMessage);
                }

                return;
            }

            string? sessionToRelease = null;
            try
            {
                IsBusy = true;

                string username = (Username ?? string.Empty).Trim();
                string password = Password ?? string.Empty;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    _dialogService.ShowMessage("请输入用户名和密码！");
                    return;
                }

                var loginResult = await Task.Run(() => _userService.Login(username, password));
                if (loginResult.Status == UserLoginStatus.AlreadyLoggedIn)
                {
                    bool shouldReplace = _dialogService.ShowConfirm(BuildReplaceMessage(loginResult), "单点登录");
                    if (!shouldReplace)
                    {
                        return;
                    }

                    loginResult = await Task.Run(() => _userService.Login(username, password, forceReplaceExistingSession: true));
                }

                if (!loginResult.IsSuccess || loginResult.User == null)
                {
                    ShowLoginFailure(loginResult);
                    return;
                }

                sessionToRelease = loginResult.SessionId;
                if (await TryUpgradeBeforeEnterAsync(loginResult.SessionId))
                {
                    sessionToRelease = null;
                    return;
                }

                _userContextService.SetCurrentSession(loginResult.User, loginResult.SessionId);
                _searchPoolSession.ClearAll();

#if !DEBUG
                if (loginResult.User.MustChangePassword)
                {
                    bool changed = _dialogService.ShowChangePasswordDialog(isMandatory: true);
                    if (!changed)
                    {
                        _userService.Logout(loginResult.SessionId);
                        _userContextService.Clear();
                        _dialogService.ShowMessage("必须修改初始密码后才能进入系统。");
                        return;
                    }
                }
#endif

                sessionToRelease = null;
                LoginSucceeded?.Invoke(loginResult.User);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(sessionToRelease))
                {
                    _userService.Logout(sessionToRelease);
                }

                _dialogService.ShowError("登录失败：" + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 库内版本更高时注销刚建立的会话、下载安装包并退出。返回 true 表示不应进入主窗口。
        /// </summary>
        private async Task<bool> TryUpgradeBeforeEnterAsync(string sessionId)
        {
            AppReleasePackageSummary? published = await _appReleaseService.GetPublishedAsync();
            if (published == null
                || !AppReleaseVersionSupport.IsNewer(published.Version, AppVersionInfo.DisplayVersion))
            {
                return false;
            }

            _userService.Logout(sessionId);
            _dialogService.ShowMessage(
                $"检测到新版本 {published.Version}（当前 {AppVersionInfo.DisplayVersion}）。\n\n将下载安装包并自动安装，完成后重新打开。请稍候，不要关闭计算机。",
                "软件升级");
            _upgradeStatusText = "正在下载安装包…";
            OnPropertyChanged(nameof(LoginStatusText));

            AppReleaseUpgradeStartResult started = await _appReleaseService.DownloadAndStartInstallerAsync(published.Id);
            if (!started.Started)
            {
                _upgradeStatusText = string.Empty;
                OnPropertyChanged(nameof(LoginStatusText));
                _dialogService.ShowError(started.Message, "软件升级");
                return true;
            }

            RequestShutdown?.Invoke();
            return true;
        }

        private void ShowLoginFailure(UserLoginResult loginResult)
        {
            if (loginResult.Status == UserLoginStatus.InvalidCredentials)
            {
                _dialogService.ShowError("用户名或密码错误！");
                return;
            }

            if (loginResult.Status == UserLoginStatus.LockedOut)
            {
                _dialogService.ShowError(loginResult.Message, "账号已锁定");
                return;
            }

            _dialogService.ShowError(loginResult.Message, "单点登录");
        }

        private static string BuildReplaceMessage(UserLoginResult loginResult)
        {
            string terminalName = string.IsNullOrWhiteSpace(loginResult.ExistingTerminalName)
                ? "未知终端"
                : loginResult.ExistingTerminalName;

            string loginTimeText = loginResult.ExistingLoginTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知时间";
            return $"该账号已在终端【{terminalName}】于 {loginTimeText} 登录。\n\n是否强制顶替该终端并继续登录？";
        }
    }
}
