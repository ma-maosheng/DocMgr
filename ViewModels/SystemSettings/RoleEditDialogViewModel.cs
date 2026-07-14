using System;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class RoleEditDialogViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IDialogService _dialogService;
        private readonly Role? _currentRole;
        private readonly bool _isAddMode;

        private string _name = string.Empty;
        private string _description = string.Empty;

        public RoleEditDialogViewModel(IUserService userService, IDialogService dialogService, Role? roleToEdit)
        {
            _userService = userService;
            _dialogService = dialogService;
            _currentRole = roleToEdit;
            _isAddMode = roleToEdit == null;

            TitleText = _isAddMode ? "新增角色" : "编辑角色";

            if (!_isAddMode && _currentRole != null)
            {
                Name = _currentRole.Name ?? string.Empty;
                Description = _currentRole.Description ?? string.Empty;
            }

            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string TitleText { get; }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private void Confirm()
        {
            string name = (Name ?? string.Empty).Trim();
            string desc = (Description ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(name))
            {
                _dialogService.ShowMessage("角色名称不能为空！");
                return;
            }

            try
            {
                if (_isAddMode)
                {
                    _userService.AddRole(new Role { Name = name, Description = desc });
                }
                else
                {
                    if (_currentRole == null)
                    {
                        _dialogService.ShowError("当前编辑对象无效。");
                        return;
                    }

                    _currentRole.Name = name;
                    _currentRole.Description = desc;
                    _userService.UpdateRole(_currentRole);
                }

                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"操作失败：{ex.Message}");
            }
        }
    }
}