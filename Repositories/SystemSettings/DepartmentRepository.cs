using DocMgr.Data;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Repositories.SystemSettings;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly AppDbContext _dbContext;

    public DepartmentRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<Department> GetAll()
    {
        return _dbContext.Departments.OrderBy(item => item.Id).ToList();
    }

    public Department? GetById(int departmentId)
    {
        return _dbContext.Departments.FirstOrDefault(item => item.Id == departmentId);
    }

    public void Add(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);
        _dbContext.Departments.Add(department);
    }

    public void Remove(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);
        _dbContext.Departments.Remove(department);
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }
}
