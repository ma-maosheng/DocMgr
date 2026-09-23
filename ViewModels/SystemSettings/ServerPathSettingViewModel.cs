using System.Collections.ObjectModel;
using DocMgr.Services.SystemSettings;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class ServerPathSettingViewModel : ViewModelBase
    {
        private readonly IServerPathSettingService _serverPathSettingService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;

        private ObservableCollection<ServerPathSetting> _settings = new();
        public ObservableCollection<ServerPathSetting> Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        private ServerPathSetting? _selectedSetting;
        public ServerPathSetting? SelectedSetting
        {
            get => _selectedSetting;
            set
            {
                if (SetProperty(ref _selectedSetting, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>网管负责人可维护；其余角色仅浏览列表。</summary>
        public bool CanMaintain { get; }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public ServerPathSettingViewModel(
            IServerPathSettingService serverPathSettingService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _serverPathSettingService = serverPathSettingService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            CanMaintain = ServerPathSettingPermissionSupport.CanMaintain(_userContextService.CurrentUser);

            RefreshCommand = new RelayCommand(_ => LoadData());
            AddCommand = new RelayCommand(_ => Add(), _ => CanMaintain);
            EditCommand = new RelayCommand(_ => Edit(), _ => CanMaintain && SelectedSetting != null);
            DeleteCommand = new RelayCommand(_ => Delete(), _ => CanMaintain && SelectedSetting != null);

            LoadData();
        }

        private void LoadData()
        {
            var list = _serverPathSettingService.GetAll();
            Settings = new ObservableCollection<ServerPathSetting>(list);
        }

        private void Add()
        {
            if (!EnsureCanMaintain())
            {
                return;
            }

            if (_dialogService.ShowServerPathSettingEditDialog(null))
            {
                LoadData();
            }
        }

        private void Edit()
        {
            if (!EnsureCanMaintain() || SelectedSetting == null)
            {
                return;
            }

            if (_dialogService.ShowServerPathSettingEditDialog(SelectedSetting))
            {
                LoadData();
            }
        }

        private void Delete()
        {
            if (!EnsureCanMaintain() || SelectedSetting == null)
            {
                return;
            }

            if (_dialogService.ShowConfirm(
                    $"确定要删除路径 [{SelectedSetting.PathName}]（{SelectedSetting.DepartmentName}）吗？",
                    "警告"))
            {
                try
                {
                    _serverPathSettingService.Delete(SelectedSetting.Id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError($"删除失败：{ex.Message}");
                }
            }
        }

        private bool EnsureCanMaintain()
        {
            if (CanMaintain)
            {
                return true;
            }

            _dialogService.ShowError(ServerPathSettingPermissionSupport.DeniedMessage);
            return false;
        }
    }
}
