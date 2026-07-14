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
        /// 清除防磁磁盘柜档口专用类别。
        /// </summary>
        void ClearHardDiskDedicatedSlotCategory(int cabinetId, string faceCode, string slotCode);

        /// <summary>
        /// 启动时为尚未配置专用类别的防磁磁盘柜格口补默认「空白硬盘专用档口」；不覆盖用户已设置的用途。
        /// </summary>
        void EnsureAllMagneticDiskSlotsUseBlankCategoryOnStartup();

        /// <summary>
        /// 将全部防磁磁盘柜格口用途重置为空白硬盘专用档口（仅测试数据准备等场景使用）。
        /// </summary>
        void ResetAllMagneticDiskSlotsToBlankCategory();

        void DeleteCabinet(int cabinetId);
        Task<List<Cabinet>> GetAllCabinetsAsync();
    }
}
