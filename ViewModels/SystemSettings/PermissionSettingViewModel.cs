using System.Collections.ObjectModel;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    /// <summary>
    /// 权限设置页 ViewModel：按角色汇总展示各业务菜单的访问权限矩阵。
    /// 系统权限由「部门 + 角色」按业务规则判定，本页为只读查阅，不提供在线编辑。
    /// </summary>
    public class PermissionSettingViewModel : ViewModelBase
    {
        private readonly IUserService _userService;

        private ObservableCollection<Role> _roles = new();
        private Role? _selectedRole;

        public PermissionSettingViewModel(IUserService userService)
        {
            _userService = userService;

            RefreshCommand = new RelayCommand(_ => LoadData());
            LoadData();
        }

        /// <summary>系统现有角色列表。</summary>
        public ObservableCollection<Role> Roles
        {
            get => _roles;
            private set => SetProperty(ref _roles, value);
        }

        /// <summary>当前查看的角色。</summary>
        public Role? SelectedRole
        {
            get => _selectedRole;
            set => SetProperty(ref _selectedRole, value);
        }

        public RelayCommand RefreshCommand { get; }

        /// <summary>按角色类别汇总的菜单权限矩阵（只读）。</summary>
        public IReadOnlyList<PermissionSettingSupport.PermissionMatrixRow> PermissionRows { get; } =
            PermissionSettingSupport.BuildMatrix();

        private void LoadData()
        {
            var list = _userService.GetAllRoles()
                .OrderBy(role => role.Id)
                .ToList();
            Role? previous = SelectedRole;
            Roles = new ObservableCollection<Role>(list);
            SelectedRole = Roles.FirstOrDefault(role => role.Id == previous?.Id) ?? Roles.FirstOrDefault();
        }
    }
}
