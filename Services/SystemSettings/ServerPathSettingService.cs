using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.SystemSettings;

public class ServerPathSettingService : IServerPathSettingService
{
    private readonly IServerPathSettingRepository _repository;
    private readonly IUserContextService _userContextService;

    public ServerPathSettingService(
        IServerPathSettingRepository repository,
        IUserContextService userContextService)
    {
        _repository = repository;
        _userContextService = userContextService;
    }

    public List<ServerPathSetting> GetAll()
    {
        return _repository.GetAll();
    }

    public IReadOnlyList<ServerPathSetting> GetWritablePathsForDepartment(string? departmentName)
    {
        string dept = departmentName?.Trim() ?? string.Empty;
        return GetAll()
            .Where(setting => ServerPathSettingDomainValues.IsWritablePermission(setting.Permission))
            .Where(setting =>
                string.Equals(setting.DepartmentName?.Trim(), ServerPathSettingDomainValues.PublicDepartment, StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(dept)
                    && string.Equals(setting.DepartmentName?.Trim(), dept, StringComparison.Ordinal)))
            .OrderBy(setting => setting.PathName, StringComparer.Ordinal)
            .ToList();
    }

    public void Add(ServerPathSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ServerPathSettingPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
        ValidateSetting(setting);

        _repository.Add(setting);
        _repository.SaveChanges();
    }

    public void Update(ServerPathSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ServerPathSettingPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);

        var existing = _repository.GetById(setting.Id)
            ?? throw new InvalidOperationException("服务器路径设置不存在或已被删除。");

        ValidateSetting(setting);

        existing.DepartmentName = setting.DepartmentName;
        existing.PathName = setting.PathName;
        existing.PhysicalPath = setting.PhysicalPath;
        existing.Permission = setting.Permission;
        existing.CapacityTb = setting.CapacityTb;

        _repository.SaveChanges();
    }

    public void Delete(int id)
    {
        ServerPathSettingPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
        var existing = _repository.GetById(id);
        if (existing == null)
        {
            return;
        }

        _repository.Remove(existing);
        _repository.SaveChanges();
    }

    private static void ValidateSetting(ServerPathSetting setting)
    {
        setting.DepartmentName = (setting.DepartmentName ?? string.Empty).Trim();
        setting.PathName = (setting.PathName ?? string.Empty).Trim();
        setting.PhysicalPath = (setting.PhysicalPath ?? string.Empty).Trim();
        setting.Permission = (setting.Permission ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(setting.DepartmentName))
        {
            throw new InvalidOperationException("部门不能为空。");
        }

        if (string.IsNullOrWhiteSpace(setting.PathName))
        {
            throw new InvalidOperationException("路径名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(setting.PhysicalPath))
        {
            throw new InvalidOperationException("物理地址不能为空。");
        }

        if (!ServerPathSettingDomainValues.PermissionOptions.Contains(setting.Permission))
        {
            throw new InvalidOperationException("权限取值无效。");
        }

        if (setting.CapacityTb <= 0)
        {
            throw new InvalidOperationException("容量必须大于 0。");
        }
    }
}
