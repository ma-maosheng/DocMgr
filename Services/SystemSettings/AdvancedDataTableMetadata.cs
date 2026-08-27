using DocMgr.Models.ArchiveContainers;
using System.Collections.Generic;

namespace DocMgr.Services.SystemSettings
{
    internal sealed record TableMetadataEntry(
        string Description,
        string? Relationships = null,
        string? MaintenanceNotes = null);

    internal static class AdvancedDataTableMetadata
    {
        public static bool TryGet(string entityShortName, out TableMetadataEntry entry)
            => Entries.TryGetValue(entityShortName, out entry!);

        public static readonly Dictionary<string, TableMetadataEntry> Entries = new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(User)] = new(
                "系统登录账号，包含用户名、密码哈希、所属部门及角色等基础身份信息。",
                "→ 引用 Departments（DepartmentId）；→ 引用 Roles（RoleId）；← 被 UserSessions、UserPreferences 引用。",
                "只读浏览。禁止在此删除当前登录账号；用户维护请使用「用户管理」页面。"),

            [nameof(Role)] = new(
                "系统角色定义，用于控制菜单与功能权限。",
                "← 被 Users 引用（RoleId）。",
                "只读浏览。角色变更会影响全部关联用户的权限，请通过「角色设置」维护。"),

            [nameof(Department)] = new(
                "组织架构中的部门节点，供用户归属及业务单据部门字段引用。",
                "← 被 Users 及各类业务单据引用。",
                "可维护。删除前请确认无用户或业务记录仍引用该部门。"),

            [nameof(ServerPathSetting)] = new(
                "服务器路径预设，按部门配置可访问的路径名称、权限与容量上限。",
                "DepartmentName 引用 Departments.Name 或固定值「公用」。",
                "可维护。请通过「服务器路径设置」页面维护。"),

            [nameof(UserSession)] = new(
                "记录用户登录会话令牌及过期时间，用于会话校验。",
                "→ 引用 Users（UserId）。",
                "只读浏览。会话由登录/登出流程自动维护，通常无需手工干预。"),

            [nameof(Cabinet)] = new(
                "资料室物理柜体主数据，包含柜名、规格及布局相关属性。",
                "← 被 CabinetHardDiskSlotCategoryAssignments、CabinetArchiveSlotCategoryAssignments、CabinetArchiveBoxPlacements、YearlyArchiveBoxes 等引用。",
                "可维护。柜名在保存时会自动规范化；删除前需先清理柜位分配与档案盒关联。"),

            [nameof(CabinetHardDiskSlotCategoryAssignment)] = new(
                "定义某资料柜指定面/槽位可存放的硬盘介质分类。",
                "→ 引用 Cabinets（CabinetId，级联删除）。",
                "只读浏览。通常由柜体布局初始化或业务配置写入。"),

            [nameof(CabinetArchiveSlotCategoryAssignment)] = new(
                "定义标准滑道式档案柜指定面/档口的模拟介质资料存放用途（未设置、年度资料专用、历史资料专用、混用档口）。",
                "→ 引用 Cabinets（CabinetId，级联删除）。",
                "只读浏览。通常由开柜界面设置或启动补全写入。"),

            [nameof(CabinetArchiveBoxPlacement)] = new(
                "记录档案盒在资料柜中的物理位置（柜名、面、槽位）及来源业务键。",
                "逻辑关联 YearlyArchiveBoxes（通过 BoxCode 等字段）；← 被柜体开柜视图读取。",
                "只读浏览。位置数据由年度登记/柜体管理流程同步，手工改动可能导致定位不一致。"),

            [nameof(CabinetSlotSpecification)] = new(
                "资料柜各面槽位的容量与类型规格定义。",
                "逻辑关联 Cabinets（按柜名或规格编码）。",
                "只读浏览。规格数据影响柜体布局计算，请通过柜体管理功能维护。"),

            [nameof(ArchiveBoxSpecification)] = new(
                "档案盒尺寸/容量等规格模板，供年度档案盒生成与柜位匹配使用。",
                "← 被 YearlyArchiveBoxes 等业务引用。",
                "只读浏览。"),

            [nameof(CabinetSlotSpecialRule)] = new(
                "针对特定柜名/面/槽位的例外规则（如禁用、预留等）。",
                "逻辑关联 Cabinets（CabinetName）。",
                "只读浏览。柜名保存时会自动规范化。"),

            [nameof(AerialPhoto)] = new(
                "历史航片资料台账，记录图号、比例尺、摄区及存放信息。",
                "独立主数据表，可被检索与导出引用。",
                "可维护。删除前确认无外部引用或附件依赖。"),

            [nameof(TopoMap)] = new(
                "历史地形图资料台账。",
                "独立主数据表。",
                "可维护。"),

            [nameof(OtherMap)] = new(
                "除航片、地形图外的其他历史存档资料（交接记录等）。",
                "独立主数据表。",
                "只读浏览。"),

            [nameof(HistoryArchiveDisposalRecord)] = new(
                "历史存档资料离库处置主单，按资料类别办理草稿至办结全过程。",
                "← 被 HistoryArchiveDisposalItems 引用（一对多）。",
                "只读浏览。办结后会将对应台账标为已离库并撤除柜位，请勿手工改生命周期。"),

            [nameof(HistoryArchiveDisposalItem)] = new(
                "历史存档离库处置明细，一盒一行，固化盒内摘要与原柜位。",
                "→ 引用 HistoryArchiveDisposalRecords（DisposalRecordId，级联删除）。",
                "只读浏览。"),

            [nameof(ProjectInfo)] = new(
                "测绘/调查项目基础信息，供年度登记等业务引用项目编号与名称。",
                "← 被 YearlyArchiveRegisterRecords 等业务逻辑引用（项目编号/名称字段）。",
                "可维护。项目编号一旦被业务单据引用，修改或删除需谨慎。"),

            [nameof(HardDiskMedium)] = new(
                "资料室硬盘介质主档，含磁盘编号、序列号及当前状态。",
                "→ 关联 HardDiskLedgers、HardDiskRegisterLocks；← 被 HardDiskMediaApplications、HardDiskMediaTransactions、YearlyElectronicArchiveUnitMediumLinks 引用。",
                "只读浏览。介质生命周期由硬盘管理模块维护，级联删除会影响台账与交易记录。"),

            [nameof(HardDiskLedger)] = new(
                "硬盘介质的状态台账，与 HardDiskMedium 一对一。",
                "→ 引用 HardDiskMedia（MediumId，级联删除）。",
                "只读浏览。"),

            [nameof(HardDiskRegisterLock)] = new(
                "防止同一硬盘在登记流程中被重复占用的业务锁记录。",
                "→ 引用 HardDiskMedia（MediumId，级联删除）。",
                "只读浏览。锁记录由年度登记/硬盘借还流程自动写入与释放。"),

            [nameof(HardDiskMediaApplication)] = new(
                "硬盘借出/出库申请单主表。",
                "→ 引用 HardDiskMedia（MediumId，级联删除）；← 被 HardDiskMediaTransactions 引用。",
                "只读浏览。"),

            [nameof(HardDiskMediaTransaction)] = new(
                "硬盘借出、归还等操作流水。",
                "→ 引用 HardDiskMedia（MediumId）；→ 引用 HardDiskMediaApplications（ApplicationId，可置空）。",
                "只读浏览。"),

            [nameof(OpticalDiscMedium)] = new(
                "数据光盘介质主档（静态信息）。系统不管理空白光盘，仅对立档时写入数据的光盘做台账管理，且管理为资料业务的伴生业务。",
                "→ 关联 OpticalDiscLedgers（一对一）；← 被 OpticalDiscMediaTransactions、YearlyElectronicArchiveUnitDiscLinks 引用。",
                "只读浏览。光盘随资料立档/移库/销毁等业务被动维护，级联删除会影响台账与流水。"),

            [nameof(OpticalDiscLedger)] = new(
                "数据光盘的状态/位置/持有人台账，与 OpticalDiscMedium 一对一（台账分离）。",
                "→ 引用 OpticalDiscMedia（MediumId，级联删除）。",
                "只读浏览。由资料业务自动维护。"),

            [nameof(OpticalDiscMediaTransaction)] = new(
                "数据光盘的立档入库、迁档销毁等流转流水（持久化）。",
                "→ 引用 OpticalDiscMedia（MediumId，级联删除）。",
                "只读浏览。由资料业务自动写入。"),

            [nameof(YearlyArchiveRegisterRecord)] = new(
                "年度资料登记流程主单，记录申请状态、项目、部门及审批进度。",
                "→ 逻辑关联 ProjectInfos、Departments；← 被 YearlyArchiveRegisterMedias 引用（一对多）。",
                "可维护。删除会级联影响下级介质与明细，且可能破坏审批/归档链路，务必按子表→主表顺序操作。"),

            [nameof(YearlyArchiveRegisterMedia)] = new(
                "一条登记申请下的介质分类汇总（模拟/电子等），含介质类型、数量与处置方式。",
                "→ 引用 YearlyArchiveRegisterRecords（YearlyArchiveRegisterRecordId）；← 被 YearlyArchiveRegisterMediaItems、YearlyElectronicArchiveUnitMediaLinks 引用。",
                "可维护。删除前需先处理下级 MediaItems 及电子档案关联。"),

            [nameof(YearlyArchiveRegisterMediaItem)] = new(
                "登记介质条目下的资料子项（含密级）；电子资料含扩展明细（资料类型、所属子类、目录/文件清单等）；模拟资料含分类扩展（资料类型、所属子类、组织形式）。",
                "→ 引用 YearlyArchiveRegisterMedias（YearlyArchiveRegisterMediaId）；← 被 YearlyArchiveBoxMediaItemLinks 引用；电子子项可关联 YearlyArchiveRegisterElectronicMediaItemDetails；模拟子项可关联 YearlyArchiveRegisterSimulatedMediaItemDetails。",
                "可维护。删除前需确认档案盒关联；电子子项删除会级联删除扩展头表与目录/文件明细。"),

            [nameof(YearlyArchiveRegisterElectronicMediaItemDetail)] = new(
                "电子登记介质子项的扩展头信息：资料类型、所属子类、数据组织形式、数据量等。",
                "→ 引用 YearlyArchiveRegisterMediaItems（MediaItemId，1:1）；← 被 YearlyArchiveRegisterElectronicMediaItemEntries 引用。",
                "只读浏览。通常随登记流程写入。"),

            [nameof(YearlyArchiveRegisterSimulatedMediaItemDetail)] = new(
                "模拟登记介质子项的扩展头信息：资料类型、所属子类、组织形式（散页/装订）。",
                "→ 引用 YearlyArchiveRegisterMediaItems（MediaItemId，1:1）。",
                "只读浏览。通常随建档或存档文本直办写入。"),

            [nameof(YearlyArchiveRegisterElectronicMediaItemEntry)] = new(
                "电子资料子项下的目录或文件清单。",
                "→ 引用 YearlyArchiveRegisterElectronicMediaItemDetails（ElectronicMediaItemDetailId）。",
                "只读浏览。条目类型需与头表【数据组织形式】一致。"),

            [nameof(YearlyArchiveBox)] = new(
                "年度资料归档生成的档案盒实体，含盒号、柜位及规格信息。",
                "逻辑关联 Cabinets、ArchiveBoxSpecifications；← 被 YearlyArchiveBoxMediaItemLinks 引用。",
                "可维护。删除前需先解除 MediaItemLinks 及柜位分配。"),

            [nameof(YearlyArchiveBoxMediaItemLink)] = new(
                "档案盒与登记介质明细的多对一关联（一条明细只能入一盒）。",
                "→ 引用 YearlyArchiveBoxes、YearlyArchiveRegisterMediaItems（均级联删除）。",
                "只读浏览。关联由归档流程自动建立。"),

            ["YearlyArchiveBoxYearlyArchiveRegisterRecord"] = new(
                "年度档案盒与年度资料登记申请的多对多关联（EF 隐式中间表）。",
                "→ 引用 YearlyArchiveBoxes（ArchiveBoxesId）；→ 引用 YearlyArchiveRegisterRecords（RegisterRecordsId）。",
                "只读浏览。关联由模拟介质立档流程写入，手工改动可能影响登记单与档案盒的对应关系。"),

            ["YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit"] = new(
                "年度资料登记申请与电子立档单元的多对多关联（EF 隐式中间表）。",
                "→ 引用 YearlyArchiveRegisterRecords（RegisterRecordsId）；→ 引用 YearlyElectronicArchiveUnits（ElectronicArchiveUnitsId）。",
                "只读浏览。关联由电子立档流程写入。"),

            [nameof(YearlyElectronicArchiveUnit)] = new(
                "电子档案编号主实体，聚合硬盘/光盘/登记介质等多种关联。",
                "← 被 YearlyElectronicArchiveUnitMediumLinks、YearlyElectronicArchiveUnitDiscLinks、YearlyElectronicArchiveUnitMediaLinks 引用。",
                "只读浏览。"),

            [nameof(YearlyElectronicArchiveUnitMediumLink)] = new(
                "电子档案单元与 HardDiskMedium 的关联。",
                "→ 引用 YearlyElectronicArchiveUnits、HardDiskMedia。",
                "只读浏览。"),

            [nameof(YearlyElectronicArchiveUnitDiscLink)] = new(
                "电子档案单元与 OpticalDiscMedium 的关联。",
                "→ 引用 YearlyElectronicArchiveUnits、OpticalDiscMedia。",
                "只读浏览。"),

            [nameof(YearlyElectronicArchiveUnitMediaLink)] = new(
                "电子档案单元与 YearlyArchiveRegisterMedia 的一对一关联。",
                "→ 引用 YearlyElectronicArchiveUnits、YearlyArchiveRegisterMedias（级联删除）。",
                "只读浏览。"),

            [nameof(SystemAttachment)] = new(
                "业务单据关联的附件文件元数据（路径、类型、所属业务键等）。",
                "通过 BusinessType/BusinessRecordId 等字段逻辑关联各业务表。",
                "可维护。删除记录不会自动删除磁盘文件，清理附件需同时处理文件存储。"),

            [nameof(ToDoReadState)] = new(
                "记录用户对首页待办项的已读/未读状态。",
                "通过 UserId 与待办键逻辑关联 Users。",
                "可维护。可安全清空以重置待办提醒，不影响业务主数据。"),

            [nameof(UserPreference)] = new(
                "用户级界面偏好设置（如列宽、筛选条件等 JSON 配置）。",
                "→ 逻辑关联 Users（UserId）。",
                "可维护。删除仅影响对应用户的个性化设置。"),

            [nameof(FieldDomainDefinition)] = new(
                "各实体字段的中文显示名、域值开关及排序配置。",
                "← 被 FieldDomainOptions 引用（一对多）。",
                "可维护。修改 DisplayName 会影响全局下拉/表单标签；删除会级联删除域值选项。"),

            [nameof(FieldDomainOption)] = new(
                "字段域定义下的可选值列表（Scope + OptionValue + OptionLabel）。",
                "→ 引用 FieldDomainDefinitions（FieldDomainDefinitionId）。",
                "可维护。删除或禁用选项会影响依赖该域值的业务页面下拉列表。"),

            [nameof(DbOperationLog)] = new(
                "系统对数据库增删改操作的审计日志。",
                "独立日志表，无外键约束。",
                "只读浏览。日志由系统自动写入，不建议手工删改。"),

            [nameof(ArchiveContainerProjection)] = new(
                "数据库视图 vw_ArchiveContainerSummaries，聚合展示各类档案容器摘要。",
                "只读视图，非实体表，不可写入。",
                "只读浏览。视图为查询投影，不支持删除/清空操作。"),

            [nameof(BusinessLogicSettings)] = new(
                "系统级业务规则配置，如申请单逾期策略等。",
                "独立配置表，通常仅一条记录。",
                "只读浏览。修改请使用「业务逻辑设置」页面。"),

            [nameof(YearlyArchiveFilingFact)] = new(
                "年度资料立档后的核心台账事实，记录介质/容器/位置及生命周期状态。",
                "← 被出库、移库、归还、检索结果集等业务引用。",
                "只读浏览。由立档流程自动写入。"),

            [nameof(YearlyArchiveMaterialTransaction)] = new(
                "立档事实的流转留痕：立档、迁档、出库、归还等。",
                "→ 引用 YearlyArchiveFilingFacts（FilingFactId）。",
                "只读浏览。由资料业务自动写入。"),

            [nameof(YearlyArchiveOutboundRecord)] = new(
                "资料借出/出库申请与审批主单。",
                "← 被 YearlyArchiveOutboundItems、YearlyArchiveOutboundSyncEntries 引用。",
                "只读浏览。由资料出库流程维护。"),

            [nameof(YearlyArchiveOutboundItem)] = new(
                "出库单下的拟领用资料明细。",
                "→ 引用 YearlyArchiveOutboundRecords；← 可被 YearlyArchiveReturnItems 引用。",
                "只读浏览。"),

            [nameof(YearlyArchiveOutboundSyncEntry)] = new(
                "出库办结时同步写回登记份数、立档事实等的流水条目。",
                "→ 引用 YearlyArchiveOutboundRecords、YearlyArchiveOutboundItems。",
                "只读浏览。"),

            [nameof(YearlyArchiveRelocationRecord)] = new(
                "资料容器/介质移库（迁档）主单。",
                "← 被 YearlyArchiveRelocationItems 引用。",
                "只读浏览。"),

            [nameof(YearlyArchiveRelocationItem)] = new(
                "移库单下的立档事实明细及移库前后位置。",
                "→ 引用 YearlyArchiveRelocationRecords、YearlyArchiveFilingFacts。",
                "只读浏览。"),

            [nameof(YearlyArchiveReturnRecord)] = new(
                "资料归还登记主单，关联源出库单。",
                "← 被 YearlyArchiveReturnItems 引用。",
                "只读浏览。"),

            [nameof(YearlyArchiveReturnItem)] = new(
                "归还单下对应出库明细的收回记录。",
                "→ 引用 YearlyArchiveReturnRecords、YearlyArchiveOutboundItems。",
                "只读浏览。"),

            [nameof(YearlyArchiveSearchResultSet)] = new(
                "已立档资料检索后保存的结果集，可发起出库。",
                "← 被 YearlyArchiveSearchResultSetItems 引用。",
                "只读浏览。"),

            [nameof(YearlyArchiveSearchResultSetItem)] = new(
                "检索结果集内的立档事实或内容条目。",
                "→ 引用 YearlyArchiveSearchResultSets、YearlyArchiveFilingFacts。",
                "只读浏览。"),

            [nameof(YearlyElectronicArchiveUnitMediaItemLink)] = new(
                "电子立档单元与登记介质明细的多对一关联及立档路径快照。",
                "→ 引用 YearlyElectronicArchiveUnits、YearlyArchiveRegisterMediaItems。",
                "只读浏览。"),
        };
    }
}
