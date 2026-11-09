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

        private ObservableCollection<ProjectSettingRow> _projects = new();
        public ObservableCollection<ProjectSettingRow> Projects
        {
            get => _projects;
            set => SetProperty(ref _projects, value);
        }

        private ProjectSettingRow? _selectedProject;
        public ProjectSettingRow? SelectedProject
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
        public RelayCommand ExportCommand { get; }
        public RelayCommand ImportCommand { get; }

        public ProjectSettingViewModel(
            IProjectService projectService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _projectService = projectService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            Projects = new ObservableCollection<ProjectSettingRow>();

            SearchCommand = new RelayCommand(_ => LoadData());
            RefreshCommand = new RelayCommand(_ => Refresh());
            AddCommand = new RelayCommand(_ => AddProject(), _ => CanMaintainProjects);
            EditCommand = new RelayCommand(_ => EditProject(), _ => CanMaintainProjects && SelectedProject != null);
            DeleteCommand = new RelayCommand(_ => DeleteProject(), _ => CanMaintainProjects && SelectedProject != null);
            CopyProjectNameCommand = new RelayCommand(_ => CopyProjectName(), _ => CanCopyProjectName());
            ExportCommand = new RelayCommand(async _ => await ExportProjectsAsync());
            ImportCommand = new RelayCommand(async _ => await ImportProjectsAsync(), _ => CanMaintainProjects);

            LoadData();
        }

        private void LoadData()
        {
            List<ProjectInfo> list = _projectService.SearchProjects(SearchYear, SearchKeyword);
            IReadOnlySet<int> referencedIds = _projectService.FindReferencedProjectIds(list);
            Projects = new ObservableCollection<ProjectSettingRow>(
                list.Select(project => new ProjectSettingRow(project, referencedIds.Contains(project.Id))));
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

            if (_dialogService.ShowProjectEditDialog(SelectedProject.Project))
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

        private async Task ExportProjectsAsync()
        {
            List<ProjectInfo> projects = Projects
                .Select(row => new ProjectInfo
                {
                    ProjectName = row.ProjectName,
                    ProjectCode = row.ProjectCode,
                    ImplementYear = row.ImplementYear,
                    CapitalMgrDept = row.CapitalMgrDept,
                    Remark = row.Remark
                })
                .ToList();
            if (projects.Count == 0)
            {
                _dialogService.ShowMessage("当前列表没有可导出的项目。");
                return;
            }

            string? filePath = _dialogService.SaveFileDialog(
                "Excel Files|*.xlsx",
                "导出项目信息",
                $"项目信息_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _dialogService.SetBusyState(true);
            try
            {
                await Task.Run(() => _projectService.ExportProjects(filePath, projects));
                _dialogService.ShowMessage($"已导出当前列表 {projects.Count} 条：\n{filePath}", "完成");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"导出失败: {ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private async Task ImportProjectsAsync()
        {
            if (!EnsureCanMaintainProjects())
            {
                return;
            }

            string? filePath = _dialogService.OpenFileDialog("Excel Files|*.xlsx;*.xls", "选择项目信息 Excel");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                IReadOnlyList<string> sheetNames = await Task.Run(() => _projectService.GetProjectExcelSheetNames(filePath));
                if (sheetNames.Count == 0)
                {
                    _dialogService.ShowMessage("所选文件没有可导入的工作表。");
                    return;
                }

                string? sheetName = sheetNames.Count == 1
                    ? sheetNames[0]
                    : _dialogService.ShowSheetSelectionDialog(sheetNames.ToList(), "选择项目信息工作表")?.SheetName;
                if (string.IsNullOrWhiteSpace(sheetName))
                {
                    return;
                }

                if (!_dialogService.ShowConfirm(
                        "只新增库内还没有的项目。\n同一实施年度下，项目名称已存在且内容完全相同的行会跳过。\n内容不一致，或文件里同一项目互相矛盾的行不会导入，也不会修改库内记录。\n实施年度若合并了多行，这些行都按该合并单元格中的年度导入。",
                        "导入项目信息"))
                {
                    return;
                }

                string message;
                _dialogService.SetBusyState(true);
                try
                {
                    message = await Task.Run(() => _projectService.ImportProjects(filePath, sheetName));
                    LoadData();
                }
                finally
                {
                    _dialogService.SetBusyState(false);
                }

                _dialogService.ShowMessage(message, "导入项目信息");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"导入失败: {ex.Message}");
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
