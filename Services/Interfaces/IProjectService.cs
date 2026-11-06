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

        /// <summary>
        /// 新增项目。同一实施年度下项目名称不可重复。
        /// </summary>
        void AddProject(ProjectInfo project);

        /// <summary>
        /// 保存项目。实施年度保持原值。项目名称有变化时，同步到仍指向该项目的业务记录。
        /// 返回非空说明时，同一年度另有同名项目，只按名称保存的记录本次未改。
        /// </summary>
        string? UpdateProject(ProjectInfo project);

        /// <summary>
        /// 删除尚未被年度资料业务引用的项目。已被使用时抛出说明。
        /// </summary>
        void DeleteProject(int projectId);
    }
}
