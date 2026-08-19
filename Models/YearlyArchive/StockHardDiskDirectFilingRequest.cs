using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 存量硬盘直办提交请求。
    /// </summary>
    public sealed class StockHardDiskDirectFilingRequest
    {
        public string RootPath { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string ProjectCode { get; init; } = string.Empty;

        public string DiskCode { get; init; } = string.Empty;

        public string SerialNumber { get; init; } = string.Empty;

        public string DiskType { get; init; } = string.Empty;

        public string Brand { get; init; } = string.Empty;

        public string Capacity { get; init; } = string.Empty;

        public string InterfaceType { get; init; } = string.Empty;

        public DateTime? FactoryDate { get; init; }

        public string StorageLocation { get; init; } = string.Empty;

        public string SourceType { get; init; } = ArchiveRegisterDomainValues.SourceTypeStockDirect;

        public string ArchivePurpose { get; init; } = ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage;

        public string ConfidentialLevel { get; init; } = "秘密";

        public string ProvideUnit { get; init; } = string.Empty;

        public string MaterialCategory { get; init; } = ArchiveRegisterDomainValues.ElectronicMaterialCategoryData;

        public string SubCategory { get; init; } = ArchiveRegisterDomainValues.DefaultStockDirectSubCategory;

        public IReadOnlyList<StockHardDiskMaterialDraft> Materials { get; init; } = Array.Empty<StockHardDiskMaterialDraft>();
    }

    /// <summary>
    /// 存量硬盘直办提交结果。
    /// </summary>
    public sealed class StockHardDiskDirectFilingResult
    {
        public bool Succeeded { get; init; }

        public string Message { get; init; } = string.Empty;

        public string ElectronicArchiveNo { get; init; } = string.Empty;

        public string DiskCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public IReadOnlyList<string> FormNos { get; init; } = Array.Empty<string>();

        public static StockHardDiskDirectFilingResult Fail(string message)
            => new() { Succeeded = false, Message = message };
    }
}
