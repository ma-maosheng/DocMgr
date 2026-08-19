using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 电子介质内容扫描服务契约：扫描磁盘目录与文件明细以登记电子介质内容。
    /// </summary>
    public interface IElectronicMediaContentScanService
    {
        /// <summary>
        /// 将所选目录视为子项根目录，扫描其直接子项：一级子目录记为「目录」，根下散文件记为「文件」。
        /// 目录型允许二者同时存在。数据量与文件个数按各根目录整棵树统计。
        /// </summary>
        ElectronicMediaContentScanResult ScanDirectories(IReadOnlyList<string> directoryPaths, string? storageRootDirectory = null);

        ElectronicMediaContentScanResult ScanFiles(IReadOnlyList<string> filePaths, string? storageRootDirectory = null);
    }
}
