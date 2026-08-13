using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.NetworkTransfer;

/// <summary>
/// 年度资料档案管理与出入网管理之间的一次跨域业务链。
/// 该实体仅承担关联与执行进度汇总，不替代各业务单据和台账。
/// </summary>
[Table("NetworkArchiveBusinessChains")]
public sealed class NetworkArchiveBusinessChain
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string ChainNo { get; set; } = string.Empty;

    [Required]
    public string ScenarioKind { get; set; } = string.Empty;

    [Required]
    public string PrimaryBusinessType { get; set; } = string.Empty;

    public int PrimaryBusinessId { get; set; }

    public string StatusSummary { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<NetworkArchiveBusinessTask> Tasks { get; set; } =
        new List<NetworkArchiveBusinessTask>();
}

/// <summary>
/// 跨域业务链中的执行任务或关联子单。
/// </summary>
[Table("NetworkArchiveBusinessTasks")]
public sealed class NetworkArchiveBusinessTask
{
    [Key]
    public int Id { get; set; }

    public int BusinessChainId { get; set; }

    [Required]
    public string TaskKind { get; set; } = string.Empty;

    [Required]
    public string BusinessType { get; set; } = string.Empty;

    public int? BusinessId { get; set; }

    public string BusinessNo { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = string.Empty;

    [Required]
    public string DedupKey { get; set; } = string.Empty;

    public string ResultMessage { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public NetworkArchiveBusinessChain? BusinessChain { get; set; }
}
