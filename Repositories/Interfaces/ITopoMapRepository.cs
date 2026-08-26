using DocMgr.Models.HistoryArchive;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 地形图资料数据访问契约：地形图成果数据读写。
/// </summary>
public interface ITopoMapRepository
{
    bool ExistsByScale(string scale);

    List<string> GetDistinctScales();

    List<TopoMap> GetByScale(string scale);

    /// <summary>
    /// 获取全部地形图记录。
    /// </summary>
    List<TopoMap> GetAll();

    void Import(List<TopoMap> maps, bool isRecreate);

    void DeleteByScale(string scale);

    /// <summary>
    /// 按主键删除单条地形图记录。
    /// </summary>
    void DeleteById(int id);

    void Update(TopoMap map);
}
