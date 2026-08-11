using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class ServerPathSettingEditDialogViewModel : ViewModelBase
    {
        private readonly IServerPathSettingService _serverPathSettingService;
        private readonly IUserService _userService;
        private readonly IDialogService _dialogService;
        private readonly ServerPathSetting? _currentSetting;
        private readonly bool _isAddMode;

        private string _selectedDepartment = ServerPathSettingDomainValues.PublicDepartment;
        private string _pathName = string.Empty;
        private string _physicalPath = string.Empty;
        private string _selectedPermission = ServerPathSettingDomainValues.PermissionReadWrite;
        private string _capacityTbText = string.Empty;

        public ServerPathSettingEditDialogViewModel(
            IServerPathSettingService serverPathSettingService,
            IUserService userService,
            IDialogService dialogService,
            ServerPathSetting? settingToEdit)
        {
            _serverPathSettingService = serverPathSettingService;
            _userService = userService;
            _dialogService = dialogService;
            _currentSetting = settingToEdit;
            _isAddMode = settingToEdit == null;

            TitleText = _isAddMode ? "新增服务器路径" : "编辑服务器路径";

            DepartmentOptions = BuildDepartmentOptions();
            PermissionOptions = ServerPathSettingDomainValues.PermissionOptions.ToList();

            if (!_isAddMode && _currentSetting != null)
            {
                SelectedDepartment = _currentSetting.DepartmentName ?? ServerPathSettingDomainValues.PublicDepartment;
                PathName = _currentSetting.PathName ?? string.Empty;
                PhysicalPath = _currentSetting.PhysicalPath ?? string.Empty;
                SelectedPermission = _currentSetting.Permission ?? ServerPathSettingDomainValues.PermissionReadWrite;
                CapacityTbText = _currentSetting.CapacityTb.ToString("0.####", CultureInfo.InvariantCulture);
            }

            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string TitleText { get; }

        public List<string> DepartmentOptions { get; }

        public List<string> PermissionOptions { get; }

        public string SelectedDepartment
        {
            get => _selectedDepartment;
            set => SetProperty(ref _selectedDepartment, value);
        }

        public string PathName
        {
            get => _pathName;
            set => SetProperty(ref _pathName, value);
        }

        public string PhysicalPath
        {
            get => _physicalPath;
            set => SetProperty(ref _physicalPath, value);
        }

        public string SelectedPermission
        {
            get => _selectedPermission;
            set => SetProperty(ref _selectedPermission, value);
        }

        public string CapacityTbText
        {
            get => _capacityTbText;
            set => SetProperty(ref _capacityTbText, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private List<string> BuildDepartmentOptions()
        {
            var options = new List<string> { ServerPathSettingDomainValues.PublicDepartment };
            options.AddRange(_userService.GetAllDepartments()
                .Select(item => item.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal));
            return options;
        }

        private void Confirm()
        {
            string department = (SelectedDepartment ?? string.Empty).Trim();
            string pathName = (PathName ?? string.Empty).Trim();
            string physicalPath = (PhysicalPath ?? string.Empty).Trim();
            string permission = (SelectedPermission ?? string.Empty).Trim();
            string capacityText = (CapacityTbText ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(department))
            {
                _dialogService.ShowMessage("请选择部门。");
                return;
            }

            if (string.IsNullOrWhiteSpace(pathName))
            {
                _dialogService.ShowMessage("路径名称不能为空。");
                return;
            }

            if (string.IsNullOrWhiteSpace(physicalPath))
            {
                _dialogService.ShowMessage("物理地址不能为空。");
                return;
            }

            if (string.IsNullOrWhiteSpace(permission))
            {
                _dialogService.ShowMessage("请选择权限。");
                return;
            }

            if (!decimal.TryParse(capacityText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal capacityTb)
                && !decimal.TryParse(capacityText, NumberStyles.Number, CultureInfo.CurrentCulture, out capacityTb))
            {
                _dialogService.ShowMessage("容量格式无效，请输入大于 0 的数字（单位 TB）。");
                return;
            }

            try
            {
                if (_isAddMode)
                {
                    _serverPathSettingService.Add(new ServerPathSetting
                    {
                        DepartmentName = department,
                        PathName = pathName,
                        PhysicalPath = physicalPath,
                        Permission = permission,
                        CapacityTb = capacityTb
                    });
                }
                else
                {
                    if (_currentSetting == null)
                    {
                        _dialogService.ShowError("当前编辑对象无效。");
                        return;
                    }

                    _currentSetting.DepartmentName = department;
                    _currentSetting.PathName = pathName;
                    _currentSetting.PhysicalPath = physicalPath;
                    _currentSetting.Permission = permission;
                    _currentSetting.CapacityTb = capacityTb;
                    _serverPathSettingService.Update(_currentSetting);
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
