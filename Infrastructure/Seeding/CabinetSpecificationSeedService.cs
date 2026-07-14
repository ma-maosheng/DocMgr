using DocMgr.Models.Cabinets;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Infrastructure.Seeding;

public static class CabinetSpecificationSeedService
{
    /// <summary>
    /// 写入档口规格、档案盒规格和档口特例规则初始化数据。
    /// </summary>
    public static void SeedDefaults(ICabinetSpecificationSeedRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        bool changed = false;

        foreach (var seed in SlotSeeds)
        {
            var entity = repository.GetSlotSpecificationByCabinetType(seed.CabinetTypeCode);
            if (entity == null)
            {
                repository.AddSlotSpecification(new CabinetSlotSpecification
                {
                    CabinetTypeCode = seed.CabinetTypeCode,
                    DisplayName = seed.DisplayName,
                    WidthCm = seed.WidthCm,
                    HeightCm = seed.HeightCm,
                    DepthCm = seed.DepthCm,
                    SortOrder = seed.SortOrder
                });
                changed = true;
                continue;
            }

            if (entity.DisplayName != seed.DisplayName)
            {
                entity.DisplayName = seed.DisplayName;
                changed = true;
            }

            if (entity.WidthCm != seed.WidthCm)
            {
                entity.WidthCm = seed.WidthCm;
                changed = true;
            }

            if (entity.HeightCm != seed.HeightCm)
            {
                entity.HeightCm = seed.HeightCm;
                changed = true;
            }

            if (entity.DepthCm != seed.DepthCm)
            {
                entity.DepthCm = seed.DepthCm;
                changed = true;
            }

            if (entity.SortOrder != seed.SortOrder)
            {
                entity.SortOrder = seed.SortOrder;
                changed = true;
            }
        }

        foreach (var seed in SpecialRuleSeeds)
        {
            var entity = repository.GetSpecialRuleByRuleKey(seed.RuleKey);
            if (entity == null)
            {
                repository.AddSpecialRule(new CabinetSlotSpecialRule
                {
                    RuleKey = seed.RuleKey,
                    CabinetName = seed.CabinetName,
                    OpenFaceCode = seed.OpenFaceCode,
                    SlotCode = seed.SlotCode,
                    RequiredBoxSpecification = seed.RequiredBoxSpecification,
                    RequiredArchiveFaceCode = seed.RequiredArchiveFaceCode,
                    LayoutModeOverride = seed.LayoutModeOverride,
                    SpecialRuleText = seed.SpecialRuleText,
                    IsEnabled = seed.IsEnabled,
                    SortOrder = seed.SortOrder
                });
                changed = true;
                continue;
            }

            if (entity.CabinetName != seed.CabinetName)
            {
                entity.CabinetName = seed.CabinetName;
                changed = true;
            }

            if (entity.OpenFaceCode != seed.OpenFaceCode)
            {
                entity.OpenFaceCode = seed.OpenFaceCode;
                changed = true;
            }

            if (entity.SlotCode != seed.SlotCode)
            {
                entity.SlotCode = seed.SlotCode;
                changed = true;
            }

            if (entity.RequiredBoxSpecification != seed.RequiredBoxSpecification)
            {
                entity.RequiredBoxSpecification = seed.RequiredBoxSpecification;
                changed = true;
            }

            if (entity.RequiredArchiveFaceCode != seed.RequiredArchiveFaceCode)
            {
                entity.RequiredArchiveFaceCode = seed.RequiredArchiveFaceCode;
                changed = true;
            }

            if (entity.LayoutModeOverride != seed.LayoutModeOverride)
            {
                entity.LayoutModeOverride = seed.LayoutModeOverride;
                changed = true;
            }

            if (entity.SpecialRuleText != seed.SpecialRuleText)
            {
                entity.SpecialRuleText = seed.SpecialRuleText;
                changed = true;
            }

            if (entity.IsEnabled != seed.IsEnabled)
            {
                entity.IsEnabled = seed.IsEnabled;
                changed = true;
            }

            if (entity.SortOrder != seed.SortOrder)
            {
                entity.SortOrder = seed.SortOrder;
                changed = true;
            }
        }

        foreach (var seed in BoxSeeds)
        {
            var entity = repository.GetArchiveBoxSpecificationByName(seed.Name);
            if (entity == null)
            {
                repository.AddArchiveBoxSpecification(new ArchiveBoxSpecification
                {
                    Name = seed.Name,
                    WidthCm = seed.WidthCm,
                    HeightCm = seed.HeightCm,
                    ThicknessCm = seed.ThicknessCm,
                    SortOrder = seed.SortOrder
                });
                changed = true;
                continue;
            }

            if (entity.WidthCm != seed.WidthCm)
            {
                entity.WidthCm = seed.WidthCm;
                changed = true;
            }

            if (entity.HeightCm != seed.HeightCm)
            {
                entity.HeightCm = seed.HeightCm;
                changed = true;
            }

            if (entity.ThicknessCm != seed.ThicknessCm)
            {
                entity.ThicknessCm = seed.ThicknessCm;
                changed = true;
            }

            if (entity.SortOrder != seed.SortOrder)
            {
                entity.SortOrder = seed.SortOrder;
                changed = true;
            }
        }

        if (changed)
        {
            repository.SaveChanges();
        }
    }

    private static readonly SlotSpecificationSeed[] SlotSeeds =
    [
        new("Standard", "标准滑道式档案柜档口", 78m, 33m, 25m, 10),
            new("Vertical", "立式文件柜档口", 83m, 40m, 38m, 20),
            new("Horizontal", "卧式文件柜档口", 83m, 33m, 38m, 30),
            new("MagneticDisk", "防磁磁盘柜抽屉格口", 23.33m, 16.67m, 52m, 40)
    ];

    private static readonly ArchiveBoxSpecificationSeed[] BoxSeeds =
    [
        new("标准(10cm)", 23m, 30m, 10m, 10),
            new("标准(5cm)", 23m, 30m, 5m, 20),
            new("标准(3cm)", 23m, 30m, 3m, 30),
            new("标准(2cm)", 23m, 30m, 2m, 40),
            new("非标(10cm)", 30m, 30m, 10m, 50)
    ];

    private static readonly CabinetSlotSpecialRuleSeed[] SpecialRuleSeeds =
    [
        new("JIA-A-2-1-SPINE", "甲", "A", "2-1", "标准(10cm)", "A", string.Empty, "甲柜A面该格口按盒脊向外存放23cm航摄像片（标准10cm盒）。", true, 10),
            new("JIA-A-3-1-SPINE", "甲", "A", "3-1", "标准(10cm)", "A", string.Empty, "甲柜A面该格口按盒脊向外存放23cm航摄像片（标准10cm盒）。", true, 20),
            new("JIA-A-4-1-SPINE", "甲", "A", "4-1", "标准(10cm)", "A", string.Empty, "甲柜A面该格口按盒脊向外存放23cm航摄像片（标准10cm盒）。", true, 30),
            new("JIA-A-5-1-SPINE", "甲", "A", "5-1", "标准(10cm)", "A", string.Empty, "甲柜A面该格口按盒脊向外存放23cm航摄像片（标准10cm盒）。", true, 40),
            new("JIA-A-6-1-SPINE", "甲", "A", "6-1", "标准(10cm)", "A", string.Empty, "甲柜A面该格口按盒脊向外存放23cm航摄像片（标准10cm盒）。", true, 50),
            new("JIA-2-2-B-BORROW", "甲", string.Empty, "2-2", "非标(10cm)", "B", "中列跨面联动（甲柜B面借用A面空间，盒脊向外）", "甲柜存放非标档案盒时按尽可能充分利用档口空间的特例规则摆放；当前格口为B面第二列借用A面空间。", true, 60),
            new("YI-2-2-B-BORROW", "乙", string.Empty, "2-2", "非标(10cm)", "B", "中列跨面联动（乙柜B面借用A面空间，盒脊向外）", "乙柜存放非标档案盒时按尽可能充分利用档口空间的特例规则摆放；当前格口为B面第二列借用A面空间。", true, 70)
    ];

    private sealed record SlotSpecificationSeed(string CabinetTypeCode, string DisplayName, decimal WidthCm, decimal HeightCm, decimal DepthCm, int SortOrder);

    private sealed record ArchiveBoxSpecificationSeed(string Name, decimal WidthCm, decimal HeightCm, decimal ThicknessCm, int SortOrder);

    private sealed record CabinetSlotSpecialRuleSeed(
        string RuleKey,
        string CabinetName,
        string OpenFaceCode,
        string SlotCode,
        string RequiredBoxSpecification,
        string RequiredArchiveFaceCode,
        string LayoutModeOverride,
        string SpecialRuleText,
        bool IsEnabled,
        int SortOrder);
}
