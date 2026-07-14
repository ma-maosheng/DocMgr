using DocMgr.Data;
using DocMgr.Models.Projects;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Repositories.Projects;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _dbContext;

    public ProjectRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<ProjectInfo> GetAll()
    {
        return _dbContext.ProjectInfos
            .OrderByDescending(project => project.ImplementYear)
            .ThenBy(project => project.Id)
            .ToList();
    }

    public List<ProjectInfo> Search(string? year, string? keyword)
    {
        IQueryable<ProjectInfo> query = _dbContext.ProjectInfos.AsQueryable();

        if (!string.IsNullOrWhiteSpace(year))
        {
            query = query.Where(project => project.ImplementYear.Contains(year));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(project =>
                project.ProjectName.Contains(keyword) ||
                project.ProjectCode.Contains(keyword) ||
                project.CapitalMgrDept.Contains(keyword));
        }

        return query.OrderByDescending(project => project.ImplementYear).ToList();
    }

    public ProjectInfo? GetById(int projectId)
    {
        return _dbContext.ProjectInfos.FirstOrDefault(project => project.Id == projectId);
    }

    public void Add(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        _dbContext.ProjectInfos.Add(project);
    }

    public void Remove(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        _dbContext.ProjectInfos.Remove(project);
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }
}
