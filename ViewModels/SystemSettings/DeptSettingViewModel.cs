using System.Collections.ObjectModel;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class DeptSettingViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IDialogService _dialogService;

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

        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public DeptSettingViewModel(IUserService userService, IDialogService dialogService)
        {
            _userService = userService;
            _dialogService = dialogService;

            RefreshCommand = new RelayCommand(_ => LoadData());
            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(_ => Edit(), _ => SelectedDepartment != null);
            DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedDepartment != null);

            LoadData();
        }

        private void LoadData()
        {
            var list = _userService.GetAllDepartments();
            Departments = new ObservableCollection<Department>(list);
        }

        private void Add()
        {
            if (_dialogService.ShowDeptEditDialog(null))
            {
                LoadData();
            }
        }

        private void Edit()
        {
            if (SelectedDepartment == null) return;

            if (_dialogService.ShowDeptEditDialog(SelectedDepartment))
            {
                LoadData();
            }
        }

        private void Delete()
        {
            if (SelectedDepartment == null) return;

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
    }
}