using System;
using System.Collections.Generic;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 存档文本资料直办提交请求（一盒一提交）。
    /// </summary>
    public sealed class StockTextArchiveDirectFilingRequest
    {
        public string Year { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string ProjectCode { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string SourceType { get; init; } = ArchiveRegisterDomainValues.SourceTypeStockDirect;

        public string ArchivePurpose { get; init; } = ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage;

        public string ConfidentialLevel { get; init; } = "秘密";

        public string ProvideUnit { get; init; } = ArchiveRegisterDomainValues.ProvideUnitArchiveRoom;

        public string BoxSpecification { get; init; } = "标准(5cm)";

        public string CabinetName { get; init; } = string.Empty;

        public string Side { get; init; } = string.Empty;

        public int Row { get; init; }

        public int Column { get; init; }

        public string Remarks { get; init; } = string.Empty;

        public IReadOnlyList<StockTextArchiveMediaGroupDraft> MediaGroups { get; init; }
            = Array.Empty<StockTextArchiveMediaGroupDraft>();
    }

    /// <summary>
    /// 存档文本直办：一组模拟介质（类型 + 子项）。
    /// </summary>
    public sealed class StockTextArchiveMediaGroupDraft
    {
        public string MediaType { get; init; } = ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper;

        public IReadOnlyList<StockTextArchiveMediaItemDraft> Items { get; init; }
            = Array.Empty<StockTextArchiveMediaItemDraft>();
    }

    /// <summary>
    /// 存档文本直办：模拟资料子项。
    /// </summary>
    public sealed class StockTextArchiveMediaItemDraft
    {
        public string ContentDesc { get; init; } = string.Empty;

        public string ConfidentialLevel { get; init; } = "秘密";

        public int ContentCount { get; init; } = 1;

        public string Note { get; init; } = string.Empty;

        public string MaterialCategory { get; init; } = ArchiveRegisterDomainValues.SimulatedMaterialCategoryText;

        public string SubCategory { get; init; } = ArchiveRegisterDomainValues.SimulatedSubCategoryOther;

        public string OrganizationForm { get; init; } = ArchiveRegisterDomainValues.SimulatedOrganizationFormBound;
    }

    /// <summary>
    /// 存档文本直办提交结果。
    /// </summary>
    public sealed class StockTextArchiveDirectFilingResult
    {
        public bool Succeeded { get; init; }

        public string Message { get; init; } = string.Empty;

        public string FormNo { get; init; } = string.Empty;

        public string ArchiveSequenceNo { get; init; } = string.Empty;

        public string BoxLocationCode { get; init; } = string.Empty;

        public int ItemCount { get; init; }

        public static StockTextArchiveDirectFilingResult Fail(string message)
            => new() { Succeeded = false, Message = message };
    }
}
