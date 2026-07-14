using DocMgr.Models.Projects;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 项目信息数据访问契约：测绘项目数据读写。
/// </summary>
public interface IProjectRepository
{
    List<ProjectInfo> GetAll();

    List<ProjectInfo> Search(string? year, string? keyword);

    ProjectInfo? GetById(int projectId);

    void Add(ProjectInfo project);

    void Remove(ProjectInfo project);

    int SaveChanges();
}
