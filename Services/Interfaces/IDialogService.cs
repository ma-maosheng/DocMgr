using System.Collections.Generic;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Cabinets;
using DocMgr.ViewModels.Shared;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 桌面对话框与窗口交互服务契约：消息提示、确认、输入、文件选择及自定义弹窗。
    /// </summary>
    public interface IDialogService
    {
        string? OpenFileDialog(string filter, string title);
        string? PickFolder(string title);
        IReadOnlyList<string>? PickFolders(string title, bool multiselect = true);
        IReadOnlyList<string>? PickFiles(string title, bool multiselect = true, string? filter = null);
        string? SaveFileDialog(string filter, string title, string defaultFileName);
        void ShowElectronicMediaItemEntriesDialog(string title, IReadOnlyList<ElectronicMediaItemEntryDisplayItem> entries, string summaryText);
        void ShowMessage(string message, string title = "提示");
        void ShowTextDetailDialog(string content, string title = "详情");
        void ShowError(string message, string title = "错误");
        bool ShowConfirm(string message, string title = "确认");

        /// <summary>
        /// 查看系统附件：图像用 Windows 照片查看器打开（跳过百度网盘「智能看图」）；其他类型提供打开/另存为选项。
        /// </summary>
        void ShowSystemAttachmentView(SystemAttachment attachment);

        /// <summary>
        /// 选择工作表。取消返回 null。结果中含「以文本行为单位拆分」勾选状态。
        /// </summary>
        /// <param name="showExpandItemsByTextLineOption">是否显示按文本行拆分勾选。</param>
        /// <param name="expandItemsByTextLineContent">勾选框文案；空则用默认（资料子项）。</param>
        /// <param name="expandItemsByTextLineToolTip">勾选框提示；空则用默认。</param>
        SheetSelectionResult? ShowSheetSelectionDialog(
            List<string> sheetNames,
            string title = "选择Sheet",
            bool showExpandItemsByTextLineOption = false,
            string? expandItemsByTextLineContent = null,
            string? expandItemsByTextLineToolTip = null);
        ImportMode? ShowImportOptionDialog(string tableName);

        void SetBusyState(bool isBusy);

        /// <summary>
        /// 在调用方父窗口上覆盖进度条（Excel 导入等）。须在 using 中使用，结束时自动移除。
        /// </summary>
        IOperationProgressSession ShowOperationProgress(string title, string initialStatus);

        bool ShowUserEditDialog(User? userToEdit);

        /// <summary>
        /// 当前用户修改登录密码。取消或失败返回 false。
        /// </summary>
        /// <param name="isMandatory">为 true 时表示登录后必须先改密，取消将无法进入系统。</param>
        bool ShowChangePasswordDialog(bool isMandatory = false);
        bool ShowCabinetEditDialog(Cabinet cabinetToEdit);
        CabinetArchiveBoxPlacementMode? ShowCabinetArchiveBoxPlacementEditDialog(string title, string summary, CabinetArchiveBoxPlacementMode initialMode);
        CabinetHardDiskSlotCategoryEditResult? ShowCabinetHardDiskSlotCategoryEditDialog(string title, string summary, string? initialCategoryName);
        CabinetArchiveSlotCategoryEditResult? ShowCabinetArchiveSlotCategoryEditDialog(string title, string summary, string? initialCategoryName);
        void ShowCabinetOpenDialog(CabinetOpenRequest request);
        void ShowCabinetSlotDetailDialog(CabinetOpenRequest request, CabinetSlotViewModel slot, bool canShowSlotZoom);
        void ShowCabinetArchiveBoxContentDialog(string boxCode);

        /// <summary>打开年度模拟档案盒待还资料追溯详情窗体。</summary>
        void ShowCabinetArchiveBoxPendingReturnDetailDialog(string boxCode, string boxLabel, int pendingReturnCopyCount);

        /// <summary>打开电子介质袋内容窗体（按袋 Id）。</summary>
        void ShowCabinetElectronicBagContentDialog(int electronicArchiveUnitId);

        /// <summary>打开电子介质袋内容窗体（按物理位置编号）。</summary>
        void ShowCabinetElectronicBagContentDialogByLocation(string storageLocationCode);
        bool ShowTopoMapEditDialog(TopoMap mapToEdit);
        bool ShowAerialPhotoEditDialog(AerialPhoto photoToEdit);
        bool ShowOtherMapEditDialog(OtherMap mapToEdit);
        bool ShowProjectEditDialog(ProjectInfo? projectToEdit);
        bool ShowHardDiskMediumEditDialog(HardDiskMedium mediumToEdit, bool persistOnConfirm = true);

        /// <summary>
        /// 从本机物理磁盘中选择一块，供硬盘介质半自动登记回填。取消时返回 null。
        /// </summary>
        LocalPhysicalDiskInfo? ShowLocalPhysicalDiskPickerDialog();

        /// <summary>
        /// 打开高影仪直拍窗口。确认后返回 JPEG 内容；取消返回 null。
        /// </summary>
        DocumentCameraCaptureResult? ShowDocumentCameraCaptureDialog();

        /// <summary>
        /// 选择一个或多个硬盘介质。
        /// </summary>
        IReadOnlyList<HardDiskMedium>? ShowHardDiskMediumSelectionDialog(IEnumerable<string>? initialSelectedCodes = null, int? currentElectronicArchiveUnitId = null, string? selectionMode = null);
        bool ShowHardDiskMediaOutboundApplicationEditDialog(HardDiskMediaApplication applicationToEdit);
        bool ShowHardDiskDisposalEditDialog(HardDiskDisposalRecord record);
        bool ShowNetworkInboundEditDialog(NetworkInboundRecord record, NetworkTransferWorkspaceMode mode);
        bool ShowNetworkOutboundEditDialog(NetworkOutboundRecord record, NetworkTransferWorkspaceMode mode);
        bool ShowNetworkOnNetDisposalEditDialog(NetworkOnNetDisposalRecord record);
        bool ShowHistoryArchiveDisposalEditDialog(HistoryArchiveDisposalRecord record);
        bool ShowArchiveDisposalEditDialog(YearlyArchiveDisposalRecord record);
        bool ShowHardDiskInventoryRegisterEditDialog(HardDiskInventoryRegisterRecord record);
        bool ShowSimulatedArchiveInventoryRegisterEditDialog(YearlyArchiveInventoryRegisterRecord record);
        bool ShowElectronicArchiveInventoryRegisterEditDialog(YearlyArchiveInventoryRegisterRecord record);
        bool ShowArchiveRegisterEditDialog(ArchiveRegisterWorkspaceMode workspaceMode, out int? committedRecordId, int? initialRecordId = null);

        /// <summary>
        /// 以只读方式查看资料建档申请单信息。
        /// </summary>
        void ShowArchiveRegisterApplicationViewDialog(YearlyArchiveRegisterRecord record);
        bool ShowArchiveOutboundEditDialog(
            ArchiveOutboundWorkspaceMode workspaceMode,
            out int? committedRecordId,
            int? initialRecordId = null,
            YearlyArchiveOutboundRecord? initialDraft = null);

        /// <summary>
        /// 以只读方式查看资料借出申请单信息。
        /// </summary>
        void ShowArchiveOutboundApplicationViewDialog(YearlyArchiveOutboundRecord record);
        int? ShowSearchResultSetPickDialog(IEnumerable<int>? excludedResultSetIds = null);

        /// <summary>
        /// 查看指定年度已有项目；确认采用后返回所选项目，仅关闭时返回 null。
        /// </summary>
        ProjectInfo? ShowYearProjectPickDialog(string year, IReadOnlyList<ProjectInfo> projects);
        void ShowArchiveDetailWindow(ArchiveDetailOpenRequest request);
        bool ShowDeptEditDialog(Department? deptToEdit);
        bool ShowRoleEditDialog(Role? roleToEdit);
        /// <summary>
        /// 存档文本直办 Excel 导入预览；成功导入至少一盒时返回 true。
        /// </summary>
        bool ShowStockTextArchiveExcelImportDialog(IReadOnlyList<StockTextArchiveExcelBoxDraft> boxes);

        bool ShowServerPathSettingEditDialog(ServerPathSetting? settingToEdit);
        bool ShowHardDiskMediaApprovalEditDialog(HardDiskMediaApplication application, User? currentUser, out HardDiskMediaApprovalInput? approvalInput);

        /// <summary>
        /// 以只读方式查看硬盘介质申请单信息。
        /// </summary>
        void ShowHardDiskMediaApplicationViewDialog(HardDiskMediaApplication application);
        bool HasHardDiskMediaOutboundApplicationCommittedChanges { get; }
        bool HasHardDiskMediaApprovalCommittedChanges { get; }
        bool HasHardDiskMediumCommittedChanges { get; }
    }
}
