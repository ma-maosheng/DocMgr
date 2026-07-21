using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 归档容器（档案盒/电子介质袋）位置推荐与占位统计逻辑。
    /// </summary>
    public partial class ArchiveFilingService
    {
        private void UpsertArchiveBoxPlacement(YearlyArchiveBox box, DateTime updatedAt)
        {
            ArgumentNullException.ThrowIfNull(box);

            var placement = _archiveFilingRepository.GetArchiveBoxPlacementByCode(box.BoxLocationCode);

            string nowText = updatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            string sourceRecordKey = string.Join("|", box.RegisterRecords.Select(record => record.Id).Distinct().OrderBy(id => id));
            string normalizedPlacementMode = string.Equals(box.PlacementMode, "FrontOut", StringComparison.OrdinalIgnoreCase)
                ? "FrontOut"
                : "SpineOut";
            box.PlacementMode = normalizedPlacementMode;

            if (placement == null)
            {
                _archiveFilingRepository.AddArchiveBoxPlacement(new CabinetArchiveBoxPlacement
                {
                    BoxCode = box.BoxLocationCode,
                    BoxSpecification = NormalizeArchiveBoxSpecification(box.Specs),
                    CabinetName = box.CabinetName,
                    FaceCode = box.Side,
                    SlotCode = $"{box.Row}-{box.Column}",
                    PlacementMode = normalizedPlacementMode,
                    SourceType = "YearlyArchive",
                    SourceRecordKey = sourceRecordKey,
                    CreatedAt = nowText,
                    UpdatedAt = nowText,
                    UpdatedBy = box.ArchivedBy
                });
                return;
            }

            placement.BoxSpecification = NormalizeArchiveBoxSpecification(box.Specs);
            placement.CabinetName = box.CabinetName;
            placement.FaceCode = box.Side;
            placement.SlotCode = $"{box.Row}-{box.Column}";
            placement.SourceType = "YearlyArchive";
            placement.SourceRecordKey = sourceRecordKey;
            placement.PlacementMode = normalizedPlacementMode;
            placement.UpdatedAt = nowText;
            placement.UpdatedBy = box.ArchivedBy;
            if (string.IsNullOrWhiteSpace(placement.CreatedAt))
            {
                placement.CreatedAt = nowText;
            }
        }

        private string ResolveSuggestedPlacementMode(string cabinetName, string side, int row, int column, string boxSpecification)
        {
            string normalizedCabinetName = CabinetNameNormalizer.Normalize(cabinetName);
            string slotCode = $"{row}-{column}";
            string normalizedSide = string.IsNullOrWhiteSpace(side) ? string.Empty : side.Trim().ToUpperInvariant();

            var specialRule = _archiveFilingRepository.GetCabinetSlotSpecialRule(
                normalizedCabinetName,
                slotCode,
                boxSpecification,
                normalizedSide);

            return string.IsNullOrWhiteSpace(specialRule?.LayoutModeOverride) || !specialRule.LayoutModeOverride.Contains("盒面向外", StringComparison.OrdinalIgnoreCase)
                ? "SpineOut"
                : "FrontOut";
        }

        private static string NormalizeArchiveBoxSpecification(string value)
        {
            return value?.Trim() switch
            {
                "厚" => "标准(10cm)",
                "中" => "标准(5cm)",
                "薄" => "标准(3cm)",
                _ => string.IsNullOrWhiteSpace(value) ? "标准(5cm)" : value.Trim()
            };
        }

        private static void EnsureDefaultArchiveBoxSpecifications(IDictionary<string, ArchiveBoxSpecification> specificationLookup)
        {
            ArgumentNullException.ThrowIfNull(specificationLookup);

            AddArchiveBoxSpecificationIfMissing(specificationLookup, "标准(10cm)", 23m, 30m, 10m, 10);
            AddArchiveBoxSpecificationIfMissing(specificationLookup, "标准(5cm)", 23m, 30m, 5m, 20);
            AddArchiveBoxSpecificationIfMissing(specificationLookup, "标准(3cm)", 23m, 30m, 3m, 30);
            AddArchiveBoxSpecificationIfMissing(specificationLookup, "标准(2cm)", 23m, 30m, 2m, 40);
            AddArchiveBoxSpecificationIfMissing(specificationLookup, "非标(10cm)", 30m, 30m, 10m, 50);
        }

        private static void AddArchiveBoxSpecificationIfMissing(IDictionary<string, ArchiveBoxSpecification> specificationLookup, string name, decimal widthCm, decimal heightCm, decimal thicknessCm, int sortOrder)
        {
            if (specificationLookup.ContainsKey(name))
            {
                return;
            }

            specificationLookup[name] = new ArchiveBoxSpecification
            {
                Name = name,
                WidthCm = widthCm,
                HeightCm = heightCm,
                ThicknessCm = thicknessCm,
                SortOrder = sortOrder
            };
        }

        private static decimal ResolveSlotWidth(CabinetType cabinetType, IReadOnlyDictionary<string, CabinetSlotSpecification> slotSpecificationLookup)
        {
            string cabinetTypeCode = GetCabinetTypeCode(cabinetType);
            if (slotSpecificationLookup.TryGetValue(cabinetTypeCode, out var slotSpecification) && slotSpecification.WidthCm > 0m)
            {
                return slotSpecification.WidthCm;
            }

            return cabinetType switch
            {
                CabinetType.Standard => 78m,
                CabinetType.Vertical => 83m,
                CabinetType.Horizontal => 83m,
                CabinetType.MagneticDisk => 23.33m,
                _ => 78m
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

        private static double ResolveOccupiedWidthForBox(YearlyArchiveBox box, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup, IReadOnlyDictionary<string, ArchiveBoxSpecification> specificationLookup)
        {
            string placementMode = placementLookup.TryGetValue(box.BoxLocationCode, out var placement) && !string.IsNullOrWhiteSpace(placement.PlacementMode)
                ? placement.PlacementMode
                : "SpineOut";
            return ResolveOccupiedWidth(placementMode, NormalizeArchiveBoxSpecification(box.Specs), specificationLookup);
        }

        private static double ResolveOccupiedWidth(string placementMode, string boxSpecification, IReadOnlyDictionary<string, ArchiveBoxSpecification> specificationLookup)
        {
            if (!specificationLookup.TryGetValue(boxSpecification, out var specification))
            {
                return 10d;
            }

            return string.Equals(placementMode, "FrontOut", StringComparison.OrdinalIgnoreCase)
                ? (double)specification.WidthCm
                : (double)specification.ThicknessCm;
        }

        private static bool TryResolvePlacementMode(string cabinetName, string side, int row, int column, string boxSpecification, IReadOnlyCollection<CabinetSlotSpecialRule> specialRules, out string placementMode)
        {
            placementMode = "SpineOut";
            string slotCode = $"{row}-{column}";
            string normalizedCabinetName = CabinetNameNormalizer.Normalize(cabinetName);

            var matchingRules = specialRules
                .Where(item => item.CabinetName == normalizedCabinetName)
                .Where(item => item.SlotCode == slotCode)
                .OrderBy(item => item.SortOrder)
                .ToList();
            if (matchingRules.Count == 0)
            {
                return true;
            }

            var matchedRule = matchingRules.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.RequiredArchiveFaceCode)
                || string.Equals(item.RequiredArchiveFaceCode, side, StringComparison.OrdinalIgnoreCase));
            if (matchedRule == null)
            {
                return false;
            }

            placementMode = string.IsNullOrWhiteSpace(matchedRule.LayoutModeOverride) || !matchedRule.LayoutModeOverride.Contains("盒面向外", StringComparison.OrdinalIgnoreCase)
                ? "SpineOut"
                : "FrontOut";
            return true;
        }

        private static List<TContainer> OrderContainersByCode<TContainer>(IEnumerable<TContainer> containers)
            where TContainer : class, IArchiveContainer
        {
            ArgumentNullException.ThrowIfNull(containers);

            return containers
                .OrderBy(item => item.ContainerCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<ArchiveContainerSummary>> GetExistingContainerSummariesForProjectAsync(string projectName, string year, ArchiveContainerKind containerKind)
        {
            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(year))
            {
                return new List<ArchiveContainerSummary>();
            }

            var projections = await _archiveFilingRepository.GetArchiveContainerProjectionsAsync(projectName, year, containerKind);

            return projections
                .Select(item => item.ToSummary())
                .ToList();
        }

        /// <inheritdoc/>
        public ElectronicArchiveUiDecision ResolveElectronicArchiveUiDecision(ElectronicArchiveScenarioInput input)
            => ArchiveFilingBusinessRules.ResolveUiDecision(input);

        /// <inheritdoc/>
        public string ResolveHardDiskSelectionMode(string? hardDiskCopyTargetMode)
            => ArchiveFilingBusinessRules.ResolveHardDiskSelectionMode(hardDiskCopyTargetMode);

        private static string BuildArchiveSlotKey(string cabinetName, string side, int row, int column)
        {
            if (string.IsNullOrWhiteSpace(cabinetName) || string.IsNullOrWhiteSpace(side) || row <= 0 || column <= 0)
            {
                return string.Empty;
            }

            return $"{CabinetNameNormalizer.Normalize(cabinetName)}|{side.Trim().ToUpperInvariant()}|{row}|{column}";
        }

        private static string BuildElectronicUnitSlotCode(string cabinetName, string side, int row, int column)
        {
            if (string.IsNullOrWhiteSpace(cabinetName) || string.IsNullOrWhiteSpace(side) || row <= 0 || column <= 0)
            {
                return string.Empty;
            }

            return $"{CabinetNameNormalizer.Normalize(cabinetName)}{side.Trim().ToUpperInvariant()}-{row}-{column}";
        }

        private static string ResolveLatestSlotKey(IEnumerable<YearlyArchiveBox> boxes)
        {
            ArgumentNullException.ThrowIfNull(boxes);

            var latest = boxes
                .OrderByDescending(item => item.ArchivedDate)
                .ThenByDescending(item => item.Id)
                .FirstOrDefault();

            return latest == null
                ? string.Empty
                : BuildArchiveSlotKey(latest.CabinetName, latest.Side, latest.Row, latest.Column);
        }

        private async Task<Dictionary<string, int>> LoadOccupiedArchiveSlotBoxCountsAsync()
        {
            var slotBoxCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            AddArchiveSlotBoxCounts(
                slotBoxCounts,
                await _archiveFilingRepository.GetYearlyArchiveBoxLocationCodesAsync());

            AddArchiveSlotBoxCounts(
                slotBoxCounts,
                await _archiveFilingRepository.GetTopoMapBoxNumbersAsync());

            AddArchiveSlotBoxCounts(
                slotBoxCounts,
                await _archiveFilingRepository.GetAerialPhotoBoxNumbersAsync());

            AddArchiveSlotBoxCounts(
                slotBoxCounts,
                await _archiveFilingRepository.GetOtherMapBoxNumbersAsync());

            return slotBoxCounts;
        }

        /// <summary>
        /// 按档口汇总已占用的档内序号（年度在用盒 + 历史图件位置编码）。
        /// </summary>
        private async Task<Dictionary<string, HashSet<int>>> LoadOccupiedArchiveSlotSequenceIndexesAsync()
        {
            var slotSequences = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            AddArchiveSlotSequenceIndexes(
                slotSequences,
                await _archiveFilingRepository.GetYearlyArchiveBoxLocationCodesAsync());

            AddArchiveSlotSequenceIndexes(
                slotSequences,
                await _archiveFilingRepository.GetTopoMapBoxNumbersAsync());

            AddArchiveSlotSequenceIndexes(
                slotSequences,
                await _archiveFilingRepository.GetAerialPhotoBoxNumbersAsync());

            AddArchiveSlotSequenceIndexes(
                slotSequences,
                await _archiveFilingRepository.GetOtherMapBoxNumbersAsync());

            return slotSequences;
        }

        /// <summary>
        /// 收集指定档口当前已占用的盒序号（含历史图件；可选排除某盒）。
        /// </summary>
        private async Task<IReadOnlyList<int>> CollectOccupiedBoxSequenceIndexesInSlotAsync(
            string cabinetName,
            string side,
            int row,
            int column,
            int? excludeBoxId = null)
        {
            string slotKey = BuildArchiveSlotKey(cabinetName, side, row, column);
            if (string.IsNullOrWhiteSpace(slotKey))
            {
                return Array.Empty<int>();
            }

            var occupied = new HashSet<int>();

            var boxesInSlot = await _archiveFilingRepository.GetInUseYearlyArchiveBoxesInSlotAsync(
                cabinetName,
                side,
                row,
                column);
            foreach (var box in boxesInSlot)
            {
                if (excludeBoxId is int excludedId && box.Id == excludedId)
                {
                    continue;
                }

                if (box.BoxIndex > 0)
                {
                    occupied.Add(box.BoxIndex);
                }

                if (ArchiveSlotLocationSupport.TryParseSequenceIndex(box.BoxLocationCode, out int fromCode))
                {
                    occupied.Add(fromCode);
                }
            }

            var slotSequences = await LoadOccupiedArchiveSlotSequenceIndexesAsync();
            if (slotSequences.TryGetValue(slotKey, out var historyIndexes))
            {
                foreach (int index in historyIndexes)
                {
                    occupied.Add(index);
                }
            }

            return occupied.OrderBy(index => index).ToList();
        }

        private static void AddArchiveSlotBoxCounts(IDictionary<string, int> slotBoxCounts, IEnumerable<string?> sourceValues)
        {
            ArgumentNullException.ThrowIfNull(slotBoxCounts);
            ArgumentNullException.ThrowIfNull(sourceValues);

            foreach (string boxCode in sourceValues
                .SelectMany(SplitArchiveBoxCodes)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!TryBuildArchiveSlotKeyFromBoxCode(boxCode, out string slotKey))
                {
                    continue;
                }

                slotBoxCounts[slotKey] = slotBoxCounts.TryGetValue(slotKey, out int count)
                    ? count + 1
                    : 1;
            }
        }

        private static void AddArchiveSlotSequenceIndexes(
            IDictionary<string, HashSet<int>> slotSequences,
            IEnumerable<string?> sourceValues)
        {
            ArgumentNullException.ThrowIfNull(slotSequences);
            ArgumentNullException.ThrowIfNull(sourceValues);

            foreach (string boxCode in sourceValues
                .SelectMany(SplitArchiveBoxCodes)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!TryBuildArchiveSlotKeyFromBoxCode(boxCode, out string slotKey)
                    || !ArchiveSlotLocationSupport.TryParseSequenceIndex(boxCode, out int sequenceIndex))
                {
                    continue;
                }

                if (!slotSequences.TryGetValue(slotKey, out var indexes))
                {
                    indexes = new HashSet<int>();
                    slotSequences[slotKey] = indexes;
                }

                indexes.Add(sequenceIndex);
            }
        }

        private static IEnumerable<string> SplitArchiveBoxCodes(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return Enumerable.Empty<string>();
            }

            return source
                .Split([';', '；', ',', '，', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(code => !string.IsNullOrWhiteSpace(code));
        }

        private static bool TryBuildArchiveSlotKeyFromBoxCode(string? boxCode, out string slotKey)
        {
            slotKey = string.Empty;
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                return false;
            }

            var parts = boxCode.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4)
            {
                return false;
            }

            string cabinetAndFace = parts[0].Trim();
            if (cabinetAndFace.Length < 2)
            {
                return false;
            }

            char faceToken = cabinetAndFace[^1];
            if (faceToken != 'A' && faceToken != 'a' && faceToken != 'B' && faceToken != 'b')
            {
                return false;
            }

            if (!int.TryParse(parts[1], out int row) || !int.TryParse(parts[2], out int column))
            {
                return false;
            }

            slotKey = BuildArchiveSlotKey(cabinetAndFace[..^1], faceToken.ToString(), row, column);
            return !string.IsNullOrWhiteSpace(slotKey);
        }

        private static string ResolveFirstFullyEmptyStackBottomSlotKey(IReadOnlyList<Cabinet> cabinets, IEnumerable<string> occupiedSlotKeys)
        {
            ArgumentNullException.ThrowIfNull(cabinets);
            ArgumentNullException.ThrowIfNull(occupiedSlotKeys);

            var occupiedSlots = occupiedSlotKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var cabinet in cabinets)
            {
                var sides = cabinet.FaceCount > 1 ? new[] { "A", "B" } : new[] { "A" };
                foreach (var side in sides)
                {
                    for (int column = 1; column <= cabinet.ColumnCount; column++)
                    {
                        bool isFullStackEmpty = true;
                        for (int row = 1; row <= cabinet.LayerCount; row++)
                        {
                            if (occupiedSlots.Contains(BuildArchiveSlotKey(cabinet.Name, side, row, column)))
                            {
                                isFullStackEmpty = false;
                                break;
                            }
                        }

                        if (isFullStackEmpty)
                        {
                            return BuildArchiveSlotKey(cabinet.Name, side, 1, column);
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static int ResolveSuggestionStagePriority(
            string slotKey,
            IReadOnlySet<string> sameYearSameProjectSlotKeys,
            string sameYearLastProjectSlotKey,
            string recentLastProjectSlotKey,
            string firstThreeLayerEmptyBottomSlotKey)
        {
            if (!string.IsNullOrWhiteSpace(slotKey) && sameYearSameProjectSlotKeys.Contains(slotKey))
            {
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(slotKey)
                && !string.IsNullOrWhiteSpace(sameYearLastProjectSlotKey)
                && string.Equals(slotKey, sameYearLastProjectSlotKey, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(slotKey)
                && !string.IsNullOrWhiteSpace(recentLastProjectSlotKey)
                && string.Equals(slotKey, recentLastProjectSlotKey, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (!string.IsNullOrWhiteSpace(slotKey)
                && !string.IsNullOrWhiteSpace(firstThreeLayerEmptyBottomSlotKey)
                && string.Equals(slotKey, firstThreeLayerEmptyBottomSlotKey, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            return 4;
        }
    }
}
