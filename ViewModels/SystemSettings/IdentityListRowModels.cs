using DocMgr.Infrastructure.Seeding;
using DocMgr.Models.SystemSettings;

namespace DocMgr.ViewModels.SystemSettings;

/// <summary>部门设置列表行（部门说明列叠加必有提示）。</summary>
public sealed class DepartmentListRow
{
    public DepartmentListRow(Department entity)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public Department Entity { get; }

    public int Id => Entity.Id;

    public string Name => Entity.Name;

    /// <summary>列表「部门说明」：必有部门带勿改勿删提示，否则为库内说明。</summary>
    public string DescriptionDisplay =>
        SystemIdentitySeedCatalog.FormatDepartmentDescriptionDisplay(Entity.Name, Entity.Description);
}
