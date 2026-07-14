namespace DocMgr.Models.YearlyArchive
{
    public sealed class BatchSimulatedSlotPhysicalMoveRequest
    {
        public string SourceCabinetName { get; set; } = string.Empty;

        public string SourceFace { get; set; } = string.Empty;

        public int SourceRow { get; set; }

        public int SourceColumn { get; set; }

        public string TargetCabinetName { get; set; } = string.Empty;

        public string TargetFace { get; set; } = string.Empty;

        public int TargetRow { get; set; }

        public int TargetColumn { get; set; }

        public string Remarks { get; set; } = string.Empty;
    }

    public sealed class BatchSlotRelocationEndpoint
    {
        public string CabinetName { get; init; } = string.Empty;

        public string FaceCode { get; init; } = string.Empty;

        public int Row { get; init; }

        public int Column { get; init; }

        public string SlotCode { get; init; } = string.Empty;

        /// <summary>
        /// 迁档介质轨：<see cref="ArchiveRegisterDomainValues.MediaKindSimulated"/> 或 <see cref="ArchiveRegisterDomainValues.MediaKindElectronic"/>。
        /// </summary>
        public string MediaKind { get; init; } = ArchiveRegisterDomainValues.MediaKindSimulated;

        /// <summary>
        /// 防磁磁盘柜源档口专用类别；模拟介质轨时为空。
        /// </summary>
        public string DedicatedSlotCategoryName { get; init; } = string.Empty;

        public int ItemCount { get; init; }

        public int YearlyArchiveBoxCount => ItemCount;

        public string DisplayText =>
            string.Equals(MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                ? $"{CabinetName}{FaceCode}-{Row}-{Column}（{ItemCount} 袋）"
                : $"{CabinetName}{FaceCode}-{Row}-{Column}（{ItemCount} 盒）";
    }

}
