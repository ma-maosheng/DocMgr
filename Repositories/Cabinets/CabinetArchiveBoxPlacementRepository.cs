using DocMgr.Data;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.YearlyArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.Cabinets;

public class CabinetArchiveBoxPlacementRepository : ICabinetArchiveBoxPlacementRepository
{
    private readonly AppDbContext _dbContext;

    public CabinetArchiveBoxPlacementRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public YearlyArchiveBox? GetYearlyArchiveBoxByLocationCode(string boxLocationCode)
    {
        if (string.IsNullOrWhiteSpace(boxLocationCode))
        {
            return null;
        }

        string normalized = boxLocationCode.Trim();
        return _dbContext.YearlyArchiveBoxes
            .FirstOrDefault(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                && box.BoxLocationCode == normalized);
    }

    public List<YearlyArchiveBox> GetYearlyArchiveBoxesByLocationCodes(IReadOnlyCollection<string> boxLocationCodes)
    {
        if (boxLocationCodes == null || boxLocationCodes.Count == 0)
        {
            return [];
        }

        var normalizedCodes = boxLocationCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedCodes.Count == 0)
        {
            return [];
        }

        return _dbContext.YearlyArchiveBoxes
            .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                && normalizedCodes.Contains(box.BoxLocationCode))
            .ToList();
    }

    public List<YearlyArchiveBox> GetInUseYearlyArchiveBoxesBySlot(string cabinetName, string faceCode, string slotCode)
    {
        // slotCode 即「行-列」，与 ArchiveSlotLocationSupport.BuildSlotKey 的后半段同构，直接拼接档口键。
        string slotKey = $"{CabinetNameNormalizer.Normalize(cabinetName)}{faceCode.Trim().ToUpperInvariant()}-{slotCode.Trim()}";
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return [];
        }

        string slotPrefix = slotKey + "-";
        return _dbContext.YearlyArchiveBoxes
            .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .Where(box => box.BoxLocationCode == slotKey || box.BoxLocationCode.StartsWith(slotPrefix))
            .ToList();
    }

    public HistoryArchiveBox? GetHistoryArchiveBoxByCode(string boxCode)
    {
        if (string.IsNullOrWhiteSpace(boxCode))
        {
            return null;
        }

        string normalized = boxCode.Trim();
        return _dbContext.HistoryArchiveBoxes
            .FirstOrDefault(box => box.BoxCode == normalized);
    }

    public List<HistoryArchiveBox> GetInStockHistoryArchiveBoxesBySlot(string cabinetName, string faceCode, string slotCode)
    {
        // slotCode 即「行-列」，直接拼接档口键。
        string slotKey = $"{CabinetNameNormalizer.Normalize(cabinetName)}{faceCode.Trim().ToUpperInvariant()}-{slotCode.Trim()}";
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return [];
        }

        string slotPrefix = slotKey + "-";
        return _dbContext.HistoryArchiveBoxes
            .Where(box => box.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleInStock)
            .Where(box => box.BoxCode == slotKey || box.BoxCode.StartsWith(slotPrefix))
            .ToList();
    }

    public Dictionary<string, string> GetHistoryPlacementModeLookup(string cabinetName)
    {
        return _dbContext.HistoryArchiveBoxes
            .AsNoTracking()
            .Where(box => box.LifecycleStatus == HistoryArchiveDisposalDomainValues.LifecycleInStock)
            .Where(box => box.BoxCode.StartsWith(cabinetName))
            .ToDictionary(box => box.BoxCode, box => box.PlacementMode, StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, string> GetYearlyPlacementModeLookup(string cabinetName)
    {
        return _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse)
            .Where(box => box.BoxLocationCode.StartsWith(cabinetName))
            .ToDictionary(box => box.BoxLocationCode, box => box.PlacementMode, StringComparer.OrdinalIgnoreCase);
    }

    public List<ArchiveBoxSpecification> GetArchiveBoxSpecifications()
    {
        return _dbContext.ArchiveBoxSpecifications
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToList();
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }
}
