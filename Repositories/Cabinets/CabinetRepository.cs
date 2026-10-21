using DocMgr.Data;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.YearlyArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.Cabinets;

public class CabinetRepository : ICabinetRepository
{
    private readonly AppDbContext _dbContext;

    public CabinetRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<Cabinet> GetAll()
    {
        return _dbContext.Cabinets.ToList();
    }

    public Task<List<Cabinet>> GetAllAsync()
    {
        return _dbContext.Cabinets.ToListAsync();
    }

    public bool Any()
    {
        return _dbContext.Cabinets.Any();
    }

    public Task<bool> AnyAsync()
    {
        return _dbContext.Cabinets.AnyAsync();
    }

    public Cabinet? GetById(int cabinetId)
    {
        return _dbContext.Cabinets.FirstOrDefault(item => item.Id == cabinetId);
    }

    public CabinetStructureSnapshot? GetStructureSnapshot(int cabinetId)
    {
        var row = _dbContext.Cabinets
            .AsNoTracking()
            .Where(item => item.Id == cabinetId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Type,
                item.FaceCount,
                item.LayerCount,
                item.ColumnCount,
                item.HardDiskSlotCapacity,
                item.OpticalDiscSlotCapacity
            })
            .FirstOrDefault();
        if (row == null)
        {
            return null;
        }

        return new CabinetStructureSnapshot(
            row.Id,
            row.Name,
            row.Type,
            row.FaceCount,
            row.LayerCount,
            row.ColumnCount,
            row.HardDiskSlotCapacity,
            row.OpticalDiscSlotCapacity);
    }

    public IReadOnlyList<string> GetCabinetNamesExcept(int cabinetId)
    {
        return _dbContext.Cabinets
            .AsNoTracking()
            .Where(item => item.Id != cabinetId)
            .Select(item => item.Name)
            .ToList();
    }

    public void Add(Cabinet cabinet)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        _dbContext.Cabinets.Add(cabinet);
    }

    public void AddRange(IEnumerable<Cabinet> cabinets)
    {
        ArgumentNullException.ThrowIfNull(cabinets);
        _dbContext.Cabinets.AddRange(cabinets);
    }

    public void Update(Cabinet cabinet)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        _dbContext.Cabinets.Update(cabinet);
    }

    public void Remove(Cabinet cabinet)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        _dbContext.Cabinets.Remove(cabinet);
    }

    public CabinetHardDiskSlotCategoryAssignment? GetSlotCategoryAssignment(int cabinetId, string faceCode, string slotCode)
    {
        string normalizedFaceCode = faceCode.Trim();
        string normalizedSlotCode = slotCode.Trim();
        return _dbContext.CabinetHardDiskSlotCategoryAssignments
            .AsEnumerable()
            .FirstOrDefault(item =>
                item.CabinetId == cabinetId
                && string.Equals(item.FaceCode, normalizedFaceCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.SlotCode, normalizedSlotCode, StringComparison.OrdinalIgnoreCase));
    }

    public List<CabinetHardDiskSlotCategoryAssignment> GetSlotCategoryAssignmentsByCabinetId(int cabinetId)
    {
        return _dbContext.CabinetHardDiskSlotCategoryAssignments
            .Where(item => item.CabinetId == cabinetId)
            .ToList();
    }

    public void AddSlotCategoryAssignment(CabinetHardDiskSlotCategoryAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _dbContext.CabinetHardDiskSlotCategoryAssignments.Add(assignment);
    }

    public void RemoveSlotCategoryAssignment(CabinetHardDiskSlotCategoryAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _dbContext.CabinetHardDiskSlotCategoryAssignments.Remove(assignment);
    }

    public CabinetArchiveSlotCategoryAssignment? GetArchiveSlotCategoryAssignment(int cabinetId, string faceCode, string slotCode)
    {
        string normalizedFaceCode = faceCode.Trim();
        string normalizedSlotCode = slotCode.Trim();
        return _dbContext.CabinetArchiveSlotCategoryAssignments
            .AsEnumerable()
            .FirstOrDefault(item =>
                item.CabinetId == cabinetId
                && string.Equals(item.FaceCode, normalizedFaceCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.SlotCode, normalizedSlotCode, StringComparison.OrdinalIgnoreCase));
    }

    public List<CabinetArchiveSlotCategoryAssignment> GetArchiveSlotCategoryAssignmentsByCabinetId(int cabinetId)
    {
        return _dbContext.CabinetArchiveSlotCategoryAssignments
            .Where(item => item.CabinetId == cabinetId)
            .ToList();
    }

    public void AddArchiveSlotCategoryAssignment(CabinetArchiveSlotCategoryAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _dbContext.CabinetArchiveSlotCategoryAssignments.Add(assignment);
    }

    public void RemoveArchiveSlotCategoryAssignment(CabinetArchiveSlotCategoryAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _dbContext.CabinetArchiveSlotCategoryAssignments.Remove(assignment);
    }

    public bool HasInStockMediaInMagneticDiskSlot(string cabinetName, string faceCode, string slotCode)
    {
        if (string.IsNullOrWhiteSpace(cabinetName)
            || string.IsNullOrWhiteSpace(faceCode)
            || string.IsNullOrWhiteSpace(slotCode)
            || !TryParseMagneticDiskSlotRowColumn(slotCode, out int row, out int column))
        {
            return false;
        }

        string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName.Trim(), faceCode.Trim(), row, column);
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return false;
        }

        var hardDiskLocations = _dbContext.HardDiskMedia
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.Ledger != null)
            .Where(item => item.Ledger!.MediaStatus == HardDiskMedium.StatusInStockBlank
                || item.Ledger.MediaStatus == HardDiskMedium.StatusInStockData
                || item.Ledger.MediaStatus == HardDiskMedium.StatusInStockDamaged)
            .Select(item => item.Ledger!.StorageLocation)
            .ToList();

        if (hardDiskLocations.Any(location => IsSameMagneticDiskSlot(location, slotKey)))
        {
            return true;
        }

        var opticalDiscLocations = _dbContext.OpticalDiscMedia
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.Ledger != null)
            .Where(item => item.Ledger!.MediaStatus == OpticalDiscMedium.StatusInStock
                || item.Ledger.MediaStatus == OpticalDiscMedium.StatusDamaged)
            .Select(item => item.Ledger!.StorageLocation)
            .ToList();

        return opticalDiscLocations.Any(location => IsSameMagneticDiskSlot(location, slotKey));
    }

    public (int MaxHardDiskOccupancy, int MaxOpticalDiscOccupancy) GetMaxMagneticSlotOccupancy(Cabinet cabinet)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        if (cabinet.Type != CabinetType.MagneticDisk || cabinet.Id <= 0 || string.IsNullOrWhiteSpace(cabinet.Name))
        {
            return (0, 0);
        }

        var assignments = GetSlotCategoryAssignmentsByCabinetId(cabinet.Id);
        if (assignments.Count == 0)
        {
            return (0, 0);
        }

        string cabinetName = cabinet.Name.Trim();
        var hardDiskLocations = _dbContext.HardDiskMedia
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.Ledger != null)
            .Where(item => item.Ledger!.MediaStatus == HardDiskMedium.StatusInStockBlank
                || item.Ledger.MediaStatus == HardDiskMedium.StatusInStockData
                || item.Ledger.MediaStatus == HardDiskMedium.StatusInStockDamaged)
            .Select(item => item.Ledger!.StorageLocation)
            .ToList();

        var opticalDiscLocations = _dbContext.OpticalDiscMedia
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.Ledger != null)
            .Where(item => item.Ledger!.MediaStatus == OpticalDiscMedium.StatusInStock
                || item.Ledger.MediaStatus == OpticalDiscMedium.StatusDamaged)
            .Select(item => item.Ledger!.StorageLocation)
            .ToList();

        var electronicLocations = _dbContext.YearlyElectronicArchiveUnits
            .AsNoTracking()
            .Where(unit => unit.UnitLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .Select(unit => unit.StorageLocation)
            .ToList();

        int maxHardDisk = 0;
        int maxOpticalDisc = 0;
        foreach (var assignment in assignments)
        {
            if (!TryParseMagneticDiskSlotRowColumn(assignment.SlotCode, out int row, out int column))
            {
                continue;
            }

            string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, assignment.FaceCode, row, column);
            if (string.IsNullOrWhiteSpace(slotKey))
            {
                continue;
            }

            int occupancy = hardDiskLocations.Count(location => IsSameMagneticDiskSlot(location, slotKey))
                + opticalDiscLocations.Count(location => IsSameMagneticDiskSlot(location, slotKey))
                + electronicLocations.Count(location => IsSameMagneticDiskSlot(location, slotKey));

            if (CabinetHardDiskSlotCategoryAssignment.IsOpticalDiscDedicatedCategory(assignment.CategoryName))
            {
                maxOpticalDisc = Math.Max(maxOpticalDisc, occupancy);
            }
            else
            {
                maxHardDisk = Math.Max(maxHardDisk, occupancy);
            }
        }

        return (maxHardDisk, maxOpticalDisc);
    }

    public bool HasArchiveBoxesInStandardSlot(string cabinetName, string faceCode, string slotCode)
    {
        if (string.IsNullOrWhiteSpace(cabinetName)
            || string.IsNullOrWhiteSpace(faceCode)
            || string.IsNullOrWhiteSpace(slotCode)
            || !TryParseMagneticDiskSlotRowColumn(slotCode, out int row, out int column))
        {
            return false;
        }

        string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, faceCode, row, column);
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return false;
        }

        bool hasYearlyBox = _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .Select(box => box.BoxLocationCode)
            .AsEnumerable()
            .Any(location => ArchiveSlotLocationSupport.IsSameSlot(location, slotKey));
        if (hasYearlyBox)
        {
            return true;
        }

        return _dbContext.HistoryArchiveBoxes
            .AsNoTracking()
            .Where(box => box.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleInStock)
            .Select(box => box.BoxCode)
            .AsEnumerable()
            .Any(boxCode => ArchiveSlotLocationSupport.IsSameSlot(boxCode, slotKey));
    }

    /// <inheritdoc />
    public bool HasStoredContent(string cabinetName)
        => GetStoredContentSlots(cabinetName).Count > 0;

    /// <inheritdoc />
    public IReadOnlyList<CabinetStoredContentSlot> GetStoredContentSlots(string cabinetName)
    {
        if (string.IsNullOrWhiteSpace(cabinetName))
        {
            return Array.Empty<CabinetStoredContentSlot>();
        }

        string name = cabinetName.Trim();
        var slots = new Dictionary<string, StoredContentSlotBuilder>(StringComparer.OrdinalIgnoreCase);
        AddLocations(slots, name, "年度档案盒", _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .Select(box => box.BoxLocationCode)
            .ToList());
        AddLocations(slots, name, "历史档案盒", _dbContext.HistoryArchiveBoxes
            .AsNoTracking()
            .Select(box => new { box.BoxCode, box.LifecycleStatus })
            .AsEnumerable()
            .Where(box => !HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(box.LifecycleStatus))
            .Select(box => box.BoxCode));
        AddHistoryLedger(slots, name, "地形图", LoadTopoMapPlacements());
        AddHistoryLedger(slots, name, "航摄影像", LoadAerialPhotoPlacements());
        AddHistoryLedger(slots, name, "其他图件", LoadOtherMapPlacements());
        AddLocations(slots, name, "电子立档", _dbContext.YearlyElectronicArchiveUnits
            .AsNoTracking()
            .Where(unit => unit.UnitLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .Select(unit => unit.StorageLocation)
            .ToList());
        AddLocations(slots, name, "硬盘", QueryInStockHardDiskLocations());
        AddLocations(slots, name, "光盘", QueryInStockOpticalDiscLocations());

        return slots.Values
            .OrderBy(item => item.FaceCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.LayerIndex)
            .ThenBy(item => item.ColumnIndex)
            .Select(item => item.ToSlot())
            .ToList();
    }

    private List<string> QueryInStockHardDiskLocations()
    {
        return _dbContext.HardDiskMedia
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.Ledger != null)
            .Where(item => item.Ledger!.MediaStatus == HardDiskMedium.StatusInStockBlank
                || item.Ledger.MediaStatus == HardDiskMedium.StatusInStockData
                || item.Ledger.MediaStatus == HardDiskMedium.StatusInStockDamaged
                || item.Ledger.MediaStatus == HardDiskMedium.StatusInStockLost
                || item.Ledger.MediaStatus == HardDiskMedium.StatusInStockScrap)
            .Select(item => item.Ledger!.StorageLocation)
            .ToList();
    }

    private List<string> QueryInStockOpticalDiscLocations()
    {
        return _dbContext.OpticalDiscMedia
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.Ledger != null)
            .Where(item => item.Ledger!.MediaStatus == OpticalDiscMedium.StatusInStock
                || item.Ledger.MediaStatus == OpticalDiscMedium.StatusDamaged
                || item.Ledger.MediaStatus == OpticalDiscMedium.StatusLost
                || item.Ledger.MediaStatus == OpticalDiscMedium.StatusScrap)
            .Select(item => item.Ledger!.StorageLocation)
            .ToList();
    }

    private static void AddLocations(
        Dictionary<string, StoredContentSlotBuilder> slots,
        string cabinetName,
        string contentKind,
        IEnumerable<string?> locations)
    {
        foreach (string? location in locations)
        {
            AddLocation(slots, cabinetName, contentKind, location);
        }
    }

    private static void AddHistoryLedger(
        Dictionary<string, StoredContentSlotBuilder> slots,
        string cabinetName,
        string contentKind,
        IEnumerable<HistoryLedgerPlacement> rows)
    {
        foreach (HistoryLedgerPlacement row in rows)
        {
            if (HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(row.LifecycleStatus))
            {
                continue;
            }

            foreach (string code in HistoryArchiveBoxCodeSupport.SplitBoxCodes(row.BoxNumber))
            {
                AddLocation(slots, cabinetName, contentKind, code);
            }
        }
    }

    private static void AddLocation(
        Dictionary<string, StoredContentSlotBuilder> slots,
        string cabinetName,
        string contentKind,
        string? location)
    {
        if (!ArchiveSlotLocationSupport.TryParseSlotLocation(location, out string parsedCabinetName, out string faceCode, out int layerIndex, out int columnIndex)
            || !CabinetNameNormalizer.AreSame(parsedCabinetName, cabinetName))
        {
            return;
        }

        string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(parsedCabinetName, faceCode, layerIndex, columnIndex);
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return;
        }

        if (!slots.TryGetValue(slotKey, out StoredContentSlotBuilder? builder))
        {
            builder = new StoredContentSlotBuilder(slotKey, faceCode.Trim().ToUpperInvariant(), layerIndex, columnIndex);
            slots.Add(slotKey, builder);
        }

        builder.AddKind(contentKind);
    }

    private sealed class StoredContentSlotBuilder
    {
        private readonly List<string> _kinds = new();

        public StoredContentSlotBuilder(string slotKey, string faceCode, int layerIndex, int columnIndex)
        {
            SlotKey = slotKey;
            FaceCode = faceCode;
            LayerIndex = layerIndex;
            ColumnIndex = columnIndex;
        }

        public string SlotKey { get; }

        public string FaceCode { get; }

        public int LayerIndex { get; }

        public int ColumnIndex { get; }

        public void AddKind(string contentKind)
        {
            if (!_kinds.Contains(contentKind, StringComparer.Ordinal))
            {
                _kinds.Add(contentKind);
            }
        }

        public CabinetStoredContentSlot ToSlot()
            => new()
            {
                SlotKey = SlotKey,
                FaceCode = FaceCode,
                LayerIndex = LayerIndex,
                ColumnIndex = ColumnIndex,
                ContentKinds = _kinds
            };
    }

    private List<HistoryLedgerPlacement> LoadTopoMapPlacements()
        => _dbContext.TopoMaps.AsNoTracking()
            .Select(item => new { item.BoxNumber, item.LifecycleStatus })
            .AsEnumerable()
            .Select(item => new HistoryLedgerPlacement(item.BoxNumber, item.LifecycleStatus))
            .ToList();

    private List<HistoryLedgerPlacement> LoadAerialPhotoPlacements()
        => _dbContext.AerialPhotos.AsNoTracking()
            .Select(item => new { item.BoxNumber, item.LifecycleStatus })
            .AsEnumerable()
            .Select(item => new HistoryLedgerPlacement(item.BoxNumber, item.LifecycleStatus))
            .ToList();

    private List<HistoryLedgerPlacement> LoadOtherMapPlacements()
        => _dbContext.OtherMaps.AsNoTracking()
            .Select(item => new { item.BoxNumber, item.LifecycleStatus })
            .AsEnumerable()
            .Select(item => new HistoryLedgerPlacement(item.BoxNumber, item.LifecycleStatus))
            .ToList();

    private readonly record struct HistoryLedgerPlacement(string? BoxNumber, string? LifecycleStatus);

    private static bool IsSameMagneticDiskSlot(string? storageLocation, string slotKey)
        => !string.IsNullOrWhiteSpace(storageLocation)
            && ArchiveSlotLocationSupport.IsSameSlot(storageLocation, slotKey);

    private static bool TryParseMagneticDiskSlotRowColumn(string slotCode, out int row, out int column)
    {
        row = 0;
        column = 0;
        if (string.IsNullOrWhiteSpace(slotCode))
        {
            return false;
        }

        string[] parts = slotCode.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && int.TryParse(parts[0], out row)
            && int.TryParse(parts[1], out column)
            && row > 0
            && column > 0;
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }

    public Task<int> SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }
}
