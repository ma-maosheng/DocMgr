using System.Collections.ObjectModel;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 台账页的实施年度与项目筛选。选「全部」时同名项目带上实施年度；选定项目后按项目编号查询。
    /// </summary>
    public sealed class LedgerProjectFilter : ViewModelBase
    {
        private readonly Action? _onProjectChanged;
        private string _selectedImplementYear = ProjectFilterOptionFactory.AllImplementYearsOption;
        private int? _selectedProjectId;
        private bool _suppressReload;

        public LedgerProjectFilter(Action? onProjectChanged = null)
        {
            _onProjectChanged = onProjectChanged;
        }

        public ObservableCollection<string> ImplementYears { get; } = new()
        {
            ProjectFilterOptionFactory.AllImplementYearsOption
        };

        public ObservableCollection<ProjectFilterOption> ProjectOptions { get; } = new();

        public string SelectedImplementYear
        {
            get => _selectedImplementYear;
            set
            {
                if (string.IsNullOrWhiteSpace(value) || !SetProperty(ref _selectedImplementYear, value))
                {
                    return;
                }

                if (_suppressReload)
                {
                    return;
                }

                SelectedProjectId = null;
                ReloadProjects();
            }
        }

        public int? SelectedProjectId
        {
            get => _selectedProjectId;
            set
            {
                if (SetProperty(ref _selectedProjectId, value))
                {
                    _onProjectChanged?.Invoke();
                }
            }
        }

        public IProjectService? ProjectService { get; set; }

        public void Load(IProjectService projectService)
        {
            ProjectService = projectService;
            _suppressReload = true;
            try
            {
                ImplementYears.Clear();
                ImplementYears.Add(ProjectFilterOptionFactory.AllImplementYearsOption);
                foreach (string year in ProjectFilterOptionFactory.ListImplementYears(projectService))
                {
                    ImplementYears.Add(year);
                }

                if (!ImplementYears.Contains(_selectedImplementYear))
                {
                    _selectedImplementYear = ProjectFilterOptionFactory.AllImplementYearsOption;
                    OnPropertyChanged(nameof(SelectedImplementYear));
                }
            }
            finally
            {
                _suppressReload = false;
            }

            ReloadProjects();
        }

        public void Reset()
        {
            SelectedImplementYear = ProjectFilterOptionFactory.AllImplementYearsOption;
            SelectedProjectId = null;
        }

        private void ReloadProjects()
        {
            if (ProjectService == null)
            {
                return;
            }

            ProjectFilterOptionFactory.Reload(ProjectOptions, ProjectService, SelectedImplementYear);
        }
    }
}
