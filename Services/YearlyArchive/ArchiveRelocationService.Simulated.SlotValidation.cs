using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        /// <summary>
        /// 校验模拟迁档目标档口：柜型、标准柜「年度资料」类别、档口宽度容量。
        /// 通过返回 null。
        /// </summary>
        private async Task<string?> ValidateSimulatedRelocationTargetSlotAsync(
            YearlyArchiveBox source,
            SimulatedRelocationRequest request)
        {
            if (!TryResolveSimulatedTargetSlot(
                    request,
                    out string cabinetName,
                    out string faceCode,
                    out int row,
                    out int column,
                    out string? resolveIssue))
            {
                return resolveIssue;
            }

            var targetCabinet = (await _filingRepository.GetNonMagneticCabinetsAsync())
                .FirstOrDefault(item =>
                    string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (targetCabinet == null)
            {
                return $"未找到目标档案柜 [{cabinetName}]，模拟介质只能迁入滑道式/立式/卧式档案柜。";
            }

            if (targetCabinet.Type == CabinetType.Standard)
            {
                string slotCode = ArchiveStorageSlotCategorySupport.BuildSlotCode(row, column);
                string? storedCategory = await _filingRepository.GetArchiveSlotCategoryNameAsync(
                    targetCabinet.Id,
                    faceCode,
                    slotCode);
                string? categoryIssue = ArchiveStorageSlotCategorySupport.TryValidateStandardSlotCategory(
                    targetCabinet,
                    faceCode,
                    slotCode,
                    storedCategory,
                    ArchiveStorageSlotCategorySupport.ExpectedYearlyMaterialsCategory,
                    $"{cabinetName}{faceCode}-{slotCode}");
                if (!string.IsNullOrWhiteSpace(categoryIssue))
                {
                    return categoryIssue;
                }
            }

            var boxesInTarget = await _filingRepository.GetInUseYearlyArchiveBoxesInSlotAsync(
                cabinetName,
                faceCode,
                row,
                column);
            var occupyingBoxes = boxesInTarget
                .Where(box => box.Id != source.Id)
                .ToList();

            var specificationLookup = (await _filingRepository.GetArchiveBoxSpecificationsAsync())
                .ToDictionary(item => item.Name, item => item, StringComparer.OrdinalIgnoreCase);
            var slotSpecificationLookup = (await _filingRepository.GetCabinetSlotSpecificationsAsync())
                .ToDictionary(item => item.CabinetTypeCode, item => item, StringComparer.OrdinalIgnoreCase);

            string cabinetTypeCode = GetCabinetTypeCodeForBatchMove(targetCabinet.Type);
            if (!slotSpecificationLookup.TryGetValue(cabinetTypeCode, out var slotSpecification))
            {
                return $"未找到柜型 [{cabinetTypeCode}] 的档口规格配置。";
            }

            string incomingSpecs = request.MoveContentsToNewEmptyBox
                ? request.NewBoxSpecification
                : source.Specs;
            decimal incomingThickness = ResolveArchiveBoxThickness(specificationLookup, incomingSpecs);
            decimal occupiedWidth = occupyingBoxes.Sum(box =>
                ResolveArchiveBoxThickness(specificationLookup, box.Specs));
            decimal totalWidthAfterMove = occupiedWidth + incomingThickness;
            if (totalWidthAfterMove > slotSpecification.WidthCm)
            {
                return $"目标档口可用宽度不足（迁入后需 {totalWidthAfterMove:0.##}cm，档口 {slotSpecification.WidthCm:0.##}cm）。";
            }

            return null;
        }

        private static bool TryResolveSimulatedTargetSlot(
            SimulatedRelocationRequest request,
            out string cabinetName,
            out string faceCode,
            out int row,
            out int column,
            out string? issue)
        {
            cabinetName = request.NewCabinetName?.Trim() ?? string.Empty;
            faceCode = request.NewSide?.Trim() ?? string.Empty;
            row = request.NewRow ?? 0;
            column = request.NewColumn ?? 0;

            if (!string.IsNullOrWhiteSpace(cabinetName)
                && !string.IsNullOrWhiteSpace(faceCode)
                && row > 0
                && column > 0)
            {
                issue = null;
                return true;
            }

            if (ArchiveSlotLocationSupport.TryParseSlotLocation(
                    request.NewStorageLocation,
                    out cabinetName,
                    out faceCode,
                    out row,
                    out column)
                && !string.IsNullOrWhiteSpace(cabinetName)
                && !string.IsNullOrWhiteSpace(faceCode)
                && row > 0
                && column > 0)
            {
                issue = null;
                return true;
            }

            issue = "请完整选择新的存放档口。";
            return false;
        }
    }
}
