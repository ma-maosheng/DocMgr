using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class UserEditDialogViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IDialogService _dialogService;
        private readonly User? _currentUser;
        private readonly bool _isAddMode;

        private string _loginName = string.Empty;
        private string _realName = string.Empty;
        private string _selectedDepartment = string.Empty;
        private string _selectedRole = string.Empty;
        private string _password = string.Empty;

        public UserEditDialogViewModel(IUserService userService, IDialogService dialogService, User? userToEdit)
        {
            _userService = userService;
            _dialogService = dialogService;

            _isAddMode = userToEdit == null;
            _currentUser = userToEdit;

            Title = _isAddMode ? "新增用户" : "编辑用户";

            if (!_isAddMode && _currentUser != null)
            {
                LoginName = _currentUser.LoginName ?? string.Empty;
                RealName = _currentUser.RealName ?? string.Empty;
                SelectedDepartment = _currentUser.Department ?? string.Empty;
                SelectedRole = _currentUser.Role ?? string.Empty;
            }

            DepartmentOptions = _userService.GetAllDepartments().Select(d => d.Name).ToList();
            RoleOptions = _userService.GetAllRoles().Select(r => r.Name).ToList();

            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Title { get; }

        public List<string> DepartmentOptions { get; }

        public List<string> RoleOptions { get; }

        public string LoginName
        {
            get => _loginName;
            set => SetProperty(ref _loginName, value);
        }

        public string RealName
        {
            get => _realName;
            set => SetProperty(ref _realName, value);
        }

        public string SelectedDepartment
        {
            get => _selectedDepartment;
            set => SetProperty(ref _selectedDepartment, value);
        }

        public string SelectedRole
        {
            get => _selectedRole;
            set => SetProperty(ref _selectedRole, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private void Confirm()
        {
            string login = (LoginName ?? string.Empty).Trim();
            string real = (RealName ?? string.Empty).Trim();
            string dept = (SelectedDepartment ?? string.Empty).Trim();
            string role = (SelectedRole ?? string.Empty).Trim();
            string pwd = Password ?? string.Empty;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(real))
            {
                _dialogService.ShowMessage("登录账号和真实姓名不能为空！", "提示");
                return;
            }

            try
            {
                if (_isAddMode)
                {
                    if (string.IsNullOrEmpty(pwd))
                    {
                        _dialogService.ShowMessage("新增用户时密码不能为空！", "提示");
                        return;
                    }

                    var newUser = new User
                    {
                        LoginName = login,
                        RealName = real,
                        Department = dept,
                        Role = role,
                        CreatedDate = DateTime.Now
                    };

                    _userService.AddUser(newUser, pwd);
                }
                else
                {
                    if (_currentUser == null)
                    {
                        _dialogService.ShowError("当前编辑对象无效。");
                        return;
                    }

                    _currentUser.LoginName = login;
                    _currentUser.RealName = real;
                    _currentUser.Department = dept;
                    _currentUser.Role = role;

                    _userService.UpdateUser(_currentUser, string.IsNullOrEmpty(pwd) ? null : pwd);
                }

                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"操作失败：{ex.Message}\n可能原因：登录账号已存在。", "错误");
            }
        }
    }
}