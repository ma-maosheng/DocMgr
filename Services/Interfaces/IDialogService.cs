using System.Collections.Generic;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Cabinets;
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
        /// 查看系统附件：图像在程序内预览，其他类型提供明确的打开/另存为选项。
        /// </summary>
        void ShowSystemAttachmentView(SystemAttachment attachment);

        string? ShowSheetSelectionDialog(List<string> sheetNames, string title = "选择Sheet");
        ImportMode? ShowImportOptionDialog(string tableName);

        void SetBusyState(bool isBusy);

        bool ShowUserEditDialog(User? userToEdit);
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
        /// 选择一个或多个硬盘介质。
        /// </summary>
        IReadOnlyList<HardDiskMedium>? ShowHardDiskMediumSelectionDialog(IEnumerable<string>? initialSelectedCodes = null, int? currentElectronicArchiveUnitId = null, string? selectionMode = null);
        bool ShowHardDiskMediaOutboundApplicationEditDialog(HardDiskMediaApplication applicationToEdit);
        bool ShowHardDiskDisposalEditDialog(HardDiskDisposalRecord record);
        bool ShowNetworkInboundEditDialog(NetworkInboundRecord record, NetworkTransferWorkspaceMode mode);
        bool ShowNetworkOutboundEditDialog(NetworkOutboundRecord record, NetworkTransferWorkspaceMode mode);
        bool ShowNetworkOnNetDisposalEditDialog(NetworkOnNetDisposalRecord record);
        bool ShowNetworkProcessedOutputEditDialog();
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
        void ShowArchiveDetailWindow(ArchiveDetailOpenRequest request);
        bool ShowDeptEditDialog(Department? deptToEdit);
        bool ShowRoleEditDialog(Role? roleToEdit);
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
