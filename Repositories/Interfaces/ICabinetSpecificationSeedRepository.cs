namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 档案盒规格种子数据访问契约：规格初始化数据读写。
/// </summary>
public interface ICabinetSpecificationSeedRepository
{
    CabinetSlotSpecification? GetSlotSpecificationByCabinetType(string cabinetTypeCode);

    void AddSlotSpecification(CabinetSlotSpecification specification);

    CabinetSlotSpecialRule? GetSpecialRuleByRuleKey(string ruleKey);

    void AddSpecialRule(CabinetSlotSpecialRule rule);

    ArchiveBoxSpecification? GetArchiveBoxSpecificationByName(string name);

    void AddArchiveBoxSpecification(ArchiveBoxSpecification specification);

    int SaveChanges();
}
