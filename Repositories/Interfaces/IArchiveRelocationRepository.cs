using DocMgr.Models.HistoryArchive;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces
{
    /// <summary>
    /// 档案移库数据访问契约：移库换位涉及的容器与位置数据读写。
    /// </summary>
    public interface IArchiveRelocationRepository
    {
        Task<IArchiveFilingRepositoryTransaction> BeginTransactionAsync();

        Task<string?> GetLastRelocationNoByPrefixAsync(string prefix);

        void AddRelocationRecord(YearlyArchiveRelocationRecord record);

        Task<int> SaveChangesAsync();

        Task<YearlyArchiveBox?> GetArchiveBoxForRelocationAsync(int boxId);

        Task<YearlyArchiveBox?> GetArchiveBoxBySequenceNoAsync(string sequenceNo);

        /// <summary>按盒号列表加载引用该盒号的在库历史资料台账（带跟踪，用于迁档改写 BoxNumber）。</summary>
        Task<List<HistoryArchiveLedgerReferenceGroup>> GetHistoryLedgerReferencesByBoxCodesAsync(
            IReadOnlyCollection<string> boxCodes);

        /// <summary>加载指定档口内全部在库历史资料台账引用（带跟踪，用于整档口批量搬迁）。</summary>
        Task<List<HistoryArchiveLedgerReferenceGroup>> GetHistoryLedgerReferencesInSlotAsync(
            string cabinetName,
            string face,
            int row,
            int column);

        /// <summary>获取被未办结历史处置单占用的盒号集合（历史迁档互斥校验）。</summary>
        Task<HashSet<string>> GetHistoryDisposalLockedBoxCodesAsync();

        /// <summary>按盒号加载在库历史档案盒实体（带跟踪，用于迁档改写盒号与位置）。</summary>
        Task<List<HistoryArchiveBox>> GetHistoryArchiveBoxesByCodesForUpdateAsync(
            IReadOnlyCollection<string> boxCodes);

        /// <summary>加载指定档口内全部在库历史档案盒实体（带跟踪）。</summary>
        Task<List<HistoryArchiveBox>> GetHistoryArchiveBoxesInSlotForUpdateAsync(
            string cabinetName,
            string face,
            int row,
            int column);

        Task<YearlyElectronicArchiveUnit?> GetElectronicUnitForRelocationAsync(int unitId);

        Task<YearlyElectronicArchiveUnit?> GetElectronicUnitByArchiveNoAsync(string archiveNo);

        Task<List<YearlyArchiveBox>> GetSimulatedTargetBoxesAsync(string projectName, string year, int excludeBoxId);

        Task<List<YearlyElectronicArchiveUnit>> GetElectronicTargetUnitsAsync(string projectName, string year, int excludeUnitId);

        Task<List<YearlyArchiveFilingFact>> GetFilingFactsBySourceLinksAsync(
            string sourceLinkType,
            IReadOnlyCollection<int> sourceLinkIds);

        Task<List<YearlyArchiveFilingFact>> GetFilingFactsByContainerAsync(
            string mediaKind,
            int containerId);

        Task<HardDiskMedium?> GetHardDiskMediumByCodeWithLedgerAsync(string diskCode);

        Task<List<YearlyElectronicArchiveUnitMediumLink>> GetElectronicUnitMediumLinksAsync(int unitId);

    Task<List<YearlyElectronicArchiveUnitMediumLink>> GetElectronicMediumLinksByMediumIdAsync(int mediumId);

        Task<List<YearlyElectronicArchiveUnitDiscLink>> GetElectronicUnitDiscLinksAsync(int unitId);

        Task<List<YearlyArchiveBox>> GetSimulatedSourceCandidatesAsync(string projectName, string year);

        Task<List<YearlyElectronicArchiveUnit>> GetElectronicSourceCandidatesAsync(string projectName, string year);

        Task<List<YearlyElectronicArchiveUnit>> GetInUseElectronicArchiveUnitsInSlotForRelocationAsync(
            string cabinetName,
            string side,
            int row,
            int column);
    }
}
