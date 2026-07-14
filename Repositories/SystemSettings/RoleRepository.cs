using DocMgr.Data;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Repositories.SystemSettings;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _dbContext;

    public RoleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<Role> GetAll()
    {
        return _dbContext.Roles.OrderBy(item => item.Id).ToList();
    }

    public Role? GetById(int roleId)
    {
        return _dbContext.Roles.FirstOrDefault(item => item.Id == roleId);
    }

    public void Add(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        _dbContext.Roles.Add(role);
    }

    public void Remove(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        _dbContext.Roles.Remove(role);
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }
}
