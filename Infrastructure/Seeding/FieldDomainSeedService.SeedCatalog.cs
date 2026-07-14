using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Infrastructure.Seeding;

public static partial class FieldDomainSeedService
{
    private static List<FieldDomainSeed> BuildSeeds()
    {
        return new List<FieldDomainSeed>
            {
                new(
                    "YearlyArchiveRegisterRecord",
                    "SourceType",
                    "资料来源",
                    "登记申请中的资料来源选项。",
                    true,
                    10,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, "内部", "内部", true, 10),
                        new(string.Empty, "外来", "外来", true, 20),
                    }),
                new(
                    "YearlyArchiveRegisterMedia",
                    "MediaKind",
                    "介质类别",
                    "资料登记页模拟介质模板使用的介质类别选项。",
                    true,
                    15,
                    new List<FieldDomainOptionSeed>
                    {
                        new("Template=Simulated", "模拟", "模拟", true, 10),
                    }),
                new(
                    "YearlyArchiveRegisterMediaItem",
                    "ItemType",
                    "明细类型",
                    "资料登记页子项模板使用的明细类型选项。",
                    true,
                    18,
                    new List<FieldDomainOptionSeed>
                    {
                        new("Template=Data", "资料", "资料", true, 10),
                        new("Template=Proof", "证明", "证明", true, 20),
                    }),
                new(
                    nameof(YearlyArchiveRegisterElectronicMediaItemDetail),
                    nameof(YearlyArchiveRegisterElectronicMediaItemDetail.MaterialCategory),
                    "资料类型",
                    "电子资料子项的资料类型选项。",
                    true,
                    19,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument, ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument, true, 10),
                        new(string.Empty, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData, true, 20),
                    }),
                new(
                    nameof(YearlyArchiveRegisterElectronicMediaItemDetail),
                    nameof(YearlyArchiveRegisterElectronicMediaItemDetail.SubCategory),
                    "所属子类",
                    "电子资料子项所属子类，按资料类型区分作用域。",
                    true,
                    20,
                    new List<FieldDomainOptionSeed>
                    {
                        new(ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocumentScope, "外来资料类", "外来资料类", true, 10),
                        new(ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocumentScope, "策划设计类", "策划设计类", true, 20),
                        new(ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocumentScope, "检查记录类", "检查记录类", true, 30),
                        new(ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocumentScope, "总结报告类", "总结报告类", true, 40),
                        new(ArchiveRegisterDomainValues.ElectronicMaterialCategoryDataScope, "外来收集数据", "外来收集数据", true, 110),
                        new(ArchiveRegisterDomainValues.ElectronicMaterialCategoryDataScope, "原始观测数据", "原始观测数据", true, 120),
                        new(ArchiveRegisterDomainValues.ElectronicMaterialCategoryDataScope, "过程处理数据", "过程处理数据", true, 130),
                        new(ArchiveRegisterDomainValues.ElectronicMaterialCategoryDataScope, "过程检查数据", "过程检查数据", true, 140),
                        new(ArchiveRegisterDomainValues.ElectronicMaterialCategoryDataScope, "最终成果数据", "最终成果数据", true, 150),
                    }),
                new(
                    nameof(YearlyArchiveRegisterElectronicMediaItemDetail),
                    nameof(YearlyArchiveRegisterElectronicMediaItemDetail.DataOrganizationForm),
                    "数据组织形式",
                    "电子资料的数据组织形式选项。",
                    true,
                    21,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory, true, 10),
                        new(string.Empty, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormFile, ArchiveRegisterDomainValues.ElectronicDataOrganizationFormFile, true, 20),
                    }),
                new(
                    "YearlyArchiveRegisterMedia",
                    "MediaType",
                    "介质类型",
                    "同一字段按介质类别区分作用域配置。",
                    true,
                    20,
                    new List<FieldDomainOptionSeed>
                    {
                        new("MediaKind=电子", "U盘", "U盘", true, 10),
                        new("MediaKind=电子", "光盘", "光盘", true, 20),
                        new("MediaKind=电子", "硬盘", "硬盘", true, 30),
                        new("MediaKind=电子", "内网", "内网", true, 40),
                        new("MediaKind=模拟;ItemType=资料", "装订文本", "装订文本", true, 50),
                        new("MediaKind=模拟;ItemType=资料", "散页文本", "散页文本", true, 60),
                        new("MediaKind=模拟;ItemType=资料", "散页图件", "散页图件", true, 70),
                        new("MediaKind=模拟;ItemType=资料", "大幅图件", "大幅图件", true, 80),
                        new("MediaKind=模拟;ItemType=资料", "其他", "其他", true, 90),
                        new("MediaKind=模拟;ItemType=证明", "装订文本", "装订文本", true, 100),
                        new("MediaKind=模拟;ItemType=证明", "散页文本", "散页文本", true, 110),
                        new("MediaKind=模拟;ItemType=证明", "其他", "其他", true, 120),

                    }),
                new(
                    "YearlyArchiveRegisterMedia",
                    "Disposition",
                    "处置方式",
                    "按介质类别区分处置方式。",
                    true,
                    30,
                    new List<FieldDomainOptionSeed>
                    {
                        new("MediaKind=电子", "介质带回", "介质带回", true, 10),
                        new("MediaKind=电子", "介质留存", "介质留存", true, 20),
                        new("MediaKind=电子", "无需处置", "无需处置", true, 30),
                        new("MediaKind=模拟", "介质留存", "介质留存", true, 40),
                    }),
                new(
                    "YearlyArchiveRegisterRecord",
                    "ArchivePurpose",
                    "库管模式",
                    "登记申请中的库管模式选项。",
                    true,
                    40,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, "院管资料、短期存档", "院管资料、短期存档", true, 10),
                        new(string.Empty, "院管资料、长期存档", "院管资料、长期存档", true, 20),
                        new(string.Empty, "外部委托、代管代发", "外部委托、代管代发", true, 30),
                    }),
                new(
                    "YearlyArchiveRegisterMediaItem",
                    "ConfidentialLevel",
                    "密级",
                    "申请人填写的资料子项密级选项。",
                    true,
                    55,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, "否", "否", true, 10),
                        new(string.Empty, "秘密", "秘密", true, 20),
                        new(string.Empty, "机密", "机密", true, 30),
                        new(string.Empty, "绝密", "绝密", true, 40),
                    }),
                new(
                    "YearlyArchiveRegisterRecord",
                    "ProdDeptOpinion",
                    "生产管理科意见",
                    "生产管理科审批意见选项。",
                    true,
                    60,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, "同意", "同意", true, 10),
                        new(string.Empty, "不同意", "不同意", true, 20),
                    }),
                new(
                    "YearlyArchiveRegisterRecord",
                    "RndDeptOpinion",
                    "科研开发室意见",
                    "科研开发室审批意见选项。",
                    true,
                    70,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, "同意", "同意", true, 10),
                        new(string.Empty, "不同意", "不同意", true, 20),
                    }),
                new(
                    "YearlyArchiveRegisterRecord",
                    "DeputyOpinion",
                    "分管领导意见",
                    "分管领导审批意见选项。",
                    true,
                    80,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, "同意", "同意", true, 10),
                        new(string.Empty, "不同意", "不同意", true, 20),
                    }),
                new(
                    "TopoMap",
                    "BoxSpecification",
                    "档案盒规格",
                    "历史存档地形图的档案盒规格选项。",
                    true,
                    90,
                    BuildArchiveBoxSpecificationOptions()),
                new(
                    "AerialPhoto",
                    "BoxSpecification",
                    "档案盒规格",
                    "历史存档航摄影像的档案盒规格选项。",
                    true,
                    100,
                    BuildArchiveBoxSpecificationOptions()),
                new(
                    "OtherMap",
                    "BoxSpecification",
                    "档案盒规格",
                    "历史存档其他图件的档案盒规格选项。",
                    true,
                    110,
                    BuildArchiveBoxSpecificationOptions()),
                new(
                    "HardDiskMedium",
                    "DiskType",
                    "硬盘类型",
                    "硬盘介质登记中的硬盘类型选项。",
                    true,
                    120,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, "机械硬盘", "机械硬盘", true, 10),
                        new(string.Empty, "固态硬盘", "固态硬盘", true, 20),
                        new(string.Empty, "移动硬盘", "移动硬盘", true, 30),
                        new(string.Empty, "其他", "其他", true, 40)
                    }),
                new(
                    "HardDiskMedium",
                    "InterfaceType",
                    "接口类型",
                    "硬盘介质登记中的接口类型选项。",
                    true,
                    130,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, "SATA", "SATA", true, 10),
                        new(string.Empty, "SAS", "SAS", true, 20),
                        new(string.Empty, "USB", "USB", true, 30),
                        new(string.Empty, "Type-C", "Type-C", true, 40),
                        new(string.Empty, "其他", "其他", true, 50)
                    }),
                new(
                    "HardDiskMedium",
                    "MediaNature",
                    "介质属性",
                    "硬盘介质当前属性选项。",
                    true,
                    140,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, HardDiskMedium.NatureBlank, HardDiskMedium.NatureBlank, true, 10),
                        new(string.Empty, HardDiskMedium.NatureDataCarrier, HardDiskMedium.NatureDataCarrier, true, 20)
                    }),
                new(
                    "HardDiskMedium",
                    "CurrentStatus",
                    "当前状态",
                    "硬盘介质主表当前状态选项。",
                    true,
                    150,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, HardDiskMedium.StatusInStockBlank, HardDiskMedium.StatusInStockBlank, true, 10),
                        new(string.Empty, HardDiskMedium.StatusInStockData, HardDiskMedium.StatusInStockData, true, 20),
                        new(string.Empty, HardDiskMedium.StatusInStockDamaged, HardDiskMedium.StatusInStockDamaged, true, 30),
                        new(string.Empty, HardDiskMedium.StatusOutTemporary, HardDiskMedium.StatusOutTemporary, true, 40),
                        new(string.Empty, HardDiskMedium.StatusOutLongTerm, HardDiskMedium.StatusOutLongTerm, true, 50),
                        new(string.Empty, HardDiskMedium.StatusOutPermanent, HardDiskMedium.StatusOutPermanent, true, 60),
                        new(string.Empty, HardDiskMedium.StatusOutDestroyed, HardDiskMedium.StatusOutDestroyed, true, 70),
                        new(string.Empty, HardDiskMedium.StatusOutLost, HardDiskMedium.StatusOutLost, true, 80)
                    }),
                new(
                    nameof(HardDiskLedger),
                    nameof(HardDiskLedger.MediaStatus),
                    "介质状态",
                    "硬盘台账当前介质状态选项。",
                    true,
                    155,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, HardDiskMedium.StatusInStockBlank, HardDiskMedium.StatusInStockBlank, true, 10),
                        new(string.Empty, HardDiskMedium.StatusInStockData, HardDiskMedium.StatusInStockData, true, 20),
                        new(string.Empty, HardDiskMedium.StatusInStockDamaged, HardDiskMedium.StatusInStockDamaged, true, 30),
                        new(string.Empty, HardDiskMedium.StatusOutTemporary, HardDiskMedium.StatusOutTemporary, true, 40),
                        new(string.Empty, HardDiskMedium.StatusOutLongTerm, HardDiskMedium.StatusOutLongTerm, true, 50),
                        new(string.Empty, HardDiskMedium.StatusOutPermanent, HardDiskMedium.StatusOutPermanent, true, 60),
                        new(string.Empty, HardDiskMedium.StatusOutDestroyed, HardDiskMedium.StatusOutDestroyed, true, 70),
                        new(string.Empty, HardDiskMedium.StatusOutLost, HardDiskMedium.StatusOutLost, true, 80)
                    }),
                new(
                    nameof(HardDiskLedger),
                    nameof(HardDiskLedger.MediaNature),
                    "介质属性",
                    "硬盘台账当前介质属性选项。",
                    true,
                    156,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, HardDiskMedium.NatureBlank, HardDiskMedium.NatureBlank, true, 10),
                        new(string.Empty, HardDiskMedium.NatureDataCarrier, HardDiskMedium.NatureDataCarrier, true, 20)
                    }),
                new(
                    "HardDiskMediaApplication",
                    "ApplicationType",
                    "申请类型",
                    "硬盘介质业务申请类型选项。",
                    true,
                    160,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, HardDiskMediaApplication.TypeOutboundTemporary, HardDiskMediaApplication.TypeOutboundTemporary, true, 10),
                        new(string.Empty, HardDiskMediaApplication.TypeOutboundLongTerm, HardDiskMediaApplication.TypeOutboundLongTerm, true, 20),
                        new(string.Empty, HardDiskMediaApplication.TypeOutboundPermanent, HardDiskMediaApplication.TypeOutboundPermanent, true, 30),
                        new(string.Empty, HardDiskMediaApplication.TypeOutboundDestroy, HardDiskMediaApplication.TypeOutboundDestroy, false, 40),
                        new(string.Empty, HardDiskMediaApplication.TypeReturnBlankRegistration, HardDiskMediaApplication.TypeReturnBlankRegistration, true, 50),
                        new(string.Empty, HardDiskMediaApplication.TypeReturnDataRegistration, HardDiskMediaApplication.TypeReturnDataRegistration, true, 60),
                        new(string.Empty, HardDiskMediaApplication.TypeReturnDamagedRegistration, HardDiskMediaApplication.TypeReturnDamagedRegistration, true, 70),
                        new(string.Empty, HardDiskMediaApplication.TypeLossRegistration, HardDiskMediaApplication.TypeLossRegistration, true, 80),
                        new(string.Empty, HardDiskMediaApplication.TypeRelocate, HardDiskMediaApplication.TypeRelocate, true, 90)
                    }),
                new(
                    "HardDiskMediaApplication",
                    "ApplicationStatus",
                    "申请状态",
                    "硬盘介质混合审批流程状态选项。",
                    true,
                    170,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, HardDiskMediaApplication.StatusDraft, HardDiskMediaApplication.StatusDraft, true, 10),
                        new(string.Empty, HardDiskMediaApplication.StatusSubmitted, HardDiskMediaApplication.StatusSubmitted, true, 20),
                        new(string.Empty, HardDiskMediaApplication.StatusApproved, HardDiskMediaApplication.StatusApproved, true, 30),
                        new(string.Empty, HardDiskMediaApplication.StatusSignedUploaded, HardDiskMediaApplication.StatusSignedUploaded, true, 40),
                        new(string.Empty, HardDiskMediaApplication.StatusCompleted, HardDiskMediaApplication.StatusCompleted, true, 50),
                        new(string.Empty, HardDiskMediaApplication.StatusWithdrawn, HardDiskMediaApplication.StatusWithdrawn, true, 60),
                        new(string.Empty, HardDiskMediaApplication.StatusForceWithdrawn, HardDiskMediaApplication.StatusForceWithdrawn, true, 70)
                    }),
                new(
                    "HardDiskMediaTransaction",
                    "TransactionType",
                    "流转类型",
                    "硬盘介质生命周期流转类型选项。",
                    true,
                    180,
                    new List<FieldDomainOptionSeed>
                    {
                        new(string.Empty, HardDiskMediaTransaction.TypeRegister, HardDiskMediaTransaction.TypeRegister, true, 10),
                        new(string.Empty, HardDiskMediaTransaction.TypeOutboundTemporary, HardDiskMediaTransaction.TypeOutboundTemporary, true, 20),
                        new(string.Empty, HardDiskMediaTransaction.TypeOutboundLongTerm, HardDiskMediaTransaction.TypeOutboundLongTerm, true, 30),
                        new(string.Empty, HardDiskMediaTransaction.TypeOutboundPermanent, HardDiskMediaTransaction.TypeOutboundPermanent, true, 40),
                        new(string.Empty, HardDiskMediaTransaction.TypeOutboundDestroy, HardDiskMediaTransaction.TypeOutboundDestroy, true, 50),
                        new(string.Empty, HardDiskMediaTransaction.TypeReturnRegistration, HardDiskMediaTransaction.TypeReturnRegistration, true, 60),
                        new(string.Empty, HardDiskMediaTransaction.TypeLossRegistration, HardDiskMediaTransaction.TypeLossRegistration, true, 70),
                        new(string.Empty, HardDiskMediaTransaction.TypeRelocate, HardDiskMediaTransaction.TypeRelocate, true, 80)
                    })
            };
    }

    private static List<FieldDomainOptionSeed> BuildArchiveBoxSpecificationOptions()
    {
        return new List<FieldDomainOptionSeed>
            {
                new(string.Empty, "标准(10cm)", "标准(10cm)", true, 10),
                new(string.Empty, "标准(5cm)", "标准(5cm)", true, 20),
                new(string.Empty, "标准(3cm)", "标准(3cm)", true, 30),
                new(string.Empty, "标准(2cm)", "标准(2cm)", true, 40),
                new(string.Empty, "非标(10cm)", "非标(10cm)", true, 50)
            };
    }
}
