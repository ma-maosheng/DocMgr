using DocMgr.Models.SystemSettings;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 部门数据访问契约：部门数据读写。
/// </summary>
public interface IDepartmentRepository
{
    List<Department> GetAll();

    Department? GetById(int departmentId);

    void Add(Department department);

    void Remove(Department department);

    int SaveChanges();
}
