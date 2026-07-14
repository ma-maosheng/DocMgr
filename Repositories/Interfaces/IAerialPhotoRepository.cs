using DocMgr.Models.HistoryArchive;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 航摄资料数据访问契约：航摄成果数据读写。
/// </summary>
public interface IAerialPhotoRepository
{
    bool ExistsByCategory(string categoryName);

    List<string> GetDistinctCategories();

    List<AerialPhoto> GetByCategory(string categoryName);

    void Import(string categoryName, List<AerialPhoto> items, bool isRecreate);

    void DeleteByCategory(string categoryName);

    void Update(AerialPhoto photo);
}
