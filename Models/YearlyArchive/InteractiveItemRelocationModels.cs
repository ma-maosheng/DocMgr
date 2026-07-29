namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 开柜交互式单件物理迁档请求（数量=1 的多件请求薄封装）。
    /// </summary>
    public sealed class InteractiveItemPhysicalMoveRequest
    {
        public string MediaKind { get; set; } = ArchiveRegisterDomainValues.MediaKindSimulated;

        public int SourceBoxId { get; set; }

        public int SourceUnitId { get; set; }

        public int SourceMediumId { get; set; }

        public string TargetCabinetName { get; set; } = string.Empty;

        public string TargetFace { get; set; } = string.Empty;

        public int TargetRow { get; set; }

        public int TargetColumn { get; set; }

        public string Remarks { get; set; } = string.Empty;

        /// <summary>
        /// 转为同档口多件请求（单件列表）。
        /// </summary>
        public InteractiveItemsPhysicalMoveRequest ToItemsRequest()
        {
            var request = new InteractiveItemsPhysicalMoveRequest
            {
                MediaKind = MediaKind,
                TargetCabinetName = TargetCabinetName,
                TargetFace = TargetFace,
                TargetRow = TargetRow,
                TargetColumn = TargetColumn,
                Remarks = Remarks
            };

            if (SourceBoxId > 0)
            {
                request.SourceBoxIds.Add(SourceBoxId);
            }

            if (SourceUnitId > 0)
            {
                request.SourceUnitIds.Add(SourceUnitId);
            }

            if (SourceMediumId > 0)
            {
                request.SourceMediumIds.Add(SourceMediumId);
            }

            return request;
        }
    }

    /// <summary>
    /// 开柜交互式多件物理迁档请求：同一源档口内若干实体迁入同一目标档口空余位。
    /// </summary>
    public sealed class InteractiveItemsPhysicalMoveRequest
    {
        public string MediaKind { get; set; } = ArchiveRegisterDomainValues.MediaKindSimulated;

        public List<int> SourceBoxIds { get; set; } = [];

        public List<int> SourceUnitIds { get; set; } = [];

        public List<int> SourceMediumIds { get; set; } = [];

        public string TargetCabinetName { get; set; } = string.Empty;

        public string TargetFace { get; set; } = string.Empty;

        public int TargetRow { get; set; }

        public int TargetColumn { get; set; }

        public string Remarks { get; set; } = string.Empty;
    }

    /// <summary>
    /// 开柜交互式迁档源会话项。
    /// </summary>
    public sealed class InteractiveItemRelocationSource
    {
        public string MediaKind { get; init; } = ArchiveRegisterDomainValues.MediaKindSimulated;

        public int SourceBoxId { get; init; }

        public int SourceUnitId { get; init; }

        public int SourceMediumId { get; init; }

        public string DisplayText { get; init; } = string.Empty;

        public string BoxSpecification { get; init; } = string.Empty;

        public string SourceDedicatedSlotCategoryName { get; init; } = string.Empty;

        public string SourceStorageLocation { get; init; } = string.Empty;

        /// <summary>源档口键（柜面-层-列），用于校验同档口多选。</summary>
        public string SourceSlotKey { get; init; } = string.Empty;

        public bool IsOpticalDiscMedia { get; init; }
    }
}
