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

        /// <summary>出网外部离线·硬盘·介质带回：是否使用资料室库存空盘。</summary>
        public bool UseInStockBlankHardDisk { get; set; }

        /// <summary>出网外部离线征用库内空盘时的介质 Id。</summary>
        public int? RequisitionedMediumId { get; set; }

        /// <summary>出网外部离线征用库内空盘时的硬盘编号。</summary>
        public string RequisitionedHardDiskCode { get; set; } = string.Empty;

        /// <summary>出网外部离线征用库内空盘后，该盘是否需归还。</summary>
        public bool RequisitionedDiskNeedReturn { get; set; }

        /// <summary>出网外部离线征用库内空盘且需归还时的预计归还日期。</summary>
        public DateTime? ExpectedReturnDate { get; set; }

        public virtual YearlyArchiveRegisterRecord? RegisterRecord { get; set; }

        public virtual NetworkTransfer.NetworkInboundRecord? NetworkInboundRecord { get; set; }

        public virtual NetworkTransfer.NetworkOutboundRecord? NetworkOutboundRecord { get; set; }

        public virtual List<YearlyArchiveRegisterMediaItem> Items { get; set; } = new();
        public virtual List<YearlyElectronicArchiveUnitMediaLink> ElectronicArchiveUnitLinks { get; set; } = new();
    }

    public class YearlyArchiveRegisterMediaItem
    {
        public int Id { get; set; }
        public int YearlyArchiveRegisterMediaId { get; set; }
        public string ItemType { get; set; } = "????";
        public string ContentDesc { get; set; } = string.Empty;
        public int ContentCount { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;

        /// <summary>
        /// 资料子项密级（由申请人填写，审批时可由资料室同步修正）。
        /// </summary>
        public string ConfidentialLevel { get; set; } = ArchiveRegisterDomainValues.ConfidentialLevelNone;
        public virtual YearlyArchiveRegisterMedia? MediaEntry { get; set; }
        public virtual YearlyArchiveRegisterElectronicMediaItemDetail? ElectronicDetail { get; set; }
        public virtual List<YearlyArchiveBoxMediaItemLink> ArchiveBoxLinks { get; set; } = new();
        public virtual List<YearlyElectronicArchiveUnitMediaItemLink> ElectronicArchiveUnitMediaItemLinks { get; set; } = new();
    }
}
