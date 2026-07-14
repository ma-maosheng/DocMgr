# 资料室管理系统重构方案 V2

## 1. 目标

- 统一目录、文件和命名空间的分层规则，降低认知负担。
- 将超长文件按“业务编排 / 规则 / 映射 / 数据定义”拆分，避免单文件巨石化。
- 在不改变业务行为的前提下，建立可渐进迁移的架构基线。

## 2. 目标目录结构（建议）

```text
DocMgr/
  Config/
    DependencyInjection/
      ServiceCollectionExtensions.cs
      ServiceCollectionExtensions.Repositories.cs
      ServiceCollectionExtensions.Services.cs
      ServiceCollectionExtensions.ViewModels.cs
  Data/
  Infrastructure/
    Seeding/
      FieldDomainSeedService.cs
      FieldDomainSeedService.AliasMaps.cs
      FieldDomainSeedService.SeedCatalog.cs
      FieldDomainSeedService.Models.cs
  Models/
    CabinetMgr/
    HardDiskMedia/
    HistoryArchive/
    ProjectMgr/
    Shared/
    SystemSettings/
    YearlyArchive/
  Repositories/
    Interfaces/
    Abstractions/
  Services/
    Interfaces/
    Shared/
    CabinetMgr/
    HardDiskMedia/
    HistoryArchive/
    ProjectMgr/
    SystemSettings/
    YearlyArchive/
  ViewModels/
  Views/
```

## 3. 命名规范

- 目录：`PascalCase`，禁止缩写目录名（如后续将 `*Mgr` 渐进迁移为完整词）。
- 文件：单一职责，优先“一主类型一文件”，跨职责使用 `*.Feature.cs` 或 `*.Part.cs`。
- 接口：`I` 前缀，仅用于契约层。
- 命名空间：统一使用 `namespace DocMgr.<Layer>.<Domain>;`（文件作用域风格）。
- 常量：领域常量保留在对应领域模型或专用 `Constants` 类型中，避免横向散落。

## 4. 超长文件拆分规则

- `>1000` 行文件必须拆分。
- 拆分优先级：  
  1) 配置/映射数据；  
  2) 工具与转换逻辑；  
  3) 业务流程编排；  
  4) UI 状态管理。  
- 拆分方式：优先 `partial class`（不改外部调用），随后再逐步提炼为独立服务。

## 5. 本次已落地改造

### 5.1 依赖注入注册拆分

- `ServiceCollectionExtensions` 拆为 4 个文件：
  - 入口：`ServiceCollectionExtensions.cs`
  - 仓储注册：`ServiceCollectionExtensions.Repositories.cs`
  - 领域服务注册：`ServiceCollectionExtensions.Services.cs`
  - ViewModel 注册：`ServiceCollectionExtensions.ViewModels.cs`

收益：注册层职责清晰，可独立审阅与定位。

### 5.2 超长文件拆分（`FieldDomainSeedService`）

原文件超过 1000 行，已拆分为：

- `FieldDomainSeedService.cs`：核心编排与别名生成规则。
- `FieldDomainSeedService.AliasMaps.cs`：字段别名映射表。
- `FieldDomainSeedService.SeedCatalog.cs`：域值种子目录。
- `FieldDomainSeedService.Models.cs`：种子数据记录类型。

收益：单文件体量显著下降，映射与流程分离，便于后续域值扩展。

### 5.3 服务层超长文件拆分（第二批）

- `Services/YearlyArchive/ArchiveRegisterService.cs` 拆分为：
  - `ArchiveRegisterService.cs`（流程主干）
  - `ArchiveRegisterService.PrintingAndMaintenance.cs`（打印、附件、域值装配与维护）
- `Services/CabinetMgr/CabinetOpenLayoutService.cs` 拆分为：
  - `CabinetOpenLayoutService.cs`（开柜主流程、介质装配、档案展开）
  - `CabinetOpenLayoutService.LayoutAndTypes.cs`（布局算法、容量计算、内部 record 类型）

收益：高复杂服务由“流程 + 算法/装配”分离，跨团队维护时可并行修改，冲突更少。

### 5.4 服务层超长文件拆分（第三批-阶段1）

- `Services/YearlyArchive/ArchiveFilingService.cs` 已拆分为：
  - `ArchiveFilingService.cs`（立档主流程与核心编排）
  - `ArchiveFilingService.MediaLinking.cs`（介质链接、并档约束、状态同步）
  - `ArchiveFilingService.ElectronicHardDiskFlow.cs`（硬盘留存/归还/格式化及电子介质袋校验）
  - `ArchiveFilingService.ContainerPlacement.cs`（容器推荐、占位统计、摆放规则）

收益：高耦合的电子立档硬盘流程与容器推荐算法解耦，后续可独立测试和独立演进。

### 5.5 服务层超长文件拆分（第三批-阶段2）

- `Services/HardDiskMedia/HardDiskMediaService.cs` 已开始拆分：
  - `HardDiskMediaService.Overview.cs`（总览统计、风险洞察、分布分析）
  - `HardDiskMediaService.cs`（保留业务主流程，后续继续按申请流/导入流拆分）

当前收益：把“分析报表类”逻辑从主业务流程中剥离，后续继续拆分申请流时冲突更少。

### 5.6 服务与 ViewModel 超长文件拆分（第三批-阶段3）

- `Services/HardDiskMedia/HardDiskMediaService.cs` 继续拆分为：
  - `HardDiskMediaService.Importing.cs`（导入模板、Excel 解析、导入校验与导入事务）
  - `HardDiskMediaService.Attachments.cs`（申请签字件上传/删除/查看准备）
  - `HardDiskMediaService.cs`（保留申请主流程、审批办结、规则校验等核心编排）
- `ViewModels/YearlyArchive/ArchiveFilingViewModel.cs` 拆分为：
  - `ArchiveFilingViewModel.cs`（主流程与通用 UI 状态）
  - `ArchiveFilingViewModel.ElectronicHardDiskFlow.cs`（电子介质留存硬盘来源判定、外来硬盘登记、档口推荐与快照）

当前收益：把导入/附件等横切能力和电子硬盘立档复杂分支从主文件解耦，显著降低主文件认知负担并减少后续冲突面。

### 5.7 服务层规则块拆分（第三批-阶段4）

- `Services/HardDiskMedia/HardDiskMediaService.cs` 继续拆分规则与状态映射块：
  - 新增 `HardDiskMediaService.ApplicationRules.cs`（申请状态映射、介质状态写回、借出锁定/解锁、角色与状态校验、办结前状态保护规则）
  - 主文件 `HardDiskMediaService.cs` 保留业务流程编排，移除上述规则实现细节

当前收益：把“流程编排”与“状态机规则/校验规则”进一步隔离，便于后续继续拆分申请处理流程并降低变更冲突。

### 5.8 服务层申请处理块拆分（第三批-阶段5）

- `Services/HardDiskMedia/HardDiskMediaService.cs` 继续拆分申请处理流程：
  - 新增 `HardDiskMediaService.ApplicationProcessing.cs`（审批、退回、作废、办结、打印、归位目标解析）
  - 主文件 `HardDiskMediaService.cs` 仅保留介质台账主流程与基础查询接口

当前收益：`HardDiskMediaService.cs` 已降至 1000 行以内，核心主文件聚焦编排入口，申请处理细节独立后更利于后续单元测试与职责演进。

### 5.9 ViewModel 工作流块拆分（第三批-阶段6）

- `ViewModels/YearlyArchive/ArchiveFilingViewModel.cs` 继续拆分：
  - 新增 `ArchiveFilingViewModel.Workflow.cs`（初始化、待办刷新、选择恢复、提交流程编排）
  - 主文件 `ArchiveFilingViewModel.cs` 移除对应工作流实现，保留状态与其余交互逻辑

当前收益：`ArchiveFilingViewModel` 中“状态定义”与“工作流编排”解耦，后续继续分离模式切换/位置计算逻辑时冲突更小。

### 5.10 ViewModel 模式与位置块拆分（第三批-阶段7）

- `ViewModels/YearlyArchive/ArchiveFilingViewModel.cs` 继续拆分：
  - 新增 `ArchiveFilingViewModel.ModeAndLocation.cs`（模式切换、步骤重置、模拟介质档口计算/推荐、档口快照）
  - 主文件移除对应实现，保留属性状态与其余协作逻辑

当前收益：模式切换与位置选择从主文件解耦，后续针对“可选立档方式”调整规则时可独立迭代并减少主文件冲突。

### 5.11 ViewModel 记录投影块拆分（第三批-阶段8）

- `ViewModels/YearlyArchive/ArchiveFilingViewModel.cs` 继续拆分：
  - 新增 `ArchiveFilingViewModel.RecordProjection.cs`（选中记录变更、摘要更新、容器列表加载、模拟/电子记录重建与步骤联动）
  - 主文件移除对应投影与重建实现，仅保留状态与其余协作逻辑

当前收益：视图层的“数据投影重建”从主文件独立，便于针对清单构建性能与筛选规则做局部优化，主文件进一步接近 1000 行边界。

### 5.12 ViewModel 场景决策块拆分（第三批-阶段9）

- `ViewModels/YearlyArchive/ArchiveFilingViewModel.cs` 继续拆分：
  - 新增 `ArchiveFilingViewModel.ElectronicScenario.cs`（电子场景决策、留存硬盘来源推断、可用立档方式同步与 UI 步骤联动）
  - 主文件移除对应方法，保留状态属性与基础入口

当前收益：`ArchiveFilingViewModel.cs` 已降到 1000 行以内，电子场景规则与 UI 状态刷新逻辑独立后，后续策略变更可在局部文件演进。

### 5.13 命名空间风格统一（第三批-阶段10）

- 按“结构优先、命名空间统一”目标，对 `Infrastructure/Seeding` 全目录统一为文件作用域命名空间：
  - `FieldDomainSeedService*.cs`
  - `CabinetSpecificationSeedService.cs`
  - `CabinetArchiveBoxPlacementSyncService.cs`
  - `DevSystemSettingsSeeder.cs`
- `Config/DependencyInjection` 目录已保持文件作用域风格，无需额外改动。

当前收益：目录内代码风格一致，减少无关缩进层级，后续跨文件移动/拆分时命名空间处理成本更低。

### 5.14 Project 管理域命名空间语义收敛（第三批-阶段11）

- 按“层级语义清晰”原则，将 `ProjectMgr` 相关层命名空间统一到 `Projects`：
  - `DocMgr.Repositories.ProjectMgr -> DocMgr.Repositories.Projects`
  - `DocMgr.Services.ProjectMgr -> DocMgr.Services.Projects`
  - `DocMgr.ViewModels.ProjectMgr -> DocMgr.ViewModels.Projects`
  - `DocMgr.Views.ProjectMgr -> DocMgr.Views.Projects`
- 同步更新依赖注入与跨层引用：
  - `Config/DependencyInjection/ServiceCollectionExtensions.*.cs`
  - `Services/Shared/DialogService.cs`
  - `Views/MainWindow.xaml.cs`
  - `GlobalUsings.FeatureFolders.cs`
- 同步更新 WPF `x:Class` 与 `clr-namespace`：
  - `Views/ProjectMgr/ProjectEditDialog.xaml`
  - `Views/ProjectMgr/SurveyProjectSettingPage.xaml`

当前收益：在不改变行为、不强制迁移数据模型命名空间的前提下，先完成应用层/表示层语义统一，为后续目录迁移（`ProjectMgr -> Projects`）提供稳定过渡面。

### 5.15 Cabinet 管理域命名空间语义收敛（第三批-阶段12）

- 按“层级语义清晰”原则，将 `CabinetMgr` 相关应用层命名空间统一到 `Cabinets`：
  - `DocMgr.Repositories.CabinetMgr -> DocMgr.Repositories.Cabinets`
  - `DocMgr.Services.CabinetMgr -> DocMgr.Services.Cabinets`
  - `DocMgr.ViewModels.CabinetMgr -> DocMgr.ViewModels.Cabinets`
  - `DocMgr.Views.CabinetMgr -> DocMgr.Views.Cabinets`
  - `DocMgr.Views.CabinetMgr.Behaviors -> DocMgr.Views.Cabinets.Behaviors`
- 同步更新依赖注入与跨层引用：
  - `Config/DependencyInjection/ServiceCollectionExtensions.*.cs`
  - `Services/Shared/DialogService.cs`
  - `Views/MainWindow.xaml.cs`
  - `GlobalUsings.FeatureFolders.cs`
- 同步更新 WPF `x:Class` 与行为命名空间引用：
  - `Views/CabinetMgr/Cabinet*.xaml`
  - `Views/CabinetMgr/CabinetLayoutPage.xaml` 中 `behaviors` 映射

当前收益：柜体管理域在仓储、服务、ViewModel 与视图层的命名语义已统一，后续可在不触及行为逻辑的前提下继续推进目录物理迁移与模型层命名收敛。

### 5.16 Project 模型层命名空间收敛（第三批-阶段13）

- 将 `ProjectInfo` 模型命名空间统一为：
  - `DocMgr.Models.ProjectMgr -> DocMgr.Models.Projects`
- 同步更新项目模型引用链路：
  - `Repositories/ProjectMgr/ProjectRepository.cs`
  - `Repositories/Interfaces/IProjectRepository.cs`
  - `Repositories/YearlyArchive/ArchiveRegisterSimulationRepository.cs`
  - `Repositories/Interfaces/IArchiveRegisterSimulationRepository.cs`
  - `Services/YearlyArchive/ArchiveRegisterSimulationService.cs`
  - `Infrastructure/Seeding/DevSystemSettingsSeeder.cs`
  - `Services/Shared/DialogService.cs`
  - `GlobalUsings.FeatureFolders.cs`
- 同步更新 EF Core 迁移模型快照与设计器中的实体类型字符串：
  - `Data/Migrations/AppDbContextModelSnapshot.cs`
  - `Data/Migrations/*Designer.cs`（涉及 `ProjectInfo` 的历史迁移）

当前收益：`Project` 领域在模型层与应用层命名语义一致，避免跨层出现 `Projects` 与 `ProjectMgr` 并存的认知割裂，同时保持迁移元数据一致性。

### 5.17 Cabinet 模型层命名空间收敛（第三批-阶段14）

- 将 `Cabinet` 相关模型命名空间统一为：
  - `DocMgr.Models.CabinetMgr -> DocMgr.Models.Cabinets`
- 同步更新柜体模型引用链路（仓储/服务/ViewModel/视图/接口）：
  - `Repositories/CabinetMgr/*.cs`
  - `Repositories/Interfaces/I*Cabinet*.cs`
  - `Services/CabinetMgr/*.cs`
  - `Services/Interfaces/I*Cabinet*.cs`
  - `ViewModels/CabinetMgr/*.cs`
  - `Views/CabinetMgr/CabinetOpenDialog.xaml.cs`
  - `Services/HardDiskMedia/HardDiskMediaService.cs`
  - `Services/YearlyArchive/ArchiveFilingService.cs`
  - `Data/AppDbContext.cs`
  - `GlobalUsings.FeatureFolders.cs`
  - `Services/Shared/DialogService.cs`
- 同步更新 EF Core 迁移模型快照与设计器中的实体类型字符串：
  - `Data/Migrations/AppDbContextModelSnapshot.cs`
  - `Data/Migrations/*Designer.cs`（涉及 `Cabinet` 相关实体的历史迁移）

当前收益：`Cabinets` 领域已完成“应用层 + 模型层”命名语义收敛，跨层不再混用 `CabinetMgr` 与 `Cabinets`，为后续目录物理迁移与命名统一提供稳定基础。

### 5.18 Cabinet 模型目录物理迁移（第三批-阶段15）

- 在不改变行为与类型命名的前提下，完成模型目录物理迁移：
  - `Models/CabinetMgr/*.cs -> Models/Cabinets/*.cs`
- 保持文件内容与命名空间不变（已在前一阶段统一为 `DocMgr.Models.Cabinets`），本阶段仅做路径收敛。

当前收益：模型层“目录结构”与“命名空间语义”完全一致，降低新成员定位成本，并为后续 `Repositories/Services/ViewModels/Views` 的目录物理迁移提供模板路径。

### 5.19 Project 模型目录物理迁移（第三批-阶段16）

- 在不改变行为与类型命名的前提下，完成 `Project` 模型目录物理迁移：
  - `Models/ProjectMgr/ProjectInfo.cs -> Models/Projects/ProjectInfo.cs`
- 本阶段仅做路径收敛；命名空间保持 `DocMgr.Models.Projects` 不变。

当前收益：`Project` 领域模型目录与命名空间完全对齐，消除 `ProjectMgr` 路径遗留，降低跨层定位成本。

### 5.20 Repositories 目录物理迁移（第三批-阶段17）

- 在不改变行为、类型与命名空间的前提下，完成仓储层目录物理迁移：
  - `Repositories/CabinetMgr/*.cs -> Repositories/Cabinets/*.cs`
  - `Repositories/ProjectMgr/ProjectRepository.cs -> Repositories/Projects/ProjectRepository.cs`
- 本阶段仅做路径收敛，保持既有依赖注入注册与接口契约不变。

当前收益：`Repositories` 层目录结构已与命名空间语义一致，`*Mgr` 路径遗留进一步清理，为后续 `Services/ViewModels/Views` 目录迁移提供一致模板。

### 5.21 Services 目录物理迁移（第三批-阶段18，第一段）

- 在不改变行为、类型与命名空间的前提下，完成 `Services` 层第一段目录迁移：
  - `Services/CabinetMgr/CabinetService.cs -> Services/Cabinets/CabinetService.cs`
  - `Services/CabinetMgr/CabinetArchiveBoxPlacementService.cs -> Services/Cabinets/CabinetArchiveBoxPlacementService.cs`
  - `Services/ProjectMgr/ProjectService.cs -> Services/Projects/ProjectService.cs`
- `CabinetOpenLayoutService` 两个拆分文件体量较大，保留在下一段单独迁移以降低单批风险。

当前收益：`Services` 层已完成主要业务服务目录收敛，并保持每批次可编译、可回滚。

### 5.22 Services 目录物理迁移（第三批-阶段18，第二段）

- 完成 `CabinetOpenLayoutService` 两个拆分文件的目录迁移：
  - `Services/CabinetMgr/CabinetOpenLayoutService.cs -> Services/Cabinets/CabinetOpenLayoutService.cs`
  - `Services/CabinetMgr/CabinetOpenLayoutService.LayoutAndTypes.cs -> Services/Cabinets/CabinetOpenLayoutService.LayoutAndTypes.cs`
- 迁移后 `Services/CabinetMgr` 已无代码文件残留，`Services` 层 `Cabinet` 域目录与命名空间语义一致。

当前收益：`Services` 层 `*Mgr` 历史路径已全部完成收敛，后续可继续推进 `ViewModels` 与 `Views` 的目录物理迁移。

### 5.23 ViewModels 目录物理迁移（第三批-阶段19）

- 在不改变行为、类型与命名空间的前提下，完成 `ViewModels` 层目录物理迁移：
  - `ViewModels/ProjectMgr/*.cs -> ViewModels/Projects/*.cs`
  - `ViewModels/CabinetMgr/*.cs -> ViewModels/Cabinets/*.cs`
- 迁移后 `ViewModels/ProjectMgr` 与 `ViewModels/CabinetMgr` 已无 `.cs` 文件残留。

当前收益：`ViewModels` 层目录结构与命名空间语义完全一致，`*Mgr` 遗留进一步收敛，为 `Views` 层同构迁移建立一致基线。

### 5.24 Views 目录物理迁移（第三批-阶段20）

- 在不改变行为、类型与命名空间的前提下，完成 `Views` 层目录物理迁移：
  - `Views/ProjectMgr/* -> Views/Projects/*`
  - `Views/CabinetMgr/* -> Views/Cabinets/*`
  - `Views/CabinetMgr/Behaviors/* -> Views/Cabinets/Behaviors/*`
- 迁移后 `Views/ProjectMgr` 与 `Views/CabinetMgr` 已无代码与 XAML 文件残留。

当前收益：展示层目录结构已与命名空间语义完全一致，`ProjectMgr/CabinetMgr` 路径遗留在主要业务层已基本收敛。

### 5.25 MainWindow 导航标识收敛（第三批-阶段21）

- 将主窗口导航分组的控件命名从历史标识收敛为新语义命名：
  - `ExpProjectMgr -> ExpProjects`
  - `ExpCabinetMgr -> ExpCabinets`
- 同步更新 `MainWindow.xaml.cs` 中对应访问点，保持行为不变。

当前收益：主导航层命名与目录/命名空间收敛结果一致，避免 UI 层残留历史术语造成认知噪声。

### 5.26 历史痕迹清理与总体验证（第三批-阶段22）

- 删除迁移过程中遗留的历史备份文件：
  - `Views/MainWindow.xaml.cs.md1`
- 对业务代码（排除文档）执行 `ProjectMgr/CabinetMgr` 关键字残留扫描，确认已无代码残留。
- 进行总编译验收，持续满足“无行为改动前提下可编译”目标。

当前收益：结构迁移后的代码面已完成语义收敛清理，仅文档保留历史术语用于重构轨迹追溯。

### 5.27 提交前格式化与终验（第三批-阶段23）

- 执行解决方案级格式化：
  - `dotnet format DocMgr.sln`
- 执行终验编译：
  - `dotnet build`（0 warning / 0 error）

当前收益：在保持行为不变的前提下，代码风格与 using 排序完成统一，提交前质量门禁通过。

## 6. 后续分批重构计划

### 批次 A（低风险）

- 将 `Config/DependencyInjection` 与 `Infrastructure/Seeding` 全面统一为文件作用域命名空间。
- 对通用工具型类补充注释和单元测试。

### 批次 B（中风险）

- 拆分 `ArchiveRegisterService`、`CabinetOpenLayoutService`：
  - `*.Workflow.cs`（流程）
  - `*.Validator.cs`（规则）
  - `*.Mapper.cs`（映射）
- 保持原公开接口不变。

### 批次 C（高风险）

- 拆分 `ArchiveFilingViewModel`、`HardDiskMediaService`、`ArchiveFilingService`。
- 引入“应用服务 + 领域规则服务 + 查询服务”结构，降低 ViewModel 与服务耦合。

## 7. 迁移守则

- 每批次必须保证可编译并通过基础功能回归。
- 严禁在同一批次混入行为改动与结构改动。
- 新增注释只解释“为什么”，不解释显而易见的“做了什么”。
