using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 可并入非空硬盘的一行：区分可并档硬盘袋与光盘袋。
    /// </summary>
    public sealed class ExistingElectronicArchiveUnitListItem
    {
        public required YearlyElectronicArchiveUnit Unit { get; init; }

        public bool BelongsToCurrentProject { get; init; }

        public bool BelongsToCurrentYear { get; init; }

        public decimal AvailableCapacityMb { get; init; }

        public decimal TotalCapacityMb { get; init; }

        /// <summary>可用容量。总容量未登记时显示「未登记」。</summary>
        public string AvailableCapacityText { get; init; } = "—";

        /// <summary>可用容量能够容纳当前待立档数据量。容量未登记时不视为可推荐。</summary>
        public bool CanHoldPendingBatch { get; init; }

        public string GroupLabel => BelongsToCurrentProject ? "本项目已有" : "其他非空";

        public string ElectronicArchiveNo => Unit.ElectronicArchiveNo;

        public string StorageLocation => Unit.StorageLocation;

        public string Remarks => Unit.Remarks;

        /// <summary>
        /// 袋内硬盘号为空表示光盘袋，不参与并档。
        /// </summary>
        public bool IsOpticalDiscBag => string.IsNullOrWhiteSpace(Unit.LinkedMediumCodes);

        public bool CanSelectForAppend => !IsOpticalDiscBag;

        public string LinkedMediumCodesDisplay => IsOpticalDiscBag ? "光盘" : Unit.LinkedMediumCodes.Trim();

        public static ExistingElectronicArchiveUnitListItem From(
            YearlyElectronicArchiveUnit unit,
            bool belongsToCurrentProject = false,
            bool belongsToCurrentYear = false,
            decimal requiredMb = 0)
        {
            (decimal availableMb, decimal totalMb, string availableText) = ResolveAvailableCapacity(unit);
            bool canHold = totalMb > 0 && (requiredMb <= 0 || availableMb >= requiredMb);
            return new ExistingElectronicArchiveUnitListItem
            {
                Unit = unit,
                BelongsToCurrentProject = belongsToCurrentProject,
                BelongsToCurrentYear = belongsToCurrentYear,
                AvailableCapacityMb = availableMb,
                TotalCapacityMb = totalMb,
                AvailableCapacityText = availableText,
                CanHoldPendingBatch = canHold
            };
        }

        private static (decimal AvailableMb, decimal TotalMb, string Text) ResolveAvailableCapacity(YearlyElectronicArchiveUnit unit)
        {
            HardDiskMedium? medium = unit.MediumLinks?
                .Select(link => link.HardDiskMedium)
                .FirstOrDefault(item => item != null && !item.IsDeleted);
            if (medium == null)
            {
                return (0, 0, "—");
            }

            decimal totalMb = ElectronicMediaCapacitySupport.ParseCapacityTextToMb(medium.Capacity);
            if (totalMb <= 0)
            {
                return (0, 0, "未登记");
            }

            decimal usedMb = medium.ArchiveItemLinks?.Sum(link => link.DataSizeMb) ?? 0;
            decimal availableMb = Math.Max(0, totalMb - usedMb);
            string text = availableMb <= 0 ? "0 MB" : ElectronicMediaCapacitySupport.FormatCapacityMb(availableMb);
            return (availableMb, totalMb, text);
        }
    }
}
