using System;
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

        /// <summary>档口内已放盒数（含历史与年度，按盒去重）。</summary>
        public int PlacedBoxCount { get; init; }

        /// <summary>档口标准容量（按标准盒厚折算）；0 表示未登记规格无法折算。</summary>
        public int StandardBoxCapacity { get; init; }

        /// <summary>档口剩余可放盒数（标准容量-已放）；容量未知时为 0。</summary>
        public int RemainingBoxCapacity => StandardBoxCapacity <= 0
            ? 0
            : Math.Max(0, StandardBoxCapacity - PlacedBoxCount);

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

        public bool IsMixedUseArchiveSlot { get; init; }

        public string DedicatedSlotCategoryName { get; init; } = string.Empty;
    }
}
