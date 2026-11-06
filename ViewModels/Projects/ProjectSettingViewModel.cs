using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DocMgr.Services.Projects;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Projects
{
    public class ProjectSettingViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;

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

        /// <summary>资料管理员可新增、编辑、删除；其他人仅浏览与检索。</summary>
        public bool CanMaintainProjects =>
            ProjectSettingPermissionSupport.CanMaintain(_userContextService.CurrentUser);

        // === Commands ===
        public RelayCommand SearchCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand CopyProjectNameCommand { get; }

        public ProjectSettingViewModel(
            IProjectService projectService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _projectService = projectService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            Projects = new ObservableCollection<ProjectInfo>();

            SearchCommand = new RelayCommand(_ => LoadData());
            RefreshCommand = new RelayCommand(_ => Refresh());
            AddCommand = new RelayCommand(_ => AddProject(), _ => CanMaintainProjects);
            EditCommand = new RelayCommand(_ => EditProject(), _ => CanMaintainProjects && SelectedProject != null);
            DeleteCommand = new RelayCommand(_ => DeleteProject(), _ => CanMaintainProjects && SelectedProject != null);
            CopyProjectNameCommand = new RelayCommand(_ => CopyProjectName(), _ => CanCopyProjectName());

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
            if (!EnsureCanMaintainProjects())
            {
                return;
            }

            if (_dialogService.ShowProjectEditDialog(null))
            {
                LoadData();
            }
        }

        private void EditProject()
        {
            if (SelectedProject == null)
            {
                return;
            }

            if (!EnsureCanMaintainProjects())
            {
                return;
            }

            if (_dialogService.ShowProjectEditDialog(SelectedProject))
            {
                LoadData();
            }
        }

        private void DeleteProject()
        {
            if (SelectedProject == null)
            {
                return;
            }

            if (!EnsureCanMaintainProjects())
            {
                return;
            }

            if (_dialogService.ShowConfirm($"确定要删除项目 [{SelectedProject.ProjectName}] 吗？", "警告"))
            {
                try
                {
                    _projectService.DeleteProject(SelectedProject.Id);
                    LoadData();
                    _dialogService.ShowMessage("删除成功。");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError($"删除失败: {ex.Message}");
                }
            }
        }

        private bool CanCopyProjectName() =>
            !string.IsNullOrWhiteSpace(SelectedProject?.ProjectName);

        /// <summary>把当前选中项目的名称写入剪贴板。</summary>
        private void CopyProjectName()
        {
            string projectName = SelectedProject?.ProjectName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(projectName))
            {
                _dialogService.ShowMessage("请先选中要拷贝名称的项目。");
                return;
            }

            try
            {
                Clipboard.SetText(projectName);
                _dialogService.ShowMessage($"已拷贝项目名称：{projectName}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"拷贝项目名称失败: {ex.Message}");
            }
        }

        private bool EnsureCanMaintainProjects()
        {
            if (CanMaintainProjects)
            {
                return true;
            }

            _dialogService.ShowMessage(ProjectSettingPermissionSupport.MaintainDeniedMessage);
            return false;
        }
    }
}
