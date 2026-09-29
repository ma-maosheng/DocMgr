using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        /// <summary>
        /// 校验电子迁档目标档口：防磁柜、专用类别（资料类型×介质类型）、盘位容量。
        /// 通过返回 null。
        /// </summary>
        /// <param name="sourceHostsAtTargetAfterOperation">
        /// 操作后源袋本体落在目标档口（物理迁移 / 换盘不备份）；备份新建袋时为 false。
        /// </param>
        private async Task<string?> ValidateElectronicRelocationTargetSlotAsync(
            YearlyElectronicArchiveUnit source,
            string? targetLocation,
            string relocationMode,
            bool sourceHostsAtTargetAfterOperation)
        {
            string location = targetLocation?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(location))
            {
                return "请完整选择新的存放档口。";
            }

            string expectedCategory;
            try
            {
                expectedCategory = await ResolveElectronicRelocationExpectedSlotCategoryAsync(
                    source,
                    relocationMode);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            if (string.IsNullOrWhiteSpace(expectedCategory))
            {
                return "无法解析目标档口要求的专用类别，请检查源袋载体类型。";
            }

            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(
                    location,
                    out string cabinetName,
                    out string faceCode,
                    out int row,
                    out int column))
            {
                return $"无法解析目标存放位置 [{location}]，请重新选择档口。";
            }

            var cabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(cabinetName);
            if (cabinet == null)
            {
                return $"未找到存放位置对应的防磁磁盘柜 [{cabinetName}]，请重新选择档口。";
            }

            if (cabinet.Type != CabinetType.MagneticDisk)
            {
                return "电子介质袋只能放入防磁磁盘柜专用档口。";
            }

            string slotCode = ArchiveStorageSlotCategorySupport.BuildSlotCode(row, column);
            string? storedCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                cabinet.Id,
                faceCode,
                slotCode);
            string normalizedStored = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(storedCategory);
            string expectedDisplay = ArchiveElectronicStorageSlotCategorySupport.ResolveCategoryDisplayName(expectedCategory);

            if (string.IsNullOrWhiteSpace(normalizedStored))
            {
                return $"档口 [{cabinet.Name}{faceCode.Trim().ToUpperInvariant()}-{slotCode}] 尚未设置专用类别。"
                    + $"当前迁档要求「{expectedDisplay}」档口，请重新选择位置或在开柜界面完成设置。";
            }

            if (!CabinetHardDiskSlotCategoryAssignment.MatchesCategory(normalizedStored, expectedCategory))
            {
                string actualDisplay = ArchiveElectronicStorageSlotCategorySupport.ResolveCategoryDisplayName(normalizedStored);
                return $"目标存放位置 [{location}] 的档口用途为「{actualDisplay}」，"
                    + $"与当前迁档要求的「{expectedDisplay}」不一致，请按资料类型与介质类型重新选择档口。";
            }

            string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, faceCode, row, column);
            string slotPrefix = slotKey + "-";
            int? excludeUnitId = sourceHostsAtTargetAfterOperation ? source.Id : null;
            var occupiedIndexes = await _filingRepository.GetElectronicUnitSequenceIndexesInSlotAsync(
                slotKey,
                slotPrefix,
                excludeUnitId);
            int occupiedCount = occupiedIndexes.Count;
            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                expectedCategory,
                cabinet);
            if (occupiedCount + 1 > slotCapacity)
            {
                return $"目标档口盘位不足（迁入后需 {occupiedCount + 1} 袋，档口容量 {slotCapacity} 袋）。";
            }

            return null;
        }

        /// <summary>
        /// 按源档口资料类型 + 迁档模式介质类型解析期望专用档口类别。
        /// </summary>
        private async Task<string> ResolveElectronicRelocationExpectedSlotCategoryAsync(
            YearlyElectronicArchiveUnit source,
            string relocationMode)
        {
            var mediaKind = ResolveElectronicRelocationTargetMediaKind(source, relocationMode);
            var materialKind = await ResolveElectronicRelocationSourceMaterialKindAsync(source, relocationMode);
            return ArchiveElectronicStorageSlotCategorySupport.ResolveDedicatedSlotCategory(materialKind, mediaKind);
        }

        private static ArchiveElectronicStorageSlotCategorySupport.SlotMediaKind ResolveElectronicRelocationTargetMediaKind(
            YearlyElectronicArchiveUnit source,
            string relocationMode)
        {
            if (ArchiveRelocationMode.IsMoveToBlankHardDisk(relocationMode))
            {
                return ArchiveElectronicStorageSlotCategorySupport.SlotMediaKind.HardDisk;
            }

            if (ArchiveRelocationMode.IsMoveToBlankOpticalDisc(relocationMode))
            {
                return ArchiveElectronicStorageSlotCategorySupport.SlotMediaKind.OpticalDisc;
            }

            return ArchiveElectronicStorageSlotCategorySupport.ResolveMediaKindFromCarrierType(source.StorageCarrierType);
        }

        private async Task<ArchiveElectronicStorageSlotCategorySupport.SlotMaterialKind> ResolveElectronicRelocationSourceMaterialKindAsync(
            YearlyElectronicArchiveUnit source,
            string relocationMode)
        {
            string? sourceCategory = await TryGetMagneticSlotCategoryNameByLocationAsync(
                ResolveElectronicUnitPhysicalStorageLocation(source));

            if (ArchiveElectronicStorageSlotCategorySupport.TryResolveMaterialAndMediaKind(
                    sourceCategory,
                    out var materialKind,
                    out _))
            {
                if (materialKind == ArchiveElectronicStorageSlotCategorySupport.SlotMaterialKind.Damaged
                    && ArchiveRelocationMode.IsMoveToBlankCarrier(relocationMode))
                {
                    return ArchiveElectronicStorageSlotCategorySupport.SlotMaterialKind.YearlyData;
                }

                return materialKind;
            }

            var linkedMedia = source.MediumLinks
                .Select(link => link.HardDiskMedium)
                .Where(medium => medium != null)
                .Cast<HardDiskMedium>()
                .ToList();
            string linkedStatus = ArchiveElectronicStorageSlotCategorySupport.ResolveLinkedMediumMediaStatus(linkedMedia);
            return ArchiveElectronicStorageSlotCategorySupport.ResolveDamagedMaterialKindOrDefault(
                linkedStatus,
                ArchiveElectronicStorageSlotCategorySupport.SlotMaterialKind.YearlyData);
        }

        private async Task<string?> TryGetMagneticSlotCategoryNameByLocationAsync(string? storageLocation)
        {
            if (string.IsNullOrWhiteSpace(storageLocation)
                || !ArchiveSlotLocationSupport.TryParseSlotLocation(
                    storageLocation,
                    out string cabinetName,
                    out string faceCode,
                    out int row,
                    out int column))
            {
                return null;
            }

            var cabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(cabinetName);
            if (cabinet == null)
            {
                return null;
            }

            return await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                cabinet.Id,
                faceCode,
                ArchiveStorageSlotCategorySupport.BuildSlotCode(row, column));
        }

        private void EnsureElectronicRelocationTargetSlotOrThrow(string? blockReason)
        {
            if (!string.IsNullOrWhiteSpace(blockReason))
            {
                throw new InvalidOperationException(blockReason);
            }
        }
    }
}
