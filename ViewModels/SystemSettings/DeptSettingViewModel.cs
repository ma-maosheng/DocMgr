using System.Collections.ObjectModel;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class DeptSettingViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;

        private ObservableCollection<Department> _departments = new();
        public ObservableCollection<Department> Departments
        {
            get => _departments;
            set => SetProperty(ref _departments, value);
        }

        private Department? _selectedDepartment;
        public Department? SelectedDepartment
        {
            get => _selectedDepartment;
            set
            {
                if (SetProperty(ref _selectedDepartment, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>系统管理员可维护；其余角色仅浏览列表。</summary>
        public bool CanMaintain { get; }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public DeptSettingViewModel(
            IUserService userService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _userService = userService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            CanMaintain = ArchiveRegisterBusinessRules.IsSystemAdministrator(_userContextService.CurrentUser);

            RefreshCommand = new RelayCommand(_ => LoadData());
            AddCommand = new RelayCommand(_ => Add(), _ => CanMaintain);
            EditCommand = new RelayCommand(_ => Edit(), _ => CanMaintain && SelectedDepartment != null);
            DeleteCommand = new RelayCommand(_ => Delete(), _ => CanMaintain && SelectedDepartment != null);

            LoadData();
        }

        private void LoadData()
        {
            var list = _userService.GetAllDepartments();
            Departments = new ObservableCollection<Department>(list);
        }

        private void Add()
        {
            if (!EnsureCanMaintain())
            {
                return;
            }

            if (_dialogService.ShowDeptEditDialog(null))
            {
                LoadData();
            }
        }

        private void Edit()
        {
            if (!EnsureCanMaintain() || SelectedDepartment == null)
            {
                return;
            }

            if (_dialogService.ShowDeptEditDialog(SelectedDepartment))
            {
                LoadData();
            }
        }

        private void Delete()
        {
            if (!EnsureCanMaintain() || SelectedDepartment == null)
            {
                return;
            }

            if (_dialogService.ShowConfirm($"确定要删除部门 [{SelectedDepartment.Name}] 吗？", "警告"))
            {
                try
                {
                    _userService.DeleteDepartment(SelectedDepartment.Id);
                    LoadData();
                }
                catch
                {
                    _dialogService.ShowError("删除失败，可能该部门已被使用。");
                }
            }
        }

        private bool EnsureCanMaintain()
        {
            if (CanMaintain)
            {
                return true;
            }

            _dialogService.ShowError("仅系统管理员可维护部门。");
            return false;
        }
    }
}
