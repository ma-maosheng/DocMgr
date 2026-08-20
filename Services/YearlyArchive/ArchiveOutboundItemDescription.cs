using System.Text.Json;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 借出申请明细的文字摘要与打印描述。
    /// </summary>
    internal static class ArchiveOutboundItemDescription
    {
        public static string BuildMaterialSummary(IReadOnlyCollection<YearlyArchiveOutboundItem> items)
        {
            var labels = items
                .OrderBy(item => item.SortOrder)
                .Select(BuildSummaryLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToList();

            if (labels.Count == 0)
            {
                return string.Empty;
            }

            if (labels.Count == 1)
            {
                return labels[0];
            }

            return string.Join("；", labels.Select((label, index) => $"{index + 1}.{label}"));
        }

        public static IReadOnlyList<string> BuildPrintDetailLines(
            IReadOnlyCollection<YearlyArchiveOutboundItem> items,
            IReadOnlySet<int>? depletedFilingFactIds = null,
            IReadOnlyDictionary<int, string>? classificationByFilingFactId = null)
        {
            var lines = items
                .OrderBy(item => item.SortOrder)
                .Select((item, index) => FormatPrintDetailLine(
                    item,
                    index + 1,
                    forApplicationPrint: true,
                    depletedFilingFactIds: depletedFilingFactIds,
                    classificationByFilingFactId: classificationByFilingFactId))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            return lines.Count > 0 ? lines : ["(无)"];
        }

        /// <summary>
        /// 交接单「具体资料明细」行：涉及硬盘时优先列出编号，无编号则列序列号。
        /// </summary>
        public static IReadOnlyList<string> BuildHandoverPrintDetailLines(
            IReadOnlyCollection<YearlyArchiveOutboundItem> items,
            IReadOnlyDictionary<int, YearlyArchiveFilingFact> factsById,
            IReadOnlyDictionary<int, string>? classificationByFilingFactId = null)
        {
            var lines = items
                .OrderBy(item => item.SortOrder)
                .Select((item, index) =>
                {
                    factsById.TryGetValue(item.FilingFactId, out var fact);
                    return FormatPrintDetailLine(
                        item,
                        index + 1,
                        forHandover: true,
                        fact: fact,
                        classificationByFilingFactId: classificationByFilingFactId);
                })
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            return lines.Count > 0 ? lines : ["(无)"];
        }

        public static string BuildSinglePrintDetailLine(YearlyArchiveOutboundItem item, int index) =>
            FormatPrintDetailLine(item, index);

        private static string BuildSummaryLabel(YearlyArchiveOutboundItem item)
        {
            string materialName = item.MaterialName?.Trim() ?? string.Empty;
            string itemName = item.ItemName?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(materialName) && !string.IsNullOrWhiteSpace(itemName)
                && !string.Equals(materialName, itemName, StringComparison.Ordinal))
            {
                return $"{materialName}/{itemName}";
            }

            if (!string.IsNullOrWhiteSpace(materialName))
            {
                return materialName;
            }

            return itemName;
        }

        private static string FormatPrintDetailLine(
            YearlyArchiveOutboundItem item,
            int index,
            bool forHandover = false,
            bool forApplicationPrint = false,
            YearlyArchiveFilingFact? fact = null,
            IReadOnlySet<int>? depletedFilingFactIds = null,
            IReadOnlyDictionary<int, string>? classificationByFilingFactId = null)
        {
            var segments = new List<string>();

            bool depletesStock = forApplicationPrint
                && depletedFilingFactIds != null
                && item.FilingFactId > 0
                && depletedFilingFactIds.Contains(item.FilingFactId)
                && ArchiveSimulatedLongTermWithdrawalDepletionSupport.IsTargetItem(item);

            if (depletesStock)
            {
                segments.Add(ArchiveSimulatedLongTermWithdrawalDepletionSupport.PrintItemMarker);
            }

            if (item.ItemArchiveYear is int archiveYear)
            {
                segments.Add($"{archiveYear}年");
            }

            AppendSegment(segments, item.ItemProjectName);

            string mediaKind = item.MediaKind?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(mediaKind))
            {
                segments.Add(mediaKind);
            }

            AppendSegment(segments, item.MediaType);

            if (classificationByFilingFactId != null
                && item.FilingFactId > 0
                && classificationByFilingFactId.TryGetValue(item.FilingFactId, out string? classification)
                && !string.IsNullOrWhiteSpace(classification))
            {
                AppendSegment(segments, classification);
            }

            string materialDisplay = BuildSummaryLabel(item);
            if (!string.IsNullOrWhiteSpace(materialDisplay))
            {
                segments.Add(materialDisplay);
            }

            if (forApplicationPrint)
            {
                segments.Add(FormatApplicationPrintConfidentialClause(item.ConfidentialLevel));
            }
            else
            {
                AppendSegment(segments, FormatConfidentialLevel(item.ConfidentialLevel));
            }

            if (IsContentEntryScope(item))
            {
                AppendSegment(segments, $"范围{FormatSelectionScope(item)}");
            }

            AppendSegment(segments, FormatUsageClause(item, depletesStock));

            if (!string.IsNullOrWhiteSpace(item.ContainerCode))
            {
                segments.Add($"盒/袋{item.ContainerCode.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(item.CurrentStorageLocation))
            {
                segments.Add($"位置{item.CurrentStorageLocation.Trim()}");
            }

            if (forHandover)
            {
                string? handoverDiskClause = FormatHandoverDiskClause(item, fact);
                if (!string.IsNullOrWhiteSpace(handoverDiskClause))
                {
                    segments.Add(handoverDiskClause);
                }
            }
            else if (!string.IsNullOrWhiteSpace(item.RequisitionedDiskCode))
            {
                string diskClause = $"库内空盘{item.RequisitionedDiskCode.Trim()}";
                if (item.ShowRequisitionedDiskNeedReturn)
                {
                    diskClause += item.RequisitionedDiskNeedReturn ? "需归还" : "不需归还";
                }

                segments.Add(diskClause);
            }

            if (forApplicationPrint)
            {
                AppendSegment(segments, FormatReturnExpectationClause(item));
            }

            return segments.Count == 0
                ? string.Empty
                : $"({index}) {string.Join("，", segments)}";
        }

        /// <summary>
        /// 申请单明细中的归还预期：资料归还或硬盘归还时补充应还日期。
        /// </summary>
        private static string? FormatReturnExpectationClause(YearlyArchiveOutboundItem item)
        {
            if (!ArchiveOutboundReturnSupport.ItemRequiresExpectedReturnDate(item))
            {
                return null;
            }

            DateTime? dueDate = item.ExpectedReturnDate?.Date;
            if (!dueDate.HasValue)
            {
                return null;
            }

            string dateText = dueDate.Value.ToString("yyyy-MM-dd");

            if (IsHardDiskReturnExpectation(item))
            {
                return $"硬盘归还预期{dateText}";
            }

            if (IsMaterialReturnExpectation(item))
            {
                return $"资料归还预期{dateText}";
            }

            return $"归还预期{dateText}";
        }

        private static bool IsMaterialReturnExpectation(YearlyArchiveOutboundItem item) =>
            string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
            && item.NeedReturn
            && string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal);

        private static bool IsHardDiskReturnExpectation(YearlyArchiveOutboundItem item)
        {
            if (item.ShowRequisitionedDiskNeedReturn && item.RequisitionedDiskNeedReturn)
            {
                return true;
            }

            return string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
                && item.NeedReturn
                && string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                && ArchiveOutboundDomainValues.IsHardDiskStorageCarrier(item.StorageCarrierType);
        }

        private static string FormatUsageClause(YearlyArchiveOutboundItem item, bool depletesStock = false)
        {
            return item.UsageMode switch
            {
                ArchiveOutboundDomainValues.UsageModeWithdrawal => FormatWithdrawalUsage(item, depletesStock),
                ArchiveOutboundDomainValues.UsageModeCopy => FormatCopyUsage(item),
                ArchiveOutboundDomainValues.UsageModeDuplicate => FormatDuplicateUsage(item),
                _ => item.UsageModeDisplay
            };
        }

        private static string FormatWithdrawalUsage(YearlyArchiveOutboundItem item, bool depletesStock = false)
        {
            string copyText = item.CopyCount is > 0 ? $"{item.CopyCount}份" : string.Empty;
            string returnText = item.NeedReturn ? "需归还资料" : "不需归还资料";
            string usage = string.IsNullOrEmpty(copyText)
                ? $"提档{returnText}"
                : $"提档{copyText}{returnText}";

            if (depletesStock)
            {
                usage += "，办结后库内份数归零";
            }

            return usage;
        }

        private static string FormatCopyUsage(YearlyArchiveOutboundItem item)
        {
            return item.CopyCount is > 0
                ? $"复制{item.CopyCount}份"
                : "复制";
        }

        private static string FormatDuplicateUsage(YearlyArchiveOutboundItem item)
        {
            return "拷贝1份";
        }

        private static string FormatConfidentialLevel(string? confidentialLevel)
        {
            string normalized = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(confidentialLevel);
            return string.Equals(normalized, ArchiveRegisterDomainValues.ConfidentialLevelNone, StringComparison.Ordinal)
                ? string.Empty
                : $"密级{normalized}";
        }

        /// <summary>
        /// 申请单「具体资料明细」中的涉密标识：不涉密时也须明确写出。
        /// </summary>
        private static string FormatApplicationPrintConfidentialClause(string? confidentialLevel)
        {
            string normalized = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(confidentialLevel);
            if (string.IsNullOrWhiteSpace(normalized)
                || string.Equals(normalized, ArchiveRegisterDomainValues.ConfidentialLevelNone, StringComparison.Ordinal))
            {
                return "涉密情况：不涉密";
            }

            return $"涉密情况：{normalized}";
        }

        private static bool IsContentEntryScope(YearlyArchiveOutboundItem item) =>
            string.Equals(
                item.SelectionScopeKind,
                ArchiveSearchSelectionScopeKind.ContentEntry,
                StringComparison.Ordinal);

        private static string FormatSelectionScope(YearlyArchiveOutboundItem item)
        {
            string scope = item.SelectionScopeDisplay?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(scope))
            {
                return scope;
            }

            string entryName = item.ContentEntryName?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(entryName) ? "指定内容" : entryName;
        }

        private static void AppendSegment(List<string> segments, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            segments.Add(value.Trim());
        }

        private static bool InvolvesHardDisk(YearlyArchiveOutboundItem item) =>
            ArchiveOutboundHardDiskTransferDisplaySupport.InvolvesHardDisk(item);

        private static string? FormatHandoverDiskClause(YearlyArchiveOutboundItem item, YearlyArchiveFilingFact? fact)
        {
            if (!InvolvesHardDisk(item))
            {
                return null;
            }

            var codes = new List<string>();
            AppendDistinctCode(codes, item.RequisitionedDiskCode);
            foreach (string code in ParseStringListJson(item.SelfDiskCodesJson))
            {
                AppendDistinctCode(codes, code);
            }

            if (fact != null && ArchiveFilingBusinessRules.IsHardDiskArchiveCarrierType(fact.StorageCarrierType))
            {
                AppendDistinctCode(codes, fact.MediumCode);
            }

            if (codes.Count > 0)
            {
                return FormatHandoverDiskLabel("硬盘编号", codes, item);
            }

            var serials = new List<string>();
            AppendDistinctCode(serials, item.SelfDiskSerialNo);
            foreach (string serial in ParseStringListJson(item.SelfDiskSerialNumbersJson))
            {
                AppendDistinctCode(serials, serial);
            }

            return serials.Count > 0
                ? FormatHandoverDiskLabel("硬盘序列号", serials, item)
                : null;
        }

        private static string FormatHandoverDiskLabel(
            string label,
            IReadOnlyList<string> values,
            YearlyArchiveOutboundItem item)
        {
            string clause = $"{label}：{string.Join("、", values)}";
            if (item.ShowRequisitionedDiskNeedReturn)
            {
                clause += item.RequisitionedDiskNeedReturn ? "，需归还" : "，不需归还";
            }

            return clause;
        }

        private static void AppendDistinctCode(List<string> target, string? value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            if (target.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            target.Add(normalized);
        }

        private static IReadOnlyList<string> ParseStringListJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            try
            {
                var values = JsonSerializer.Deserialize<List<string>>(json);
                return values?
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .ToList() ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
