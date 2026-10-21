using DocMgr.Models.HistoryArchive;

namespace DocMgr.Services.Interfaces;

/// <summary>
/// 历史存档杂项资料录入、档口推荐与实物照片。
/// </summary>
public interface IHistoryMiscMaterialService
{
    /// <summary>按条件查询杂项资料。</summary>
    Task<IReadOnlyList<HistoryMiscMaterialListItem>> SearchAsync(
        string? majorCategory,
        string? subCategory,
        string? containerKind,
        string? lifecycleStatus,
        string? keyword);

    /// <summary>读取一条杂项及其详情行。</summary>
    Task<HistoryMiscMaterial?> GetAsync(int id);

    /// <summary>已录入的大类、子类与容器类型。</summary>
    Task<HistoryMiscVocabulary> GetVocabularyAsync();

    /// <summary>新建时的容器编号：史-杂-登记年度-三位年度序号。</summary>
    Task<string> AllocateContainerCodeAsync(int? year = null);

    /// <summary>立式/卧式文件柜格口推荐。不按容量过滤。</summary>
    Task<IReadOnlyList<HistoryMiscSlotOption>> SuggestSlotsAsync();

    /// <summary>保存杂项、详情行与实物照片。</summary>
    Task<HistoryMiscMaterial> SaveAsync(HistoryMiscMaterialSaveRequest request, User currentUser);

    /// <summary>删除在库且未被处置占用的杂项。</summary>
    Task DeleteAsync(int id, User currentUser);

    /// <summary>读取实物照片。</summary>
    Task<IReadOnlyList<SystemAttachment>> GetPhotosAsync(int materialId);

    /// <summary>读取实物照片目录（不含文件内容），按上传时间排序。</summary>
    Task<IReadOnlyList<HistoryMiscPhotoCatalogItem>> GetPhotoCatalogAsync(int materialId);

    /// <summary>按附件 Id 读取实物照片内容。</summary>
    Task<SystemAttachment?> GetPhotoAsync(int attachmentId);
}
