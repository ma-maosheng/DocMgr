using System.Collections.ObjectModel;
using DocMgr.Infrastructure.Seeding;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class UserManagementViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;

        private ObservableCollection<User> _users = new();
        public ObservableCollection<User> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        private User? _selectedUser;
        public User? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>系统管理员可维护；其余角色仅浏览列表。</summary>
        public bool CanMaintain { get; }

        /// <summary>页底必有角色说明。</summary>
        public string RequiredRolesHint => UserRoleDomainValues.RequiredRolesPageHint;

        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public UserManagementViewModel(
            IUserService userService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _userService = userService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            CanMaintain = ArchiveRegisterBusinessRules.IsSystemAdministrator(_userContextService.CurrentUser);

            RefreshCommand = new RelayCommand(_ => LoadData());
            AddCommand = new RelayCommand(_ => AddUser(), _ => CanMaintain);
            EditCommand = new RelayCommand(_ => EditUser(), _ => CanMaintain && SelectedUser != null);
            DeleteCommand = new RelayCommand(_ => DeleteUser(), _ => CanMaintain && SelectedUser != null);

            LoadData();
        }

        private void LoadData()
        {
            Users = new ObservableCollection<User>(_userService.GetAllUsers());
        }

        private void AddUser()
        {
            if (!EnsureCanMaintain())
            {
                return;
            }

            if (_dialogService.ShowUserEditDialog(null))
            {
                LoadData();
                _dialogService.ShowMessage("用户添加成功！");
            }
        }

        private void EditUser()
        {
            if (!EnsureCanMaintain() || SelectedUser == null)
            {
                return;
            }

            if (_dialogService.ShowUserEditDialog(SelectedUser))
            {
                LoadData();
                _dialogService.ShowMessage("用户更新成功！");
            }
        }

        private void DeleteUser()
        {
            if (!EnsureCanMaintain() || SelectedUser == null)
            {
                return;
            }

            if (SystemIdentitySeedCatalog.IsSeededUser(SelectedUser.LoginName)
                || string.Equals(SelectedUser.LoginName, DefaultAdministratorBootstrap.LoginName, StringComparison.Ordinal))
            {
                _dialogService.ShowError("系统必有账号不能删除！");
                return;
            }

            if (_dialogService.ShowConfirm($"确定要删除用户 [{SelectedUser.RealName}] 吗？", "警告"))
            {
                _userService.DeleteUser(SelectedUser.Id);
                LoadData();
                _dialogService.ShowMessage("用户已删除。");
            }
        }

        private bool EnsureCanMaintain()
        {
            if (CanMaintain)
            {
                return true;
            }

            _dialogService.ShowError("仅系统管理员可维护用户。");
            return false;
        }
    }
}
