using System;
using System.Collections.Generic;

namespace DocMgr.Models.YearlyArchive
{
    public sealed class ArchiveRelocationContainerSummary
    {
        public int ContainerId { get; init; }

        public string ContainerCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string LifecycleStatus { get; init; } = ArchiveContainerLifecycleStatus.InUse;

        public string StorageCarrierType { get; init; } = string.Empty;

        /// <summary>
        /// 模拟介质档案盒规格（如「标准(5cm)」）；电子介质袋可为空。
        /// </summary>
        public string BoxSpecification { get; init; } = string.Empty;

        public string LinkedMediumCodes { get; init; } = string.Empty;

        /// <summary>
        /// 当前实际承载资料的硬盘编号（不含迁档历史拼接值）。
        /// </summary>
        public string ActiveLinkedMediumCode { get; init; } = string.Empty;

        public int ItemCount { get; init; }

        public IReadOnlyList<ArchiveRelocationItemSummary> Items { get; init; } = Array.Empty<ArchiveRelocationItemSummary>();
    }

    public sealed class ArchiveRelocationItemSummary
    {
        public int MediaItemId { get; init; }

        public string FormNo { get; init; } = string.Empty;

        /// <summary>所属年度。</summary>
        public string Year { get; init; } = string.Empty;

        /// <summary>所属项目。</summary>
        public string ProjectName { get; init; } = string.Empty;

        /// <summary>资料名称。</summary>
        public string MaterialName { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        /// <summary>迁档前物理位置编号（档口/完整位置编码）。</summary>
        public string BeforeStorageLocation { get; init; } = string.Empty;

        /// <summary>迁档后物理位置编号（未确定为「—」）。</summary>
        public string AfterStorageLocation { get; init; } = string.Empty;
    }

    public sealed class ArchiveRelocationTargetOption
    {
        public int ContainerId { get; init; }

        public string ContainerCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string DisplayText { get; init; } = string.Empty;

        public bool IsEmpty { get; init; }
    }

    public sealed class ArchiveRelocationSourceOption
    {
        public int ContainerId { get; init; }

        public string ContainerCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public int ItemCount { get; init; }

        public string DisplayText { get; init; } = string.Empty;

        public string ActiveLinkedMediumCode { get; init; } = string.Empty;
    }

    public sealed class SimulatedRelocationRequest
    {
        public string RelocationMode { get; set; } = ArchiveRelocationMode.PhysicalMove;

        public int SourceBoxId { get; set; }

        public int? TargetBoxId { get; set; }

        public string NewStorageLocation { get; set; } = string.Empty;

        public string NewCabinetName { get; set; } = string.Empty;

        public string NewSide { get; set; } = string.Empty;

        public int? NewRow { get; set; }

        public int? NewColumn { get; set; }

        public int? NewBoxIndex { get; set; }

        /// <summary>
        /// 物理位置迁移时勾选「迁入空盒」：在目标档口新建档案盒承载资料，源盒销号。
        /// </summary>
        public bool MoveContentsToNewEmptyBox { get; set; }

        public string NewBoxSpecification { get; set; } = string.Empty;

        public string Remarks { get; set; } = string.Empty;
    }

    public sealed class ElectronicRelocationRequest
    {
        public string RelocationMode { get; set; } = ArchiveRelocationMode.PhysicalMove;

        public int SourceUnitId { get; set; }

        public int? TargetUnitId { get; set; }

        public int? TargetBlankHardDiskMediumId { get; set; }

        public string TargetBlankHardDiskCode { get; set; } = string.Empty;

        public string NewStorageLocation { get; set; } = string.Empty;

        public string SourceHardDiskReturnLocation { get; set; } = string.Empty;

        public bool ConfirmHardDiskFormatted { get; set; }

        public bool ConfirmOpticalDiscDestroyed { get; set; }

        /// <summary>
        /// 迁入空白硬盘/空白光盘、并入同项目硬盘模式下：保留原件，仅在目标介质生成备份副本。
        /// </summary>
        public bool ExecuteBackupMechanism { get; set; }

        public string Remarks { get; set; } = string.Empty;
    }

    public sealed class BackupElectronicLinkWriteItem
    {
        public YearlyElectronicArchiveUnitMediaItemLink Link { get; init; } = null!;

        public int OriginalSourceLinkId { get; init; }
    }

    public sealed class ArchiveRelocationPreview
    {
        public bool CanExecute { get; init; }

        public string SummaryText { get; init; } = string.Empty;

        public string BlockReason { get; init; } = string.Empty;

        public int AffectedItemCount { get; init; }
    }

    public sealed class ArchiveRelocationResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public string RelocationNo { get; init; } = string.Empty;

        public static ArchiveRelocationResult Ok(string relocationNo, string message)
            => new() { Success = true, RelocationNo = relocationNo, Message = message };

        public static ArchiveRelocationResult Fail(string message)
            => new() { Success = false, Message = message };
    }
}
