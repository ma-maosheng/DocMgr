using System.Collections.ObjectModel;
using System.Linq;
using DocMgr.Models.Projects;
using DocMgr.Services.Interfaces;

namespace DocMgr.ViewModels.YearlyArchive
{
    internal static class ProjectFilterOptionFactory
    {
        public const string AllImplementYearsOption = "全部";

        public static string? NormalizeImplementYear(string? selectedYear)
        {
            if (string.IsNullOrWhiteSpace(selectedYear)
                || selectedYear == AllImplementYearsOption
                || selectedYear == "全部年份"
                || selectedYear == "全部年度")
            {
                return null;
            }

            return selectedYear.Trim();
        }

        public static List<string> ListImplementYears(IProjectService projectService)
        {
            return projectService.SearchProjects(null, keyword: null)
                .Select(project => project.ImplementYear?.Trim() ?? string.Empty)
                .Where(year => year.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(year => year, StringComparer.Ordinal)
                .ToList();
        }

        public static void Reload(
            ObservableCollection<ProjectFilterOption> target,
            IProjectService projectService,
            string? implementYear)
        {
            string? year = NormalizeImplementYear(implementYear);
            Fill(target, projectService.SearchProjects(year, keyword: null), showImplementYear: year == null);
        }

        public static void Fill(
            ObservableCollection<ProjectFilterOption> target,
            IEnumerable<ProjectInfo> projects,
            bool showImplementYear)
        {
            target.Clear();
            target.Add(new ProjectFilterOption { Id = null, Name = "全部项目" });

            foreach (ProjectInfo project in projects
                         .Where(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.ProjectName))
                         .OrderBy(item => item.ProjectName, StringComparer.Ordinal)
                         .ThenByDescending(item => item.ImplementYear, StringComparer.Ordinal))
            {
                string name = project.ProjectName.Trim();
                string year = project.ImplementYear?.Trim() ?? string.Empty;
                if (showImplementYear && year.Length > 0)
                {
                    name = $"{name}（{year}）";
                }

                target.Add(new ProjectFilterOption
                {
                    Id = project.Id,
                    Name = name
                });
            }
        }
    }
}
