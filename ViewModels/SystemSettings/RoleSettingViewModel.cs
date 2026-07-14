using System.Collections.ObjectModel;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class RoleSettingViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IDialogService _dialogService;

        private ObservableCollection<Role> _roles = new();
        public ObservableCollection<Role> Roles
        {
            get => _roles;
            set => SetProperty(ref _roles, value);
        }

        private Role? _selectedRole;
        public Role? SelectedRole
        {
            get => _selectedRole;
            set
            {
                if (SetProperty(ref _selectedRole, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public RoleSettingViewModel(IUserService userService, IDialogService dialogService)
        {
            _userService = userService;
            _dialogService = dialogService;

            RefreshCommand = new RelayCommand(_ => LoadData());
            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(_ => Edit(), _ => SelectedRole != null);
            DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedRole != null);

            LoadData();
        }

        private void LoadData()
        {
            var list = _userService.GetAllRoles();
            Roles = new ObservableCollection<Role>(list);
        }

        private void Add()
        {
            if (_dialogService.ShowRoleEditDialog(null))
            {
                LoadData();
            }
        }

        private void Edit()
        {
            if (SelectedRole == null) return;

            if (_dialogService.ShowRoleEditDialog(SelectedRole))
            {
                LoadData();
            }
        }

        private void Delete()
        {
            if (SelectedRole == null) return;

            if (_dialogService.ShowConfirm($"确定要删除角色 [{SelectedRole.Name}] 吗？", "警告"))
            {
                try
                {
                    _userService.DeleteRole(SelectedRole.Id);
                    LoadData();
                }
                catch
                {
                    _dialogService.ShowError("删除失败。");
                }
            }
        }
    }
}