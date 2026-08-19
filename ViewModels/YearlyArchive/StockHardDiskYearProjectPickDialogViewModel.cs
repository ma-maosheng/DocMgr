using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.Projects;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 存量硬盘直办立档：查看并选用指定年度已有项目。
    /// </summary>
    public sealed class StockHardDiskYearProjectPickDialogViewModel : ViewModelBase
    {
        private ProjectInfo? _selectedProject;

        public StockHardDiskYearProjectPickDialogViewModel(string year, IReadOnlyList<ProjectInfo> projects)
        {
            Year = year?.Trim() ?? string.Empty;
            Projects = new ObservableCollection<ProjectInfo>(projects ?? Array.Empty<ProjectInfo>());
            SelectedProject = Projects.FirstOrDefault();
            ConfirmCommand = new RelayCommand(_ => { }, _ => SelectedProject != null);
        }

        public string Year { get; }

        public string Title => string.IsNullOrWhiteSpace(Year) ? "年度已有项目" : $"{Year} 年度已有项目";

        public string HintText => Projects.Count == 0
            ? "该年度库内尚无项目。若确为新项目，可关闭本窗后继续使用当前名称。"
            : "若本次资料属于下列已有项目，请选用其名称与编号，避免同一年度项目被起成多个名字。";

        public ObservableCollection<ProjectInfo> Projects { get; }

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

        public ICommand ConfirmCommand { get; }
    }
}
