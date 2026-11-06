using DocMgr.Data;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.Projects
{
    public partial class ProjectService : IProjectService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly IUserContextService _userContextService;
        private readonly AppDbContext _dbContext;

        public ProjectService(
            IProjectRepository projectRepository,
            IUserContextService userContextService,
            AppDbContext dbContext)
        {
            _projectRepository = projectRepository;
            _userContextService = userContextService;
            _dbContext = dbContext;
        }

        public List<ProjectInfo> GetAllProjects()
        {
            return _projectRepository.GetAll();
        }

        public List<ProjectInfo> SearchProjects(string? year, string? keyword)
        {
            return _projectRepository.Search(year, keyword);
        }

        public void AddProject(ProjectInfo project)
        {
            ProjectSettingPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            ArgumentNullException.ThrowIfNull(project);
            ApplyNormalizedFields(project);
            EnsureNameAndYearPresent(project.ProjectName, project.ImplementYear);
            EnsureUniqueNameAndYear(project.Id, project.ProjectName, project.ImplementYear);
            _projectRepository.Add(project);
            _projectRepository.SaveChanges();
        }

        /// <inheritdoc />
        public string? UpdateProject(ProjectInfo project)
        {
            ProjectSettingPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            ArgumentNullException.ThrowIfNull(project);

            ProjectInfo? existing = _projectRepository.GetById(project.Id);
            if (existing == null)
            {
                throw new InvalidOperationException("未找到要修改的项目。");
            }

            string newName = project.ProjectName?.Trim() ?? string.Empty;
            string newYear = existing.ImplementYear?.Trim() ?? string.Empty;
            EnsureNameAndYearPresent(newName, newYear);
            EnsureUniqueNameAndYear(existing.Id, newName, newYear);

            string oldName = existing.ProjectName?.Trim() ?? string.Empty;
            string oldYear = existing.ImplementYear?.Trim() ?? string.Empty;
            bool nameChanged = !string.Equals(oldName, newName, StringComparison.Ordinal);
            bool yearChanged = !string.Equals(oldYear, newYear, StringComparison.Ordinal);

            existing.ProjectName = newName;
            existing.ProjectCode = project.ProjectCode?.Trim() ?? string.Empty;
            existing.ImplementYear = newYear;
            existing.CapitalMgrDept = project.CapitalMgrDept?.Trim() ?? string.Empty;
            existing.Remark = project.Remark?.Trim() ?? string.Empty;

            string? warning = nameChanged || yearChanged
                ? SyncProjectSnapshots(existing.Id, oldName, oldYear, newName, newYear, nameChanged, yearChanged)
                : null;
            _projectRepository.SaveChanges();
            return warning;
        }

        public void DeleteProject(int projectId)
        {
            ProjectSettingPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            ProjectInfo? project = _projectRepository.GetById(projectId);
            if (project == null)
            {
                return;
            }

            EnsureProjectUnused(project);
            _projectRepository.Remove(project);
            _projectRepository.SaveChanges();
        }

        private static void ApplyNormalizedFields(ProjectInfo project)
        {
            project.ProjectName = project.ProjectName?.Trim() ?? string.Empty;
            project.ProjectCode = project.ProjectCode?.Trim() ?? string.Empty;
            project.ImplementYear = project.ImplementYear?.Trim() ?? string.Empty;
            project.CapitalMgrDept = project.CapitalMgrDept?.Trim() ?? string.Empty;
            project.Remark = project.Remark?.Trim() ?? string.Empty;
        }

        private static void EnsureNameAndYearPresent(string name, string year)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("请输入项目名称。");
            }

            if (string.IsNullOrWhiteSpace(year))
            {
                throw new InvalidOperationException("请输入实施年度。");
            }
        }
    }
}
