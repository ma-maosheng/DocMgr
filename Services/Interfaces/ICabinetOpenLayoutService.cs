using DocMgr.Models.Cabinets;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 开柜布局服务契约：档口格位与档案盒内容展示。
    /// </summary>
    public interface ICabinetOpenLayoutService
    {
        /// <summary>
        /// 按开柜请求构建档口格位描述列表。
        /// </summary>
        IReadOnlyList<CabinetSlotDescriptor> BuildSlots(CabinetOpenRequest request);

        /// <summary>
        /// 获取指定档案盒内的资料内容摘要列表。
        /// </summary>
        IReadOnlyList<CabinetArchiveBoxContentDescriptor> GetArchiveBoxContents(string boxCode);
    }
}
