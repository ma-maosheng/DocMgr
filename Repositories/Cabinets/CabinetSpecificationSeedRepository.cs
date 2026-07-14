using DocMgr.Data;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Repositories.Cabinets;

public class CabinetSpecificationSeedRepository : ICabinetSpecificationSeedRepository
{
    private readonly AppDbContext _dbContext;

    public CabinetSpecificationSeedRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public CabinetSlotSpecification? GetSlotSpecificationByCabinetType(string cabinetTypeCode)
    {
        return _dbContext.CabinetSlotSpecifications
            .FirstOrDefault(item => item.CabinetTypeCode == cabinetTypeCode);
    }

    public void AddSlotSpecification(CabinetSlotSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        _dbContext.CabinetSlotSpecifications.Add(specification);
    }

    public CabinetSlotSpecialRule? GetSpecialRuleByRuleKey(string ruleKey)
    {
        return _dbContext.CabinetSlotSpecialRules
            .FirstOrDefault(item => item.RuleKey == ruleKey);
    }

    public void AddSpecialRule(CabinetSlotSpecialRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _dbContext.CabinetSlotSpecialRules.Add(rule);
    }

    public ArchiveBoxSpecification? GetArchiveBoxSpecificationByName(string name)
    {
        return _dbContext.ArchiveBoxSpecifications
            .FirstOrDefault(item => item.Name == name);
    }

    public void AddArchiveBoxSpecification(ArchiveBoxSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        _dbContext.ArchiveBoxSpecifications.Add(specification);
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }
}
