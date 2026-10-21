namespace DocMgr.Models.HistoryArchive;

/// <summary>
/// 历史存档杂项资料。一条记录对应一个存放容器。
/// </summary>
public class HistoryMiscMaterial
{
    public int Id { get; set; }

    /// <summary>资料大类。</summary>
    public string MajorCategory { get; set; } = string.Empty;

    /// <summary>资料子类，隶属于资料大类。</summary>
    public string SubCategory { get; set; } = string.Empty;

    /// <summary>存放容器类型，如盒、桶；由已录入值提示，可新增。</summary>
    public string ContainerKind { get; set; } = string.Empty;

    /// <summary>贴在容器上的编号。在库期间同一容器类型下唯一。</summary>
    public string ContainerCode { get; set; } = string.Empty;

    /// <summary>四段存放位置。在库时必填；办结离库后清空。</summary>
    public string StorageLocation { get; set; } = string.Empty;

    public string Remark { get; set; } = string.Empty;

    public string Registrant { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; }

    public string Modifier { get; set; } = string.Empty;

    public DateTime? ModifiedAt { get; set; }

    /// <summary>在库 / 离库锁定 / 已离库。</summary>
    public string LifecycleStatus { get; set; } = HistoryArchiveDisposalDomainValues.LifecycleInStock;

    /// <summary>离库办结前的存放位置。</summary>
    public string LastStorageLocation { get; set; } = string.Empty;

    public virtual List<HistoryMiscMaterialLine> Lines { get; set; } = new();
}

/// <summary>
/// 杂项资料详情行。顺序编号只在界面显示，不写入正文。
/// </summary>
public class HistoryMiscMaterialLine
{
    public int Id { get; set; }

    public int MaterialId { get; set; }

    public int SortOrder { get; set; }

    public string LineText { get; set; } = string.Empty;

    public virtual HistoryMiscMaterial? Material { get; set; }
}
