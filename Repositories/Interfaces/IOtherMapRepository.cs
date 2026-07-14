using DocMgr.Models.HistoryArchive;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 其它图件资料数据访问契约：非标准图件成果数据读写。
/// </summary>
public interface IOtherMapRepository
{
    bool ExistsByCategory(string categoryName);

    List<string> GetDistinctCategories();

    List<OtherMap> GetByCategory(string categoryName);

    void Import(string categoryName, List<OtherMap> items, bool isRecreate);

    void DeleteByCategory(string categoryName);

    void Update(OtherMap map);
}
