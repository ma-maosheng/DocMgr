using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HardDiskMedia
{
    public sealed partial class HardDiskMediaService
    {
        /// <inheritdoc/>
        public async Task<string?> RecommendBlankDedicatedSlotLocationAsync(int slotCapacity = HardDiskBlankSlotLocationSupport.DefaultSlotCapacity)
        {
            var options = await GetOrderedBlankDedicatedSlotLocationOptionsAsync(slotCapacity);
            string? slotCode = options
                .FirstOrDefault(option => option.ExistingMediumCount < slotCapacity)?.Location
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

            int resolvedCapacity = slotCapacity > 0
                ? slotCapacity
                : CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(categoryName);

            return await AllocateNextDedicatedFullLocationCoreAsync(categoryName, resolvedCapacity, reservedFullLocations);
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

            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryData);
            var occupiedIndexes = await GetOccupiedDedicatedSlotSequenceIndexesAsync(slotCode);
            if (MagneticDedicatedSlotOccupancySupport.IsSlotFull(occupiedIndexes, slotCapacity))
            {
                return await AllocateNextDedicatedFullLocationAsync(CabinetHardDiskSlotCategoryAssignment.CategoryData)
                    ?? throw new InvalidOperationException("年度数据硬盘专用档口均已满，请新增或启用新的专用档口。");
            }

            int sequenceIndex = MagneticDedicatedSlotOccupancySupport.ResolveNextSequenceIndex(occupiedIndexes, slotCapacity);
            return ArchiveSlotLocationSupport.BuildFullElectronicLocation(cabinetName, side, row, column, sequenceIndex);
        }

        private async Task<string?> AllocateNextDedicatedFullLocationCoreAsync(
            string categoryName,
            int slotCapacity,
            ISet<string>? reservedFullLocations)
        {
            var options = await GetDedicatedReturnTargetLocationOptionsAsync(categoryName);
            foreach (var option in options
                         .OrderBy(item => item.ExistingMediumCount)
                         .ThenBy(item => item.Location, StringComparer.OrdinalIgnoreCase))
            {
                string? fullLocation = await TryAllocateDedicatedFullLocationInSlotAsync(
                    option.Location,
                    slotCapacity,
                    reservedFullLocations);
                if (!string.IsNullOrWhiteSpace(fullLocation))
                {
                    return fullLocation;
                }
            }

            return await BuildFallbackDedicatedFullLocationAsync(categoryName, slotCapacity, reservedFullLocations);
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

        private async Task<string?> BuildFallbackDedicatedFullLocationAsync(
            string categoryName,
            int slotCapacity,
            ISet<string>? reservedFullLocations = null)
        {
            var dedicatedSlots = await _hardDiskMediaRepository.GetDedicatedMagneticSlotsByCategoryAsync(categoryName);
            foreach (var dedicatedSlot in dedicatedSlots
                         .Where(item => item.Cabinet != null)
                         .OrderBy(item => item, Comparer<CabinetHardDiskSlotCategoryAssignment>.Create(HardDiskBlankSlotLocationSupport.CompareDedicatedSlots)))
            {
                string slotCode = HardDiskBlankSlotLocationSupport.BuildLocationCode(
                    dedicatedSlot.Cabinet!.Name,
                    dedicatedSlot.FaceCode,
                    dedicatedSlot.SlotCode);
                string? fullLocation = await TryAllocateDedicatedFullLocationInSlotAsync(
                    slotCode,
                    slotCapacity,
                    reservedFullLocations);
                if (!string.IsNullOrWhiteSpace(fullLocation))
                {
                    return fullLocation;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetOrderedBlankDedicatedSlotLocationOptionsAsync(
            int slotCapacity = HardDiskBlankSlotLocationSupport.DefaultSlotCapacity)
        {
            var orderedLocations = await GetOrderedBlankDedicatedSlotLocationCodesAsync();
            if (orderedLocations.Count == 0)
            {
                return Array.Empty<HardDiskMediaReturnTargetLocationOption>();
            }

            var inStockCounts = await _hardDiskMediaRepository.GetInStockBlankLedgerCountsBySlotCodesAsync(orderedLocations);
            return orderedLocations
                .Select(location => new HardDiskMediaReturnTargetLocationOption
                {
                    Location = location,
                    ExistingMediumCount = inStockCounts.TryGetValue(location, out int count) ? count : 0
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
    }
}
