using DocMgr.Data;
using DocMgr.Models.Cabinets;
using DocMgr.Repositories.Interfaces;
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
        return _dbContext.CabinetHardDiskSlotCategoryAssignments.FirstOrDefault(item =>
            item.CabinetId == cabinetId &&
            item.FaceCode == faceCode &&
            item.SlotCode == slotCode);
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

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }

    public Task<int> SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }
}
