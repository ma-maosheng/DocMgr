using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HardDiskMedia
{
    public sealed partial class HardDiskMediaService
    {
        /// <inheritdoc/>
        public async Task<string?> RecommendBlankDedicatedSlotLocationAsync(int slotCapacity = 0)
        {
            var options = await GetOrderedBlankDedicatedSlotLocationOptionsAsync(slotCapacity);
            string? slotCode = options
                .FirstOrDefault(option => option.ExistingMediumCount < ResolveOptionSlotCapacity(option, slotCapacity))?.Location
                ?? options.FirstOrDefault()?.Location;

            if (!string.IsNullOrWhiteSpace(slotCode))
            {
                return HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(slotCode);
            }

            return await BuildFallbackDedicatedSlotLocationAsync(CabinetHardDiskSlotCategoryAssignment.CategoryBlank);
        }

        /// <inheritdoc/>
        public async Task<string?> AllocateNextDedicatedFullLocationAsync(
            string categoryName,
            int slotCapacity = 0,
            ISet<string>? reservedFullLocations = null)
        {
            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    categoryName,
                    CabinetHardDiskSlotCategoryAssignment.CategoryBlank))
            {
                throw new InvalidOperationException("空白硬盘专用档口应使用档口键（不含档内序号），请调用 RecommendBlankDedicatedSlotLocationAsync。");
            }

            return await AllocateNextDedicatedFullLocationCoreAsync(categoryName, slotCapacity, reservedFullLocations);
        }

        /// <inheritdoc/>
        public async Task<string> ResolveBlankInStockSlotLocationAsync(string? requestedLocation)
        {
            string trimmed = requestedLocation?.Trim() ?? string.Empty;
            string slotCode = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(trimmed);
            if (!string.IsNullOrWhiteSpace(slotCode))
            {
                return slotCode;
            }

            return await RecommendBlankDedicatedSlotLocationAsync()
                ?? throw new InvalidOperationException("未找到空白硬盘专用档口，请确认防磁磁盘柜档口用途已配置。");
        }

        /// <inheritdoc/>
        public async Task<string> ResolveDataInStockFullLocationAsync(string? requestedLocation)
        {
            string trimmed = requestedLocation?.Trim() ?? string.Empty;
            if (ArchiveSlotLocationSupport.TryParseSequenceIndex(trimmed, out _))
            {
                return trimmed;
            }

            string slotCode = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(trimmed);
            if (string.IsNullOrWhiteSpace(slotCode))
            {
                return await AllocateNextDedicatedFullLocationAsync(CabinetHardDiskSlotCategoryAssignment.CategoryData)
                    ?? throw new InvalidOperationException("未找到年度数据硬盘专用档口，请先在磁盘柜开柜界面完成设置。");
            }

            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(slotCode, out string cabinetName, out string side, out int row, out int column))
            {
                return trimmed;
            }

            var cabinet = await _archiveFilingRepository.GetMagneticDiskCabinetByNameAsync(cabinetName);
            int resolvedCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryData,
                cabinet);
            var occupiedIndexes = await GetOccupiedDedicatedSlotSequenceIndexesAsync(slotCode);
            if (MagneticDedicatedSlotOccupancySupport.IsSlotFull(occupiedIndexes, resolvedCapacity))
            {
                return await AllocateNextDedicatedFullLocationAsync(CabinetHardDiskSlotCategoryAssignment.CategoryData)
                    ?? throw new InvalidOperationException("年度数据硬盘专用档口均已满，请新增或启用新的专用档口。");
            }

            int sequenceIndex = MagneticDedicatedSlotOccupancySupport.ResolveNextSequenceIndex(occupiedIndexes, resolvedCapacity);
            return ArchiveSlotLocationSupport.BuildFullElectronicLocation(cabinetName, side, row, column, sequenceIndex);
        }

        private async Task<string?> AllocateNextDedicatedFullLocationCoreAsync(
            string categoryName,
            int slotCapacityOverride,
            ISet<string>? reservedFullLocations)
        {
            var dedicatedSlots = await _hardDiskMediaRepository.GetDedicatedMagneticSlotsByCategoryAsync(categoryName);
            foreach (var dedicatedSlot in dedicatedSlots
                         .Where(item => item.Cabinet != null)
                         .OrderBy(item => item, Comparer<CabinetHardDiskSlotCategoryAssignment>.Create(HardDiskBlankSlotLocationSupport.CompareDedicatedSlots)))
            {
                int resolvedCapacity = slotCapacityOverride > 0
                    ? slotCapacityOverride
                    : CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(categoryName, dedicatedSlot.Cabinet);
                string slotCode = HardDiskBlankSlotLocationSupport.BuildLocationCode(
                    dedicatedSlot.Cabinet!.Name,
                    dedicatedSlot.FaceCode,
                    dedicatedSlot.SlotCode);
                string? fullLocation = await TryAllocateDedicatedFullLocationInSlotAsync(
                    slotCode,
                    resolvedCapacity,
                    reservedFullLocations);
                if (!string.IsNullOrWhiteSpace(fullLocation))
                {
                    return fullLocation;
                }
            }

            return null;
        }

        private async Task<string?> TryAllocateDedicatedFullLocationInSlotAsync(
            string? slotLocation,
            int slotCapacity,
            ISet<string>? reservedFullLocations = null)
        {
            if (string.IsNullOrWhiteSpace(slotLocation))
            {
                return null;
            }

            string slotCode = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(slotLocation);
            if (string.IsNullOrWhiteSpace(slotCode))
            {
                return null;
            }

            var occupiedIndexes = await GetOccupiedDedicatedSlotSequenceIndexesAsync(slotCode, reservedFullLocations);
            if (MagneticDedicatedSlotOccupancySupport.IsSlotFull(occupiedIndexes, slotCapacity))
            {
                return null;
            }

            int sequenceIndex = MagneticDedicatedSlotOccupancySupport.ResolveNextSequenceIndex(occupiedIndexes, slotCapacity);
            return HardDiskBlankSlotLocationSupport.BuildFullLocationFromSlotCode(slotCode, sequenceIndex);
        }

        private async Task<List<int>> GetOccupiedDedicatedSlotSequenceIndexesAsync(
            string slotCode,
            ISet<string>? reservedFullLocations = null)
        {
            string slotPrefix = slotCode + "-";
            var hardDiskLocations = await _hardDiskMediaRepository.GetInStockHardDiskStorageLocationsInSlotAsync(slotCode);
            var electronicLocations = await _archiveFilingRepository.GetElectronicArchiveUnitStorageLocationsInSlotAsync(slotCode, slotPrefix);
            var opticalDiscLocations = await _hardDiskMediaRepository.GetInStockOpticalDiscStorageLocationsInSlotAsync(slotCode);
            var reservedLocations = reservedFullLocations == null
                ? Array.Empty<string>()
                : reservedFullLocations.Where(location => ArchiveSlotLocationSupport.IsSameSlot(location, slotCode));

            return MagneticDedicatedSlotOccupancySupport.CollectOccupiedSequenceIndexes(
                slotCode,
                hardDiskLocations
                    .Concat(electronicLocations)
                    .Concat(opticalDiscLocations)
                    .Concat(reservedLocations));
        }

        private async Task<string?> BuildFallbackDedicatedSlotLocationAsync(string categoryName)
        {
            var dedicatedSlot = await _hardDiskMediaRepository.GetFirstDedicatedMagneticSlotByCategoryAsync(categoryName);
            if (dedicatedSlot?.Cabinet == null)
            {
                return null;
            }

            return HardDiskBlankSlotLocationSupport.BuildLocationCode(
                dedicatedSlot.Cabinet.Name,
                dedicatedSlot.FaceCode,
                dedicatedSlot.SlotCode);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetOrderedBlankDedicatedSlotLocationOptionsAsync(
            int slotCapacity = 0)
        {
            var dedicatedSlots = await _hardDiskMediaRepository.GetDedicatedMagneticSlotsByCategoryAsync(
                CabinetHardDiskSlotCategoryAssignment.CategoryBlank);

            var orderedSlots = dedicatedSlots
                .Where(item => item.Cabinet != null)
                .OrderBy(item => item, Comparer<CabinetHardDiskSlotCategoryAssignment>.Create(HardDiskBlankSlotLocationSupport.CompareDedicatedSlots))
                .ToList();

            if (orderedSlots.Count == 0)
            {
                return Array.Empty<HardDiskMediaReturnTargetLocationOption>();
            }

            var orderedLocations = orderedSlots
                .Select(item => HardDiskBlankSlotLocationSupport.BuildLocationCode(
                    item.Cabinet!.Name,
                    item.FaceCode,
                    item.SlotCode))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var inStockCounts = await _hardDiskMediaRepository.GetInStockBlankLedgerCountsBySlotCodesAsync(orderedLocations);
            var capacityByLocation = orderedSlots
                .GroupBy(
                    item => HardDiskBlankSlotLocationSupport.BuildLocationCode(
                        item.Cabinet!.Name,
                        item.FaceCode,
                        item.SlotCode),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => slotCapacity > 0
                        ? slotCapacity
                        : CabinetHardDiskSlotCategoryAssignment.ResolveHardDiskSlotCapacity(group.First().Cabinet),
                    StringComparer.OrdinalIgnoreCase);

            return orderedLocations
                .Select(location => new HardDiskMediaReturnTargetLocationOption
                {
                    Location = location,
                    ExistingMediumCount = inStockCounts.TryGetValue(location, out int count) ? count : 0,
                    SlotCapacity = capacityByLocation.TryGetValue(location, out int capacity)
                        ? capacity
                        : CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity
                })
                .ToList();
        }

        private async Task<List<string>> GetOrderedBlankDedicatedSlotLocationCodesAsync()
        {
            var dedicatedSlots = await _hardDiskMediaRepository.GetDedicatedMagneticSlotsByCategoryAsync(
                CabinetHardDiskSlotCategoryAssignment.CategoryBlank);

            return dedicatedSlots
                .Where(item => item.Cabinet != null)
                .OrderBy(item => item, Comparer<CabinetHardDiskSlotCategoryAssignment>.Create(HardDiskBlankSlotLocationSupport.CompareDedicatedSlots))
                .Select(item => HardDiskBlankSlotLocationSupport.BuildLocationCode(
                    item.Cabinet!.Name,
                    item.FaceCode,
                    item.SlotCode))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetBlankDedicatedReturnTargetLocationOptionsAsync()
            => await GetOrderedBlankDedicatedSlotLocationOptionsAsync();

        private static int ResolveOptionSlotCapacity(HardDiskMediaReturnTargetLocationOption option, int slotCapacityOverride)
        {
            if (slotCapacityOverride > 0)
            {
                return slotCapacityOverride;
            }

            return option.SlotCapacity > 0
                ? option.SlotCapacity
                : CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity;
        }

        private async Task<int> ResolveHardDiskSlotCapacityForLocationAsync(string? location)
        {
            if (!HardDiskBlankSlotLocationSupport.TryParseLocationCode(
                    location,
                    out string cabinetName,
                    out _,
                    out _,
                    out _))
            {
                return CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity;
            }

            var cabinet = await _archiveFilingRepository.GetMagneticDiskCabinetByNameAsync(cabinetName);
            return CabinetHardDiskSlotCategoryAssignment.ResolveHardDiskSlotCapacity(cabinet);
        }
    }
}
