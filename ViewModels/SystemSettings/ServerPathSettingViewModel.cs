using System.Collections.ObjectModel;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class ServerPathSettingViewModel : ViewModelBase
    {
        private readonly IServerPathSettingService _serverPathSettingService;
        private readonly IDialogService _dialogService;

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

        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public ServerPathSettingViewModel(
            IServerPathSettingService serverPathSettingService,
            IDialogService dialogService)
        {
            _serverPathSettingService = serverPathSettingService;
            _dialogService = dialogService;

            RefreshCommand = new RelayCommand(_ => LoadData());
            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(_ => Edit(), _ => SelectedSetting != null);
            DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedSetting != null);

            LoadData();
        }

        private void LoadData()
        {
            var list = _serverPathSettingService.GetAll();
            Settings = new ObservableCollection<ServerPathSetting>(list);
        }

        private void Add()
        {
            if (_dialogService.ShowServerPathSettingEditDialog(null))
            {
                LoadData();
            }
        }

        private void Edit()
        {
            if (SelectedSetting == null)
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
            if (SelectedSetting == null)
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
    }
}
