using System.Collections.Generic;

namespace DocMgr.Models.Cabinets
{
    public sealed class CabinetSlotDescriptor
    {
        public int VisualRowIndex { get; init; }

        public int VisualColumnIndex { get; init; }

        public int LayerIndex { get; init; }

        public int ColumnIndex { get; init; }

        public string SlotCode { get; init; } = string.Empty;

        public CabinetFace Face { get; init; }

        public IReadOnlyList<CabinetArchiveBoxDescriptor> ArchiveBoxes { get; init; } = [];

        public IReadOnlyList<CabinetHardDiskMediumDescriptor> HardDiskMedia { get; init; } = [];

        public IReadOnlyList<CabinetHardDiskMediumDescriptor> PendingReturnMedia { get; init; } = [];

        public bool IsMagneticDiskSlot { get; init; }

        public int HardDiskCapacity { get; init; }

        public double SlotCanvasWidth { get; init; }

        public double SlotCanvasHeight { get; init; }

        public double UtilizationRatio { get; init; }

        public string UtilizationText { get; init; } = "0%";

        public string CapacitySummaryText { get; init; } = string.Empty;

        public string RemainingSummaryText { get; init; } = string.Empty;

        public string LayoutModeText { get; init; } = string.Empty;

        public string SlotToolTipText { get; init; } = string.Empty;

        public bool IsCrossFaceLinked { get; init; }

        public bool IsSpecialRule { get; init; }

        public string SpecialRuleText { get; init; } = string.Empty;

        public bool IsDamagedDiskDedicatedSlot { get; init; }

        public bool IsDamagedOpticalDiscDedicatedSlot { get; init; }

        public bool IsDataDiskDedicatedSlot { get; init; }

        public bool IsDataOpticalDiscDedicatedSlot { get; init; }

        public bool IsHistoricalDataDiskDedicatedSlot { get; init; }

        public bool IsHistoricalDataOpticalDiscDedicatedSlot { get; init; }

        public bool IsBlankDiskDedicatedSlot { get; init; }

        public bool IsYearlyMaterialsDedicatedSlot { get; init; }

        public bool IsHistoricalMaterialsDedicatedSlot { get; init; }

        public string DedicatedSlotCategoryName { get; init; } = string.Empty;
    }
}
