namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 服务器路径设置数据访问契约。
/// </summary>
public interface IServerPathSettingRepository
{
    List<ServerPathSetting> GetAll();

    ServerPathSetting? GetById(int id);

    void Add(ServerPathSetting setting);

    void Remove(ServerPathSetting setting);

    int SaveChanges();
}
