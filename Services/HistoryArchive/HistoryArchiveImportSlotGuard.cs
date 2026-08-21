using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.Cabinets;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HistoryArchive
{
    /// <summary>
    /// 历史存档 Excel 导入：核验落档档口用途。未设置则同步为历史资料专用；与年度专用冲突则改为混用档口。
    /// </summary>
    public sealed class HistoryArchiveImportSlotGuard
    {
        private readonly ICabinetService _cabinetService;
        private readonly IArchiveFilingRepository _archiveFilingRepository;

        public HistoryArchiveImportSlotGuard(
            ICabinetService cabinetService,
            IArchiveFilingRepository archiveFilingRepository)
        {
            _cabinetService = cabinetService;
            _archiveFilingRepository = archiveFilingRepository;
        }

        /// <summary>
        /// 先核验全部落档档口，通过后再同步用途。不合规则抛出 <see cref="InvalidOperationException"/>。
        /// </summary>
        public async Task EnsureSlotsReadyForHistoryImportAsync(IEnumerable<string?> boxNumbers)
        {
            var errors = new List<string>();
            var slots = new List<ParsedHistorySlot>();
            foreach (string code in SplitBoxNumbers(boxNumbers))
            {
                if (!ArchiveSlotLocationSupport.TryParseSlotLocation(
                        code,
                        out string cabinetName,
                        out string side,
                        out int row,
                        out int column))
                {
                    errors.Add($"档案盒编号 [{code}] 无法解析为柜面-层-列，无法核验落档档口用途。");
                    continue;
                }

                string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, side, row, column);
                if (slots.Any(item => string.Equals(item.SlotKey, slotKey, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                slots.Add(new ParsedHistorySlot(code, cabinetName.Trim(), side.Trim(), row, column, slotKey));
            }

            if (slots.Count == 0 && errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "历史存档导入前档口用途核验未通过："
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, errors.Distinct(StringComparer.Ordinal)));
            }

            if (slots.Count == 0)
            {
                return;
            }

            var cabinets = await _cabinetService.GetAllCabinetsAsync();
            var cabinetLookup = cabinets
                .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var categoryLookup = await _archiveFilingRepository.GetArchiveSlotCategoryLookupForCabinetsAsync(
                cabinets.Where(item => item.Type == CabinetType.Standard).Select(item => item.Id).ToList());

            var pendingHistorical = new List<(Cabinet Cabinet, string FaceCode, string SlotCode)>();
            var pendingMixed = new List<(Cabinet Cabinet, string FaceCode, string SlotCode)>();

            foreach (var slot in slots)
            {
                if (!cabinetLookup.TryGetValue(slot.CabinetName, out var cabinet))
                {
                    errors.Add($"未找到资料柜 [{slot.CabinetName}]（档案盒编号 {slot.SourceBoxNumber}）。");
                    continue;
                }

                string face = slot.Side.Trim();
                bool faceAllowed = cabinet.FaceCount > 1
                    ? string.Equals(face, "A", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(face, "B", StringComparison.OrdinalIgnoreCase)
                    : string.Equals(face, "A", StringComparison.OrdinalIgnoreCase);
                if (!faceAllowed || slot.Row > cabinet.LayerCount || slot.Column > cabinet.ColumnCount)
                {
                    errors.Add($"档口 [{slot.SlotKey}] 在柜体中不存在（档案盒编号 {slot.SourceBoxNumber}）。");
                    continue;
                }

                if (cabinet.Type != CabinetType.Standard)
                {
                    continue;
                }

                string slotCode = ArchiveStorageSlotCategorySupport.BuildSlotCode(slot.Row, slot.Column);
                string lookupKey = ArchiveStorageSlotCategorySupport.BuildCategoryLookupKey(cabinet.Id, face, slotCode);
                categoryLookup.TryGetValue(lookupKey, out string? storedCategory);
                string normalized = CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(storedCategory);

                if (ArchiveStorageSlotCategorySupport.MatchesCompatibleLandingCategory(
                        normalized,
                        ArchiveStorageSlotCategorySupport.ExpectedHistoricalMaterialsCategory))
                {
                    continue;
                }

                if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                        normalized,
                        CabinetArchiveSlotCategoryAssignment.CategoryYearlyMaterials))
                {
                    pendingMixed.Add((cabinet, face, slotCode));
                    continue;
                }

                var yearlyBoxes = await _archiveFilingRepository.GetInUseYearlyArchiveBoxesInSlotAsync(
                    cabinet.Name,
                    face,
                    slot.Row,
                    slot.Column);
                if (yearlyBoxes.Count > 0)
                {
                    pendingMixed.Add((cabinet, face, slotCode));
                    continue;
                }

                pendingHistorical.Add((cabinet, face, slotCode));
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "历史存档导入前档口用途核验未通过："
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, errors.Distinct(StringComparer.Ordinal)));
            }

            foreach (var item in pendingMixed.DistinctBy(entry => $"{entry.Cabinet.Id}:{entry.FaceCode}:{entry.SlotCode}"))
            {
                _cabinetService.PromoteArchiveSlotToMixedUse(
                    item.Cabinet.Id,
                    item.FaceCode,
                    item.SlotCode);
            }

            foreach (var item in pendingHistorical.DistinctBy(entry => $"{entry.Cabinet.Id}:{entry.FaceCode}:{entry.SlotCode}"))
            {
                _cabinetService.PromoteUnsetArchiveSlotToHistoricalMaterials(
                    item.Cabinet.Id,
                    item.FaceCode,
                    item.SlotCode);
            }
        }

        private static IEnumerable<string> SplitBoxNumbers(IEnumerable<string?> boxNumbers)
        {
            foreach (string? source in boxNumbers ?? Array.Empty<string?>())
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                foreach (string code in source.Split(
                    [';', '；', ',', '，', '\r', '\n'],
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    yield return code;
                }
            }
        }

        private sealed record ParsedHistorySlot(
            string SourceBoxNumber,
            string CabinetName,
            string Side,
            int Row,
            int Column,
            string SlotKey);
    }
}
