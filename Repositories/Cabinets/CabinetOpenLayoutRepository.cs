using DocMgr.Data;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Repositories.YearlyArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.Cabinets;

public class CabinetOpenLayoutRepository : ICabinetOpenLayoutRepository
{
    private readonly AppDbContext _dbContext;

    public CabinetOpenLayoutRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Dictionary<string, ArchiveBoxSpecification> GetArchiveBoxSpecificationLookup()
    {
        return _dbContext.ArchiveBoxSpecifications
            .AsNoTracking()
            .ToDictionary(item => item.Name, item => item, StringComparer.OrdinalIgnoreCase);
    }

    public CabinetSlotSpecification? GetCabinetSlotSpecification(string cabinetTypeCode)
    {
        return _dbContext.CabinetSlotSpecifications
            .AsNoTracking()
            .FirstOrDefault(item => item.CabinetTypeCode == cabinetTypeCode);
    }

    public Dictionary<string, CabinetArchiveBoxPlacement> GetPlacementLookup(string cabinetName)
    {
        return _dbContext.CabinetArchiveBoxPlacements
            .AsNoTracking()
            .Where(item => item.CabinetName == cabinetName)
            .ToDictionary(item => item.BoxCode, StringComparer.OrdinalIgnoreCase);
    }

    public Cabinet? GetCabinetByIdOrName(int cabinetId, string cabinetName)
    {
        return _dbContext.Cabinets
            .AsNoTracking()
            .FirstOrDefault(item => item.Id == cabinetId || item.Name == cabinetName);
    }

    public Dictionary<string, string> GetHardDiskSlotCategoryLookup(int cabinetId)
    {
        return _dbContext.CabinetHardDiskSlotCategoryAssignments
            .AsNoTracking()
            .Where(item => item.CabinetId == cabinetId)
            .ToDictionary(item => $"{item.FaceCode}:{item.SlotCode}", item => item.CategoryName, StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, string> GetArchiveSlotCategoryLookup(int cabinetId)
    {
        return _dbContext.CabinetArchiveSlotCategoryAssignments
            .AsNoTracking()
            .Where(item => item.CabinetId == cabinetId)
            .ToDictionary(item => $"{item.FaceCode}:{item.SlotCode}", item => item.CategoryName, StringComparer.OrdinalIgnoreCase);
    }

    public List<HardDiskMedium> GetHardDiskMediaWithLedger()
    {
        return _dbContext.HardDiskMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.DiskCode)
            .ToList();
    }

    public List<HardDiskMediaTransaction> GetHardDiskMediaTransactionsByMediumIds(IReadOnlyCollection<int> mediumIds)
    {
        return _dbContext.HardDiskMediaTransactions
            .AsNoTracking()
            .Where(item => mediumIds.Contains(item.MediumId))
            .ToList();
    }

    public List<OpticalDiscMedium> GetInStockOpticalDiscMedia()
    {
        return _dbContext.OpticalDiscMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Where(item => !item.IsDeleted)
            .Where(item => item.Ledger != null && !string.IsNullOrWhiteSpace(item.Ledger.StorageLocation))
            .Where(item => item.Ledger!.MediaStatus == OpticalDiscMedium.StatusInStock
                || item.Ledger.MediaStatus == OpticalDiscMedium.StatusDamaged)
            .OrderBy(item => item.DiscCode)
            .ToList();
    }

    public List<OpticalDiscMedium> GetOpticalDiscMediaWithLedger()
    {
        return _dbContext.OpticalDiscMedia
            .AsNoTracking()
            .Include(item => item.Ledger)
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.DiscCode)
            .ToList();
    }

    public List<OpticalDiscMediaTransaction> GetOpticalDiscMediaTransactionsByMediumIds(IReadOnlyCollection<int> mediumIds)
    {
        return _dbContext.OpticalDiscMediaTransactions
            .AsNoTracking()
            .Where(item => mediumIds.Contains(item.MediumId))
            .ToList();
    }

    public List<YearlyElectronicArchiveUnitMediumLink> GetElectronicArchiveUnitMediumLinksByMediumIds(IReadOnlyCollection<int> mediumIds)
    {
        return _dbContext.YearlyElectronicArchiveUnitMediumLinks
            .AsNoTracking()
            .Include(link => link.ElectronicArchiveUnit)
                .ThenInclude(unit => unit.MediaItemLinks)
                    .ThenInclude(itemLink => itemLink.MediaItem)
                        .ThenInclude(item => item!.MediaEntry)
                            .ThenInclude(media => media!.RegisterRecord)
            .Include(link => link.ElectronicArchiveUnit)
                .ThenInclude(unit => unit.MediaItemLinks)
                    .ThenInclude(itemLink => itemLink.MediaItem)
                        .ThenInclude(item => item!.ElectronicDetail)
            .Where(link => mediumIds.Contains(link.HardDiskMediumId))
            .Where(link => link.ElectronicArchiveUnit != null)
            .ToList();
    }

    public List<YearlyElectronicArchiveUnitDiscLink> GetElectronicArchiveUnitDiscLinksByMediumIds(IReadOnlyCollection<int> mediumIds)
    {
        return _dbContext.YearlyElectronicArchiveUnitDiscLinks
            .AsNoTracking()
            .Include(link => link.ElectronicArchiveUnit)
                .ThenInclude(unit => unit.MediaItemLinks)
                    .ThenInclude(itemLink => itemLink.MediaItem)
                        .ThenInclude(item => item!.MediaEntry)
                            .ThenInclude(media => media!.RegisterRecord)
            .Include(link => link.ElectronicArchiveUnit)
                .ThenInclude(unit => unit.MediaItemLinks)
                    .ThenInclude(itemLink => itemLink.MediaItem)
                        .ThenInclude(item => item!.ElectronicDetail)
            .Where(link => mediumIds.Contains(link.OpticalDiscMediumId))
            .Where(link => link.ElectronicArchiveUnit != null)
            .ToList();
    }

    public Dictionary<string, decimal> GetUsedDataSizeMbByMediumCodes(IReadOnlyCollection<string> mediumCodes)
    {
        if (mediumCodes == null || mediumCodes.Count == 0)
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedCodes = mediumCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedCodes.Count == 0)
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        return _dbContext.YearlyElectronicArchiveUnitMediaItemLinks
            .AsNoTracking()
            .Where(link => normalizedCodes.Contains(link.MediumCode))
            .Select(link => new { link.MediumCode, link.DataSizeMb })
            .AsEnumerable()
            .GroupBy(link => link.MediumCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(link => link.DataSizeMb),
                StringComparer.OrdinalIgnoreCase);
    }

    public List<TopoMap> GetTopoMaps()
    {
        return _dbContext.TopoMaps
            .AsNoTracking()
            .ToList();
    }

    public List<AerialPhoto> GetAerialPhotos()
    {
        return _dbContext.AerialPhotos
            .AsNoTracking()
            .ToList();
    }

    public List<OtherMap> GetOtherMaps()
    {
        return _dbContext.OtherMaps
            .AsNoTracking()
            .ToList();
    }

    public List<YearlyArchiveBox> GetYearlyArchiveBoxesWithContents()
    {
        return _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .Include(box => box.MediaItemLinks)
                .ThenInclude(link => link.MediaItem)
                    .ThenInclude(item => item.MediaEntry)
                        .ThenInclude(media => media!.RegisterRecord)
            .Include(box => box.RegisterRecords)
            .ToList();
    }

    public YearlyArchiveBox? FindInUseYearlyArchiveBoxByLocationCode(string boxLocationCode)
    {
        if (string.IsNullOrWhiteSpace(boxLocationCode))
        {
            return null;
        }

        string normalizedBoxCode = boxLocationCode.Trim();
        return _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .FirstOrDefault(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                && box.BoxLocationCode == normalizedBoxCode);
    }

    public List<YearlyArchiveBox> GetYearlyArchiveBoxesByIds(IReadOnlyCollection<int> boxIds)
    {
        if (boxIds == null || boxIds.Count == 0)
        {
            return [];
        }

        return _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .Where(box => boxIds.Contains(box.Id))
            .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .Where(box => !string.IsNullOrWhiteSpace(box.BoxLocationCode))
            .ToList();
    }

    public List<YearlyArchiveBoxMediaItemRow> GetYearlyArchiveBoxMediaItemRows(YearlyArchiveBox box)
    {
        ArgumentNullException.ThrowIfNull(box);

        string normalizedBoxCode = box.BoxLocationCode?.Trim() ?? string.Empty;
        var facts = _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.ContainerKind == ArchiveContainerKind.ArchiveBox
                && (fact.ContainerId == box.Id
                    || fact.BoxLocationCode == normalizedBoxCode
                    || fact.CurrentStorageLocation == normalizedBoxCode
                    || fact.StorageLocation == normalizedBoxCode))
            .ToList()
            .OrderBy(fact => fact.FormNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(fact => fact.ItemName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(fact => fact.Id)
            .ToList();

        if (facts.Count == 0)
        {
            facts = BuildSyntheticFactsFromMediaItemLinks(box);
        }

        if (facts.Count == 0)
        {
            return [];
        }

        return BuildMediaItemRowsFromFacts(facts);
    }

    public YearlyElectronicArchiveUnit? FindInUseElectronicArchiveUnitByLocationCode(string storageLocationCode)
    {
        if (string.IsNullOrWhiteSpace(storageLocationCode))
        {
            return null;
        }

        string normalizedLocation = storageLocationCode.Trim();
        return _dbContext.YearlyElectronicArchiveUnits
            .AsNoTracking()
            .FirstOrDefault(unit => unit.UnitLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                && unit.StorageLocation == normalizedLocation);
    }

    public YearlyElectronicArchiveUnit? FindInUseElectronicArchiveUnitById(int unitId)
    {
        if (unitId <= 0)
        {
            return null;
        }

        return _dbContext.YearlyElectronicArchiveUnits
            .AsNoTracking()
            .FirstOrDefault(unit => unit.Id == unitId
                && unit.UnitLifecycleStatus == ArchiveContainerLifecycleStatus.InUse);
    }

    public List<YearlyArchiveBoxMediaItemRow> GetElectronicArchiveUnitMediaItemRows(YearlyElectronicArchiveUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        string normalizedLocation = unit.StorageLocation?.Trim() ?? string.Empty;
        string normalizedArchiveNo = unit.ElectronicArchiveNo?.Trim() ?? string.Empty;
        var facts = _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.ContainerKind == ArchiveContainerKind.ElectronicBag
                && (fact.ContainerId == unit.Id
                    || fact.ContainerCode == normalizedArchiveNo
                    || fact.CurrentStorageLocation == normalizedLocation
                    || fact.StorageLocation == normalizedLocation))
            .ToList()
            .OrderBy(fact => fact.FormNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(fact => fact.ItemName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(fact => fact.Id)
            .ToList();

        if (facts.Count == 0)
        {
            facts = BuildSyntheticElectronicFactsFromMediaItemLinks(unit);
        }

        if (facts.Count == 0)
        {
            return [];
        }

        return BuildMediaItemRowsFromFacts(facts);
    }

    private List<YearlyArchiveBoxMediaItemRow> BuildMediaItemRowsFromFacts(List<YearlyArchiveFilingFact> facts)
    {
        var factIds = facts.Select(fact => fact.Id).Where(id => id > 0).ToList();

        var outboundRows = factIds.Count == 0
            ? []
            : (
                from item in _dbContext.YearlyArchiveOutboundItems.AsNoTracking()
                join record in _dbContext.YearlyArchiveOutboundRecords.AsNoTracking()
                    on item.OutboundRecordId equals record.Id
                where factIds.Contains(item.FilingFactId)
                    && item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                    && record.Status == YearlyArchiveOutboundRecord.Completed
                select new
                {
                    item.FilingFactId,
                    item.NeedReturn,
                    item.ReservationStatus,
                    item.CopyCount,
                }).ToList()
                .Select(row => new
                {
                    row.FilingFactId,
                    row.NeedReturn,
                    row.ReservationStatus,
                    CopyCount = Math.Max(1, row.CopyCount ?? 1),
                })
                .ToList();

        var pendingReturnByFactId = outboundRows
            .Where(row => row.NeedReturn
                && string.Equals(row.ReservationStatus, ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed, StringComparison.Ordinal))
            .GroupBy(row => row.FilingFactId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.CopyCount));

        var noReturnByFactId = outboundRows
            .Where(row => !row.NeedReturn
                && !string.Equals(row.ReservationStatus, ArchiveOutboundDomainValues.SyncEntryPhaseReturned, StringComparison.Ordinal))
            .GroupBy(row => row.FilingFactId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.CopyCount));

        var returnRows = factIds.Count == 0
            ? []
            : (
                from returnItem in _dbContext.YearlyArchiveReturnItems.AsNoTracking()
                join returnRecord in _dbContext.YearlyArchiveReturnRecords.AsNoTracking()
                    on returnItem.ReturnRecordId equals returnRecord.Id
                where factIds.Contains(returnItem.FilingFactId)
                    && returnRecord.Status == YearlyArchiveReturnRecord.Completed
                select new
                {
                    returnItem.FilingFactId,
                    returnItem.LossCopyCount,
                    returnItem.ReturnCopyCount,
                    returnItem.ItemCondition,
                }).ToList();

        var lostByFactId = returnRows
            .GroupBy(row => row.FilingFactId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(row => row.LossCopyCount > 0
                    ? Math.Max(0, row.LossCopyCount)
                    : ArchiveReturnDomainValues.IsLossCondition(row.ItemCondition)
                        ? Math.Max(1, row.ReturnCopyCount)
                        : 0));

        var mediaItemIds = facts
            .Select(fact => fact.MediaItemId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var supplementByMediaItemId = LoadMediaItemSupplements(mediaItemIds);

        var projectYearByRegisterRecordId = LoadRegisterRecordContextById(
            facts.Select(fact => fact.RegisterRecordId).Where(id => id > 0).Distinct().ToList());

            return facts.Select(fact =>
            {
                var context = fact.RegisterRecordId > 0
                    ? projectYearByRegisterRecordId.GetValueOrDefault(fact.RegisterRecordId, RegisterRecordDisplayContext.Empty)
                    : RegisterRecordDisplayContext.Empty;

                return new YearlyArchiveBoxMediaItemRow
                {
                    Fact = fact,
                    PendingReturnCopyCount = fact.Id > 0 ? pendingReturnByFactId.GetValueOrDefault(fact.Id) : 0,
                    NoReturnCopyCount = fact.Id > 0 ? noReturnByFactId.GetValueOrDefault(fact.Id) : 0,
                    LostCopyCount = fact.Id > 0 ? lostByFactId.GetValueOrDefault(fact.Id) : 0,
                    InventoryLostCopyCount = Math.Max(0, fact.InventoryLostCopyCount),
                    ProjectYear = context.ProjectYear,
                    ArchivePurpose = context.ArchivePurpose,
                    Supplement = fact.MediaItemId > 0
                    ? supplementByMediaItemId.GetValueOrDefault(fact.MediaItemId, CabinetArchiveBoxMediaItemSupplement.Empty)
                    : CabinetArchiveBoxMediaItemSupplement.Empty,
            };
        }).ToList();
    }

    private sealed class RegisterRecordDisplayContext
    {
        public static RegisterRecordDisplayContext Empty { get; } = new();

        public string ProjectYear { get; init; } = string.Empty;

        public string ArchivePurpose { get; init; } = string.Empty;
    }

    private Dictionary<int, RegisterRecordDisplayContext> LoadRegisterRecordContextById(IReadOnlyCollection<int> registerRecordIds)
    {
        if (registerRecordIds.Count == 0)
        {
            return [];
        }

        var records = _dbContext.YearlyArchiveRegisterRecords
            .AsNoTracking()
            .Where(record => registerRecordIds.Contains(record.Id))
            .Select(record => new { record.Id, record.ProjectId, record.ArchivePurpose })
            .ToList();

        var projectIds = records
            .Where(record => record.ProjectId.HasValue)
            .Select(record => record.ProjectId!.Value)
            .Distinct()
            .ToList();

        var yearByProjectId = projectIds.Count == 0
            ? new Dictionary<int, string>()
            : _dbContext.ProjectInfos
                .AsNoTracking()
                .Where(project => projectIds.Contains(project.Id))
                .ToDictionary(project => project.Id, project => project.ImplementYear?.Trim() ?? string.Empty);

        return records.ToDictionary(
            record => record.Id,
            record => new RegisterRecordDisplayContext
            {
                ProjectYear = record.ProjectId.HasValue && yearByProjectId.TryGetValue(record.ProjectId.Value, out string? year)
                    ? year
                    : string.Empty,
                ArchivePurpose = record.ArchivePurpose?.Trim() ?? string.Empty,
            });
    }

    private Dictionary<int, CabinetArchiveBoxMediaItemSupplement> LoadMediaItemSupplements(IReadOnlyCollection<int> mediaItemIds)
    {
        if (mediaItemIds.Count == 0)
        {
            return [];
        }

        return _dbContext.YearlyArchiveRegisterMediaItems
            .AsNoTracking()
            .Where(item => mediaItemIds.Contains(item.Id))
            .Include(item => item.ElectronicDetail!)
                .ThenInclude(detail => detail.Entries)
            .Include(item => item.MediaEntry)
            .AsSplitQuery()
            .ToList()
            .ToDictionary(item => item.Id, BuildMediaItemSupplement);
    }

    private static CabinetArchiveBoxMediaItemSupplement BuildMediaItemSupplement(YearlyArchiveRegisterMediaItem mediaItem)
    {
        var detail = mediaItem.ElectronicDetail;
        var entryKinds = detail?.Entries?
            .Select(entry => entry.EntryKind)
            .ToList() ?? [];

        return new CabinetArchiveBoxMediaItemSupplement
        {
            Note = mediaItem.Note?.Trim() ?? string.Empty,
            StoragePath = mediaItem.StoragePath?.Trim() ?? string.Empty,
            MediaType = mediaItem.MediaEntry?.MediaType?.Trim() ?? string.Empty,
            Disposition = mediaItem.MediaEntry?.Disposition?.Trim() ?? string.Empty,
            MaterialCategory = detail?.MaterialCategory?.Trim() ?? string.Empty,
            SubCategory = detail?.SubCategory?.Trim() ?? string.Empty,
            DataOrganizationForm = detail?.DataOrganizationForm?.Trim() ?? string.Empty,
            DataSizeMb = detail?.DataSizeMb ?? 0m,
            ContentEntryBreakdownText = ElectronicContentEntryStatsSupport.FormatBreakdownFromEntries(entryKinds),
        };
    }

    private List<YearlyArchiveFilingFact> BuildSyntheticFactsFromMediaItemLinks(YearlyArchiveBox box)
    {
        var links = _dbContext.YearlyArchiveBoxMediaItemLinks
            .AsNoTracking()
            .Where(link => link.YearlyArchiveBoxId == box.Id)
            .Include(link => link.MediaItem)
                .ThenInclude(item => item.MediaEntry)
                    .ThenInclude(media => media!.RegisterRecord)
            .ToList();

        if (links.Count == 0)
        {
            return [];
        }

        return links
            .Where(link => link.MediaItem != null)
            .OrderBy(link => link.MediaItem.MediaEntry?.RegisterRecord?.FormNo ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link.MediaItem.ContentDesc ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link.Id)
            .Select(link =>
            {
                var mediaItem = link.MediaItem;
                var record = mediaItem.MediaEntry?.RegisterRecord;
                var mediaEntry = mediaItem.MediaEntry;
                return new YearlyArchiveFilingFact
                {
                    MediaKind = ArchiveRegisterDomainValues.MediaKindSimulated,
                    RegisterRecordId = record?.Id ?? 0,
                    RegisterMediaId = mediaEntry?.Id ?? mediaItem.YearlyArchiveRegisterMediaId,
                    MediaItemId = mediaItem.Id,
                    FormNo = record?.FormNo?.Trim() ?? string.Empty,
                    MaterialName = record?.MaterialName?.Trim() ?? box.ProjectName?.Trim() ?? string.Empty,
                    ProjectName = record?.ProjectName?.Trim() ?? box.ProjectName?.Trim() ?? string.Empty,
                    ProvideUnit = record?.ProvideUnit?.Trim() ?? string.Empty,
                    ApplicantName = record?.ApplicantName?.Trim() ?? string.Empty,
                    ItemType = mediaItem.ItemType?.Trim() ?? string.Empty,
                    ItemName = mediaItem.ContentDesc?.Trim() ?? string.Empty,
                    ConfidentialLevel = mediaItem.ConfidentialLevel?.Trim() ?? ArchiveRegisterDomainValues.ConfidentialLevelNone,
                    ContentCount = mediaItem.ContentCount,
                    ContainerKind = ArchiveContainerKind.ArchiveBox,
                    ContainerId = box.Id,
                    ContainerCode = box.ArchiveSequenceNo?.Trim() ?? string.Empty,
                    StorageLocation = box.BoxLocationCode?.Trim() ?? string.Empty,
                    BoxLocationCode = box.BoxLocationCode?.Trim() ?? string.Empty,
                    StorageCarrierType = mediaEntry?.MediaType?.Trim() ?? string.Empty,
                    Disposition = mediaEntry?.Disposition?.Trim() ?? string.Empty,
                    BoxSpecs = box.Specs?.Trim() ?? string.Empty,
                    FiledAt = box.ArchivedDate,
                    FiledBy = box.ArchivedBy?.Trim() ?? string.Empty,
                    LifecycleStatus = FilingFactLifecycleStatus.InArchive,
                };
            })
            .ToList();
    }

    private List<YearlyArchiveFilingFact> BuildSyntheticElectronicFactsFromMediaItemLinks(YearlyElectronicArchiveUnit unit)
    {
        var links = _dbContext.YearlyElectronicArchiveUnitMediaItemLinks
            .AsNoTracking()
            .Where(link => link.YearlyElectronicArchiveUnitId == unit.Id)
            .Include(link => link.MediaItem)
                .ThenInclude(item => item.MediaEntry)
                    .ThenInclude(media => media!.RegisterRecord)
            .ToList();

        if (links.Count == 0)
        {
            return [];
        }

        return links
            .Where(link => link.MediaItem != null)
            .OrderBy(link => link.FormNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link.ItemName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link.Id)
            .Select(link => ArchiveFilingFactRepository.BuildElectronicFactFromLink(link, unit, link.MediaItem))
            .ToList();
    }

    public Dictionary<int, CabinetOccupationLockDescriptor> GetActiveWithdrawalLocksByArchiveBoxIds(IReadOnlyCollection<int> boxIds)
        => BuildActiveWithdrawalLocksByContainerIds(boxIds, ArchiveContainerKind.ArchiveBox);

    public Dictionary<int, CabinetOccupationLockDescriptor> GetActiveWithdrawalLocksByElectronicUnitIds(IReadOnlyCollection<int> unitIds)
        => BuildActiveWithdrawalLocksByContainerIds(unitIds, ArchiveContainerKind.ElectronicBag);

    private Dictionary<int, CabinetOccupationLockDescriptor> BuildActiveWithdrawalLocksByContainerIds(
        IReadOnlyCollection<int> containerIds,
        ArchiveContainerKind containerKind)
    {
        if (containerIds == null || containerIds.Count == 0)
        {
            return new Dictionary<int, CabinetOccupationLockDescriptor>();
        }

        int[] inFlightStatuses =
        [
            YearlyArchiveOutboundRecord.Submitted,
            YearlyArchiveOutboundRecord.Approved,
            YearlyArchiveOutboundRecord.SignedUploaded
        ];

        var rows = (
            from item in _dbContext.YearlyArchiveOutboundItems.AsNoTracking()
            join record in _dbContext.YearlyArchiveOutboundRecords.AsNoTracking()
                on item.OutboundRecordId equals record.Id
            join fact in _dbContext.YearlyArchiveFilingFacts.AsNoTracking()
                on item.FilingFactId equals fact.Id
            where containerIds.Contains(fact.ContainerId)
                && fact.ContainerKind == containerKind
                && item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                && item.ReservationStatus == ArchiveOutboundDomainValues.SyncEntryPhaseActive
                && inFlightStatuses.Contains(record.Status)
            select new
            {
                fact.ContainerId,
                record.OutboundNo,
                CopyCount = item.CopyCount ?? 1,
            }).ToList();

        return rows
            .GroupBy(row => row.ContainerId)
            .ToDictionary(
                group => group.Key,
                group => BuildWithdrawalOccupationLockDescriptor(group.Select(row => (row.OutboundNo, row.CopyCount)).ToList()));
    }

    private static CabinetOccupationLockDescriptor BuildWithdrawalOccupationLockDescriptor(
        IReadOnlyList<(string OutboundNo, int CopyCount)> rows)
    {
        if (rows.Count == 0)
        {
            return CabinetOccupationLockDescriptor.Empty;
        }

        var outboundNos = rows
            .Select(row => row.OutboundNo?.Trim() ?? string.Empty)
            .Where(outboundNo => outboundNo.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(outboundNo => outboundNo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int reservedCopyCount = rows.Sum(row => Math.Max(1, row.CopyCount));
        string outboundNoText = string.Join("、", outboundNos);
        string copyHint = reservedCopyCount > 1 ? $"拟提档 {reservedCopyCount} 份" : "拟提档";

        return new CabinetOccupationLockDescriptor
        {
            HasLock = true,
            LockKindText = "出库预订",
            BusinessTypeText = "资料借出申请",
            BusinessNoText = outboundNoText,
            ReservedCopyCount = reservedCopyCount,
            ToolTipSupplement = string.IsNullOrWhiteSpace(outboundNoText)
                ? $"出库预订：{copyHint}"
                : $"出库预订：{copyHint}\n出库单号：{outboundNoText}",
        };
    }

    public IReadOnlyDictionary<int, IReadOnlyList<ActiveWithdrawalReservationSnapshot>> GetActiveWithdrawalReservationsByFilingFactIds(
        IReadOnlyCollection<int> filingFactIds)
    {
        if (filingFactIds == null || filingFactIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<ActiveWithdrawalReservationSnapshot>>();
        }

        int[] inFlightStatuses =
        [
            YearlyArchiveOutboundRecord.Submitted,
            YearlyArchiveOutboundRecord.Approved,
            YearlyArchiveOutboundRecord.SignedUploaded
        ];

        var rows = (
            from item in _dbContext.YearlyArchiveOutboundItems.AsNoTracking()
            join record in _dbContext.YearlyArchiveOutboundRecords.AsNoTracking()
                on item.OutboundRecordId equals record.Id
            where filingFactIds.Contains(item.FilingFactId)
                && item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                && item.ReservationStatus == ArchiveOutboundDomainValues.SyncEntryPhaseActive
                && inFlightStatuses.Contains(record.Status)
            select new
            {
                item.FilingFactId,
                record.OutboundNo,
                item.CopyCount,
            }).ToList();

        return rows
            .GroupBy(row => row.FilingFactId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ActiveWithdrawalReservationSnapshot>)group
                    .Select(row => new ActiveWithdrawalReservationSnapshot
                    {
                        FilingFactId = row.FilingFactId,
                        OutboundNo = row.OutboundNo?.Trim() ?? string.Empty,
                        ReservedCopyCount = Math.Max(1, row.CopyCount ?? 1),
                    })
                    .ToList());
    }

    public IReadOnlyList<CabinetHardDiskOccupationLockInfo> GetHardDiskOccupationLocksByElectronicUnitId(int electronicArchiveUnitId)
    {
        if (electronicArchiveUnitId <= 0)
        {
            return Array.Empty<CabinetHardDiskOccupationLockInfo>();
        }

        var mediumIds = _dbContext.YearlyElectronicArchiveUnitMediumLinks
            .AsNoTracking()
            .Where(link => link.YearlyElectronicArchiveUnitId == electronicArchiveUnitId)
            .Select(link => link.HardDiskMediumId)
            .Distinct()
            .ToList();

        if (mediumIds.Count == 0)
        {
            return Array.Empty<CabinetHardDiskOccupationLockInfo>();
        }

        return (
            from medium in _dbContext.HardDiskMedia.AsNoTracking()
            join lockItem in _dbContext.HardDiskRegisterLocks.AsNoTracking()
                on medium.Id equals lockItem.MediumId
            where mediumIds.Contains(medium.Id)
                && !medium.IsDeleted
            orderby medium.DiskCode
            select new CabinetHardDiskOccupationLockInfo
            {
                DiskCode = medium.DiskCode ?? string.Empty,
                BusinessType = lockItem.BusinessType,
                BusinessNo = lockItem.BusinessNo ?? string.Empty,
            }).ToList();
    }

    public Dictionary<int, CabinetOccupationLockDescriptor> GetActiveOutboundApplicationLocksByMediumIds(IReadOnlyCollection<int> mediumIds)
    {
        if (mediumIds == null || mediumIds.Count == 0)
        {
            return new Dictionary<int, CabinetOccupationLockDescriptor>();
        }

        int[] activeStatuses =
        [
            HardDiskMediaApplication.StatusDraft,
            HardDiskMediaApplication.StatusSubmitted,
            HardDiskMediaApplication.StatusApproved,
            HardDiskMediaApplication.StatusSignedUploaded,
            HardDiskMediaApplication.StatusPendingUpload,
            HardDiskMediaApplication.StatusPendingProcess,
        ];

        var rows = _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Where(item => mediumIds.Contains(item.MediumId))
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeOutboundTemporary ||
                           item.ApplicationType == HardDiskMediaApplication.TypeOutboundLongTerm ||
                           item.ApplicationType == HardDiskMediaApplication.TypeOutboundPermanent)
            .Where(item => activeStatuses.Contains(item.ApplicationStatus))
            .Select(item => new
            {
                item.MediumId,
                item.ApplicationNo,
                item.ApplicationType,
            })
            .ToList();

        return rows
            .GroupBy(row => row.MediumId)
            .ToDictionary(
                group => group.Key,
                group => BuildOutboundApplicationOccupationLockDescriptor(group.First().ApplicationNo, group.First().ApplicationType));
    }

    private static CabinetOccupationLockDescriptor BuildOutboundApplicationOccupationLockDescriptor(string? applicationNo, string? applicationType)
    {
        string businessNo = string.IsNullOrWhiteSpace(applicationNo) ? "（无）" : applicationNo.Trim();
        string outboundType = string.IsNullOrWhiteSpace(applicationType) ? string.Empty : applicationType.Trim();
        string supplement = $"占用锁\n业务类型：{HardDiskRegisterLock.BusinessTypeOutboundApplication}\n业务单号：{businessNo}";
        if (!string.IsNullOrWhiteSpace(outboundType))
        {
            supplement += $"\n申请类型：{outboundType}";
        }

        return new CabinetOccupationLockDescriptor
        {
            HasLock = true,
            LockKindText = "占用锁",
            BusinessTypeText = HardDiskRegisterLock.BusinessTypeOutboundApplication,
            BusinessNoText = businessNo,
            ToolTipSupplement = supplement,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<SimulatedArchiveBoxPendingReturnDetailRow> GetSimulatedArchiveBoxPendingReturnDetails(string boxLocationCode)
    {
        if (string.IsNullOrWhiteSpace(boxLocationCode))
        {
            return Array.Empty<SimulatedArchiveBoxPendingReturnDetailRow>();
        }

        var box = FindInUseYearlyArchiveBoxByLocationCode(boxLocationCode);
        if (box == null)
        {
            return Array.Empty<SimulatedArchiveBoxPendingReturnDetailRow>();
        }

        var mediaItemRows = GetYearlyArchiveBoxMediaItemRows(box);
        var factIds = mediaItemRows
            .Select(row => row.Fact.Id)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (factIds.Count == 0)
        {
            return Array.Empty<SimulatedArchiveBoxPendingReturnDetailRow>();
        }

        var filingFactNoById = mediaItemRows
            .Where(row => row.Fact.Id > 0)
            .GroupBy(row => row.Fact.Id)
            .ToDictionary(
                group => group.Key,
                group => group.First().Fact.FilingFactNo?.Trim() ?? string.Empty);

        var rows = (
            from item in _dbContext.YearlyArchiveOutboundItems.AsNoTracking()
            join record in _dbContext.YearlyArchiveOutboundRecords.AsNoTracking()
                on item.OutboundRecordId equals record.Id
            where factIds.Contains(item.FilingFactId)
                && item.MediaKind == ArchiveRegisterDomainValues.MediaKindSimulated
                && item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                && item.NeedReturn
                && record.Status == YearlyArchiveOutboundRecord.Completed
                && item.ReservationStatus == ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed
            orderby record.CompletedAt descending, record.OutboundNo, item.SortOrder, item.Id
            select new { item, record })
            .ToList();

        return rows
            .Select(row =>
            {
                int pendingReturnCopyCount = Math.Max(1, row.item.CopyCount ?? 1);
                DateTime? expectedReturnDate = row.item.ExpectedReturnDate ?? row.record.ExpectedReturnDate;

                return new SimulatedArchiveBoxPendingReturnDetailRow
                {
                    OutboundRecordId = row.record.Id,
                    OutboundNo = row.record.OutboundNo?.Trim() ?? string.Empty,
                    OutboundStatusDisplay = row.record.StatusStr,
                    OutboundCompletedAt = row.record.CompletedAt,
                    ApplicantName = row.record.ApplicantName?.Trim() ?? string.Empty,
                    ApplicantDept = row.record.ApplicantDept?.Trim() ?? string.Empty,
                    ApplyDate = row.record.ApplyDate,
                    Reason = row.record.Reason?.Trim() ?? string.Empty,
                    FilingFactId = row.item.FilingFactId,
                    FilingFactNo = filingFactNoById.GetValueOrDefault(row.item.FilingFactId) ?? string.Empty,
                    FormNo = row.item.FormNo?.Trim() ?? string.Empty,
                    MaterialName = row.item.MaterialName?.Trim() ?? string.Empty,
                    ItemName = row.item.ItemName?.Trim() ?? string.Empty,
                    MediaType = row.item.MediaType?.Trim() ?? string.Empty,
                    PendingReturnCopyCount = pendingReturnCopyCount,
                    ExpectedReturnDate = expectedReturnDate,
                    ArchivePurpose = row.item.ArchivePurpose?.Trim() ?? string.Empty,
                };
            })
            .ToList();
    }
}
