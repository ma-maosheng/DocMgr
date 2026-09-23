using System.Collections.ObjectModel;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.SystemSettings;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    /// <summary>
    /// 角色概览：只读展示系统角色列表与菜单权限矩阵；不提供在线增删改。
    /// 调整账号权限请到「用户管理」；角色定义以种子数据为准。
    /// </summary>
    public class RoleSettingViewModel : ViewModelBase
    {
        private readonly IUserService _userService;

        private ObservableCollection<Role> _roles = new();

        public RoleSettingViewModel(IUserService userService)
        {
            _userService = userService;
            RefreshCommand = new RelayCommand(_ => LoadData());
            LoadData();
        }

        public ObservableCollection<Role> Roles
        {
            get => _roles;
            set => SetProperty(ref _roles, value);
        }

        /// <summary>按角色类别汇总的菜单权限矩阵（只读）。</summary>
        public IReadOnlyList<PermissionSettingSupport.PermissionMatrixRow> PermissionRows { get; } =
            PermissionSettingSupport.BuildMatrix();

        public RelayCommand RefreshCommand { get; }

        private void LoadData()
        {
            var list = _userService.GetAllRoles();
            Roles = new ObservableCollection<Role>(list);
        }
    }
}
