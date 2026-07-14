using DocMgr.Models.Cabinets;
using System.Collections.Generic;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 档案盒摆放服务契约：计算并维护档案盒在档口（格位）内的摆放位置。
    /// </summary>
    public interface ICabinetArchiveBoxPlacementService
    {
        /// <summary>
        /// 获取指定档案盒当前的放置方式，未登记时返回默认值。
        /// </summary>
        CabinetArchiveBoxPlacementMode GetPlacementMode(string boxCode);

        /// <summary>
        /// 批量更新指定柜体、面别、档口下所有档案盒的放置方式。
        /// </summary>
        int UpdateSlotPlacementMode(string cabinetName, string faceCode, string slotCode, CabinetArchiveBoxPlacementMode placementMode, string updatedBy);

        /// <summary>
        /// 更新单个档案盒的放置方式。
        /// </summary>
        bool UpdateBoxPlacementMode(string boxCode, CabinetArchiveBoxPlacementMode placementMode, string updatedBy);

        /// <summary>
        /// 获取可供设置的档案盒规格列表。
        /// </summary>
        IReadOnlyList<string> GetAvailableBoxSpecifications();

        /// <summary>
        /// 为单个档案盒设置规格。
        /// </summary>
        bool ResetBoxSpecification(string boxCode, string boxSpecification, string updatedBy);
    }
}
