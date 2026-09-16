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

        public string SourceHistoryBoxCode { get; init; } = string.Empty;

        public IReadOnlyList<int> SourceBoxIds { get; init; } = [];

        public IReadOnlyList<int> SourceUnitIds { get; init; } = [];

        public IReadOnlyList<int> SourceMediumIds { get; init; } = [];

        /// <summary>历史资料源盒号列表；仅 MediaKind=历史 时使用。</summary>
        public IReadOnlyList<string> SourceHistoryBoxCodes { get; init; } = [];

        public string BoxSpecification { get; init; } = string.Empty;

        public string SourceDedicatedSlotCategoryName { get; init; } = string.Empty;

        public string SourceStorageLocation { get; init; } = string.Empty;

        public string SourceSlotKey { get; init; } = string.Empty;

        /// <summary>源柜体 ID；用于跨柜迁档成功后定向刷新源柜开柜窗。</summary>
        public int SourceCabinetId { get; init; }

        /// <summary>源柜面；用于跨柜迁档成功后定向刷新源柜开柜窗。</summary>
        public CabinetFace SourceCabinetFace { get; init; }

        public string DisplayText { get; init; } = string.Empty;

        public bool IsOpticalDiscMedia { get; init; }

        public IReadOnlyList<InteractiveItemRelocationSource> ToRelocationSources()
        {
            var boxIds = ResolveIds(SourceBoxIds, SourceBoxId);
            var unitIds = ResolveIds(SourceUnitIds, SourceUnitId);
            var mediumIds = ResolveIds(SourceMediumIds, SourceMediumId);
            var historyBoxCodes = ResolveBoxCodes(
                SourceHistoryBoxCodes.Count > 0 || string.IsNullOrWhiteSpace(SourceHistoryBoxCode)
                    ? SourceHistoryBoxCodes
                    : [SourceHistoryBoxCode]);

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
                    SourceSlotKey = SourceSlotKey,
                    SourceCabinetId = SourceCabinetId,
                    SourceCabinetFace = SourceCabinetFace
                }).ToList();
            }

            if (string.Equals(MediaKind, ArchiveRegisterDomainValues.MediaKindHistory, StringComparison.Ordinal)
                && historyBoxCodes.Count > 0)
            {
                return historyBoxCodes.Select(code => new InteractiveItemRelocationSource
                {
                    MediaKind = MediaKind,
                    SourceHistoryBoxCode = code,
                    DisplayText = DisplayText,
                    BoxSpecification = BoxSpecification,
                    SourceDedicatedSlotCategoryName = SourceDedicatedSlotCategoryName,
                    SourceStorageLocation = SourceStorageLocation,
                    SourceSlotKey = SourceSlotKey,
                    SourceCabinetId = SourceCabinetId,
                    SourceCabinetFace = SourceCabinetFace
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
                    SourceCabinetId = SourceCabinetId,
                    SourceCabinetFace = SourceCabinetFace,
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
                    SourceCabinetId = SourceCabinetId,
                    SourceCabinetFace = SourceCabinetFace,
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
                    SourceHistoryBoxCode = SourceHistoryBoxCode,
                    DisplayText = DisplayText,
                    BoxSpecification = BoxSpecification,
                    SourceDedicatedSlotCategoryName = SourceDedicatedSlotCategoryName,
                    SourceStorageLocation = SourceStorageLocation,
                    SourceSlotKey = SourceSlotKey,
                    SourceCabinetId = SourceCabinetId,
                    SourceCabinetFace = SourceCabinetFace,
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

        private static IReadOnlyList<string> ResolveBoxCodes(IReadOnlyList<string>? codes)
        {
            if (codes == null || codes.Count == 0)
            {
                return [];
            }

            return codes
                .Select(code => code?.Trim() ?? string.Empty)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }
}
