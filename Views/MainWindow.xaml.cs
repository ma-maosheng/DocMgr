using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Views.Cabinets;
using DocMgr.Views.HardDiskMedia;
using DocMgr.Views.HistoryArchive;
using DocMgr.Views.NetworkTransfer;
using DocMgr.Views.Projects;
using DocMgr.Views.SystemSettings;
using DocMgr.ViewModels.YearlyArchive;
using DocMgr.Views.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;


namespace DocMgr.Views
{
    public partial class MainWindow : Window
    {
        public User? CurrentUser { get; private set; }

        private readonly IServiceScope _windowScope;
        private readonly IUserContextService _userContextService;
        private readonly IDbOperationLogContextService _operationLogContextService;
        private readonly IServiceScopeFactory _scopeFactory;
        private IToDoCenterService? _toDoCenter;
        private readonly IToDoNotificationPresenter _toDoNotificationPresenter;
        private DispatcherTimer? _sessionHeartbeatTimer;
        private bool _isHandlingSessionInvalidation;
        private bool _isSessionInvalid;
        private Button? _activeNavButton;

        public MainWindow(User user)
        {
            InitializeComponent();

            _windowScope = App.CurrentProvider.CreateScope();
            _userContextService = _windowScope.ServiceProvider.GetRequiredService<IUserContextService>();
            _operationLogContextService = _windowScope.ServiceProvider.GetRequiredService<IDbOperationLogContextService>();
            _scopeFactory = _windowScope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
            _toDoNotificationPresenter = _windowScope.ServiceProvider.GetRequiredService<IToDoNotificationPresenter>();

            WindowState = WindowState.Maximized;
            CurrentUser = user;

            if (CurrentUser != null)
            {
                TxtCurrentUserName.Text = CurrentUser.RealName;
                TxtCurrentUserDept.Text = CurrentUser.Department;
                TxtCurrentUserRole.Text = CurrentUser.Role;
            }

            ApplyPermissions();
            UpdateMenuVisibility();

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            Activated += MainWindow_Activated;
            MainContentFrame.Navigated += MainContentFrame_Navigated;
            MainContentFrame.NavigationFailed += MainContentFrame_NavigationFailed;
            PreviewMouseDown += MainWindow_PreviewMouseDown;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            UpdateCurrentPageActions();
        }

        public MainWindow()
        {
            InitializeComponent();
            _windowScope = App.CurrentProvider.CreateScope();
            _userContextService = _windowScope.ServiceProvider.GetRequiredService<IUserContextService>();
            _operationLogContextService = _windowScope.ServiceProvider.GetRequiredService<IDbOperationLogContextService>();
            _scopeFactory = _windowScope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
            _toDoNotificationPresenter = _windowScope.ServiceProvider.GetRequiredService<IToDoNotificationPresenter>();
            Closed += MainWindow_Closed;
            Activated += MainWindow_Activated;
            MainContentFrame.Navigated += MainContentFrame_Navigated;
            MainContentFrame.NavigationFailed += MainContentFrame_NavigationFailed;
            PreviewMouseDown += MainWindow_PreviewMouseDown;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            UpdateCurrentPageActions();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _toDoCenter = _windowScope.ServiceProvider.GetRequiredService<IToDoCenterService>();
            _toDoCenter.PropertyChanged += ToDoCenter_PropertyChanged;

            UpdateToDoBadge();
            StartSessionHeartbeat();
            _ = InitializeAfterLoginAsync();
        }

        private async Task InitializeAfterLoginAsync()
        {
            if (CurrentUser == null || _toDoCenter == null)
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var prefService = scope.ServiceProvider.GetRequiredService<IUserPreferenceService>();
                var preference = await prefService.GetOrCreateAsync(CurrentUser.Id).ConfigureAwait(true);
                await _toDoCenter.ApplyPreferenceAsync(preference).ConfigureAwait(true);

                UpdateToDoBadge();

                if (_toDoCenter.EnableToDoPopup && _toDoCenter.PendingCount > 0)
                {
                    _toDoNotificationPresenter.Show(
                        this,
                        _toDoCenter.Items,
                        OpenToDoItemAsync,
                        AcknowledgeToDosAsync);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] 登录后初始化失败: {ex.Message}");
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            StopSessionHeartbeat();
            Activated -= MainWindow_Activated;
            MainContentFrame.Navigated -= MainContentFrame_Navigated;
            MainContentFrame.NavigationFailed -= MainContentFrame_NavigationFailed;
            PreviewMouseDown -= MainWindow_PreviewMouseDown;
            PreviewKeyDown -= MainWindow_PreviewKeyDown;

            if (_toDoCenter != null)
            {
                _toDoCenter.PropertyChanged -= ToDoCenter_PropertyChanged;
                _toDoCenter.StopAutoRefresh();
            }

            ReleaseCurrentSession();
            _windowScope.Dispose();
        }

        private void StartSessionHeartbeat()
        {
            if (_sessionHeartbeatTimer != null)
            {
                return;
            }

            _sessionHeartbeatTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };

            _sessionHeartbeatTimer.Tick += SessionHeartbeatTimer_Tick;
            _sessionHeartbeatTimer.Start();
        }

        private void StopSessionHeartbeat()
        {
            if (_sessionHeartbeatTimer == null)
            {
                return;
            }

            _sessionHeartbeatTimer.Tick -= SessionHeartbeatTimer_Tick;
            _sessionHeartbeatTimer.Stop();
            _sessionHeartbeatTimer = null;
        }

        private void SessionHeartbeatTimer_Tick(object? sender, EventArgs e)
        {
            if (_isHandlingSessionInvalidation || _isSessionInvalid)
            {
                return;
            }

            EnsureSessionIsValid();
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            if (_isSessionInvalid)
            {
                return;
            }

            EnsureSessionIsValid();
        }

        private void MainWindow_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (EnsureSessionIsValid())
            {
                return;
            }

            e.Handled = true;
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (EnsureSessionIsValid())
            {
                return;
            }

            e.Handled = true;
        }

        private bool EnsureSessionIsValid()
        {
            if (_isHandlingSessionInvalidation || _isSessionInvalid)
            {
                return false;
            }

            string? sessionId = _userContextService.CurrentSessionId;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return false;
            }

            UserSessionHeartbeatResult heartbeatResult;
            using (var scope = _scopeFactory.CreateScope())
            {
                var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                heartbeatResult = userService.RefreshSession(sessionId);
            }

            if (heartbeatResult.IsValid)
            {
                return true;
            }

            _isHandlingSessionInvalidation = true;
            _isSessionInvalid = true;

            try
            {
                HandleSessionInvalidation(heartbeatResult);
            }
            finally
            {
                _isHandlingSessionInvalidation = false;
            }

            return false;
        }

        private void HandleSessionInvalidation(UserSessionHeartbeatResult heartbeatResult)
        {
            StopSessionHeartbeat();
            IsEnabled = false;
            _toDoCenter?.StopAutoRefresh();
            _userContextService.Clear();

            MessageBox.Show(
                heartbeatResult.Message,
                "登录状态失效",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            var loginWindow = new LoginWindow();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();

            Close();
        }

        private void ReleaseCurrentSession()
        {
            string? sessionId = _userContextService.CurrentSessionId;
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                using var scope = _scopeFactory.CreateScope();
                var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                userService.Logout(sessionId);
            }

            _userContextService.Clear();
        }

        private void ToDoCenter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IToDoCenterService.PendingCount))
            {
                UpdateToDoBadge();
            }
        }

        private void UpdateToDoBadge()
        {
            if (_toDoCenter == null || !_toDoCenter.EnableToDoBadge)
            {
                BdToDoBadge.Visibility = Visibility.Collapsed;
                return;
            }

            var count = _toDoCenter.PendingCount;
            if (count <= 0)
            {
                BdToDoBadge.Visibility = Visibility.Collapsed;
                return;
            }

            TxtToDoBadge.Text = count > 99 ? "99+" : count.ToString();
            BdToDoBadge.Visibility = Visibility.Visible;
        }

        private async void BtnToDoCenter_Click(object sender, RoutedEventArgs e)
        {
            if (_toDoCenter == null)
            {
                _toDoCenter = _windowScope.ServiceProvider.GetRequiredService<IToDoCenterService>();
                await _toDoCenter.RefreshAsync();
            }

            _toDoNotificationPresenter.Show(
                this,
                _toDoCenter.Items,
                OpenToDoItemAsync,
                AcknowledgeToDosAsync);
        }

        private void BtnGoBack_Click(object sender, RoutedEventArgs e)
        {
            GoBackCurrentPage();
        }

        private void BtnReturnHome_Click(object sender, RoutedEventArgs e)
        {
            ReturnHome();
        }

        public void GoBackCurrentPage()
        {
            if (!MainContentFrame.CanGoBack)
            {
                return;
            }

            MainContentFrame.GoBack();
        }

        public void ReturnHome()
        {
            try
            {
                ClearNavigationHistory();
                MainContentFrame.Content = null;
                TxtPageTitle.Text = "首页";
                SetActiveNavButton(null);
                UpdateCurrentPageActions();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "返回首页时发生错误：\n\n" + ex.Message,
                    "导航错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                UpdateCurrentPageActions();
            }
        }

        public void CloseCurrentPage()
        {
            ReturnHome();
        }

        public void NavigateToArchiveRegisterPage(int? recordId = null, ArchiveRegisterWorkspaceMode mode = ArchiveRegisterWorkspaceMode.Application)
        {
            Page page = mode switch
            {
                ArchiveRegisterWorkspaceMode.Approval => new ArchiveRegisterApprovalPage(recordId),
                _ => new ArchiveRegisterApplicationPage(recordId)
            };

            MainContentFrame.Navigate(page);
        }

        public void NavigateToArchiveDetailPage(
            int recordId,
            ArchiveDetailHighlightContext? searchHighlight = null,
            string? filterPoolMediaKind = null,
            int? filingFactId = null)
        {
            if (recordId <= 0)
            {
                MessageBox.Show("无效的记录编号。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TxtPageTitle.Text = searchHighlight == null
                ? "年度资料档案化管理（资料查看）"
                : "年度资料档案化管理（资料查看·检索定位）";
            MainContentFrame.Navigate(new ArchiveDetailPage(recordId, searchHighlight, filterPoolMediaKind, filingFactId));
        }

        private void MainContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            TxtPageTitle.Text = ResolvePageTitle(MainContentFrame.Content);
            _operationLogContextService.SetCurrentPage(TxtPageTitle.Text);
            UpdateActiveNavHighlight();
            UpdateCurrentPageActions();
        }

        /// <summary>
        /// 根据当前 Frame 内容高亮对应侧栏菜单项。
        /// </summary>
        private void UpdateActiveNavHighlight()
        {
            SetActiveNavButton(ResolveNavButtonForContent(MainContentFrame.Content));
        }

        private void SetActiveNavButton(Button? button)
        {
            if (ReferenceEquals(_activeNavButton, button))
            {
                return;
            }

            if (_activeNavButton != null)
            {
                _activeNavButton.Tag = null;
            }

            _activeNavButton = button;

            if (button != null)
            {
                button.Tag = "Active";
            }
        }

        private Button? ResolveNavButtonForContent(object? content)
        {
            return content switch
            {
                null => null,
                ProjectSettingPage => BtnProjectInfo,
                ArchiveRegisterSimulationPage => BtnArchiveSimulation,
                ArchiveRegisterApplicationPage => BtnArchiveRegisterApply,
                ArchiveRegisterApprovalPage => BtnArchiveRegisterApprove,
                ArchiveFilingPage => BtnArchiveFiling,
                ArchiveFilingLedgerPage => BtnArchiveFilingLedger,
                ArchiveSimulatedRelocationPage => BtnArchiveSimulatedRelocation,
                ArchiveElectronicRelocationPage => BtnArchiveElectronicRelocation,
                ArchiveRelocationLedgerPage => BtnArchiveRelocationLedger,
                ArchiveSearchPage => BtnArchiveSearch,
                ArchiveFilingSearchPage page => ResolveArchiveFilingSearchNavButton(page),
                ArchiveFilingSearchPoolPage => BtnArchiveFilingSearchPool,
                ArchiveOutboundApplyPage => BtnArchiveOutboundApply,
                ArchiveOutboundApprovalPage => BtnArchiveOutboundApproval,
                ArchiveReturnWorkbenchPage page => ResolveArchiveReturnNavButton(page),
                ArchiveCirculationLedgerPage => BtnArchiveCirculationLedger,
                ArchiveInventoryRegisterPage page => ResolveArchiveInventoryRegisterNavButton(page),
                ArchiveDisposalPage page => ResolveArchiveDisposalNavButton(page),
                NetworkInboundApplicationPage => BtnNetInboundApply,
                NetworkInboundApprovalPage => BtnNetInboundApprove,
                NetworkOutboundApplicationPage => BtnNetOutboundApply,
                NetworkOutboundApprovalPage => BtnNetOutboundApprove,
                NetworkOnNetDisposalPage => BtnNetDispose,
                TopoMapPage => BtnHistMap,
                AerialPhotoPage => BtnHistAerial,
                OtherMapPage => BtnOtherData,
                CabinetLayoutPage => BtnCabRegister,
                CabinetSearchPage => BtnCabSearch,
                HardDiskMediaPage => BtnDiskSearch,
                HardDiskMediumLedgerPage => BtnDiskRegister,
                HardDiskMediaOutboundApplicationPage => BtnDiskBorrow,
                HardDiskMediaApprovalPage => BtnDiskApproval,
                HardDiskMediaReturnRegistrationPage page => ResolveHardDiskReturnNavButton(page),
                HardDiskInventoryRegisterPage => BtnDiskInventoryRegister,
                HardDiskDisposalPage => BtnDiskOffWarehouse,
                HardDiskMediaTransactionPage => BtnDiskDispose,
                OpticalDiscMediaPage => BtnOpticalDiscOverview,
                OpticalDiscMediumLedgerPage => BtnOpticalDiscLedger,
                UserManagementPage => BtnUserMgr,
                DeptSettingPage => BtnDeptMgr,
                RoleSettingPage => BtnRoleMgr,
                ServerPathSettingPage => BtnServerPathMgr,
                TestPreparationPage => BtnTestPreparation,
                AdvancedDataPage => BtnAdvancedData,
                BusinessLogicSettingsPage => BtnBusinessLogicSettings,
                UserPreferencePage => BtnUserPreference,
                DbOperationLogPage => BtnDbOperationLog,
                _ => null
            };
        }

        private Button? ResolveArchiveFilingSearchNavButton(ArchiveFilingSearchPage page)
        {
            if (page.DataContext is ArchiveFilingSearchViewModel viewModel)
            {
                if (string.Equals(viewModel.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
                {
                    return BtnArchiveElectronicFilingSearch;
                }

                if (string.Equals(viewModel.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal))
                {
                    return BtnArchiveSimulatedFilingSearch;
                }
            }

            return BtnArchiveSearch;
        }

        private Button ResolveArchiveReturnNavButton(ArchiveReturnWorkbenchPage page) =>
            page.WorkspaceMode == ArchiveReturnWorkspaceMode.Application
                ? BtnArchiveReturnApply
                : BtnArchiveReturnApproval;

        private Button ResolveHardDiskReturnNavButton(HardDiskMediaReturnRegistrationPage page) =>
            page.WorkspaceMode == HardDiskReturnWorkspaceMode.Approval
                ? BtnDiskReturnApproval
                : BtnDiskReturnApply;

        private void MainContentFrame_NavigationFailed(object? sender, NavigationFailedEventArgs e)
        {
            e.Handled = true;
            Exception? ex = e.Exception;
            MessageBox.Show(
                this,
                "打开页面失败，已取消导航。\n\n" + (ex?.Message ?? "未知错误"),
                "导航错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            UpdateCurrentPageActions();
        }

        private void UpdateCurrentPageActions()
        {
            bool hasActivePage = MainContentFrame.Content is Page;
            bool canGoBack = hasActivePage && MainContentFrame.CanGoBack;

            if (FindName("BtnGoBack") is Button goBackButton)
            {
                goBackButton.IsEnabled = canGoBack;
                goBackButton.Visibility = hasActivePage ? Visibility.Visible : Visibility.Collapsed;
            }

            if (FindName("BtnReturnHome") is Button returnHomeButton)
            {
                returnHomeButton.IsEnabled = hasActivePage;
                returnHomeButton.Visibility = hasActivePage ? Visibility.Visible : Visibility.Collapsed;
            }

            MainContentFrame.Visibility = hasActivePage ? Visibility.Visible : Visibility.Collapsed;

            if (FindName("HomeContentPanel") is Grid homeContentPanel)
            {
                homeContentPanel.Visibility = hasActivePage ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void ClearNavigationHistory()
        {
            if (MainContentFrame.JournalOwnership != JournalOwnership.OwnsJournal)
            {
                return;
            }

            while (MainContentFrame.RemoveBackEntry() != null)
            {
            }
        }

        private static string ResolvePageTitle(object? content)
        {
            return content switch
            {
                null => "首页",
                ArchiveRegisterApplicationPage => "年度资料档案化管理（资料建档·建档申请）",
                ArchiveRegisterApprovalPage => "年度资料档案化管理（资料建档·申请审批）",
                ArchiveDetailPage => "年度资料档案化管理（资料查看）",
                ArchiveRegisterSimulationPage => "年度资料档案化管理（模拟测试·模拟登记_方式1）",
                ArchiveFilingPage => "年度资料档案化管理（资料建档·资料立档）",
                ArchiveFilingLedgerPage => "年度资料档案化管理（资料建档·立档台账）",
                ArchiveRelocationLedgerPage => "年度资料档案化管理（资料迁档·迁档台账）",
                ArchiveCirculationLedgerPage => "年度资料档案化管理（资料流转·流转台账）",
                ArchiveInventoryRegisterPage page => string.Equals(
                        page.MediaKind,
                        ArchiveInventoryRegisterDomainValues.MediaKindElectronic,
                        StringComparison.Ordinal)
                    ? "年度资料档案化管理（盘库登记·电子资料盘库）"
                    : "年度资料档案化管理（盘库登记·模拟资料盘库）",
                ArchiveDisposalPage page => string.Equals(
                        page.MediaKind,
                        ArchiveRegisterDomainValues.MediaKindElectronic,
                        StringComparison.Ordinal)
                    ? "年度资料档案化管理（离库处置·电子资料离库处置）"
                    : "年度资料档案化管理（离库处置·模拟资料离库处置）",
                ArchiveSimulatedRelocationPage => "年度资料档案化管理（资料迁档·模拟介质资料迁档）",
                ArchiveElectronicRelocationPage => "年度资料档案化管理（资料迁档·电子介质资料迁档）",
                ArchiveOutboundApplyPage => "年度资料档案化管理（资料流转·借出申请）",
                ArchiveOutboundApprovalPage => "年度资料档案化管理（资料流转·审批出库）",
                NetworkInboundApplicationPage => "年度资料出入网管理（入网申请）",
                NetworkInboundApprovalPage => "年度资料出入网管理（入网审批）",
                NetworkOutboundApplicationPage => "年度资料出入网管理（出网申请）",
                NetworkOutboundApprovalPage => "年度资料出入网管理（出网审批）",
                NetworkOnNetDisposalPage => "年度资料出入网管理（在网数据处置）",
                ArchiveReturnWorkbenchPage page => page.WorkspaceMode == ArchiveReturnWorkspaceMode.Application
                    ? "年度资料档案化管理（资料流转·归还申请）"
                    : "年度资料档案化管理（资料流转·审批入库）",
                ArchiveSearchPage => "年度资料档案化管理（资料检索·资料检索_方式1）",
                ProjectSettingPage => "年度项目管理（项目信息设置）",
                CabinetLayoutPage => "档案柜管理（档案柜登记）",
                CabinetSearchPage => "档案柜管理（档案柜检索）",
                TopoMapPage => "历史存档资料管理（地形图）",
                OtherMapPage => "历史存档资料管理（其他图件）",
                AerialPhotoPage => "历史存档资料管理（航摄影像）",
                HardDiskMediumLedgerPage => "介质管理（硬盘·初始登记）",
                OpticalDiscMediaPage => "介质管理（光盘·概览）",
                OpticalDiscMediumLedgerPage => "介质管理（光盘·流转台账）",
                HardDiskMediaOutboundApplicationPage => "介质管理（硬盘·出库申请）",
                HardDiskMediaReturnRegistrationPage page => page.WorkspaceMode == HardDiskReturnWorkspaceMode.Approval
                    ? "介质管理（硬盘·审批入库）"
                    : "介质管理（硬盘·归还申请）",
                HardDiskMediaTransactionPage => "介质管理（硬盘·硬盘台账）",
                HardDiskMediaApprovalPage => "介质管理（硬盘·出库审批）",
                HardDiskInventoryRegisterPage => "介质管理（硬盘·盘库登记）",
                HardDiskDisposalPage => "介质管理（硬盘·离库处置）",
                HardDiskMediaPage => "介质管理（硬盘·概览）",
                UserManagementPage => "系统设置（用户管理）",
                DeptSettingPage => "系统设置（部门设置）",
                RoleSettingPage => "系统设置（角色设置）",
                ServerPathSettingPage => "系统设置（服务器路径设置）",
                TestPreparationPage => "系统设置（测试准备）",
                UserPreferencePage => "系统设置（个人设置）",
                BusinessLogicSettingsPage => "系统设置（业务逻辑设置）",
                DbOperationLogPage => "系统设置（数据库操作日志）",
                AdvancedDataPage => "高级数据管理",
                _ => "首页"
            };
        }

        public Task OpenToDoItemAsync(ToDoItem item)
        {
            if (item == null) return Task.CompletedTask;

            if (item.BizType == "YearlyArchiveRegister")
            {
                using var scope = _scopeFactory.CreateScope();
                var reg = scope.ServiceProvider.GetRequiredService<IArchiveRegisterService>();
                var mode = reg.IsArchiveAdminUser(CurrentUser)
                    ? ArchiveRegisterWorkspaceMode.Approval
                    : ArchiveRegisterWorkspaceMode.Application;
                NavigateToArchiveRegisterPage(item.BizId > 0 ? item.BizId : null, mode);
                return Task.CompletedTask;
            }

            if (item.BizType == "YearlyArchiveFiling")
            {
                TxtPageTitle.Text = "年度资料档案化管理（资料建档·资料立档）";
                MainContentFrame.Navigate(new ArchiveFilingPage());
                return Task.CompletedTask;
            }

            if (item.BizType == "HardDiskMediaApplication")
            {
                NavigateToHardDiskApprovalPage(item.BizId);
                return Task.CompletedTask;
            }

            if (item.BizType == "HardDiskMediaReturnRegistration")
            {
                NavigateToHardDiskReturnPage(HardDiskReturnWorkspaceMode.Approval);
                return Task.CompletedTask;
            }

            if (item.BizType == "HardDiskDisposal")
            {
                TxtPageTitle.Text = "介质管理（硬盘·离库处置）";
                MainContentFrame.Navigate(new HardDiskDisposalPage());
                return Task.CompletedTask;
            }

            if (item.BizType == "ArchiveDisposal")
            {
                string mediaKind = ArchiveRegisterDomainValues.MediaKindSimulated;
                if (!string.IsNullOrWhiteSpace(item.Title)
                    && item.Title.Contains("电子资料离库处置", StringComparison.Ordinal))
                {
                    mediaKind = ArchiveRegisterDomainValues.MediaKindElectronic;
                }

                NavigateToArchiveDisposalPage(mediaKind);
                return Task.CompletedTask;
            }

            if (item.BizType == "YearlyArchiveOutboundApproval"
                || item.BizType == "YearlyArchiveOutboundHandover")
            {
                TxtPageTitle.Text = "年度资料档案化管理（资料流转·审批出库）";
                MainContentFrame.Navigate(new ArchiveOutboundApprovalPage(item.BizId));
                return Task.CompletedTask;
            }

            if (item.BizType == "NetworkInboundApproval" || item.BizType == "NetworkInboundHandover")
            {
                TxtPageTitle.Text = "年度资料出入网管理（入网审批）";
                MainContentFrame.Navigate(new NetworkInboundApprovalPage(item.BizId));
                return Task.CompletedTask;
            }

            if (item.BizType == "NetworkOutboundApproval" || item.BizType == "NetworkOutboundHandover")
            {
                TxtPageTitle.Text = "年度资料出入网管理（出网审批）";
                MainContentFrame.Navigate(new NetworkOutboundApprovalPage(item.BizId));
                return Task.CompletedTask;
            }

            if (item.BizType == "HardDiskMediaOutboundOverdue")
            {
                NavigateToHardDiskReturnPage(HardDiskReturnWorkspaceMode.Application);
                return Task.CompletedTask;
            }

            if (item.BizType == "YearlyArchiveReturn")
            {
                NavigateToArchiveReturnPage(ArchiveReturnWorkspaceMode.Approval);
                return Task.CompletedTask;
            }

            if (item.BizType == "YearlyArchiveReturnRecord")
            {
                NavigateToArchiveReturnPage(ArchiveReturnWorkspaceMode.Approval);
                return Task.CompletedTask;
            }

            MessageBox.Show($"暂不支持该待办类型跳转：{item.BizType}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        public async Task AcknowledgeToDosAsync(IEnumerable<ToDoItem> items)
        {
            if (_toDoCenter == null || !_toDoCenter.MarkAllAsReadOnAcknowledge)
            {
                return;
            }

            await _toDoCenter.MarkAsReadAsync(items);
        }

        private void ApplyPermissions()
        {
            if (CurrentUser == null)
            {
                return;
            }

            bool isSystemAdmin = IsSystemAdministrator();
            bool isArchiveAdmin = CanAccessArchiveRelocation();
            bool canFiling = CanAccessArchiveFiling();
            bool canMediaAdmin = CanAccessArchiveMediaAdmin();
            // 申请菜单：仅部门资料管理员（不含资料室）；系统管理员保留运维例外。
            bool canSubmitApplication = CanAccessDepartmentArchiveApply() || isSystemAdmin;

            SystemSettingsExpander.Visibility = Visibility.Visible;
            SystemSettingsExpander.IsEnabled = true;

            ExpYearlyArchive.IsEnabled = true;
            ExpNetworkMgr.IsEnabled = true;
            ExpHistoryArchive.IsEnabled = true;
            ExpCabinets.IsEnabled = true;
            ExpMediaMgr.IsEnabled = true;
            ExpProjects.IsEnabled = true;

            SetNavButton(BtnUserMgr, isSystemAdmin);
            SetNavButton(BtnDeptMgr, isSystemAdmin);
            SetNavButton(BtnRoleMgr, isSystemAdmin);
            SetNavButton(BtnServerPathMgr, isSystemAdmin);
            SetNavButton(BtnTestPreparation, isSystemAdmin);
            SetNavButton(BtnAdvancedData, isSystemAdmin);
            SetNavButton(BtnBusinessLogicSettings, isSystemAdmin);
            SetNavButton(BtnUserPreference, true);
            SetNavButton(BtnDbOperationLog, true);

            SetNavButton(BtnProjectInfo, true);

            SetNavButton(BtnArchiveSimulation, true);
            // 申请：部门资料管理员；审批及后续办理：资料室资料管理员。
            SetNavButton(BtnArchiveRegisterApply, canSubmitApplication);
            SetNavButton(BtnArchiveRegisterApprove, isArchiveAdmin);
            SetNavButton(BtnArchiveFiling, canFiling);
            SetNavButton(BtnArchiveFilingLedger, canFiling);
            SetNavButton(BtnArchiveRelocationLedger, canFiling);
            SetNavButton(BtnArchiveCirculationLedger, canFiling);
            SetNavButton(BtnArchiveSearch, true);
            SetNavButton(BtnArchiveElectronicFilingSearch, true);
            SetNavButton(BtnArchiveSimulatedFilingSearch, true);
            SetNavButton(BtnArchiveFilingSearchPool, true);
            SetNavButton(BtnArchiveOutboundApply, canSubmitApplication);
            SetNavButton(BtnArchiveOutboundApproval, isArchiveAdmin);
            SetNavButton(BtnArchiveReturnApply, canSubmitApplication);
            SetNavButton(BtnArchiveReturnApproval, isArchiveAdmin);
            SetNavButton(BtnArchiveSimulatedRelocation, isArchiveAdmin);
            SetNavButton(BtnArchiveElectronicRelocation, isArchiveAdmin);
            SetNavButton(BtnArchiveSimulatedInventoryRegister, isArchiveAdmin);
            SetNavButton(BtnArchiveElectronicInventoryRegister, isArchiveAdmin);
            SetNavButton(BtnArchiveSimulatedDisposal, true);
            SetNavButton(BtnArchiveElectronicDisposal, true);

            SetNavButton(BtnNetInboundApply, canSubmitApplication);
            SetNavButton(BtnNetInboundApprove, isArchiveAdmin);
            SetNavButton(BtnNetOutboundApply, canSubmitApplication);
            SetNavButton(BtnNetOutboundApprove, isArchiveAdmin);
            SetNavButton(BtnNetDispose, isArchiveAdmin);

            SetNavButton(BtnHistMap, true);
            SetNavButton(BtnHistAerial, true);
            SetNavButton(BtnHistSatellite, true);
            SetNavButton(BtnHistDoc, true);
            SetNavButton(BtnOtherData, true);

            SetNavButton(BtnCabRegister, true);
            SetNavButton(BtnCabSearch, true);

            SetNavButton(BtnDiskSearch, true);
            SetNavButton(BtnDiskRegister, canMediaAdmin);
            SetNavButton(BtnDiskBorrow, canSubmitApplication);
            SetNavButton(BtnDiskApproval, canMediaAdmin);
            SetNavButton(BtnDiskReturnApply, canSubmitApplication);
            SetNavButton(BtnDiskReturnApproval, canMediaAdmin);
            SetNavButton(BtnDiskInventoryRegister, canMediaAdmin);
            SetNavButton(BtnDiskOffWarehouse, canMediaAdmin);
            SetNavButton(BtnDiskDispose, true);
            SetNavButton(BtnOpticalDiscOverview, true);
            SetNavButton(BtnOpticalDiscLedger, true);

            SetNavButton(BtnHelpDoc, true);
        }

        private static void SetNavButton(Button button, bool isEnabled)
        {
            button.Visibility = Visibility.Visible;
            button.IsEnabled = isEnabled;
        }

        private bool IsSystemAdministrator()
        {
            if (CurrentUser == null)
            {
                return false;
            }

            return CurrentUser.Role == "Administrator" || CurrentUser.Role == "管理员";
        }

        private bool CanAccessArchiveMediaAdmin()
        {
            if (CurrentUser == null)
            {
                return false;
            }

            string dept = CurrentUser.Department?.Trim() ?? string.Empty;
            string role = CurrentUser.Role?.Trim() ?? string.Empty;

            return (string.Equals(dept, "资料室", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(role, "部门资料管理员", StringComparison.OrdinalIgnoreCase))
                   || IsSystemAdministrator();
        }

        private bool CanAccessArchiveFiling()
        {
            if (CurrentUser == null) return false;

            bool isAdmin = CurrentUser.Role == "Administrator" || CurrentUser.Role == "管理员";
            bool isArchiveRoomDataManager =
                CurrentUser.Department == "资料室" &&
                CurrentUser.Role == "部门资料管理员";

            return isAdmin || isArchiveRoomDataManager;
        }

        private bool CanAccessArchiveRelocation()
        {
            if (CurrentUser == null)
            {
                return false;
            }

            using var scope = _scopeFactory.CreateScope();
            var registerService = scope.ServiceProvider.GetRequiredService<IArchiveRegisterService>();
            return registerService.IsArchiveAdminUser(CurrentUser);
        }

        /// <summary>
        /// 部门资料管理员（不含资料室）：各类申请业务操作人。
        /// </summary>
        private bool CanAccessDepartmentArchiveApply()
        {
            if (CurrentUser == null)
            {
                return false;
            }

            using var scope = _scopeFactory.CreateScope();
            var registerService = scope.ServiceProvider.GetRequiredService<IArchiveRegisterService>();
            return registerService.IsDepartmentArchiveAdmin(CurrentUser);
        }

        private void BtnDeptSetting_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "系统设置（部门设置）";
            MainContentFrame.Navigate(new DeptSettingPage());
        }

        private void BtnRoleSetting_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "系统设置（角色设置）";
            MainContentFrame.Navigate(new RoleSettingPage());
        }

        private void BtnServerPathSetting_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "系统设置（服务器路径设置）";
            MainContentFrame.Navigate(new ServerPathSettingPage());
        }

        private void BtnUserManagement_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "系统设置（用户管理）";
            MainContentFrame.Navigate(new UserManagementPage());
        }

        private void BtnTestPreparation_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "系统设置（测试准备）";
            MainContentFrame.Navigate(new TestPreparationPage());
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要注销当前用户并切换账号吗？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                StopSessionHeartbeat();
                _toDoCenter?.StopAutoRefresh();
                ReleaseCurrentSession();

                var loginWindow = new LoginWindow();
                Application.Current.MainWindow = loginWindow;
                loginWindow.Show();
                Close();
            }
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要退出系统吗？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                StopSessionHeartbeat();
                _toDoCenter?.StopAutoRefresh();
                ReleaseCurrentSession();
                Application.Current.Shutdown();
            }
        }

        private void BtnCabRegister_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "档案柜管理（档案柜登记）";
            MainContentFrame.Navigate(new CabinetLayoutPage());
        }

        private void BtnCabSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "档案柜管理（档案柜检索）";
            MainContentFrame.Navigate(new CabinetSearchPage());
        }

        private void BtnTopoMap_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "历史存档资料管理（地形图）";
            MainContentFrame.Navigate(new TopoMapPage());
        }

        private void BtnOtherMap_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "历史存档资料管理（其他图件）";
            MainContentFrame.Navigate(new OtherMapPage());
        }

        private void BtnAerialPhoto_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "历史存档资料管理（航摄影像）";
            MainContentFrame.Navigate(new AerialPhotoPage());
        }

        private void BtnHistSatellite_Click(object sender, RoutedEventArgs e)
        {
            ShowMenuNotReady("历史存档资料管理（卫星影像）");
        }

        private void BtnHistDoc_Click(object sender, RoutedEventArgs e)
        {
            ShowMenuNotReady("历史存档资料管理（文档资料）");
        }

        private void BtnProjectSetting_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "年度项目管理（项目信息设置）";
            MainContentFrame.Navigate(new ProjectSettingPage());
        }

        private void BtnArchiveRegisterApply_Click(object sender, RoutedEventArgs e)
        {
            NavigateToArchiveRegisterPage(null, ArchiveRegisterWorkspaceMode.Application);
        }

        private void BtnArchiveRegisterApprove_Click(object sender, RoutedEventArgs e)
        {
            NavigateToArchiveRegisterPage(null, ArchiveRegisterWorkspaceMode.Approval);
        }

        private void BtnArchiveSimulation_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "年度资料档案化管理（模拟测试·模拟登记_方式1）";
            MainContentFrame.Navigate(new ArchiveRegisterSimulationPage());
        }

        private void BtnAdvancedData_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "高级数据管理";
            MainContentFrame.Navigate(new AdvancedDataPage());
        }

        private void BtnArchiveFilingLedger_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "年度资料档案化管理（资料建档·立档台账）";

            if (!CanAccessArchiveFiling())
            {
                MessageBox.Show(
                    "抱歉，您没有【立档台账】的访问权限（仅资料室资料管理员可操作）。",
                    "权限提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MainContentFrame.Navigate(new ArchiveFilingLedgerPage());
        }

        public void NavigateToArchiveFilingLedger(int filingFactId)
        {
            if (!CanAccessArchiveFiling())
            {
                MessageBox.Show(
                    "抱歉，您没有【立档台账】的访问权限（仅资料室资料管理员可操作）。",
                    "权限提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ArchiveFilingLedgerNavigationState.PendingFilingFactId = filingFactId;
            TxtPageTitle.Text = "年度资料档案化管理（资料建档·立档台账）";
            MainContentFrame.Navigate(new ArchiveFilingLedgerPage());
        }

        private void BtnArchiveRelocationLedger_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "年度资料档案化管理（资料迁档·迁档台账）";

            if (!CanAccessArchiveFiling())
            {
                MessageBox.Show(
                    "抱歉，您没有【迁档台账】的访问权限（仅资料室资料管理员可操作）。",
                    "权限提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MainContentFrame.Navigate(new ArchiveRelocationLedgerPage());
        }

        private void BtnArchiveCirculationLedger_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "年度资料档案化管理（资料流转·流转台账）";

            if (!CanAccessArchiveFiling())
            {
                MessageBox.Show(
                    "抱歉，您没有【流转台账】的访问权限（仅资料室资料管理员可操作）。",
                    "权限提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MainContentFrame.Navigate(new ArchiveCirculationLedgerPage());
        }

        private void BtnArchiveSimulatedInventoryRegister_Click(object sender, RoutedEventArgs e)
        {
            NavigateToArchiveInventoryRegisterPage(ArchiveInventoryRegisterDomainValues.MediaKindSimulated);
        }

        private void BtnArchiveElectronicInventoryRegister_Click(object sender, RoutedEventArgs e)
        {
            NavigateToArchiveInventoryRegisterPage(ArchiveInventoryRegisterDomainValues.MediaKindElectronic);
        }

        private void NavigateToArchiveInventoryRegisterPage(string mediaKind)
        {
            if (!CanAccessArchiveRelocation())
            {
                MessageBox.Show("仅资料室管理员可办理盘库登记。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isElectronic = string.Equals(
                mediaKind,
                ArchiveInventoryRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal);
            TxtPageTitle.Text = isElectronic
                ? "年度资料档案化管理（盘库登记·电子资料盘库）"
                : "年度资料档案化管理（盘库登记·模拟资料盘库）";
            MainContentFrame.Navigate(new ArchiveInventoryRegisterPage(mediaKind));
        }

        private Button? ResolveArchiveInventoryRegisterNavButton(ArchiveInventoryRegisterPage page)
        {
            return string.Equals(
                    page.MediaKind,
                    ArchiveInventoryRegisterDomainValues.MediaKindElectronic,
                    StringComparison.Ordinal)
                ? BtnArchiveElectronicInventoryRegister
                : BtnArchiveSimulatedInventoryRegister;
        }

        private void BtnArchiveFiling_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "年度资料档案化管理（资料建档·资料立档）";

            if (!CanAccessArchiveFiling())
            {
                MessageBox.Show(
                    "抱歉，您没有【资料立档】的操作权限（仅资料室资料管理员可操作）。",
                    "权限提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                MainContentFrame.Navigate(new ArchiveFilingPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "无法打开资料立档页面：\n\n" + ex.Message,
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnArchiveSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "年度资料档案化管理（资料检索·资料检索_方式1）";
            MainContentFrame.Navigate(new ArchiveSearchPage());
        }

        private void BtnArchiveElectronicFilingSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "年度资料档案化管理（资料检索·电子介质资料检索）";
            MainContentFrame.Navigate(new ArchiveFilingSearchPage(ArchiveRegisterDomainValues.MediaKindElectronic));
        }

        private void BtnArchiveSimulatedFilingSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "年度资料档案化管理（资料检索·模拟介质资料检索）";
            MainContentFrame.Navigate(new ArchiveFilingSearchPage(ArchiveRegisterDomainValues.MediaKindSimulated));
        }

        public void NavigateToArchiveFilingSearchPoolPage(string mediaKind)
        {
            TxtPageTitle.Text = string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                ? "年度资料档案化管理（资料检索·电子介质检索池）"
                : "年度资料档案化管理（资料检索·模拟介质检索池）";
            MainContentFrame.Navigate(new ArchiveFilingSearchPoolPage(mediaKind));
        }

        private void BtnArchiveFilingSearchPool_Click(object sender, RoutedEventArgs e)
        {
            NavigateToArchiveFilingSearchPoolPage(ArchiveRegisterDomainValues.MediaKindSimulated);
        }

        public void NavigateToArchiveOutboundApplyPage(int? recordId = null)
        {
            TxtPageTitle.Text = "年度资料档案化管理（资料流转·借出申请）";
            MainContentFrame.Navigate(new ArchiveOutboundApplyPage(recordId.GetValueOrDefault()));
        }

        private void BtnArchiveOutboundApply_Click(object sender, RoutedEventArgs e)
        {
            NavigateToArchiveOutboundApplyPage();
        }

        private void BtnArchiveOutboundApproval_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAccessArchiveRelocation())
            {
                MessageBox.Show("仅资料室管理员可办理审批出库。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TxtPageTitle.Text = "年度资料档案化管理（资料流转·审批出库）";
            MainContentFrame.Navigate(new ArchiveOutboundApprovalPage());
        }

        private void BtnArchiveReturnApply_Click(object sender, RoutedEventArgs e)
        {
            NavigateToArchiveReturnPage(ArchiveReturnWorkspaceMode.Application);
        }

        private void BtnArchiveReturnApproval_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAccessArchiveRelocation())
            {
                MessageBox.Show("仅资料室管理员可办理归还审批入库。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NavigateToArchiveReturnPage(ArchiveReturnWorkspaceMode.Approval);
        }

        private void NavigateToArchiveReturnPage(ArchiveReturnWorkspaceMode mode)
        {
            // 旧 Handover 入口统一并入审批入库。
            if (mode == ArchiveReturnWorkspaceMode.Handover)
            {
                mode = ArchiveReturnWorkspaceMode.Approval;
            }

            TxtPageTitle.Text = mode == ArchiveReturnWorkspaceMode.Application
                ? "年度资料档案化管理（资料流转·归还申请）"
                : "年度资料档案化管理（资料流转·审批入库）";
            MainContentFrame.Navigate(new ArchiveReturnWorkbenchPage(mode));
        }

        private void BtnArchiveSimulatedRelocation_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "年度资料档案化管理（资料流转·模拟介质资料迁档）";
            if (!CanAccessArchiveRelocation())
            {
                MessageBox.Show("仅资料室管理员可执行资料迁档。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MainContentFrame.Navigate(new ArchiveSimulatedRelocationPage());
        }

        private void BtnArchiveElectronicRelocation_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "年度资料档案化管理（资料流转·电子介质资料迁档）";
            if (!CanAccessArchiveRelocation())
            {
                MessageBox.Show("仅资料室管理员可执行资料迁档。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MainContentFrame.Navigate(new ArchiveElectronicRelocationPage());
        }

        private void BtnArchiveSimulatedDisposal_Click(object sender, RoutedEventArgs e)
        {
            NavigateToArchiveDisposalPage(ArchiveRegisterDomainValues.MediaKindSimulated);
        }

        private void BtnArchiveElectronicDisposal_Click(object sender, RoutedEventArgs e)
        {
            NavigateToArchiveDisposalPage(ArchiveRegisterDomainValues.MediaKindElectronic);
        }

        private void NavigateToArchiveDisposalPage(string mediaKind)
        {
            bool isElectronic = string.Equals(
                mediaKind,
                ArchiveRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal);
            TxtPageTitle.Text = isElectronic
                ? "年度资料档案化管理（离库处置·电子资料离库处置）"
                : "年度资料档案化管理（离库处置·模拟资料离库处置）";
            MainContentFrame.Navigate(new ArchiveDisposalPage(mediaKind));
        }

        private Button? ResolveArchiveDisposalNavButton(ArchiveDisposalPage page)
        {
            return string.Equals(
                page.MediaKind,
                ArchiveRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal)
                ? BtnArchiveElectronicDisposal
                : BtnArchiveSimulatedDisposal;
        }

        private void BtnNetInboundApply_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAccessDepartmentArchiveApply() && !IsSystemAdministrator())
            {
                MessageBox.Show("仅部门资料管理员可发起入网申请。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TxtPageTitle.Text = "年度资料出入网管理（入网申请）";
            MainContentFrame.Navigate(new NetworkInboundApplicationPage());
        }

        private void BtnNetInboundApprove_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAccessArchiveRelocation())
            {
                MessageBox.Show("仅资料室管理员可办理入网审批。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TxtPageTitle.Text = "年度资料出入网管理（入网审批）";
            MainContentFrame.Navigate(new NetworkInboundApprovalPage());
        }

        private void BtnNetOutboundApply_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAccessDepartmentArchiveApply() && !IsSystemAdministrator())
            {
                MessageBox.Show("仅部门资料管理员可发起出网申请。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TxtPageTitle.Text = "年度资料出入网管理（出网申请）";
            MainContentFrame.Navigate(new NetworkOutboundApplicationPage());
        }

        private void BtnNetOutboundApprove_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAccessArchiveRelocation())
            {
                MessageBox.Show("仅资料室管理员可办理出网审批。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TxtPageTitle.Text = "年度资料出入网管理（出网审批）";
            MainContentFrame.Navigate(new NetworkOutboundApprovalPage());
        }

        private void BtnNetDispose_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAccessArchiveRelocation())
            {
                MessageBox.Show("仅资料室管理员可办理在网数据处置。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TxtPageTitle.Text = "年度资料出入网管理（在网数据处置）";
            MainContentFrame.Navigate(new NetworkOnNetDisposalPage());
        }

        private void BtnDiskLedger_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "介质管理（硬盘·初始登记）";
            MainContentFrame.Navigate(new HardDiskMediumLedgerPage());
        }

        private void BtnOpticalDiscLedger_Click(object sender, RoutedEventArgs e)
        {
            NavigateToOpticalDiscLedgerPage();
        }

        private void BtnOpticalDiscOverview_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "介质管理（光盘·概览）";
            MainContentFrame.Navigate(new OpticalDiscMediaPage());
        }

        private void NavigateToOpticalDiscLedgerPage(
            string? initialStatus = null,
            OpticalDiscLedgerQuickFilter quickFilter = OpticalDiscLedgerQuickFilter.None,
            bool recentTransactionsOnly = false)
        {
            TxtPageTitle.Text = recentTransactionsOnly
                ? "介质管理（光盘·流转台账·近90天）"
                : "介质管理（光盘·流转台账）";
            MainContentFrame.Navigate(new OpticalDiscMediumLedgerPage(initialStatus, quickFilter, recentTransactionsOnly));
        }

        /// <summary>
        /// 光盘概览 KPI 卡片下钻到流转台账（可带初始筛选）。
        /// </summary>
        public void NavigateFromOpticalDiscOverviewKpi(OpticalDiscOverviewKpiKind kind)
        {
            switch (kind)
            {
                case OpticalDiscOverviewKpiKind.TotalMedia:
                    NavigateToOpticalDiscLedgerPage();
                    break;
                case OpticalDiscOverviewKpiKind.InStock:
                    NavigateToOpticalDiscLedgerPage(OpticalDiscMedium.StatusInStock);
                    break;
                case OpticalDiscOverviewKpiKind.OutTemporary:
                    NavigateToOpticalDiscLedgerPage(OpticalDiscMedium.StatusOut);
                    break;
                case OpticalDiscOverviewKpiKind.DamagedInStock:
                    NavigateToOpticalDiscLedgerPage(OpticalDiscMedium.StatusDamaged);
                    break;
                case OpticalDiscOverviewKpiKind.Destroyed:
                    NavigateToOpticalDiscLedgerPage(OpticalDiscMedium.StatusDestroyed);
                    break;
                case OpticalDiscOverviewKpiKind.NeedReturn:
                    NavigateToOpticalDiscLedgerPage(quickFilter: OpticalDiscLedgerQuickFilter.NeedReturn);
                    break;
                case OpticalDiscOverviewKpiKind.MissingLocation:
                    NavigateToOpticalDiscLedgerPage(quickFilter: OpticalDiscLedgerQuickFilter.MissingLocation);
                    break;
                case OpticalDiscOverviewKpiKind.RecentTransactions:
                    NavigateToOpticalDiscLedgerPage(recentTransactionsOnly: true);
                    break;
                case OpticalDiscOverviewKpiKind.OutboundWithoutKeeper:
                    NavigateToOpticalDiscLedgerPage(quickFilter: OpticalDiscLedgerQuickFilter.OutboundWithoutKeeper);
                    break;
                default:
                    NavigateToOpticalDiscLedgerPage();
                    break;
            }
        }

        private void BtnDiskApplication_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "介质管理（硬盘·出库申请）";
            MainContentFrame.Navigate(new HardDiskMediaOutboundApplicationPage());
        }

        private void BtnDiskOutboundApplication_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "介质管理（硬盘·出库申请）";
            MainContentFrame.Navigate(new HardDiskMediaOutboundApplicationPage());
        }

        private void BtnDiskApproval_Click(object sender, RoutedEventArgs e)
        {
            NavigateToHardDiskApprovalPage();
        }

        private void BtnDiskReturnApply_Click(object sender, RoutedEventArgs e)
        {
            NavigateToHardDiskReturnPage(HardDiskReturnWorkspaceMode.Application);
        }

        private void BtnDiskReturnApproval_Click(object sender, RoutedEventArgs e)
        {
            NavigateToHardDiskReturnPage(HardDiskReturnWorkspaceMode.Approval);
        }

        private void BtnDiskInventoryRegister_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "介质管理（硬盘·盘库登记）";
            MainContentFrame.Navigate(new HardDiskInventoryRegisterPage());
        }

        private void BtnDiskOffWarehouse_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "介质管理（硬盘·离库处置）";
            MainContentFrame.Navigate(new HardDiskDisposalPage());
        }

        private void NavigateToHardDiskReturnPage(HardDiskReturnWorkspaceMode mode)
        {
            NavigateToHardDiskReturnPage(mode, overdueOnly: false, matchAllYears: false);
        }

        private void NavigateToHardDiskReturnPage(
            HardDiskReturnWorkspaceMode mode,
            bool overdueOnly,
            bool matchAllYears)
        {
            TxtPageTitle.Text = mode == HardDiskReturnWorkspaceMode.Approval
                ? "介质管理（硬盘·审批入库）"
                : overdueOnly
                    ? "介质管理（硬盘·归还申请·逾期）"
                    : "介质管理（硬盘·归还申请）";
            MainContentFrame.Navigate(new HardDiskMediaReturnRegistrationPage(mode, overdueOnly, matchAllYears));
        }

        private void BtnDiskTransaction_Click(object sender, RoutedEventArgs e)
        {
            NavigateToHardDiskTransactionPage();
        }

        private void BtnDiskImportExport_Click(object sender, RoutedEventArgs e)
        {
            NavigateToHardDiskMedia(HardDiskMediaWorkbenchSection.Overview, "介质管理（硬盘·概览）");
        }

        private void BtnUserPreference_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "系统设置（个人设置）";
            MainContentFrame.Navigate(new UserPreferencePage());
        }

        private void BtnBusinessLogicSettings_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "系统设置（业务逻辑设置）";
            MainContentFrame.Navigate(new BusinessLogicSettingsPage());
        }

        private void BtnDbOperationLog_Click(object sender, RoutedEventArgs e)
        {
            TxtPageTitle.Text = "系统设置（数据库操作日志）";
            MainContentFrame.Navigate(new DbOperationLogPage());
        }

        private void BtnHelpDoc_Click(object sender, RoutedEventArgs e)
        {
            ShowMenuNotReady("帮助（帮助文档）");
        }

        private void ShowMenuNotReady(string title)
        {
            TxtPageTitle.Text = title;
            MessageBox.Show("该菜单对应功能暂未开放。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void NavigateToHardDiskMedia(HardDiskMediaWorkbenchSection section, string title)
        {
            TxtPageTitle.Text = title;
            MainContentFrame.Navigate(new HardDiskMediaPage(section));
        }

        private void NavigateToHardDiskApprovalPage()
        {
            NavigateToHardDiskApprovalPage(null, null, null, false);
        }

        private void NavigateToHardDiskApprovalPage(int applicationId)
        {
            NavigateToHardDiskApprovalPage(applicationId, null, null, false);
        }

        private void NavigateToHardDiskApprovalPage(
            int? applicationId,
            string? initialStatusLabel,
            bool? signedAttachmentUploadedFilter,
            bool matchAllYears)
        {
            TxtPageTitle.Text = "介质管理（硬盘·出库审批）";
            MainContentFrame.Navigate(new HardDiskMediaApprovalPage(
                applicationId,
                initialStatusLabel,
                signedAttachmentUploadedFilter,
                matchAllYears));
        }

        private void NavigateToHardDiskTransactionPage(
            string? initialStatus = null,
            string? initialLockFilter = null,
            HardDiskLedgerQuickFilter quickFilter = HardDiskLedgerQuickFilter.None)
        {
            TxtPageTitle.Text = "介质管理（硬盘·硬盘台账）";
            MainContentFrame.Navigate(new HardDiskMediaTransactionPage(initialStatus, initialLockFilter, quickFilter));
        }

        /// <summary>
        /// 硬盘概览 KPI 卡片下钻到对应业务列表（可带初始筛选）。
        /// </summary>
        public void NavigateFromHardDiskOverviewKpi(HardDiskOverviewKpiKind kind)
        {
            switch (kind)
            {
                case HardDiskOverviewKpiKind.TotalMedia:
                    NavigateToHardDiskTransactionPage();
                    break;
                case HardDiskOverviewKpiKind.BlankInStock:
                    NavigateToHardDiskTransactionPage(HardDiskMedium.StatusInStockBlank);
                    break;
                case HardDiskOverviewKpiKind.DataInStock:
                    NavigateToHardDiskTransactionPage(HardDiskMedium.StatusInStockData);
                    break;
                case HardDiskOverviewKpiKind.Borrowed:
                    NavigateToHardDiskTransactionPage(quickFilter: HardDiskLedgerQuickFilter.BorrowedTempOrLong);
                    break;
                case HardDiskOverviewKpiKind.DamagedInStock:
                    NavigateToHardDiskTransactionPage(HardDiskMedium.StatusInStockDamaged);
                    break;
                case HardDiskOverviewKpiKind.InStockLost:
                    NavigateToHardDiskTransactionPage(HardDiskMedium.StatusInStockLost);
                    break;
                case HardDiskOverviewKpiKind.PermanentTransfer:
                    NavigateToHardDiskTransactionPage(HardDiskMedium.StatusOutPermanent);
                    break;
                case HardDiskOverviewKpiKind.DisposedMedia:
                    NavigateToHardDiskTransactionPage(HardDiskMedium.StatusDisposed);
                    break;
                case HardDiskOverviewKpiKind.SubmittedApproval:
                    NavigateToHardDiskApprovalPage(null, ApplicationWorkflowStatus.TextSubmitted, null, matchAllYears: true);
                    break;
                case HardDiskOverviewKpiKind.PendingHandover:
                    NavigateToHardDiskApprovalPage(null, ApplicationWorkflowStatus.TextApproved, null, matchAllYears: true);
                    break;
                case HardDiskOverviewKpiKind.PendingSignedUpload:
                    NavigateToHardDiskApprovalPage(
                        null,
                        ApplicationWorkflowStatus.TextSignedUploaded,
                        signedAttachmentUploadedFilter: false,
                        matchAllYears: true);
                    break;
                case HardDiskOverviewKpiKind.PendingComplete:
                    NavigateToHardDiskApprovalPage(
                        null,
                        ApplicationWorkflowStatus.TextSignedUploaded,
                        signedAttachmentUploadedFilter: true,
                        matchAllYears: true);
                    break;
                case HardDiskOverviewKpiKind.OverdueNeedReturn:
                    NavigateToHardDiskReturnPage(
                        HardDiskReturnWorkspaceMode.Application,
                        overdueOnly: true,
                        matchAllYears: true);
                    break;
                case HardDiskOverviewKpiKind.Locked:
                    NavigateToHardDiskTransactionPage(initialLockFilter: HardDiskRegisterLockFilterSupport.Any);
                    break;
                case HardDiskOverviewKpiKind.PendingDisposal:
                    TxtPageTitle.Text = "介质管理（硬盘·离库处置）";
                    MainContentFrame.Navigate(new HardDiskDisposalPage(pendingInProgress: true, matchAllYears: true));
                    break;
                case HardDiskOverviewKpiKind.DraftInventory:
                    TxtPageTitle.Text = "介质管理（硬盘·盘库登记）";
                    MainContentFrame.Navigate(new HardDiskInventoryRegisterPage(
                        ApplicationWorkflowStatus.TextDraft,
                        matchAllYears: true));
                    break;
                case HardDiskOverviewKpiKind.NeedReturn:
                    NavigateToHardDiskTransactionPage(quickFilter: HardDiskLedgerQuickFilter.NeedReturn);
                    break;
                case HardDiskOverviewKpiKind.MissingLocation:
                    NavigateToHardDiskTransactionPage(quickFilter: HardDiskLedgerQuickFilter.MissingLocationInStock);
                    break;
                case HardDiskOverviewKpiKind.MissingLedger:
                    NavigateToHardDiskTransactionPage(quickFilter: HardDiskLedgerQuickFilter.MissingLedger);
                    break;
                case HardDiskOverviewKpiKind.OutboundWithoutKeeper:
                    NavigateToHardDiskTransactionPage(quickFilter: HardDiskLedgerQuickFilter.OutboundWithoutKeeper);
                    break;
                default:
                    NavigateToHardDiskTransactionPage();
                    break;
            }
        }

        private void UpdateMenuVisibility()
        {
            if (BtnAdvancedData != null)
            {
                BtnAdvancedData.Visibility = Visibility.Visible;
            }
        }
    }
}