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

        /// <summary>
        /// 各资料名称顿号摘要。立档按介质内每一份资料名称分别写入，本字段只供确认提示与兼容旧调用。
        /// </summary>
        public string MaterialName { get; init; } = string.Empty;

        public string SourceType { get; init; } = ArchiveRegisterDomainValues.SourceTypeInternal;

        public string ArchivePurpose { get; init; } = ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage;

        public string ProvideUnit { get; init; } = ArchiveRegisterDomainValues.ProvideUnitArchiveRoom;

        public string BoxSpecification { get; init; } = "标准(5cm)";

        public string CabinetName { get; init; } = string.Empty;

        public string Side { get; init; } = string.Empty;

        public int Row { get; init; }

        public int Column { get; init; }

        /// <summary>
        /// 指定盒内序号。有值时按该序号生成物理位置；空则取格口最小可用序号。
        /// </summary>
        public int? SpecifiedBoxIndex { get; init; }

        /// <summary>
        /// Excel 导入：提交前按档口当前用途同步为年度资料专用或混用档口。
        /// </summary>
        public bool SyncUnsetSlotCategoryOnCommit { get; init; }

        public string Remarks { get; init; } = string.Empty;

        public IReadOnlyList<StockTextArchiveMediaGroupDraft> MediaGroups { get; init; }
            = Array.Empty<StockTextArchiveMediaGroupDraft>();
    }

    /// <summary>
    /// 存档文本直办：一组模拟介质（类型 + 多份资料名称）。
    /// </summary>
    public sealed class StockTextArchiveMediaGroupDraft
    {
        public string MediaType { get; init; } = ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper;

        /// <summary>本介质下的资料名称。每份资料各自带资料子项。</summary>
        public IReadOnlyList<StockTextArchiveMaterialDraft> Materials { get; init; }
            = Array.Empty<StockTextArchiveMaterialDraft>();

        /// <summary>未分组的旧调用。有 <see cref="Materials"/> 时忽略。</summary>
        public IReadOnlyList<StockTextArchiveMediaItemDraft> Items { get; init; }
            = Array.Empty<StockTextArchiveMediaItemDraft>();

        /// <summary>解析本介质的资料名称。未分组时用回退名称包住全部子项。</summary>
        public IReadOnlyList<StockTextArchiveMaterialDraft> ResolveMaterials(string? fallbackMaterialName)
        {
            if (Materials is { Count: > 0 })
            {
                return Materials;
            }

            return new[]
            {
                new StockTextArchiveMaterialDraft
                {
                    MaterialName = fallbackMaterialName?.Trim() ?? string.Empty,
                    Items = Items ?? Array.Empty<StockTextArchiveMediaItemDraft>()
                }
            };
        }
    }

    /// <summary>
    /// 存档文本直办：一份资料名称及其子项。
    /// </summary>
    public sealed class StockTextArchiveMaterialDraft
    {
        public string MaterialName { get; init; } = string.Empty;

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

        /// <summary>所属资料名称。交互式录入写在资料组上；Excel 每一行自带，便于同一盒拆成多份资料。</summary>
        public string MaterialName { get; init; } = string.Empty;

        /// <summary>
        /// 资料来源（默认内部），允许按子项分别指定；空白时回落请求级来源。
        /// </summary>
        public string SourceType { get; init; } = string.Empty;

        /// <summary>
        /// 提供单位（内部默认操作人部门/资料室；外来须填写），允许按子项分别指定；空白时回落请求级提供单位。
        /// </summary>
        public string ProvideUnit { get; init; } = string.Empty;
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
