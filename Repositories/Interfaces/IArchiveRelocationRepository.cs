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
