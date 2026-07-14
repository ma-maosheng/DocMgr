using DocMgr.Models.SystemSettings;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 角色数据访问契约：角色数据读写。
/// </summary>
public interface IRoleRepository
{
    List<Role> GetAll();

    Role? GetById(int roleId);

    void Add(Role role);

    void Remove(Role role);

    int SaveChanges();
}
