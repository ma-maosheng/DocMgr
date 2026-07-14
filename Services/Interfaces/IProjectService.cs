using System.Collections.Generic;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 测绘项目信息管理服务契约：项目的查询、新增、修改与删除。
    /// </summary>
    public interface IProjectService
    {
        List<ProjectInfo> GetAllProjects();
        List<ProjectInfo> SearchProjects(string? year, string? keyword);
        void AddProject(ProjectInfo project);
        void UpdateProject(ProjectInfo project);
        void DeleteProject(int projectId);
    }
}
