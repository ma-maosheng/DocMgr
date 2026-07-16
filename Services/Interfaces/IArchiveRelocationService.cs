using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 档案移库服务契约：档案盒/介质的换位、批量迁移与位置同步。
    /// </summary>
    public interface IArchiveRelocationService
    {
        Task<ArchiveRelocationContainerSummary?> LoadSimulatedSourceAsync(string containerCode);

        Task<ArchiveRelocationContainerSummary?> LoadSimulatedSourceByIdAsync(int boxId);

        Task<ArchiveRelocationContainerSummary?> LoadElectronicSourceAsync(string containerCode);

        Task<ArchiveRelocationContainerSummary?> LoadElectronicSourceByIdAsync(int unitId);

        Task<IReadOnlyList<ArchiveRelocationSourceOption>> GetSimulatedSourceOptionsAsync(string projectName, string year);

        Task<IReadOnlyList<ArchiveRelocationSourceOption>> GetElectronicSourceOptionsAsync(string projectName, string year);

        Task<IReadOnlyList<ArchiveRelocationTargetOption>> GetSimulatedTargetOptionsAsync(int sourceBoxId);

        Task<IReadOnlyList<ArchiveRelocationTargetOption>> GetElectronicTargetOptionsAsync(
            int sourceUnitId,
            bool hardDiskMergeTargetsOnly = false);

        Task<ArchiveRelocationPreview> PreviewSimulatedRelocationAsync(SimulatedRelocationRequest request);

        Task<ArchiveRelocationPreview> PreviewElectronicRelocationAsync(ElectronicRelocationRequest request);

        Task<ArchiveRelocationResult> ExecuteSimulatedRelocationAsync(SimulatedRelocationRequest request);

        Task<ArchiveRelocationResult> ExecuteElectronicRelocationAsync(ElectronicRelocationRequest request);

        Task<ArchiveRelocationPreview> PreviewBatchSimulatedSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request);

        Task<ArchiveRelocationResult> ExecuteBatchSimulatedSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request);

        Task<ArchiveRelocationPreview> PreviewBatchElectronicSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request);

        Task<ArchiveRelocationResult> ExecuteBatchElectronicSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request);

        Task<ArchiveRelocationPreview> PreviewBatchBlankHardDiskSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request);

        Task<ArchiveRelocationResult> ExecuteBatchBlankHardDiskSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request);

        /// <summary>空白硬盘档口批量搬迁前：若源档口有待归还空白硬盘，返回确认提示文案；否则 null。</summary>
        Task<string?> GetBatchBlankHardDiskPendingReturnConfirmMessageAsync(
            BatchSimulatedSlotPhysicalMoveRequest request);

        Task<ArchiveRelocationPreview> PreviewInteractiveItemPhysicalMoveAsync(InteractiveItemPhysicalMoveRequest request);

        Task<ArchiveRelocationResult> ExecuteInteractiveItemPhysicalMoveAsync(InteractiveItemPhysicalMoveRequest request);

        /// <summary>迁档/销号前：若源盒存在待归还提档，返回确认提示文案；否则 null。</summary>
        Task<string?> GetSimulatedPendingReturnConfirmMessageAsync(int sourceBoxId, string actionLabel);

        /// <summary>档口批量搬迁前：若源档口内档案盒存在待归还提档，返回确认提示文案；否则 null。</summary>
        Task<string?> GetBatchSimulatedPendingReturnConfirmMessageAsync(
            BatchSimulatedSlotPhysicalMoveRequest request,
            string actionLabel);
    }
}
