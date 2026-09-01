using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.Projects
{
    public class ProjectService : IProjectService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly IUserContextService _userContextService;

        public ProjectService(IProjectRepository projectRepository, IUserContextService userContextService)
        {
            _projectRepository = projectRepository;
            _userContextService = userContextService;
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
            _projectRepository.Add(project);
            _projectRepository.SaveChanges();
        }

        public void UpdateProject(ProjectInfo project)
        {
            ProjectSettingPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            var existing = _projectRepository.GetById(project.Id);
            if (existing != null)
            {
                existing.ProjectName = project.ProjectName;
                existing.ProjectCode = project.ProjectCode;
                existing.ImplementYear = project.ImplementYear;
                existing.CapitalMgrDept = project.CapitalMgrDept;
                existing.Remark = project.Remark;
                _projectRepository.SaveChanges();
            }
        }

        public void DeleteProject(int projectId)
        {
            ProjectSettingPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            var project = _projectRepository.GetById(projectId);
            if (project != null)
            {
                _projectRepository.Remove(project);
                _projectRepository.SaveChanges();
            }
        }
    }
}
