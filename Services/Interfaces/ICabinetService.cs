using System.Collections.Generic;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 档案柜管理服务契约：柜体与档口（格位）的维护与查询。
    /// </summary>
    public interface ICabinetService
    {
        List<Cabinet> GetAllCabinets();
        Cabinet? GetCabinet(int cabinetId);
        void AddCabinet(Cabinet cabinet);
        void UpdateCabinet(Cabinet cabinet);

        /// <summary>
        /// 设置防磁磁盘柜档口专用类别。
        /// </summary>
        void SetHardDiskDedicatedSlotCategory(int cabinetId, string faceCode, string slotCode, string categoryName);

        /// <summary>
        /// 读取防磁磁盘柜档口已配置的专用类别（已规范化）；未配置返回 null。
        /// </summary>
        string? GetHardDiskDedicatedSlotCategoryName(int cabinetId, string faceCode, string slotCode);

        /// <summary>
        /// 清除防磁磁盘柜档口专用类别。
        /// </summary>
        void ClearHardDiskDedicatedSlotCategory(int cabinetId, string faceCode, string slotCode);

        /// <summary>
        /// 启动时为尚未配置专用类别的防磁磁盘柜格口补默认「空白硬盘专用档口」；不覆盖用户已设置的用途。
        /// </summary>
        void EnsureAllMagneticDiskSlotsUseBlankCategoryOnStartup();

        /// <summary>
        /// 设置标准滑道式档案柜档口用途。
        /// </summary>
        void SetArchiveDedicatedSlotCategory(int cabinetId, string faceCode, string slotCode, string categoryName);

        /// <summary>
        /// 将标准滑道式档案柜档口用途重设为「未设置」。
        /// </summary>
        void ClearArchiveDedicatedSlotCategory(int cabinetId, string faceCode, string slotCode);

        /// <summary>
        /// 启动时为尚未配置用途的标准滑道式档案柜格口补默认「未设置」；不覆盖用户已设置的用途。
        /// </summary>
        void EnsureAllStandardArchiveSlotsUseUnsetCategoryOnStartup();

        /// <summary>
        /// 历史存档导入：将「未设置/未定义」档口同步为「历史资料专用档口」。
        /// 已是历史资料专用或混用时忽略；已是年度资料专用时拒绝（导入冲突应改走混用）。
        /// </summary>
        void PromoteUnsetArchiveSlotToHistoricalMaterials(int cabinetId, string faceCode, string slotCode);

        /// <summary>
        /// 存档文本 Excel 导入：将「未设置/未定义」档口同步为「年度资料专用档口」。
        /// 已是年度资料专用或混用时忽略；已是历史资料专用时拒绝（导入冲突应改走混用）。
        /// </summary>
        void PromoteUnsetArchiveSlotToYearlyMaterials(int cabinetId, string faceCode, string slotCode);

        /// <summary>
        /// Excel 导入遇用途冲突时：将档口改为「混用档口」（不要求空档口）。
        /// </summary>
        void PromoteArchiveSlotToMixedUse(int cabinetId, string faceCode, string slotCode);

        /// <summary>
        /// 柜体内是否仍有资料、档案盒、历史存档或在库介质。档口用途配置不算存放内容。
        /// </summary>
        bool HasStoredContent(int cabinetId);

        /// <summary>
        /// 列出柜体内仍有存放内容的档口。无内容时返回空列表。
        /// </summary>
        IReadOnlyList<CabinetStoredContentSlot> GetStoredContentSlots(int cabinetId);

        /// <summary>
        /// 核验拟保存的柜体是否仍能完整对应已有存放记录。不改写任何存放数据。
        /// 会造成矛盾、丢失或不完整时抛出 <see cref="InvalidOperationException"/>。
        /// </summary>
        void EnsureCabinetEditKeepsStoredContent(Cabinet proposed);

        void DeleteCabinet(int cabinetId);
        Task<List<Cabinet>> GetAllCabinetsAsync();
    }
}
