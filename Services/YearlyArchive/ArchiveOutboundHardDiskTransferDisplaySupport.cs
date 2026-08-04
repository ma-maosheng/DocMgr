using System.Text.Json;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 出库明细/盒袋卡片中「硬盘流转」只读文案：编号、借用与归还要求。
    /// </summary>
    public static class ArchiveOutboundHardDiskTransferDisplaySupport
    {
        /// <summary>
        /// 明细是否涉及硬盘流转（征用空盘、自备硬盘、提档数据硬盘等）。
        /// </summary>
        public static bool InvolvesHardDisk(YearlyArchiveOutboundItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (!string.IsNullOrWhiteSpace(item.RequisitionedDiskCode))
            {
                return true;
            }

            if (string.Equals(item.ElectronicMediumType, ArchiveOutboundDomainValues.DuplicateMediumSelfHardDisk, StringComparison.Ordinal)
                || string.Equals(item.ElectronicMediumType, ArchiveOutboundDomainValues.DuplicateMediumInStockBlank, StringComparison.Ordinal))
            {
                return true;
            }

            if (ArchiveOutboundDomainValues.IsHardDiskStorageCarrier(item.StorageCarrierType)
                || item.MediaType?.Contains("硬盘", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(item.FiledHardDiskCodes)
                || !string.IsNullOrWhiteSpace(item.SelfDiskCodesJson)
                || !string.IsNullOrWhiteSpace(item.SelfDiskSerialNo)
                || !string.IsNullOrWhiteSpace(item.SelfDiskSerialNumbersJson))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 生成单条明细的硬盘流转说明；不涉及硬盘时返回空串。
        /// </summary>
        public static string BuildDetailText(YearlyArchiveOutboundItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (!InvolvesHardDisk(item))
            {
                return string.Empty;
            }

            var segments = new List<string>();
            AppendSegment(segments, BuildBorrowRoleClause(item));
            AppendSegment(segments, BuildDiskCodeClause(item));
            AppendSegment(segments, BuildSerialClause(item));
            AppendSegment(segments, BuildCapacityClause(item));
            AppendSegment(segments, BuildReturnClause(item));
            return string.Join("；", segments);
        }

        /// <summary>
        /// 按盒/袋单元汇总硬盘流转说明（同组取首条代表性明细，并合并编号）。
        /// </summary>
        public static string BuildUnitDetailText(IReadOnlyList<YearlyArchiveOutboundItem> unitItems)
        {
            ArgumentNullException.ThrowIfNull(unitItems);

            if (unitItems.Count == 0)
            {
                return string.Empty;
            }

            var hardDiskItems = unitItems.Where(InvolvesHardDisk).ToList();
            if (hardDiskItems.Count == 0)
            {
                return string.Empty;
            }

            var sample = hardDiskItems[0];
            var segments = new List<string>();
            AppendSegment(segments, BuildBorrowRoleClause(sample));

            var codes = CollectDiskCodes(hardDiskItems);
            if (codes.Count > 0)
            {
                segments.Add($"硬盘编号：{string.Join("、", codes)}");
            }

            var serials = CollectSerialNumbers(hardDiskItems);
            if (serials.Count > 0)
            {
                segments.Add($"序列号：{string.Join("、", serials)}");
            }

            AppendSegment(segments, BuildCapacityClause(sample));
            AppendSegment(segments, BuildReturnClause(sample));
            return string.Join("；", segments);
        }

        private static string? BuildBorrowRoleClause(YearlyArchiveOutboundItem item)
        {
            if (IsInStockBlankRequisition(item))
            {
                return "借用方式：征用库内空盘（拷贝资料）";
            }

            if (string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeDuplicate, StringComparison.Ordinal)
                && string.Equals(item.ElectronicMediumType, ArchiveOutboundDomainValues.DuplicateMediumSelfHardDisk, StringComparison.Ordinal))
            {
                return "借用方式：自备硬盘拷贝";
            }

            if (string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
                && (ArchiveOutboundDomainValues.IsHardDiskStorageCarrier(item.StorageCarrierType)
                    || item.MediaType?.Contains("硬盘", StringComparison.OrdinalIgnoreCase) == true))
            {
                return "借用方式：提档库内数据硬盘原件";
            }

            if (string.Equals(item.ElectronicMediumType, ArchiveOutboundDomainValues.DuplicateMediumInStockBlank, StringComparison.Ordinal))
            {
                return "借用方式：库内空盘";
            }

            return "涉及硬盘流转";
        }

        private static string? BuildDiskCodeClause(YearlyArchiveOutboundItem item)
        {
            var codes = CollectDiskCodes([item]);
            return codes.Count == 0 ? null : $"硬盘编号：{string.Join("、", codes)}";
        }

        private static string? BuildSerialClause(YearlyArchiveOutboundItem item)
        {
            var serials = CollectSerialNumbers([item]);
            return serials.Count == 0 ? null : $"序列号：{string.Join("、", serials)}";
        }

        private static string? BuildCapacityClause(YearlyArchiveOutboundItem item)
        {
            string capacity = item.SelfDiskCapacity?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(capacity) ? null : $"容量：{capacity}";
        }

        private static string? BuildReturnClause(YearlyArchiveOutboundItem item)
        {
            if (IsInStockBlankRequisition(item) || item.ShowRequisitionedDiskNeedReturn)
            {
                bool needReturn = item.RequisitionedDiskNeedReturn;
                string clause = needReturn ? "硬盘归还：需归还" : "硬盘归还：不需归还";
                if (needReturn)
                {
                    AppendReturnDate(ref clause, item.ExpectedReturnDate);
                }

                return clause;
            }

            if (string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
                && (ArchiveOutboundDomainValues.IsHardDiskStorageCarrier(item.StorageCarrierType)
                    || item.MediaType?.Contains("硬盘", StringComparison.OrdinalIgnoreCase) == true))
            {
                string clause = item.NeedReturn ? "硬盘归还：需归还" : "硬盘归还：不需归还";
                if (item.NeedReturn)
                {
                    AppendReturnDate(ref clause, item.ExpectedReturnDate);
                }

                return clause;
            }

            if (string.Equals(item.ElectronicMediumType, ArchiveOutboundDomainValues.DuplicateMediumSelfHardDisk, StringComparison.Ordinal))
            {
                return "硬盘归还：自备介质，资料不归还资料室";
            }

            return null;
        }

        private static void AppendReturnDate(ref string clause, DateTime? expectedReturnDate)
        {
            if (expectedReturnDate.HasValue)
            {
                clause += $"（应还日期 {expectedReturnDate.Value:yyyy-MM-dd}）";
            }
        }

        private static bool IsInStockBlankRequisition(YearlyArchiveOutboundItem item) =>
            string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeDuplicate, StringComparison.Ordinal)
            && (string.Equals(item.ElectronicMediaSource, ArchiveOutboundDomainValues.ElectronicMediaSourceInStockBlank, StringComparison.Ordinal)
                || string.Equals(item.ElectronicMediumType, ArchiveOutboundDomainValues.DuplicateMediumInStockBlank, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(item.RequisitionedDiskCode)
                || item.RequisitionedMediumId is > 0);

        private static List<string> CollectDiskCodes(IReadOnlyList<YearlyArchiveOutboundItem> items)
        {
            var codes = new List<string>();
            foreach (var item in items)
            {
                AppendDistinct(codes, item.RequisitionedDiskCode);
                foreach (string code in ParseStringListJson(item.SelfDiskCodesJson))
                {
                    AppendDistinct(codes, code);
                }

                foreach (string code in SplitCodes(item.FiledHardDiskCodes))
                {
                    AppendDistinct(codes, code);
                }
            }

            return codes;
        }

        private static List<string> CollectSerialNumbers(IReadOnlyList<YearlyArchiveOutboundItem> items)
        {
            var serials = new List<string>();
            foreach (var item in items)
            {
                AppendDistinct(serials, item.SelfDiskSerialNo);
                foreach (string serial in ParseStringListJson(item.SelfDiskSerialNumbersJson))
                {
                    AppendDistinct(serials, serial);
                }
            }

            return serials;
        }

        private static IEnumerable<string> SplitCodes(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                yield break;
            }

            foreach (string part in raw.Split(['、', ',', ';', '；', '|', '/', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    yield return part;
                }
            }
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

        private static void AppendDistinct(List<string> target, string? value)
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

        private static void AppendSegment(List<string> segments, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            segments.Add(value.Trim());
        }
    }
}
