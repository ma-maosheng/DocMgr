using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 电子介质内容扫描服务契约：扫描磁盘目录与文件明细以登记电子介质内容。
    /// </summary>
    public interface IElectronicMediaContentScanService
    {
        ElectronicMediaContentScanResult ScanDirectories(IReadOnlyList<string> directoryPaths, string? storageRootDirectory = null);

        ElectronicMediaContentScanResult ScanFiles(IReadOnlyList<string> filePaths, string? storageRootDirectory = null);
    }
}
