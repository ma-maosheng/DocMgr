using System.Collections.Generic;

namespace DocMgr.Models.YearlyArchive
{
    public class YearlyArchiveRegisterMedia
    {
        public int Id { get; set; }
        public int YearlyArchiveRegisterRecordId { get; set; }
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

        public virtual YearlyArchiveRegisterRecord? RegisterRecord { get; set; }
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
