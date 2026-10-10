using System.Threading;
using DocMgr.Services.Interfaces;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 按硬盘编号读取可用容量说明。查询串行执行，避免同一 DbContext 被并发使用。
    /// </summary>
    internal static class HardDiskCapacityDescriptionLoader
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);

        /// <summary>
        /// 读取硬盘可用容量说明。编号为空时返回空字符串；查询失败时返回「容量读取失败」。
        /// </summary>
        public static async Task<string> LoadAsync(IArchiveFilingService filingService, string? diskCode)
        {
            string code = diskCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            await Gate.WaitAsync();
            try
            {
                return await filingService.DescribeHardDiskAvailableCapacityAsync(code);
            }
            catch (Exception)
            {
                return "容量读取失败";
            }
            finally
            {
                Gate.Release();
            }
        }
    }
}
