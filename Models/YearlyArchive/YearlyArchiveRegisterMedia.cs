using System.Collections.Generic;

namespace DocMgr.Models.YearlyArchive
{
    public class YearlyArchiveRegisterMedia
    {
        public int Id { get; set; }

        /// <summary>资料登记单 Id；与入网/出网单 Id 三选一。</summary>
        public int? YearlyArchiveRegisterRecordId { get; set; }

        /// <summary>档外入网单 Id；与登记单、出网单 Id 三选一。</summary>
        public int? NetworkInboundRecordId { get; set; }

        /// <summary>出网申请单 Id；与登记单、入网单 Id 三选一。</summary>
        public int? NetworkOutboundRecordId { get; set; }
        public string MediaKind { get; set; } = "???";
        public string MediaType { get; set; } = string.Empty;
        public int MediaCount { get; set; }
        public string Disposition { get; set; } = string.Empty;

        /// <summary>
        /// ????????????????
        /// </summary>
        public bool IsBorrowedHardDisk { get; set; }

        /// <summary>
        /// ????????????
        /// </summary>
        public string BorrowedHardDiskCode { get; set; } = string.Empty;

        /// <summary>出网使用库内空盘。院外为征用离室；资料室存档为预定在库空盘，盘不出室。</summary>
        public bool UseInStockBlankHardDisk { get; set; }

        /// <summary>征用或预定的库内空盘介质 Id。</summary>
        public int? RequisitionedMediumId { get; set; }

        /// <summary>征用或预定的库内空盘硬盘编号。</summary>
        public string RequisitionedHardDiskCode { get; set; } = string.Empty;

        /// <summary>出网外部离线征用库内空盘后，该盘是否需归还。资料室存档固定为否。</summary>
        public bool RequisitionedDiskNeedReturn { get; set; }

        /// <summary>出网外部离线征用库内空盘且需归还时的预计归还日期。</summary>
        public DateTime? ExpectedReturnDate { get; set; }

        /// <summary>资料室存档·硬盘并档：目标电子立档单元 Id。</summary>
        public int? AppendTargetElectronicUnitId { get; set; }

        /// <summary>资料室存档·硬盘并档：申请时的目标硬盘编号快照。</summary>
        public string AppendTargetHardDiskCode { get; set; } = string.Empty;

        public virtual YearlyArchiveRegisterRecord? RegisterRecord { get; set; }

        public virtual NetworkTransfer.NetworkInboundRecord? NetworkInboundRecord { get; set; }

        public virtual NetworkTransfer.NetworkOutboundRecord? NetworkOutboundRecord { get; set; }

        public virtual List<YearlyArchiveRegisterMediaItem> Items { get; set; } = new();
    }

    public class YearlyArchiveRegisterMediaItem
    {
        public int Id { get; set; }
        public int YearlyArchiveRegisterMediaId { get; set; }
        public string ContentDesc { get; set; } = string.Empty;
        public int ContentCount { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;

        /// <summary>
        /// 资料来源（内部产生 / 外部提供 / 存量直办），按资料子项分别登记。
        /// </summary>
        public string SourceType { get; set; } = ArchiveRegisterDomainValues.SourceTypeInternal;

        /// <summary>
        /// 提供单位（资料来源为内部产生时默认为资料室；外部提供时填写来源单位），按资料子项分别登记。
        /// </summary>
        public string ProvideUnit { get; set; } = string.Empty;

        /// <summary>
        /// 资料子项密级（由申请人填写，审批时可由资料室同步修正）。
        /// </summary>
        public string ConfidentialLevel { get; set; } = ArchiveRegisterDomainValues.ConfidentialLevelNone;
        public virtual YearlyArchiveRegisterMedia? MediaEntry { get; set; }
        public virtual YearlyArchiveRegisterElectronicMediaItemDetail? ElectronicDetail { get; set; }
        public virtual YearlyArchiveRegisterSimulatedMediaItemDetail? SimulatedDetail { get; set; }
        public virtual List<YearlyArchiveBoxMediaItemLink> ArchiveBoxLinks { get; set; } = new();
        public virtual List<YearlyElectronicArchiveUnitMediaItemLink> ElectronicArchiveUnitMediaItemLinks { get; set; } = new();
    }
}
