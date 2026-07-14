using System.Collections.ObjectModel;
using System.Linq;
using DocMgr.Models.Projects;
using DocMgr.Services.Interfaces;

namespace DocMgr.ViewModels.YearlyArchive
{
    internal static class ProjectFilterOptionFactory
    {
        public static void Reload(
            ObservableCollection<ProjectFilterOption> target,
            IProjectService projectService,
            string? selectedYear)
        {
            target.Clear();
            target.Add(new ProjectFilterOption { Id = null, Name = "全部项目" });

            string? year = string.IsNullOrWhiteSpace(selectedYear) || selectedYear == "全部年份"
                ? null
                : selectedYear.Trim();

            foreach (ProjectInfo project in projectService.SearchProjects(year, keyword: null)
                         .Where(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.ProjectName))
                         .OrderBy(item => item.ProjectName))
            {
                target.Add(new ProjectFilterOption
                {
                    Id = project.Id,
                    Name = project.ProjectName.Trim()
                });
            }
        }
    }
}
