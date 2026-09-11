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

        /// <summary>盒号（四段编码，全局唯一）。</summary>
        public string BoxCode { get; set; } = string.Empty;

        // 结构化位置信息（由盒号解析写入，冗余存储便于按档口检索）
        public string CabinetName { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public int Row { get; set; }
        public int Column { get; set; }

        /// <summary>盒在档口内的序号（盒号末段）。</summary>
        public int BoxIndex { get; set; }

        /// <summary>档案盒规格（对齐 ArchiveBoxSpecifications 名称）。</summary>
        public string BoxSpecification { get; set; } = string.Empty;

        /// <summary>放置方式（默认盒脊向外）。</summary>
        public string PlacementMode { get; set; } = "SpineOut";

        /// <summary>容器生命周期状态（在库 / 已离库，见 HistoryArchiveDisposalDomainValues）。</summary>
        public string LifecycleStatus { get; set; } = HistoryArchiveDisposalDomainValues.LifecycleInStock;

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
