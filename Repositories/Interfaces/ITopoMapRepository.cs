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

    void Import(List<TopoMap> maps, bool isRecreate);

    void DeleteByScale(string scale);

    void Update(TopoMap map);
}
