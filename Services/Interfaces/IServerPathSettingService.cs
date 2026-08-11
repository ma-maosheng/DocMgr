namespace DocMgr.Services.Interfaces;

/// <summary>
/// 服务器路径设置业务服务。
/// </summary>
public interface IServerPathSettingService
{
    List<ServerPathSetting> GetAll();

    /// <summary>获取指定部门可写入的服务器路径（含公用路径）。</summary>
    IReadOnlyList<ServerPathSetting> GetWritablePathsForDepartment(string? departmentName);

    void Add(ServerPathSetting setting);

    void Update(ServerPathSetting setting);

    void Delete(int id);
}
