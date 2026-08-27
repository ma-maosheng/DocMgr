using DocMgr.Models.HistoryArchive;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 地形图资料数据访问契约：地形图成果数据读写。
/// </summary>
public interface ITopoMapRepository
{
    bool ExistsByCategory(string categoryName);

    List<string> GetDistinctCategories();

    List<TopoMap> GetByCategory(string categoryName);

    /// <summary>
    /// 获取全部地形图记录。
    /// </summary>
    List<TopoMap> GetAll();

    void Import(string categoryName, List<TopoMap> maps, bool isRecreate);

    void DeleteByCategory(string categoryName);

    /// <summary>
    /// 按主键删除单条地形图记录。
    /// </summary>
    void DeleteById(int id);

    void Update(TopoMap map);

    /// <summary>
    /// 持久化当前上下文中已跟踪实体的变更。
    /// </summary>
    void SaveChanges();
}
