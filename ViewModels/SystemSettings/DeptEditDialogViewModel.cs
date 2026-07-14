using System;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class DeptEditDialogViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IDialogService _dialogService;
        private readonly Department? _currentDept;
        private readonly bool _isAddMode;

        private string _name = string.Empty;
        private string _description = string.Empty;

        public DeptEditDialogViewModel(IUserService userService, IDialogService dialogService, Department? deptToEdit)
        {
            _userService = userService;
            _dialogService = dialogService;
            _currentDept = deptToEdit;
            _isAddMode = deptToEdit == null;

            TitleText = _isAddMode ? "新增部门" : "编辑部门";

            if (!_isAddMode && _currentDept != null)
            {
                Name = _currentDept.Name ?? string.Empty;
                Description = _currentDept.Description ?? string.Empty;
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
                _dialogService.ShowMessage("部门名称不能为空！");
                return;
            }

            try
            {
                if (_isAddMode)
                {
                    _userService.AddDepartment(new Department
                    {
                        Name = name,
                        Description = desc
                    });
                }
                else
                {
                    if (_currentDept == null)
                    {
                        _dialogService.ShowError("当前编辑对象无效。");
                        return;
                    }

                    _currentDept.Name = name;
                    _currentDept.Description = desc;
                    _userService.UpdateDepartment(_currentDept);
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