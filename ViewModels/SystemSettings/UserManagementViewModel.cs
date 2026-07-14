using System.Collections.ObjectModel;
using System.Windows;
using DocMgr.ViewModels.Base;
using DocMgr.Views; // for ImportMode if needed

namespace DocMgr.ViewModels.SystemSettings
{
    public class UserManagementViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IDialogService _dialogService;

        // === Properties ===
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
            set => SetProperty(ref _selectedUser, value);
        }

        // === Commands ===
        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public UserManagementViewModel(IUserService userService, IDialogService dialogService)
        {
            _userService = userService;
            _dialogService = dialogService;

            RefreshCommand = new RelayCommand(_ => LoadData());
            AddCommand = new RelayCommand(_ => AddUser());
            // Edit/Delete 需要选中项
            EditCommand = new RelayCommand(_ => EditUser(), _ => SelectedUser != null);
            DeleteCommand = new RelayCommand(_ => DeleteUser(), _ => SelectedUser != null);

            LoadData(); // 初始加载
        }

        private void LoadData()
        {
            var list = _userService.GetAllUsers();
            Users = new ObservableCollection<User>(list);
        }

        private void AddUser()
        {
            // 弹出新增窗口
            // 这里的 ShowUserEditDialog 内部（目前）完成了存库逻辑
            if (_dialogService.ShowUserEditDialog(null))
            {
                LoadData(); // 刷新
                _dialogService.ShowMessage("用户添加成功！");
            }
        }

        private void EditUser()
        {
            if (SelectedUser == null) return;

            // 弹出编辑窗口
            if (_dialogService.ShowUserEditDialog(SelectedUser))
            {
                LoadData(); // 刷新
                _dialogService.ShowMessage("用户更新成功！");
            }
        }

        private void DeleteUser()
        {
            if (SelectedUser == null) return;

            if (SelectedUser.LoginName == "admin")
            {
                _dialogService.ShowError("系统默认管理员不能删除！");
                return;
            }

            if (_dialogService.ShowConfirm($"确定要删除用户 [{SelectedUser.RealName}] 吗？", "警告"))
            {
                _userService.DeleteUser(SelectedUser.Id);
                LoadData();
                _dialogService.ShowMessage("用户已删除。");
            }
        }
    }
}