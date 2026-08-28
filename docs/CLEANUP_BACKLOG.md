# 开发收口：清理清单

生成日期：2026-08-28  
范围：只做卫生、字典与死入口确认。**不**做系统工程级分层改造。

原则：

- 有引用、有菜单、有业务平行的代码一律保留。
- 结构重构（`REFACTOR_SOLUTION_V2.md` 批次 A～C）视为已完成基线；其中批次 C「再拆应用服务/查询服务」**不要开工**。
- 每项改动单独提交、不混入行为变更。

---

## P0 数据字典补中文 — 已完成（2026-08-28）

已在 `FieldAliasMap` / `ExactAliasMap` / `TableAliasMap` 补齐别名，并让 `SchemaDictionarySyncService` 刷新「未映射」与英文表名。重跑同步后：`needsReview` **0**；5 张表已有中文名。

---

## P0 确认项：孤立功能，先问再删

### 1. 加工产出登记 — 已删除（2026-08-28）

已删除独立登记入口：对话框、ViewModel、`ShowNetworkProcessedOutputEditDialog`、`RegisterProcessedOutputAsync`。

**保留** `OriginKind = 加工产出`：出网办结无关联在网对象时仍会补写该来源；处置页筛选与出网候选规则依赖此常量。存量数据不去改。

### 2. `OperationProgressDialog` 窗口 — 已删除（2026-08-28）

已删除无引用的 `Views/Shared/OperationProgressDialog.xaml`。进度仍由 `OperationProgressOverlay` + `OperationProgressDialogViewModel` 提供。

---

## P1 卫生

| 项 | 说明 | 建议 |
| --- | --- | --- |
| `类图/` | 设计稿已移至仓库外 `F:\2026\资料室管理系统\类图` | 已加入 `.gitignore` |
| `.build-*` | 本地编译检查输出 | 已在 `.gitignore`；勿提交 |
| `docs/architecture/REFACTOR_SOLUTION_V2.md` §6 | 原后续拆分层计划 | 已改为「已冻结」 |
| `Converter.cs` | 已迁到 `Views/Shared/ValueConverters.cs` | 完成 |
| 页面标题「资料检索_方式1」 | 已改为「资料检索(综合模式)」 | 完成 |

---

## P1 冒烟清单（改任何代码前先有）

无自动化测试。动字典/删入口前，至少手工过一遍：

1. 登录 → 待办打开
2. 资料建档申请 → 审批
3. 模拟立档 / 电子立档（含硬盘入位）
4. 资料出库 → 归还
5. 模拟/电子离库处置
6. 硬盘出库 → 归还；硬盘盘库；硬盘离库
7. 入网申请 → 办结写在网台账
8. 出网申请 → 审批
9. 在网数据处置
10. 历史存档（地形图/航片/其他图件）离库
11. 开柜查看盒内容、档口用途
12. 打印签批单（办结前留白）

---

## 不要动（看起来像重复，实际是业务平行）

入网/出网成对 `*Support`（校验、登记介质持久化、路径、打印等）——规则不同，禁止合成一套框架。

处置四条线（资料 / 硬盘 / 在网 / 历史存档）的 EditDialog + DomainValues——状态机相似，办结效应不同。

`ArchiveDetailPage` 与 `ArchiveDetailWindow`：窗口只是嵌页面，都在用。

`ArchiveSearchPage` 与 `ArchiveFilingSearchPage`：综合检索 vs 按介质立档检索，菜单三项都在用。

`BoolToVisConverter` 与 WPF `BooleanToVisibilityConverter`：可并存；`InverseBoolToVisConverter` / `InverseBoolRadioConverter` 有独立用途。

EF `Migrations/*.Designer.cs` 超长——禁止为「瘦身」改历史迁移。

`CabinetOpenViewModel.cs`（约 3900 行）是当前最大业务文件。 **仅当下一步要改开柜交互时再拆**，不要作为收口任务。

---

## P2 以后再说（有痛再做）

- 为 `*BusinessRules`、档口解析、份数计算补少量单元测试。
- 审批按钮状态若三处同时改坏，再抽公共；现在不要提前抽。
- 备份/还原、版本号、覆盖安装是否冲库——产品收口，比再拆服务层更值钱。

---

## 建议开工顺序

1. ~~补数据字典~~、~~删加工产出登记~~、~~删 OperationProgressDialog~~、~~冻结重构方案 §6~~、~~移出 `类图/`~~、~~Converter 迁 Shared~~、~~检索标题~~ — 已完成。
2. 停。进入使用期，用冒烟清单接真实问题。
