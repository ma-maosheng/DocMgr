using DocMgr.Data;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
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
