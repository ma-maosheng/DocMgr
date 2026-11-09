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
        /// 按与删除校验相同的范围，找出已被库内业务引用的项目。
        /// </summary>
        IReadOnlySet<int> FindReferencedProjectIds(IReadOnlyList<ProjectInfo> projects);

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

        /// <summary>
        /// 把项目列表写入 Excel。列与导入模板一致。
        /// </summary>
        void ExportProjects(string filePath, IReadOnlyList<ProjectInfo> projects);

        /// <summary>
        /// 读取 Excel 工作表名称，供导入前选择。
        /// </summary>
        IReadOnlyList<string> GetProjectExcelSheetNames(string filePath);

        /// <summary>
        /// 从 Excel 新增项目。同一实施年度下已有同名项目时不重复写入，也不修改库内记录。
        /// 实施年度列若为合并单元格，合并区域内各行使用该单元格的年度。
        /// 返回给界面的结果说明。
        /// </summary>
        string ImportProjects(string filePath, string sheetName);
    }
}
