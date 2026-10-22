using System;
using System.Collections.Generic;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 存量光盘直办提交请求。一张光盘对应一个电子光盘袋。
    /// </summary>
    public sealed class StockOpticalDiscDirectFilingRequest
    {
        public string RootPath { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string ProjectCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string SourceType { get; init; } = ArchiveRegisterDomainValues.SourceTypeInternal;

        public string ArchivePurpose { get; init; } = ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage;

        public string ProvideUnit { get; init; } = string.Empty;

        /// <summary>true 表示按文件夹指派。</summary>
        public bool AssignByFolder { get; init; }

        /// <summary>true 表示按后缀指派。与 <see cref="AssignByFolder"/> 互斥。</summary>
        public bool AssignByExtension { get; init; }

        /// <summary>未纳入的文件夹、散文件或后缀已被确认忽略。</summary>
        public bool IgnoredContentAcknowledged { get; init; }

        public int LooseFileCount { get; init; }

        public int ExcludedFolderCount { get; init; }

        /// <summary>按后缀指派时未勾选纳入的后缀数。</summary>
        public int ExcludedExtensionCount { get; init; }

        /// <summary>按后缀指派时，资料所属目录下的一级文件夹数。这些文件夹不进入后缀子项。</summary>
        public int IgnoredFolderCount { get; init; }

        /// <summary>按后缀指派时，资料所属目录下的无后缀文件数。</summary>
        public int ExtensionlessFileCount { get; init; }

        /// <summary>
        /// 本次纳入的资料。按资料名称分组，组内为子项。
        /// </summary>
        public IReadOnlyList<StockOpticalDiscMaterialDraft> Materials { get; init; } = Array.Empty<StockOpticalDiscMaterialDraft>();
    }

    /// <summary>
    /// 同一资料名称下的子项集合，对应一条建档单。
    /// </summary>
    public sealed class StockOpticalDiscMaterialDraft
    {
        public string MaterialName { get; init; } = string.Empty;

        public IReadOnlyList<StockOpticalDiscItemDraft> Items { get; init; } = Array.Empty<StockOpticalDiscItemDraft>();
    }

    /// <summary>
    /// 存量光盘直办提交结果。
    /// </summary>
    public sealed class StockOpticalDiscDirectFilingResult
    {
        public bool Succeeded { get; init; }

        public string Message { get; init; } = string.Empty;

        public string ElectronicArchiveNo { get; init; } = string.Empty;

        public string DiscCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public IReadOnlyList<string> FormNos { get; init; } = Array.Empty<string>();

        public static StockOpticalDiscDirectFilingResult Fail(string message)
            => new() { Succeeded = false, Message = message };
    }
}
