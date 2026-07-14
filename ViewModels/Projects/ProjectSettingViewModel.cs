using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Projects
{
    public class ProjectSettingViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly IDialogService _dialogService;

        // === Properties ===

        private ObservableCollection<ProjectInfo> _projects = new();
        public ObservableCollection<ProjectInfo> Projects
        {
            get => _projects;
            set => SetProperty(ref _projects, value);
        }

        private ProjectInfo? _selectedProject;
        public ProjectInfo? SelectedProject
        {
            get => _selectedProject;
            set
            {
                if (SetProperty(ref _selectedProject, value))
                {
                    // 更新命令的可执行状态
                    // [修复] RelayCommand 使用 CommandManager 管理事件，无需手动 RaiseCanExecuteChanged
                    // 如果确实需要强制刷新，可以使用 CommandManager.InvalidateRequerySuggested();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _searchYear = string.Empty;
        public string SearchYear
        {
            get => _searchYear;
            set => SetProperty(ref _searchYear, value);
        }

        private string _searchKeyword = string.Empty;
        public string SearchKeyword
        {
            get => _searchKeyword;
            set => SetProperty(ref _searchKeyword, value);
        }

        // === Commands ===
        public RelayCommand SearchCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public ProjectSettingViewModel(IProjectService projectService, IDialogService dialogService)
        {
            _projectService = projectService;
            _dialogService = dialogService;

            // 初始化集合
            Projects = new ObservableCollection<ProjectInfo>();

            // 初始化命令
            SearchCommand = new RelayCommand(_ => LoadData());
            RefreshCommand = new RelayCommand(_ => Refresh());
            AddCommand = new RelayCommand(_ => AddProject());
            EditCommand = new RelayCommand(_ => EditProject(), _ => SelectedProject != null);
            DeleteCommand = new RelayCommand(_ => DeleteProject(), _ => SelectedProject != null);

            // 初始加载
            LoadData();
        }

        private void LoadData()
        {
            var list = _projectService.SearchProjects(SearchYear, SearchKeyword);
            Projects = new ObservableCollection<ProjectInfo>(list);
        }

        private void Refresh()
        {
            SearchYear = "";
            SearchKeyword = "";
            LoadData();
        }

        private void AddProject()
        {
            // 修改：使用 DialogService
            if (_dialogService.ShowProjectEditDialog(null))
            {
                // 如果对话框返回 true，说明保存成功，刷新列表
                LoadData();
            }
        }

        private void EditProject()
        {
            if (SelectedProject == null) return;

            // 修改：使用 DialogService
            if (_dialogService.ShowProjectEditDialog(SelectedProject))
            {
                // 编辑成功也刷新列表
                LoadData();
            }
        }

        private void DeleteProject()
        {
            if (SelectedProject == null) return;

            if (_dialogService.ShowConfirm($"确定要删除项目 [{SelectedProject.ProjectName}] 吗？", "警告"))
            {
                _projectService.DeleteProject(SelectedProject.Id);
                LoadData();
                _dialogService.ShowMessage("删除成功。");
            }
        }
    }
}