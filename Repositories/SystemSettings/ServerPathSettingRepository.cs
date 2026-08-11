using DocMgr.Data;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Repositories.SystemSettings;

public class ServerPathSettingRepository : IServerPathSettingRepository
{
    private readonly AppDbContext _dbContext;

    public ServerPathSettingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<ServerPathSetting> GetAll()
    {
        return _dbContext.ServerPathSettings
            .OrderBy(item => item.DepartmentName)
            .ThenBy(item => item.PathName)
            .ToList();
    }

    public ServerPathSetting? GetById(int id)
    {
        return _dbContext.ServerPathSettings.FirstOrDefault(item => item.Id == id);
    }

    public void Add(ServerPathSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        _dbContext.ServerPathSettings.Add(setting);
    }

    public void Remove(ServerPathSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        _dbContext.ServerPathSettings.Remove(setting);
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }
}
