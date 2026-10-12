using System;
using System.Collections.Generic;

namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档资料档案盒实体（地形图/航片/其他图件共用的物理容器）。
    /// 盒号即物理位置（四段编码，如 甲A-1-2-01）；台账行通过
    /// <see cref="HistoryArchiveBoxLedgerLink"/> 与盒关联，一条台账可关联多个盒（混放为合法建模）。
    /// </summary>
    public class HistoryArchiveBox
    {
        public int Id { get; set; }

        /// <summary>盒号（四段编码，在库时全局唯一，即物理位置；已离库清空以释放档位）。</summary>
        public string BoxCode { get; set; } = string.Empty;

        /// <summary>档案盒规格（对齐 ArchiveBoxSpecifications 名称）。</summary>
        public string BoxSpecification { get; set; } = string.Empty;

        /// <summary>放置方式（默认盒脊向外）。</summary>
        public string PlacementMode { get; set; } = "SpineOut";

        /// <summary>容器生命周期状态（在库 / 已离库，见 HistoryArchiveDisposalDomainValues）。</summary>
        public string LifecycleStatus { get; set; } = HistoryArchiveDisposalDomainValues.LifecycleInStock;

        /// <summary>离库前最后盒号（办结清空 <see cref="BoxCode"/> 前写入，对齐年度盒销号）。</summary>
        public string LastStorageLocation { get; set; } = string.Empty;

        public string ArchivedBy { get; set; } = string.Empty;
        public DateTime ArchivedDate { get; set; }
        public string Remarks { get; set; } = string.Empty;

        /// <summary>盒内台账行关联。</summary>
        public virtual List<HistoryArchiveBoxLedgerLink> LedgerLinks { get; set; } = new();
    }

    /// <summary>
    /// 历史档案盒与台账行的关联（一条台账行可关联多个盒）。
    /// MaterialKind + RecordId 定位台账行，不建硬外键，避免三张台账表各自建导航。
    /// </summary>
    public class HistoryArchiveBoxLedgerLink
    {
        public int Id { get; set; }

        /// <summary>历史档案盒ID。</summary>
        public int HistoryArchiveBoxId { get; set; }

        /// <summary>台账类别（TopoMap / AerialPhoto / OtherMap，见 HistoryArchiveDisposalDomainValues.MaterialKind*）。</summary>
        public string MaterialKind { get; set; } = string.Empty;

        /// <summary>台账行ID。</summary>
        public int RecordId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual HistoryArchiveBox? Box { get; set; }
    }
}
