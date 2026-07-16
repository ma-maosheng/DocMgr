using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 开柜页单件迁档拖拽载荷。
    /// </summary>
    public sealed class InteractiveItemRelocationDragPayload
    {
        public static string DataFormat => typeof(InteractiveItemRelocationDragPayload).FullName!;

        public string MediaKind { get; init; } = ArchiveRegisterDomainValues.MediaKindSimulated;

        public int SourceBoxId { get; init; }

        public int SourceUnitId { get; init; }

        public int SourceMediumId { get; init; }

        public string BoxSpecification { get; init; } = string.Empty;

        public string SourceDedicatedSlotCategoryName { get; init; } = string.Empty;

        public string SourceStorageLocation { get; init; } = string.Empty;

        public string DisplayText { get; init; } = string.Empty;

        public bool IsOpticalDiscMedia { get; init; }

        public InteractiveItemRelocationSource ToRelocationSource()
        {
            return new InteractiveItemRelocationSource
            {
                MediaKind = MediaKind,
                SourceBoxId = SourceBoxId,
                SourceUnitId = SourceUnitId,
                SourceMediumId = SourceMediumId,
                BoxSpecification = BoxSpecification,
                SourceDedicatedSlotCategoryName = SourceDedicatedSlotCategoryName,
                SourceStorageLocation = SourceStorageLocation,
                DisplayText = DisplayText,
                IsOpticalDiscMedia = IsOpticalDiscMedia
            };
        }
    }
}
