using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.YearlyArchive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocMgr.Services.Cabinets
{
    /// <summary>
    /// 档口布局计算与内部类型定义。
    /// 与主流程拆分后，便于独立维护渲染/容量算法。
    /// </summary>
    public partial class CabinetOpenLayoutService
    {
        private static string BuildSourceSummaryText(IReadOnlyCollection<string> sourceTypes, IEnumerable<ExpandedArchiveBoxAssignment> group)
        {
            if (sourceTypes.Count == 0)
            {
                return "历史存档";
            }

            if (sourceTypes.Count > 1)
            {
                return "多来源";
            }

            string sourceType = sourceTypes.First();
            if (string.Equals(sourceType, "航摄影像", StringComparison.OrdinalIgnoreCase))
            {
                string tableSuffixText = string.Join("/", group
                    .Select(item => ExtractSourceSuffix(
                        item.SortCategory,
                        HistoryArchiveImportTableNameSupport.AerialPhotoPrefix,
                        HistoryArchiveImportTableNameSupport.LegacyAerialPhotoPrefix))
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(text => text, StringComparer.OrdinalIgnoreCase));

                return string.IsNullOrWhiteSpace(tableSuffixText)
                    ? sourceType
                    : $"{sourceType}({tableSuffixText})";
            }

            if (!string.Equals(sourceType, "地形图", StringComparison.OrdinalIgnoreCase))
            {
                return sourceType;
            }

            string scaleText = string.Join("/", group
                .Select(item => item.CategoryText)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(text => text));

            return string.IsNullOrWhiteSpace(scaleText) ? sourceType : $"{sourceType}({scaleText})";
        }

        private static string BuildArchiveIdentifierText(string sourceSummaryText, IEnumerable<ExpandedArchiveBoxAssignment> group)
        {
            if (string.Equals(sourceSummaryText, "年度资料", StringComparison.OrdinalIgnoreCase))
            {
                return BuildYearlyArchiveIdentifierText(group);
            }

            return BuildHistoryArchiveIdentifierText(sourceSummaryText, group);
        }

        /// <summary>
        /// 历史存档盒脊/盒面标识正文：按地形图/航摄影像/其他图件结构化多行。
        /// </summary>
        private static string BuildHistoryArchiveIdentifierText(string sourceSummaryText, IEnumerable<ExpandedArchiveBoxAssignment> group)
        {
            var items = group as IReadOnlyList<ExpandedArchiveBoxAssignment> ?? group.ToList();
            if (items.Count == 0)
            {
                return string.IsNullOrWhiteSpace(sourceSummaryText) ? "历史存档" : sourceSummaryText.Trim();
            }

            var sourceTypes = items
                .Select(item => item.SourceType)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sourceTypes.Count > 1
                || string.Equals(sourceSummaryText, "多来源", StringComparison.OrdinalIgnoreCase))
            {
                return BuildMultiSourceHistoryIdentifierText(items);
            }

            string sourceType = sourceTypes.Count > 0 ? sourceTypes[0] : "历史存档";
            if (items.Count == 1)
            {
                return BuildSingleHistoryItemIdentifierText(sourceType, items[0]);
            }

            return BuildMultiItemSameTypeHistoryIdentifierText(sourceType, items);
        }

        private static string BuildSingleHistoryItemIdentifierText(string sourceType, ExpandedArchiveBoxAssignment item)
        {
            var lines = new List<string>();
            string typeLine = BuildHistoryTypeLine(sourceType, new[] { item });
            if (!string.IsNullOrWhiteSpace(typeLine))
            {
                lines.Add(typeLine);
            }

            if (string.Equals(sourceType, "地形图", StringComparison.OrdinalIgnoreCase))
            {
                AppendHistoryLabeledLine(lines, "图号", item.IdentifierText);
                AppendHistoryLabeledLine(lines, "图名", item.SortSecondary);
            }
            else if (string.Equals(sourceType, "航摄影像", StringComparison.OrdinalIgnoreCase))
            {
                AppendHistoryLabeledLine(lines, "测区", item.IdentifierText);
                string boxContents = item.SortSecondary?.Trim() ?? string.Empty;
                string surveyArea = item.IdentifierText?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(boxContents)
                    && !string.Equals(boxContents, surveyArea, StringComparison.OrdinalIgnoreCase))
                {
                    AppendHistoryLabeledLine(lines, "盒内", boxContents);
                }
            }
            else if (string.Equals(sourceType, "其他图件", StringComparison.OrdinalIgnoreCase))
            {
                AppendHistoryLabeledLine(lines, "序号", item.IdentifierText);
                AppendHistoryLabeledLine(lines, "资料内容", item.SortSecondary);
            }
            else if (!string.IsNullOrWhiteSpace(item.IdentifierText))
            {
                lines.Add(item.IdentifierText.Trim());
            }

            if (lines.Count == 0)
            {
                return string.IsNullOrWhiteSpace(sourceType) ? "历史存档" : sourceType;
            }

            return string.Join("\n", lines);
        }

        private static string BuildMultiItemSameTypeHistoryIdentifierText(
            string sourceType,
            IReadOnlyList<ExpandedArchiveBoxAssignment> items)
        {
            var lines = new List<string>();
            string typeLine = BuildHistoryTypeLine(sourceType, items);
            if (!string.IsNullOrWhiteSpace(typeLine))
            {
                lines.Add(typeLine);
            }

            var identifiers = items
                .Select(item => item.IdentifierText)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string identifier in identifiers.Take(2))
            {
                lines.Add(identifier);
            }

            if (identifiers.Count > 2 || items.Count > 2)
            {
                lines.Add($"等{items.Count}条");
            }

            if (lines.Count == 0)
            {
                return string.IsNullOrWhiteSpace(sourceType) ? "历史存档" : sourceType;
            }

            return string.Join("\n", lines);
        }

        private static string BuildMultiSourceHistoryIdentifierText(IReadOnlyList<ExpandedArchiveBoxAssignment> items)
        {
            var lines = new List<string> { "多来源" };
            foreach (var typeGroup in items
                .GroupBy(item => item.SourceType, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Min(item => item.SourceSortOrder)))
            {
                if (lines.Count >= 4)
                {
                    break;
                }

                string sourceType = typeGroup.Key;
                string? identifier = typeGroup
                    .Select(item => item.IdentifierText)
                    .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

                lines.Add(string.IsNullOrWhiteSpace(identifier)
                    ? sourceType
                    : $"{sourceType} {identifier.Trim()}");
            }

            return string.Join("\n", lines);
        }

        private static string BuildHistoryTypeLine(string sourceType, IEnumerable<ExpandedArchiveBoxAssignment> items)
        {
            if (string.Equals(sourceType, "航摄影像", StringComparison.OrdinalIgnoreCase))
            {
                string scaleText = string.Join("/", items
                    .Select(item => item.CategoryText)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(text => text, StringComparer.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(scaleText))
                {
                    return $"{sourceType} {scaleText}";
                }

                string suffixText = string.Join("/", items
                    .Select(item => ExtractSourceSuffix(
                        item.SortCategory,
                        HistoryArchiveImportTableNameSupport.AerialPhotoPrefix,
                        HistoryArchiveImportTableNameSupport.LegacyAerialPhotoPrefix))
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(text => text, StringComparer.OrdinalIgnoreCase));

                return string.IsNullOrWhiteSpace(suffixText)
                    ? sourceType
                    : $"{sourceType} {suffixText}";
            }

            string scaleOrCategory = string.Join("/", items
                .Select(item => item.CategoryText)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(text => text, StringComparer.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(scaleOrCategory)
                ? sourceType
                : $"{sourceType} {scaleOrCategory}";
        }

        private static void AppendHistoryLabeledLine(ICollection<string> lines, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            lines.Add($"{label} {value.Trim()}");
        }

        /// <summary>
        /// 历史存档盒签底部数量：同类型汇总幅/张，否则回退 N条。
        /// </summary>
        private static string BuildHistoryArchiveCountText(IEnumerable<ExpandedArchiveBoxAssignment> group)
        {
            var items = group as IReadOnlyList<ExpandedArchiveBoxAssignment> ?? group.ToList();
            if (items.Count == 0)
            {
                return string.Empty;
            }

            var sourceTypes = items
                .Select(item => item.SourceType)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sourceTypes.Count > 1)
            {
                return $"{items.Count}条";
            }

            int total = 0;
            string? unit = null;
            foreach (var item in items)
            {
                if (!TryParseHistoryQuantity(item.QuantityText, out int value, out string parsedUnit))
                {
                    return $"{items.Count}条";
                }

                if (unit == null)
                {
                    unit = parsedUnit;
                }
                else if (!string.Equals(unit, parsedUnit, StringComparison.Ordinal))
                {
                    return $"{items.Count}条";
                }

                total += value;
            }

            return total > 0 && !string.IsNullOrWhiteSpace(unit)
                ? $"{total}{unit}"
                : $"{items.Count}条";
        }

        private static bool TryParseHistoryQuantity(string? quantityText, out int value, out string unit)
        {
            value = 0;
            unit = string.Empty;
            if (string.IsNullOrWhiteSpace(quantityText))
            {
                return false;
            }

            var match = Regex.Match(quantityText.Trim(), @"^(\d+)\s*(幅|张)$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            if (!int.TryParse(match.Groups[1].Value, out value) || value <= 0)
            {
                return false;
            }

            unit = match.Groups[2].Value;
            return true;
        }

        private static string BuildYearlyArchiveIdentifierText(IEnumerable<ExpandedArchiveBoxAssignment> group)
        {
            if (!TryResolveYearlyArchiveDisplayFields(group, out string archiveSequenceNoShort, out string yearText, out string projectText, out string materialName))
            {
                return "【年度-申】";
            }

            return string.Join("\n", new[]
            {
                archiveSequenceNoShort,
                FormatYearlyArchiveLabel("年度", yearText),
                FormatYearlyArchiveLabel("项目", projectText),
                string.IsNullOrWhiteSpace(materialName) ? string.Empty : FormatYearlyArchiveLabel("资料", materialName)
            }.Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        private static bool TryResolveYearlyArchiveDisplayFields(
            IEnumerable<ExpandedArchiveBoxAssignment> group,
            out string archiveSequenceNoShort,
            out string yearText,
            out string projectText,
            out string materialName)
        {
            archiveSequenceNoShort = string.Empty;
            yearText = string.Empty;
            projectText = string.Empty;
            materialName = string.Empty;

            var yearlyRecord = group
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.FormNo)
                    || !string.IsNullOrWhiteSpace(item.SortCategory)
                    || !string.IsNullOrWhiteSpace(item.ProjectName)
                    || !string.IsNullOrWhiteSpace(item.MaterialName));

            if (yearlyRecord == null)
            {
                return false;
            }

            string archiveSequenceNo = group
                .Select(item => item.ArchiveSequenceNo)
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
                ?? string.Empty;
            archiveSequenceNoShort = ArchiveContainerCodeDisplaySupport.ToShortDisplayCode(archiveSequenceNo);
            yearText = ResolveYearText(yearlyRecord.SortCategory, yearlyRecord.FormNo);
            projectText = string.IsNullOrWhiteSpace(yearlyRecord.ProjectName)
                ? "—"
                : yearlyRecord.ProjectName.Trim();
            materialName = string.IsNullOrWhiteSpace(yearlyRecord.MaterialName)
                ? string.Empty
                : yearlyRecord.MaterialName.Trim();
            return true;
        }

        private static string FormatYearlyArchiveLabel(string label, string? value)
        {
            string resolvedValue = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
            return $"{label} {resolvedValue}";
        }

        private static string ResolveYearText(string sortCategory, string formNo)
        {
            if (!string.IsNullOrWhiteSpace(sortCategory))
            {
                var sortCategoryYear = Regex.Match(sortCategory, @"\d{4}");
                if (sortCategoryYear.Success)
                {
                    return sortCategoryYear.Value;
                }
            }

            if (!string.IsNullOrWhiteSpace(formNo))
            {
                var formYear = Regex.Match(formNo, @"\d{4}");
                if (formYear.Success)
                {
                    return formYear.Value;
                }
            }

            return "年度";
        }

        private static string ResolveFormNoLastSegment(string formNo)
        {
            if (string.IsNullOrWhiteSpace(formNo))
            {
                return "未登记序号";
            }

            string normalized = formNo.Trim();
            var segments = normalized.Split(['-', '—', '_', '/', '\\', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                return segments[^1];
            }

            return normalized;
        }

        private static string BuildTopoDetailText(TopoMap map)
        {
            return string.Join(" / ", new[] { map.CoordinateSystem, map.Region }
                .Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        private static string BuildDateText(params string[] values)
        {
            return string.Join(" / ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string BuildSlotToolTipText(CabinetOpenRequest request, string slotCode, IReadOnlyList<SlotBoxInfo> boxes, double ratio, string summary, string remainingText, string layoutMode, bool isCrossFaceLinked, bool isSpecialRule, string specialRuleText)
        {
            string boxSpecs = string.Join("；", boxes
                .Select(box => string.IsNullOrWhiteSpace(box.BoxSpecification) ? "未登记规格" : box.BoxSpecification)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(text => text, StringComparer.OrdinalIgnoreCase));

            var lines = new List<string>
            {
                $"柜体：{request.CabinetName}",
                $"面别：{request.Face}",
                $"格口：{slotCode}",
                $"利用率：{FormatPercent(ratio)}",
                $"容量：{summary}",
                $"剩余：{remainingText}",
                $"布局：{layoutMode}",
                $"当前面盒数：{boxes.Count}"
            };

            if (!string.IsNullOrWhiteSpace(boxSpecs))
            {
                lines.Add($"盒规格：{boxSpecs}");
            }

            if (isSpecialRule)
            {
                lines.Add($"特例：{specialRuleText}");
            }

            return string.Join("\n", lines);
        }

        private static SlotBoxInfo CreateSlotBoxInfo(IGrouping<string, ExpandedArchiveBoxAssignment> group, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup)
        {
            var first = group.First();
            return new SlotBoxInfo(
                first.Parsed.BoxCode,
                first.Parsed.SequenceIndex,
                ResolveBoxSpecification(group, placementLookup),
                ResolvePlacementMode(first.Parsed.BoxCode, placementLookup),
                group.Select(item => item.SourceType).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        private static string ResolveSlotCode(ExpandedArchiveBoxAssignment assignment, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup)
        {
            ArgumentNullException.ThrowIfNull(assignment);

            if (placementLookup.TryGetValue(assignment.Parsed.BoxCode, out var placement)
                && !string.IsNullOrWhiteSpace(placement.SlotCode))
            {
                return placement.SlotCode;
            }

            return assignment.Parsed.SlotCode;
        }

        private static string ResolveFaceCode(ExpandedArchiveBoxAssignment assignment, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup)
        {
            ArgumentNullException.ThrowIfNull(assignment);

            if (placementLookup.TryGetValue(assignment.Parsed.BoxCode, out var placement)
                && !string.IsNullOrWhiteSpace(placement.FaceCode))
            {
                return placement.FaceCode;
            }

            return assignment.Parsed.Face.ToString();
        }

        private static string ResolveBoxSpecification(IEnumerable<ExpandedArchiveBoxAssignment> assignments, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup)
        {
            var groupedAssignments = assignments.ToList();
            if (groupedAssignments.Count == 0)
            {
                return string.Empty;
            }

            string boxCode = groupedAssignments[0].Parsed.BoxCode;
            if (placementLookup.TryGetValue(boxCode, out var placement)
                && !string.IsNullOrWhiteSpace(placement.BoxSpecification))
            {
                return placement.BoxSpecification;
            }

            return string.Join("/", groupedAssignments
                .Select(item => item.BoxSpecification)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(text => text, StringComparer.OrdinalIgnoreCase));
        }

        private static string ResolvePlacementMode(string boxCode, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup)
        {
            if (placementLookup.TryGetValue(boxCode, out var placement)
                && !string.IsNullOrWhiteSpace(placement.PlacementMode))
            {
                return placement.PlacementMode;
            }

            return "SpineOut";
        }

        private static Dictionary<string, BoxRenderLayout> CalculateBoxLayouts(IReadOnlyList<IGrouping<string, ExpandedArchiveBoxAssignment>> boxGroups, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup, IReadOnlyDictionary<string, ArchiveBoxSpecification> boxSpecificationLookup, double slotCanvasWidth, double slotCanvasHeight)
        {
            const double gap = 0.6d;

            // 档内序号齐全时按序号占位（含空序号留白），优先于面向外紧凑排布。
            bool allHaveSequence = boxGroups.Count > 0
                && boxGroups.All(group => group.First().Parsed.SequenceIndex > 0);
            if (allHaveSequence)
            {
                return CalculateSequentialLayouts(boxGroups, placementLookup, boxSpecificationLookup, slotCanvasWidth, slotCanvasHeight, gap);
            }

            var frontOutBoxGroups = boxGroups
                .Where(group => string.Equals(ResolvePlacementMode(group.Key, placementLookup), "FrontOut", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (frontOutBoxGroups.Count == boxGroups.Count && frontOutBoxGroups.Count > 0 && frontOutBoxGroups.Count <= 4)
            {
                return CalculateFrontOutLayouts(frontOutBoxGroups, placementLookup, boxSpecificationLookup, slotCanvasWidth, slotCanvasHeight, gap);
            }

            if (frontOutBoxGroups.Count > 0 && frontOutBoxGroups.Count <= 4 && frontOutBoxGroups.Count < boxGroups.Count)
            {
                var mixedLayouts = CalculateMixedLayouts(boxGroups, placementLookup, boxSpecificationLookup, slotCanvasWidth, slotCanvasHeight, gap, frontOutBoxGroups.Count);
                if (mixedLayouts != null)
                {
                    return mixedLayouts;
                }
            }

            return CalculateSequentialLayouts(boxGroups, placementLookup, boxSpecificationLookup, slotCanvasWidth, slotCanvasHeight, gap);
        }

        private static Dictionary<string, BoxRenderLayout>? CalculateMixedLayouts(IReadOnlyList<IGrouping<string, ExpandedArchiveBoxAssignment>> boxGroups, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup, IReadOnlyDictionary<string, ArchiveBoxSpecification> boxSpecificationLookup, double slotCanvasWidth, double slotCanvasHeight, double gap, int frontOutCount)
        {
            var layouts = new Dictionary<string, BoxRenderLayout>(StringComparer.OrdinalIgnoreCase);
            double currentX = 0d;
            double currentBaseline = slotCanvasHeight;
            double currentRowHeight = 0d;
            int currentFrontOutIndex = 0;

            foreach (var boxGroup in boxGroups)
            {
                string boxSpecification = ResolveBoxSpecification(boxGroup, placementLookup);
                string placementMode = ResolvePlacementMode(boxGroup.Key, placementLookup);
                double boxWidth = ResolveOccupiedWidth(placementMode, boxSpecification, boxSpecificationLookup);
                double boxHeight = ResolveBoxHeight(boxSpecification, boxSpecificationLookup);

                if (string.Equals(placementMode, "FrontOut", StringComparison.OrdinalIgnoreCase))
                {
                    boxWidth = ResolveFrontOutWidth(frontOutCount, currentFrontOutIndex, boxWidth <= 0d ? 23d : boxWidth);
                    currentFrontOutIndex++;
                }
                else if (boxWidth <= 0d)
                {
                    boxWidth = 10d;
                }

                if (boxHeight <= 0d)
                {
                    boxHeight = slotCanvasHeight;
                }

                bool shouldWrap = currentX > 0d && currentX + boxWidth > slotCanvasWidth;
                if (shouldWrap)
                {
                    currentBaseline -= currentRowHeight + gap;
                    currentX = 0d;
                    currentRowHeight = 0d;
                }

                double top = Math.Max(0d, currentBaseline - Math.Min(boxHeight, slotCanvasHeight));
                layouts[boxGroup.Key] = new BoxRenderLayout(currentX, top, boxWidth, Math.Min(boxHeight, slotCanvasHeight));
                currentX += boxWidth + gap;
                currentRowHeight = Math.Max(currentRowHeight, Math.Min(boxHeight, slotCanvasHeight));
            }

            return layouts;
        }

        private static Dictionary<string, BoxRenderLayout> CalculateSequentialLayouts(IReadOnlyList<IGrouping<string, ExpandedArchiveBoxAssignment>> boxGroups, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup, IReadOnlyDictionary<string, ArchiveBoxSpecification> boxSpecificationLookup, double slotCanvasWidth, double slotCanvasHeight, double gap)
        {
            var layouts = new Dictionary<string, BoxRenderLayout>(StringComparer.OrdinalIgnoreCase);
            var indexedGroups = boxGroups
                .Select(group => (Group: group, SequenceIndex: group.First().Parsed.SequenceIndex))
                .ToList();
            bool reserveSequenceGaps = indexedGroups.Count > 0
                && indexedGroups.All(item => item.SequenceIndex > 0);

            var measurements = CalculateBoxMeasurements(boxGroups, placementLookup, boxSpecificationLookup, slotCanvasHeight);
            if (!reserveSequenceGaps)
            {
                double currentX = 0d;
                double currentBaseline = slotCanvasHeight;
                double currentRowHeight = 0d;

                foreach (var measurement in measurements)
                {
                    bool shouldWrap = currentX > 0d && currentX + measurement.Width > slotCanvasWidth;
                    if (shouldWrap)
                    {
                        currentBaseline -= currentRowHeight + gap;
                        currentX = 0d;
                        currentRowHeight = 0d;
                    }

                    double top = Math.Max(0d, currentBaseline - measurement.Height);
                    layouts[measurement.BoxCode] = new BoxRenderLayout(currentX, top, measurement.Width, measurement.Height);
                    currentX += measurement.Width + gap;
                    currentRowHeight = Math.Max(currentRowHeight, measurement.Height);
                }

                return layouts;
            }

            // 有档内序号时按 1..max 占位：迁出留下的空序号保留横向空间，显示顺序与空间编号一致。
            var measurementByCode = measurements.ToDictionary(
                item => item.BoxCode,
                item => item,
                StringComparer.OrdinalIgnoreCase);
            double spacerWidth = measurementByCode.Values
                .Select(item => item.Width)
                .DefaultIfEmpty(10d)
                .Average();
            var groupBySequence = indexedGroups
                .GroupBy(item => item.SequenceIndex)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Group);
            int maxSequence = indexedGroups.Max(item => item.SequenceIndex);

            double cursorX = 0d;
            double baseline = slotCanvasHeight;
            double rowHeight = 0d;
            for (int sequence = 1; sequence <= maxSequence; sequence++)
            {
                if (groupBySequence.TryGetValue(sequence, out var boxGroup)
                    && measurementByCode.TryGetValue(boxGroup.Key, out var measurement))
                {
                    bool shouldWrap = cursorX > 0d && cursorX + measurement.Width > slotCanvasWidth;
                    if (shouldWrap)
                    {
                        baseline -= rowHeight + gap;
                        cursorX = 0d;
                        rowHeight = 0d;
                    }

                    double top = Math.Max(0d, baseline - measurement.Height);
                    layouts[boxGroup.Key] = new BoxRenderLayout(cursorX, top, measurement.Width, measurement.Height);
                    cursorX += measurement.Width + gap;
                    rowHeight = Math.Max(rowHeight, measurement.Height);
                }
                else
                {
                    bool shouldWrap = cursorX > 0d && cursorX + spacerWidth > slotCanvasWidth;
                    if (shouldWrap)
                    {
                        baseline -= rowHeight + gap;
                        cursorX = 0d;
                        rowHeight = 0d;
                    }

                    cursorX += spacerWidth + gap;
                }
            }

            return layouts;
        }

        private static Dictionary<string, BoxRenderLayout> CalculateFrontOutLayouts(IReadOnlyList<IGrouping<string, ExpandedArchiveBoxAssignment>> boxGroups, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup, IReadOnlyDictionary<string, ArchiveBoxSpecification> boxSpecificationLookup, double slotCanvasWidth, double slotCanvasHeight, double gap)
        {
            var measurements = boxGroups
                .Select((boxGroup, index) =>
                {
                    string boxSpecification = ResolveBoxSpecification(boxGroup, placementLookup);
                    double fullWidth = ResolveOccupiedWidth("FrontOut", boxSpecification, boxSpecificationLookup);
                    double height = ResolveBoxHeight(boxSpecification, boxSpecificationLookup);
                    if (fullWidth <= 0d)
                    {
                        fullWidth = 23d;
                    }

                    if (height <= 0d)
                    {
                        height = slotCanvasHeight;
                    }

                    return new BoxMeasurement(boxGroup.Key, ResolveFrontOutWidth(boxGroups.Count, index, fullWidth), Math.Min(height, slotCanvasHeight));
                })
                .ToList();

            double currentX = 0d;
            var layouts = new Dictionary<string, BoxRenderLayout>(StringComparer.OrdinalIgnoreCase);

            foreach (var measurement in measurements)
            {
                double top = Math.Max(0d, slotCanvasHeight - measurement.Height);
                layouts[measurement.BoxCode] = new BoxRenderLayout(currentX, top, measurement.Width, measurement.Height);
                currentX += measurement.Width + gap;
            }

            return layouts;
        }

        private static List<BoxMeasurement> CalculateBoxMeasurements(IReadOnlyList<IGrouping<string, ExpandedArchiveBoxAssignment>> boxGroups, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup, IReadOnlyDictionary<string, ArchiveBoxSpecification> boxSpecificationLookup, double slotCanvasHeight)
        {
            return boxGroups
                .Select(boxGroup =>
                {
                    string boxSpecification = ResolveBoxSpecification(boxGroup, placementLookup);
                    string placementMode = ResolvePlacementMode(boxGroup.Key, placementLookup);
                    double boxWidth = ResolveOccupiedWidth(placementMode, boxSpecification, boxSpecificationLookup);
                    double boxHeight = ResolveBoxHeight(boxSpecification, boxSpecificationLookup);

                    if (boxWidth <= 0d)
                    {
                        boxWidth = 10d;
                    }

                    if (boxHeight <= 0d)
                    {
                        boxHeight = slotCanvasHeight;
                    }

                    return new BoxMeasurement(boxGroup.Key, boxWidth, boxHeight);
                })
                .ToList();
        }

        private static (double UtilizationRatio, string CapacitySummaryText, string RemainingSummaryText, string LayoutModeText) ResolvePlacementMetrics(IReadOnlyList<SlotBoxInfo> boxes, IReadOnlyDictionary<string, ArchiveBoxSpecification> lookup, decimal slotWidth)
        {
            if (boxes.Count == 0 || slotWidth <= 0m)
            {
                return (0d, "已放 0盒", "余 0盒", "默认盒脊向外");
            }

            var orderedBoxes = boxes
                .OrderBy(box => box.SequenceIndex)
                .ThenBy(box => box.BoxCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var distinctLayouts = boxes
                .Select(box => new { Specification = box.BoxSpecification?.Trim(), Mode = NormalizePlacementMode(box.PlacementMode) })
                .Distinct()
                .ToList();

            int capacity = ResolveStandardCapacity(lookup, slotWidth);
            int used = orderedBoxes.Count;
            double occupiedWidth = ResolveOccupiedSlotWidth(orderedBoxes, lookup);
            double ratio = Math.Max(0d, slotWidth <= 0m ? 0d : occupiedWidth / (double)slotWidth);
            string summary = capacity <= 0 ? $"已放 {used}盒" : $"已用 {used}/{capacity}盒";
            string remainingText = capacity <= 0
                ? "余 0盒"
                : used < capacity ? $"余 {capacity - used}盒" : used == capacity ? "已满" : $"超 {used - capacity}盒";
            string layoutModeText = distinctLayouts.Count == 1
                ? ResolveLayoutModeText(distinctLayouts[0].Mode)
                : "混合摆放（按摆放表计算）";

            return (ratio, summary, remainingText, layoutModeText);
        }

        private static string ResolveLayoutModeText(string placementMode)
        {
            return NormalizePlacementMode(placementMode) == "FrontOut"
                ? "盒面向外（按摆放表计算）"
                : "盒脊向外（按摆放表计算）";
        }

        private static int ResolveStandardCapacity(IReadOnlyDictionary<string, ArchiveBoxSpecification> lookup, decimal slotWidth)
        {
            if (slotWidth <= 0m)
            {
                return 0;
            }

            decimal standardThickness = lookup.TryGetValue("标准(10cm)", out var standardSpecification)
                ? standardSpecification.ThicknessCm
                : lookup.Values.FirstOrDefault(item => item.ThicknessCm == 10m)?.ThicknessCm ?? 10m;

            if (standardThickness <= 0m)
            {
                return 0;
            }

            return (int)Math.Floor(slotWidth / standardThickness);
        }

        private static double ResolveOccupiedSlotWidth(IReadOnlyList<SlotBoxInfo> boxes, IReadOnlyDictionary<string, ArchiveBoxSpecification> lookup)
        {
            int frontOutCount = boxes.Count(box => string.Equals(NormalizePlacementMode(box.PlacementMode), "FrontOut", StringComparison.OrdinalIgnoreCase));
            int frontOutIndex = 0;
            double occupiedWidth = 0d;

            foreach (var box in boxes)
            {
                double boxWidth = ResolveOccupiedWidth(box.PlacementMode, box.BoxSpecification, lookup);
                if (string.Equals(NormalizePlacementMode(box.PlacementMode), "FrontOut", StringComparison.OrdinalIgnoreCase))
                {
                    boxWidth = ResolveFrontOutWidth(frontOutCount, frontOutIndex, boxWidth <= 0d ? 23d : boxWidth);
                    frontOutIndex++;
                }
                else if (boxWidth <= 0d)
                {
                    boxWidth = 10d;
                }

                occupiedWidth += boxWidth;
            }

            return occupiedWidth;
        }

        private static decimal ResolveOccupiedSize(string placementMode, string? boxSpecification, IReadOnlyDictionary<string, ArchiveBoxSpecification> lookup)
        {
            string normalizedPlacementMode = NormalizePlacementMode(placementMode);
            return normalizedPlacementMode == "FrontOut"
                ? ResolveWidth(boxSpecification, lookup)
                : ResolveThickness(boxSpecification, lookup);
        }

        private static double ResolveOccupiedWidth(string placementMode, string? boxSpecification, IReadOnlyDictionary<string, ArchiveBoxSpecification> lookup)
        {
            return (double)ResolveOccupiedSize(placementMode, boxSpecification, lookup);
        }

        private static double ResolveBoxHeight(string? boxSpecification, IReadOnlyDictionary<string, ArchiveBoxSpecification> lookup)
        {
            if (!string.IsNullOrWhiteSpace(boxSpecification) && lookup.TryGetValue(boxSpecification.Trim(), out var specification))
            {
                return (double)specification.HeightCm;
            }

            return 30d;
        }

        private static string NormalizePlacementMode(string? placementMode)
        {
            return string.Equals(placementMode?.Trim(), "FrontOut", StringComparison.OrdinalIgnoreCase)
                ? "FrontOut"
                : "SpineOut";
        }

        private static decimal ResolveWidth(string? boxSpecification, IReadOnlyDictionary<string, ArchiveBoxSpecification> lookup)
        {
            if (!string.IsNullOrWhiteSpace(boxSpecification) && lookup.TryGetValue(boxSpecification.Trim(), out var specification))
            {
                return specification.WidthCm;
            }

            return 23m;
        }

        private static decimal ResolveThickness(string? boxSpecification, IReadOnlyDictionary<string, ArchiveBoxSpecification> lookup)
        {
            if (!string.IsNullOrWhiteSpace(boxSpecification) && lookup.TryGetValue(boxSpecification.Trim(), out var specification))
            {
                return specification.ThicknessCm;
            }

            return 10m;
        }

        private static double ResolveFrontOutWidth(int frontOutCount, int index, double fullWidth)
        {
            return frontOutCount switch
            {
                1 => fullWidth,
                2 => fullWidth / 2d,
                3 when index < 2 => fullWidth / 2d,
                3 => fullWidth,
                4 => fullWidth / 2d,
                _ => fullWidth
            };
        }

        private static string GetCabinetTypeCode(CabinetType cabinetType)
        {
            return cabinetType switch
            {
                CabinetType.Standard => "Standard",
                CabinetType.Vertical => "Vertical",
                CabinetType.Horizontal => "Horizontal",
                CabinetType.MagneticDisk => "MagneticDisk",
                _ => string.Empty
            };
        }

        private static (double Width, double Height) ResolveSlotCanvasSize(CabinetOpenRequest request, CabinetSlotSpecification? slotSpecification)
        {
            if (request.CabinetType == CabinetType.MagneticDisk)
            {
                // 防磁磁盘柜的 WidthCm/HeightCm 来自布局画布像素，不能用于档口物理尺寸推算。
                return (
                    (double)(slotSpecification?.WidthCm ?? 23.33m),
                    (double)(slotSpecification?.HeightCm ?? 16.67m));
            }

            return ((double)(slotSpecification?.WidthCm ?? 78m), (double)(slotSpecification?.HeightCm ?? 33m));
        }

        private static string FormatPercent(double ratio)
        {
            return $"{Math.Round(ratio * 100d, MidpointRounding.AwayFromZero)}%";
        }

        private static string ExtractSourceSuffix(string? sourceName, params string[] prefixes)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                return string.Empty;
            }

            var trimmed = sourceName.Trim();
            var suffix = trimmed;
            if (prefixes != null)
            {
                foreach (string prefix in prefixes)
                {
                    if (string.IsNullOrWhiteSpace(prefix))
                    {
                        continue;
                    }

                    if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        suffix = trimmed[prefix.Length..].Trim();
                        break;
                    }
                }
            }

            var match = Regex.Match(suffix, @"\d+\s*cm", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value.Replace(" ", string.Empty);
            }

            match = Regex.Match(suffix, @"\d+");
            return match.Success ? match.Value : suffix;
        }

        private static IEnumerable<string> SplitArchiveBoxCodes(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return Enumerable.Empty<string>();
            }

            return source
                .Split([';', '；', ',', '，', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static ParsedArchiveBox? ParseArchiveBox(string? boxCode)
        {
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                return null;
            }

            var segments = boxCode.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 4)
            {
                return null;
            }

            var cabinetAndFace = segments[0];
            if (cabinetAndFace.Length < 2)
            {
                return null;
            }

            var faceToken = cabinetAndFace[^1];
            CabinetFace face;
            if (faceToken == 'A' || faceToken == 'a')
            {
                face = CabinetFace.A;
            }
            else if (faceToken == 'B' || faceToken == 'b')
            {
                face = CabinetFace.B;
            }
            else
            {
                return null;
            }

            if (!int.TryParse(segments[1], out var layerIndex) || !int.TryParse(segments[2], out var columnIndex))
            {
                return null;
            }

            int.TryParse(segments[3], out var sequenceIndex);

            var cabinetName = CabinetNameNormalizer.Normalize(cabinetAndFace[..^1]);
            var slotCode = $"{layerIndex}-{columnIndex}";

            return new ParsedArchiveBox(cabinetName, face, layerIndex, columnIndex, sequenceIndex, slotCode, boxCode);
        }

        private sealed record ParsedArchiveBox(
            string CabinetName,
            CabinetFace Face,
            int LayerIndex,
            int ColumnIndex,
            int SequenceIndex,
            string SlotCode,
            string BoxCode);

        private sealed record ExpandedArchiveBoxAssignment(
            ParsedArchiveBox Parsed,
            bool IsMixedPlacement,
            string SourceBoxNumberText,
            IReadOnlyList<string> RelatedBoxCodes,
            string SourceType,
            int SourceSortOrder,
            string BoxSpecification,
            string CategoryText,
            string IdentifierText,
            string FormNo,
            string ProjectName,
            string MaterialName,
            string TitleText,
            string QuantityText,
            string DetailText,
            string DateText,
            string SortCategory,
            string SortPrimary,
            string SortSecondary,
            string ArchiveSequenceNo = "",
            int? YearlyArchiveBoxId = null,
            string CurrentMapNumber = "",
            HistoryArchiveBoxContentFields? HistoryFields = null);

        private sealed record BoxRenderLayout(double X, double Y, double Width, double Height);

        private sealed record BoxMeasurement(string BoxCode, double Width, double Height);

        private sealed record SlotBoxInfo(string BoxCode, int SequenceIndex, string BoxSpecification, string PlacementMode, IReadOnlyList<string> SourceTypes);

        private sealed record SlotMetrics(
            double UtilizationRatio,
            string UtilizationText,
            string CapacitySummaryText,
            string RemainingSummaryText,
            string LayoutModeText,
            string ToolTipText,
            bool IsCrossFaceLinked,
            bool IsSpecialRule,
            string SpecialRuleText);

        private readonly record struct MediumArchiveItemDetail(
            string MediumCode,
            string FormNo,
            string MaterialName,
            string ItemName,
            string MediaType,
            string MaterialCategory,
            string SubCategory,
            string DataOrganizationForm,
            string ArchivePurpose,
            string FilingStoragePath,
            decimal DataSizeMb);

        private readonly record struct MediumArchiveContext(
            string ElectronicArchiveNo,
            string StorageLocation,
            string StoragePath,
            string StorageCarrierType,
            string ContentSummary,
            string ProjectName,
            string Year,
            string ArchivedDateText,
            string ArchivedBy,
            string LinkedMediumCodes,
            string Disposition,
            int MediaCount,
            string SourceType,
            string Remarks,
            decimal UsedCapacityMb,
            IReadOnlyList<MediumArchiveItemDetail> MediumItems,
            int ElectronicArchiveUnitId = 0);
    }
}
