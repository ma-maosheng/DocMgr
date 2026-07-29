using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 开柜页交互式迁档拖拽载荷（支持单件或多件同档口集合）。
    /// </summary>
    public sealed class InteractiveItemRelocationDragPayload
    {
        public static string DataFormat => typeof(InteractiveItemRelocationDragPayload).FullName!;

        public string MediaKind { get; init; } = ArchiveRegisterDomainValues.MediaKindSimulated;

        public int SourceBoxId { get; init; }

        public int SourceUnitId { get; init; }

        public int SourceMediumId { get; init; }

        public IReadOnlyList<int> SourceBoxIds { get; init; } = [];

        public IReadOnlyList<int> SourceUnitIds { get; init; } = [];

        public IReadOnlyList<int> SourceMediumIds { get; init; } = [];

        public string BoxSpecification { get; init; } = string.Empty;

        public string SourceDedicatedSlotCategoryName { get; init; } = string.Empty;

        public string SourceStorageLocation { get; init; } = string.Empty;

        public string SourceSlotKey { get; init; } = string.Empty;

        public string DisplayText { get; init; } = string.Empty;

        public bool IsOpticalDiscMedia { get; init; }

        public IReadOnlyList<InteractiveItemRelocationSource> ToRelocationSources()
        {
            var boxIds = ResolveIds(SourceBoxIds, SourceBoxId);
            var unitIds = ResolveIds(SourceUnitIds, SourceUnitId);
            var mediumIds = ResolveIds(SourceMediumIds, SourceMediumId);

            if (string.Equals(MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal)
                && boxIds.Count > 0)
            {
                return boxIds.Select(id => new InteractiveItemRelocationSource
                {
                    MediaKind = MediaKind,
                    SourceBoxId = id,
                    DisplayText = DisplayText,
                    BoxSpecification = BoxSpecification,
                    SourceDedicatedSlotCategoryName = SourceDedicatedSlotCategoryName,
                    SourceStorageLocation = SourceStorageLocation,
                    SourceSlotKey = SourceSlotKey
                }).ToList();
            }

            if (string.Equals(MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                && unitIds.Count > 0)
            {
                return unitIds.Select(id => new InteractiveItemRelocationSource
                {
                    MediaKind = MediaKind,
                    SourceUnitId = id,
                    DisplayText = DisplayText,
                    SourceDedicatedSlotCategoryName = SourceDedicatedSlotCategoryName,
                    SourceStorageLocation = SourceStorageLocation,
                    SourceSlotKey = SourceSlotKey,
                    IsOpticalDiscMedia = IsOpticalDiscMedia
                }).ToList();
            }

            if (mediumIds.Count > 0)
            {
                return mediumIds.Select(id => new InteractiveItemRelocationSource
                {
                    MediaKind = MediaKind,
                    SourceMediumId = id,
                    DisplayText = DisplayText,
                    SourceDedicatedSlotCategoryName = SourceDedicatedSlotCategoryName,
                    SourceStorageLocation = SourceStorageLocation,
                    SourceSlotKey = SourceSlotKey,
                    IsOpticalDiscMedia = IsOpticalDiscMedia
                }).ToList();
            }

            return
            [
                new InteractiveItemRelocationSource
                {
                    MediaKind = MediaKind,
                    SourceBoxId = SourceBoxId,
                    SourceUnitId = SourceUnitId,
                    SourceMediumId = SourceMediumId,
                    DisplayText = DisplayText,
                    BoxSpecification = BoxSpecification,
                    SourceDedicatedSlotCategoryName = SourceDedicatedSlotCategoryName,
                    SourceStorageLocation = SourceStorageLocation,
                    SourceSlotKey = SourceSlotKey,
                    IsOpticalDiscMedia = IsOpticalDiscMedia
                }
            ];
        }

        public InteractiveItemRelocationSource ToRelocationSource()
            => ToRelocationSources()[0];

        private static IReadOnlyList<int> ResolveIds(IReadOnlyList<int> list, int single)
        {
            if (list.Count > 0)
            {
                return list.Where(id => id > 0).Distinct().ToList();
            }

            return single > 0 ? [single] : [];
        }
    }
}
