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

    /// <summary>
    /// 获取全部其他图件记录。
    /// </summary>
    List<OtherMap> GetAll();

    void Import(string categoryName, List<OtherMap> items, bool isRecreate);

    void DeleteByCategory(string categoryName);

    /// <summary>
    /// 按主键删除单条其他图件记录。
    /// </summary>
    void DeleteById(int id);

    void Update(OtherMap map);
}
