#if DEBUG
using System;
using System.Collections.Generic;
using System.Windows;
using DocMgr.Views;
using DocMgr.Views.Cabinets;
using DocMgr.Views.HardDiskMedia;
using DocMgr.Views.HistoryArchive;
using DocMgr.Views.NetworkTransfer;
using DocMgr.Views.Projects;
using DocMgr.Views.Shared;
using DocMgr.Views.SystemSettings;
using DocMgr.Views.Inventory;
using DocMgr.Views.YearlyArchive;

namespace DocMgr.Infrastructure.DebugUi
{
    /// <summary>
    /// Debug 界面短码对照表：沟通时用短码定位 Page / Window。
    /// 格式：{域}-{功能}[-变体]，如 YA-OB-ED。
    /// </summary>
    public static class UiDebugIdCatalog
    {
        private static readonly IReadOnlyDictionary<Type, UiDebugIdEntry> Entries = BuildEntries();

        /// <summary>
        /// 解析界面短码；未登记时返回 <c>?-{类型名}</c>。
        /// </summary>
        public static UiDebugIdEntry Resolve(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (Entries.TryGetValue(type, out UiDebugIdEntry? entry))
            {
                return entry;
            }

            return new UiDebugIdEntry($"?-{type.Name}", type.Name, isRegistered: false);
        }

        /// <summary>
        /// 解析 FrameworkElement 对应短码。
        /// </summary>
        public static UiDebugIdEntry Resolve(FrameworkElement element)
        {
            ArgumentNullException.ThrowIfNull(element);
            return Resolve(element.GetType());
        }

        private static Dictionary<Type, UiDebugIdEntry> BuildEntries()
        {
            var map = new Dictionary<Type, UiDebugIdEntry>();

            void Add<T>(string code, string title) where T : FrameworkElement
                => map[typeof(T)] = new UiDebugIdEntry(code, title, isRegistered: true);

            // APP
            Add<MainWindow>("APP-MAIN", "主窗口");
            Add<LoginWindow>("APP-LOGIN", "登录窗口");

            // YA Pages
            Add<ArchiveRegisterApplicationPage>("YA-REG-APP", "建档申请");
            Add<ArchiveRegisterApprovalPage>("YA-REG-APV", "申请审批");
            Add<ArchiveFilingPage>("YA-FIL", "资料立档");
            Add<StockHardDiskDirectFilingPage>("YA-FIL-STK", "存量硬盘资料直办立档");
            Add<StockTextArchiveDirectFilingPage>("YA-FIL-STT", "存档文本资料直办立档");
            Add<StockTextArchiveExcelImportDialog>("YA-FIL-STT-IMP", "存档文本资料直办 Excel 导入");
            Add<ArchiveFilingSearchPage>("YA-FIL-SCH", "立档检索");
            Add<ArchiveFilingSearchPoolPage>("YA-FIL-POOL", "检索池");
            Add<ArchiveFilingLedgerPage>("YA-FIL-LDG", "立档台账");
            Add<StockContainerOverviewPage>("YA-STK-OVW", "库管资料总览");
            Add<ArchiveDetailPage>("YA-DTL", "资料查看");
            Add<ArchiveSearchPage>("YA-SCH", "资料检索(综合模式)");
            Add<ArchiveOutboundApplyPage>("YA-OB-APP", "借出申请");
            Add<ArchiveOutboundApprovalPage>("YA-OB-APV", "审批出库");
            Add<ArchiveOutboundHandoverPage>("YA-OB-HND", "出库交接");
            Add<ArchiveReturnWorkbenchPage>("YA-RTN", "归还申请/审批入库");
            Add<ArchiveCirculationLedgerPage>("YA-CIR-LDG", "流转台账");
            Add<ArchiveCrossDomainTransferLedgerPage>("YA-XDM-LDG", "跨域流转台账");
            Add<ArchiveInventoryRegisterPage>("YA-INV", "模拟/电子资料盘库");
            Add<ArchiveSimulatedRelocationPage>("YA-REL-SIM", "模拟介质资料迁档");
            Add<ArchiveElectronicRelocationPage>("YA-REL-ELC", "电子介质资料迁档");
            Add<ArchiveRelocationLedgerPage>("YA-REL-LDG", "迁档台账");
            Add<ArchiveDisposalPage>("YA-DSP", "模拟/电子资料离库处置");

            // YA Dialogs / Windows
            Add<ArchiveRegisterEditDialog>("YA-REG-ED", "建档编辑");
            Add<ArchiveRegisterApplicationViewDialog>("YA-REG-VW", "建档查看");
            Add<ArchiveOutboundEditDialog>("YA-OB-ED", "出库编辑");
            Add<ArchiveOutboundApplicationViewDialog>("YA-OB-VW", "出库查看");
            Add<ArchiveOutboundHandoverAssistantWindow>("YA-OB-HND-AST", "出库交接助手");
            Add<ArchiveReturnOutboundDetailWindow>("YA-RTN-OB-DTL", "归还-出库明细");
            Add<ArchiveReturnRehomeTargetPickDialog>("YA-RTN-RHOME", "归还目标盒选择");
            Add<ArchiveSearchResultSetPickDialog>("YA-SCH-PICK", "检索集选择");
            Add<StockHardDiskYearProjectPickDialog>("YA-FIL-STK-PRJ", "年度已有项目");
            Add<ArchiveDetailWindow>("YA-DTL-WN", "资料详情窗口");
            Add<SimulatedArchiveInventoryRegisterEditDialog>("YA-INV-SIM-ED", "模拟资料盘库编辑");
            Add<ElectronicArchiveInventoryRegisterEditDialog>("YA-INV-ELC-ED", "电子资料盘库编辑");
            Add<ElectronicMediaItemEntriesDialog>("YA-ELC-ENT", "电子目录/文件明细");
            Add<ArchiveDisposalEditDialog>("YA-DSP-ED", "资料离库处置编辑");

            // NT Pages（年度资料出入网管理 · 6 子菜单）
            Add<NetworkInboundApplicationPage>("NT-IB-APP", "入网申请");
            Add<NetworkInboundApprovalPage>("NT-IB-APV", "入网审批");
            Add<NetworkOutboundApplicationPage>("NT-OB-APP", "出网申请");
            Add<NetworkOutboundApprovalPage>("NT-OB-APV", "出网审批");
            Add<NetworkOnNetAssetLedgerPage>("NT-ONL-LDG", "在网数据台账");
            Add<NetworkOnNetDisposalPage>("NT-DSP", "在网数据处置");

            // NT Dialogs / Windows
            Add<NetworkInboundEditDialog>("NT-IB-ED", "入网编辑");
            Add<NetworkOutboundEditDialog>("NT-OB-ED", "出网编辑");
            Add<NetworkOnNetDisposalEditDialog>("NT-DSP-ED", "在网数据处置编辑");

            // HD / OD Pages
            Add<HardDiskMediaPage>("HD-MED", "概览");
            Add<HardDiskMediumLedgerPage>("HD-LDG", "初始登记");
            Add<HardDiskMediaOutboundApplicationPage>("HD-OB-APP", "出库申请");
            Add<HardDiskMediaApprovalPage>("HD-OB-APV", "出库办理");
            Add<HardDiskMediaReturnRegistrationPage>("HD-RTN", "归还申请/归还办理");
            Add<HardDiskMediaTransactionPage>("HD-TXN", "硬盘台账");
            Add<HardDiskDisposalPage>("HD-DSP", "离库处置");
            Add<HardDiskInventoryRegisterPage>("HD-INV", "盘库登记");
            Add<OpticalDiscMediaPage>("OD-MED", "概览");
            Add<OpticalDiscMediumLedgerPage>("OD-LDG", "光盘台账");

            // HD Dialogs
            Add<HardDiskMediumEditDialog>("HD-MED-ED", "硬盘介质编辑");
            Add<HardDiskMediumSelectionDialog>("HD-MED-PICK", "硬盘介质选择");
            Add<LocalPhysicalDiskPickerDialog>("HD-MED-HW", "本机硬盘选择");
            Add<HardDiskMediaOutboundApplicationEditDialog>("HD-OB-ED", "出库申请编辑");
            Add<HardDiskMediaApplicationViewDialog>("HD-OB-VW", "出库申请查看");
            Add<HardDiskMediaApprovalEditDialog>("HD-OB-APV-ED", "出库办理编辑");
            Add<HardDiskDisposalEditDialog>("HD-DSP-ED", "离库处置编辑");
            Add<HardDiskInventoryRegisterEditDialog>("HD-INV-ED", "盘库登记编辑");

            // CB
            Add<CabinetLayoutPage>("CB-LAY", "档案柜登记");
            Add<CabinetSearchPage>("CB-SCH", "开柜查看");
            Add<CabinetOpenDialog>("CB-OPEN", "开柜");
            Add<CabinetEditDialog>("CB-ED", "柜体编辑");
            Add<CabinetSlotDetailDialog>("CB-SLOT", "档口详情");
            Add<CabinetArchiveBoxContentDialog>("CB-BOX-CNT", "档案盒内容");
            Add<CabinetArchiveBoxPlacementEditDialog>("CB-BOX-PLC", "档案盒落位编辑");
            Add<CabinetArchiveBoxPendingReturnDetailDialog>("CB-BOX-PRTN", "待归还盒详情");
            Add<CabinetArchiveSlotCategoryEditDialog>("CB-SLOT-CAT", "档案档口用途");
            Add<CabinetHardDiskSlotCategoryEditDialog>("CB-HD-CAT", "硬盘档口用途");

            // HA
            Add<TopoMapPage>("HA-TOPO", "地形图图件");
            Add<AerialPhotoPage>("HA-AER", "航摄胶片、像片");
            Add<OtherMapPage>("HA-OTH", "其他资料");
            Add<HistoryArchiveBoxLedgerPage>("HA-BOX", "资料台账与资料处置·台账");
            Add<HistoryArchiveDisposalPage>("HA-DSP", "资料台账与资料处置·处置");
            Add<TopoMapEditDialog>("HA-TOPO-ED", "地形图图件编辑");
            Add<AerialPhotoEditDialog>("HA-AER-ED", "航摄胶片、像片编辑");
            Add<OtherMapEditDialog>("HA-OTH-ED", "其他资料编辑");
            Add<HistoryArchiveDisposalEditDialog>("HA-DSP-ED", "资料离库处置编辑");

            // SS
            Add<UserManagementPage>("SS-USER", "用户管理");
            Add<RoleSettingPage>("SS-ROLE", "角色概览");
            Add<ApprovalWorkflowSettingsPage>("SS-APPR", "审核审批");
            Add<ServerPathSettingPage>("SS-SRVPATH", "服务器路径设置");
            Add<DeptSettingPage>("SS-DEPT", "部门设置");
            Add<UserPreferencePage>("SS-PREF", "个人设置");
            Add<BusinessLogicSettingsPage>("SS-BIZ", "逾期设置");
            Add<AdvancedDataPage>("SS-ADV", "高级数据管理");
            Add<HelpPage>("SS-HELP", "操作手册");
            Add<DbOperationLogPage>("SS-DBLOG", "数据库操作日志");
            Add<UserSessionMonitorPage>("SS-ONLINE", "在线用户与登录记录");
            Add<UserEditDialog>("SS-USER-ED", "用户编辑");
            Add<ChangePasswordDialog>("SS-PWD", "修改密码");
            Add<ServerPathSettingEditDialog>("SS-SRVPATH-ED", "服务器路径编辑");
            Add<DeptEditDialog>("SS-DEPT-ED", "部门编辑");

            // PR
            Add<ProjectSettingPage>("PR-SET", "项目信息设置");
            Add<ProjectEditDialog>("PR-ED", "项目编辑");

            // SH
            Add<PrintPreviewWindow>("SH-PRINT", "打印预览");
            Add<ToDoNotificationWindow>("SH-TODO", "待办通知");
            Add<AttachmentPreviewWindow>("SH-ATT-PRE", "附件预览");
            Add<AttachmentViewChoiceDialog>("SH-ATT-CHO", "附件查看方式");
            Add<DocumentCameraCaptureDialog>("SH-CAM", "高影仪直拍");
            Add<TextDetailDialog>("SH-TXT", "文本详情");
            Add<LedgerIntroDialog>("SH-LDG-INTRO", "台账简介");
            Add<SheetSelectionDialog>("SH-SHEET", "工作表选择");
            Add<ImportOptionDialog>("SH-IMP", "导入选项");
            Add<OperationProgressOverlay>("SH-PROG", "操作进度");

            return map;
        }
    }

    /// <summary>
    /// 界面 Debug 短码条目。
    /// </summary>
    public sealed class UiDebugIdEntry
    {
        public UiDebugIdEntry(string code, string title, bool isRegistered)
        {
            Code = code;
            Title = title;
            IsRegistered = isRegistered;
        }

        /// <summary>短码，如 YA-OB-ED。</summary>
        public string Code { get; }

        /// <summary>中文简称。</summary>
        public string Title { get; }

        /// <summary>是否已在对照表登记。</summary>
        public bool IsRegistered { get; }

        /// <summary>角标悬停提示。</summary>
        public string ToolTipText => IsRegistered
            ? $"{Code}  {Title}"
            : $"{Code}（未登记，请补 UiDebugIdCatalog）";
    }
}
#endif
