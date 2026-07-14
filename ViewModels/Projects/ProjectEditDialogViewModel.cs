using System;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Projects
{
    public class ProjectEditDialogViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly IDialogService _dialogService;
        private readonly ProjectInfo? _project;

        private string _projectName = string.Empty;
        private string _projectCode = string.Empty;
        private string _implementYear = string.Empty;
        private string _capitalMgrDept = string.Empty;
        private string _remark = string.Empty;

        public ProjectEditDialogViewModel(IProjectService projectService, IDialogService dialogService, ProjectInfo? projectToEdit)
        {
            _projectService = projectService;
            _dialogService = dialogService;
            _project = projectToEdit;

            if (_project == null)
            {
                Title = "新增项目";
            }
            else
            {
                Title = "编辑项目";
                ProjectName = _project.ProjectName ?? string.Empty;
                ProjectCode = _project.ProjectCode ?? string.Empty;
                ImplementYear = _project.ImplementYear ?? string.Empty;
                CapitalMgrDept = _project.CapitalMgrDept ?? string.Empty;
                Remark = _project.Remark ?? string.Empty;
            }

            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Title { get; }

        public string ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        public string ProjectCode
        {
            get => _projectCode;
            set => SetProperty(ref _projectCode, value);
        }

        public string ImplementYear
        {
            get => _implementYear;
            set => SetProperty(ref _implementYear, value);
        }

        public string CapitalMgrDept
        {
            get => _capitalMgrDept;
            set => SetProperty(ref _capitalMgrDept, value);
        }

        public string Remark
        {
            get => _remark;
            set => SetProperty(ref _remark, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                _dialogService.ShowMessage("请输入项目名称");
                return;
            }

            if (string.IsNullOrWhiteSpace(ImplementYear))
            {
                _dialogService.ShowMessage("请输入实施年度");
                return;
            }

            try
            {
                if (_project == null)
                {
                    var newProject = new ProjectInfo
                    {
                        ProjectName = ProjectName.Trim(),
                        ProjectCode = ProjectCode.Trim(),
                        ImplementYear = ImplementYear.Trim(),
                        CapitalMgrDept = CapitalMgrDept.Trim(),
                        Remark = Remark.Trim()
                    };

                    _projectService.AddProject(newProject);
                }
                else
                {
                    _project.ProjectName = ProjectName.Trim();
                    _project.ProjectCode = ProjectCode.Trim();
                    _project.ImplementYear = ImplementYear.Trim();
                    _project.CapitalMgrDept = CapitalMgrDept.Trim();
                    _project.Remark = Remark.Trim();

                    _projectService.UpdateProject(_project);
                }

                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"保存失败: {ex.Message}");
            }
        }
    }
}