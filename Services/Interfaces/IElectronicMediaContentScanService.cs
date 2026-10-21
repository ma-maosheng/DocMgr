using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 电子介质内容扫描服务契约：扫描磁盘目录与文件明细以登记电子介质内容。
    /// </summary>
    public interface IElectronicMediaContentScanService
    {
        /// <summary>
        /// 将所选目录视为子项父目录，扫描其直接子项：一级子目录记为「目录」，父目录下散文件记为「文件」。
        /// 二者可同时存在。数据量与文件个数按各父目录整棵树统计。支持直接选择盘符根目录。
        /// </summary>
        /// <param name="allowEmpty">为 true 时，目录下没有可登记的文件或一级子目录也返回空明细，不抛出异常。</param>
        ElectronicMediaContentScanResult ScanDirectories(
            IReadOnlyList<string> directoryPaths,
            string? storageRootDirectory = null,
            bool allowEmpty = false);

        /// <summary>
        /// 列出资料名称文件夹下可作为资料子项的一级业务子目录，并统计其中直接散落的文件数。
        /// </summary>
        ElectronicMediaMaterialFolderListing ListMaterialFolderSubItemDirectories(string materialFolder);
    }
}
