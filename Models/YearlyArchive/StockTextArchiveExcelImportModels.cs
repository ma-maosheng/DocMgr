using System;
using System.Collections.Generic;
using System.Linq;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// Excel 解析得到的一盒存档文本直办草稿。
    /// </summary>
    public sealed class StockTextArchiveExcelBoxDraft
    {
        public int SequenceNo { get; init; }

        public int FirstRowNumber { get; init; }

        public string Year { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string BoxSpecification { get; init; } = string.Empty;

        public string SourceBoxLocationCode { get; init; } = string.Empty;

        public string CabinetName { get; init; } = string.Empty;

        public string Side { get; init; } = string.Empty;

        public int Row { get; init; }

        public int Column { get; init; }

        public int BoxIndex { get; init; }

        public string NormalizedBoxLocationCode { get; init; } = string.Empty;

        public int? ClaimedBoxCount { get; init; }

        public int ActualBoxCountInSequence { get; init; }

        public IReadOnlyList<string> ParseErrors { get; init; } = Array.Empty<string>();

        public IReadOnlyList<StockTextArchiveMediaItemDraft> Items { get; init; }
            = Array.Empty<StockTextArchiveMediaItemDraft>();

        public StockTextArchiveDirectFilingRequest ToRequest()
        {
            return new StockTextArchiveDirectFilingRequest
            {
                Year = Year,
                ProjectName = ProjectName,
                MaterialName = MaterialName,
                BoxSpecification = BoxSpecification,
                CabinetName = CabinetName,
                Side = Side,
                Row = Row,
                Column = Column,
                SpecifiedBoxIndex = BoxIndex > 0 ? BoxIndex : null,
                SyncUnsetSlotCategoryOnCommit = true,
                Remarks = "Excel导入",
                MediaGroups = new[]
                {
                    new StockTextArchiveMediaGroupDraft
                    {
                        MediaType = ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper,
                        Items = Items
                    }
                }
            };
        }
    }

    /// <summary>
    /// Excel 文件解析结果。
    /// </summary>
    public sealed class StockTextArchiveExcelParseResult
    {
        public IReadOnlyList<string> FileErrors { get; init; } = Array.Empty<string>();

        public IReadOnlyList<StockTextArchiveExcelBoxDraft> Boxes { get; init; }
            = Array.Empty<StockTextArchiveExcelBoxDraft>();

        public bool HasFileErrors => FileErrors.Count > 0;

        public static StockTextArchiveExcelParseResult Fail(params string[] errors)
            => new() { FileErrors = errors ?? Array.Empty<string>() };
    }

    /// <summary>
    /// 单盒 Excel 导入校验结果。
    /// </summary>
    public sealed class StockTextArchiveExcelBoxValidation
    {
        public StockTextArchiveExcelBoxDraft Box { get; init; } = null!;

        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

        public bool CanImport => Errors.Count == 0;
    }

    /// <summary>
    /// Excel 按盒导入的办结汇总。
    /// </summary>
    public sealed class StockTextArchiveExcelImportCommitResult
    {
        public int SucceededCount { get; init; }

        public int FailedCount { get; init; }

        public int SkippedCount { get; init; }

        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();

        public string Summary
        {
            get
            {
                var parts = new List<string>
                {
                    $"成功 {SucceededCount} 盒",
                    $"失败 {FailedCount} 盒"
                };
                if (SkippedCount > 0)
                {
                    parts.Add($"跳过 {SkippedCount} 盒");
                }

                string head = string.Join("，", parts) + "。";
                if (Messages.Count == 0)
                {
                    return head;
                }

                return head + Environment.NewLine + string.Join(Environment.NewLine, Messages.Take(20));
            }
        }
    }
}
