using DocMgr.Models.HardDiskMedia;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 读取本机物理磁盘硬件信息，供硬盘介质半自动登记回填。
    /// </summary>
    public interface ILocalPhysicalDiskHardwareService
    {
        /// <summary>
        /// 枚举本机物理磁盘。查询在后台线程执行。
        /// </summary>
        Task<IReadOnlyList<LocalPhysicalDiskInfo>> GetPhysicalDisksAsync(CancellationToken cancellationToken = default);
    }
}
