using DocMgr.Models.YearlyArchive;



namespace DocMgr.Services.YearlyArchive

{

    /// <summary>

    /// 资料归还明细的文字摘要与打印描述。

    /// </summary>

    internal static class ArchiveReturnItemDescription

    {

        public static string BuildMaterialSummary(IReadOnlyCollection<YearlyArchiveReturnItem> items)

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



        public static IReadOnlyList<string> BuildPrintDetailLines(IReadOnlyCollection<YearlyArchiveReturnItem> items) =>
            BuildPrintDetailLines(items, includeCopyCountSummary: true);

        public static IReadOnlyList<string> BuildPrintDetailLines(
            IReadOnlyCollection<YearlyArchiveReturnItem> items,
            IReadOnlyDictionary<int, string>? classificationByFilingFactId) =>
            BuildPrintDetailLines(items, includeCopyCountSummary: true, classificationByFilingFactId: classificationByFilingFactId);



        public static IReadOnlyList<string> BuildBorrowPrintDetailLines(IReadOnlyCollection<YearlyArchiveReturnItem> items) =>

            BuildPrintDetailLines(items, includeCopyCountSummary: false, borrowedOnly: true);



        public static IReadOnlyList<string> BuildIntactReturnPrintDetailLines(IReadOnlyCollection<YearlyArchiveReturnItem> items) =>

            BuildPrintDetailLines(

                items.Where(item => ArchiveReturnDomainValues.ResolveIntactReturnCopyCount(item) > 0).ToList(),

                includeCopyCountSummary: true,

                intactOnly: true);



        public static IReadOnlyList<string> BuildLossPrintDetailLines(IReadOnlyCollection<YearlyArchiveReturnItem> items) =>

            BuildPrintDetailLines(

                items.Where(item => ArchiveReturnDomainValues.HasLossReturnCopies(item)).ToList(),

                includeCopyCountSummary: true,

                lossOnly: true);



        private static IReadOnlyList<string> BuildPrintDetailLines(

            IReadOnlyCollection<YearlyArchiveReturnItem> items,

            bool includeCopyCountSummary,

            bool borrowedOnly = false,

            bool intactOnly = false,

            bool lossOnly = false,
            IReadOnlyDictionary<int, string>? classificationByFilingFactId = null)

        {

            var lines = items

                .OrderBy(item => item.SortOrder)

                .Select((item, index) => FormatPrintDetailLine(item, index + 1, includeCopyCountSummary, borrowedOnly, intactOnly, lossOnly, classificationByFilingFactId))

                .Where(line => !string.IsNullOrWhiteSpace(line))

                .ToList();



            return lines.Count > 0 ? lines : ["(无)"];

        }



        private static string BuildSummaryLabel(YearlyArchiveReturnItem item)

        {

            string materialName = item.MaterialName?.Trim() ?? string.Empty;

            string itemName = item.ItemName?.Trim() ?? string.Empty;



            if (!string.IsNullOrWhiteSpace(materialName) && !string.IsNullOrWhiteSpace(itemName)

                && !string.Equals(materialName, itemName, StringComparison.Ordinal))

            {

                return $"{materialName}/{itemName}";

            }



            return !string.IsNullOrWhiteSpace(materialName) ? materialName : itemName;

        }



        private static string FormatPrintDetailLine(

            YearlyArchiveReturnItem item,

            int index,

            bool includeCopyCountSummary,

            bool borrowedOnly,

            bool intactOnly,

            bool lossOnly,
            IReadOnlyDictionary<int, string>? classificationByFilingFactId = null)

        {

            var segments = new List<string>();



            if (item.ItemArchiveYear is int archiveYear)

            {

                segments.Add($"{archiveYear}年");

            }



            if (!string.IsNullOrWhiteSpace(item.ItemProjectName))

            {

                segments.Add(item.ItemProjectName.Trim());

            }



            string mediaKind = item.MediaKind?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(mediaKind))

            {

                segments.Add(mediaKind);

            }



            if (!string.IsNullOrWhiteSpace(item.MediaType))
            {
                segments.Add(item.MediaType.Trim());
            }

            if (classificationByFilingFactId != null
                && item.FilingFactId > 0
                && classificationByFilingFactId.TryGetValue(item.FilingFactId, out string? classification)
                && !string.IsNullOrWhiteSpace(classification))
            {
                segments.Add(classification.Trim());
            }



            string materialDisplay = BuildSummaryLabel(item);

            if (!string.IsNullOrWhiteSpace(materialDisplay))

            {

                segments.Add(materialDisplay);

            }



            if (borrowedOnly)

            {

                segments.Add(string.IsNullOrWhiteSpace(item.ConfidentialLevelDisplay)

                    ? "密级—"

                    : $"密级{item.ConfidentialLevelDisplay}");

            }

            else if (!string.IsNullOrWhiteSpace(item.ConfidentialLevelDisplay))

            {

                segments.Add($"密级{item.ConfidentialLevelDisplay}");

            }



            if (!string.IsNullOrWhiteSpace(item.SelectionScopeDisplay)

                && !string.Equals(item.SelectionScopeDisplay, "整资料子项", StringComparison.Ordinal))

            {

                segments.Add($"范围{item.SelectionScopeDisplay.Trim()}");

            }



            segments.Add(item.UsageModeDisplay);



            if (borrowedOnly)

            {

                segments.Add($"借出{ArchiveReturnDomainValues.ResolveBorrowedCopyCount(item)}份");

            }

            else if (intactOnly)

            {

                segments.Add($"完好归还{ArchiveReturnDomainValues.ResolveIntactReturnCopyCount(item)}份");

            }

            else if (lossOnly)

            {

                segments.Add($"灭失{ArchiveReturnDomainValues.ResolveLossCopyCount(item)}份");

            }

            else if (includeCopyCountSummary)

            {

                segments.Add(ArchiveReturnDomainValues.BuildReturnCopyCountSummary(item));

            }



            if (!string.IsNullOrWhiteSpace(item.ContainerCode))

            {

                segments.Add($"盒/袋{item.ContainerCode.Trim()}");

            }



            if (!string.IsNullOrWhiteSpace(item.StorageLocation))

            {

                segments.Add($"原存位置{item.StorageLocation.Trim()}");

            }



            if (!string.IsNullOrWhiteSpace(item.Remark))

            {

                segments.Add($"备注{item.Remark.Trim()}");

            }



            return segments.Count == 0

                ? string.Empty

                : $"({index}) {string.Join("，", segments)}";

        }

    }

}

