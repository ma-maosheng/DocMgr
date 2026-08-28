using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Projects;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Cabinets;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Cabinets;
using DocMgr.ViewModels.HardDiskMedia;
using DocMgr.ViewModels.HistoryArchive;
using DocMgr.ViewModels.NetworkTransfer;
using DocMgr.ViewModels.Projects;
using DocMgr.ViewModels.Shared;
using DocMgr.ViewModels.SystemSettings;
using DocMgr.ViewModels.YearlyArchive;
using DocMgr.Views.Cabinets;
using DocMgr.Views.HardDiskMedia;
using DocMgr.Views.HistoryArchive;
using DocMgr.Views.NetworkTransfer;
using DocMgr.Views.Projects;
using DocMgr.Views.Shared;
using DocMgr.Views.SystemSettings;
using DocMgr.Views.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace DocMgr.Services.Shared
{
    public class DialogService : IDialogService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        public bool HasHardDiskMediaOutboundApplicationCommittedChanges { get; private set; }
        public bool HasHardDiskMediaApprovalCommittedChanges { get; private set; }
        public bool HasHardDiskMediumCommittedChanges { get; private set; }
        private readonly ICabinetArchiveBoxContentService _cabinetArchiveBoxContentService;

        public DialogService(IServiceScopeFactory serviceScopeFactory, ICabinetArchiveBoxContentService cabinetArchiveBoxContentService)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _cabinetArchiveBoxContentService = cabinetArchiveBoxContentService;
        }

        public string? OpenFileDialog(string filter, string title)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Title = title
            };

            return dialog.ShowDialog(GetOwnerWindow()) == true ? dialog.FileName : null;
        }

        public string? PickFolder(string title)
        {
            var dialog = new OpenFolderDialog
            {
                Title = title,
                Multiselect = false
            };

            return dialog.ShowDialog(GetOwnerWindow()) == true ? dialog.FolderName : null;
        }

        public IReadOnlyList<string>? PickFolders(string title, bool multiselect = true)
        {
            var dialog = new OpenFolderDialog
            {
                Title = title,
                Multiselect = multiselect
            };

            if (dialog.ShowDialog(GetOwnerWindow()) != true)
            {
                return null;
            }

            if (multiselect && dialog.FolderNames.Length > 0)
            {
                return dialog.FolderNames;
            }

            return string.IsNullOrWhiteSpace(dialog.FolderName) ? null : new[] { dialog.FolderName };
        }

        public IReadOnlyList<string>? PickFiles(string title, bool multiselect = true, string? filter = null)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Multiselect = multiselect,
                Filter = string.IsNullOrWhiteSpace(filter) ? "所有文件|*.*" : filter
            };

            return dialog.ShowDialog(GetOwnerWindow()) == true ? dialog.FileNames : null;
        }

        public string? SaveFileDialog(string filter, string title, string defaultFileName)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                Title = title,
                FileName = defaultFileName
            };

            return dialog.ShowDialog(GetOwnerWindow()) == true ? dialog.FileName : null;
        }

        public void ShowMessage(string message, string title = "提示")
        {
            MessageBox.Show(GetOwnerWindow(), message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowTextDetailDialog(string content, string title = "详情")
        {
            var dialog = new TextDetailDialog(title, content)
            {
                Owner = GetOwnerWindow()
            };
            dialog.ShowDialog();
        }

        public void ShowElectronicMediaItemEntriesDialog(
            string title,
            IReadOnlyList<ElectronicMediaItemEntryDisplayItem> entries,
            string summaryText)
        {
            var viewModel = new ElectronicMediaItemEntriesDialogViewModel(title, entries, summaryText);
            var dialog = new ElectronicMediaItemEntriesDialog
            {
                Owner = GetOwnerWindow(),
                DataContext = viewModel
            };

            void HandleRequestClose() => dialog.Close();
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
            }
        }

        public void ShowError(string message, string title = "错误")
        {
            MessageBox.Show(GetOwnerWindow(), message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool ShowConfirm(string message, string title = "确认")
        {
            return MessageBox.Show(GetOwnerWindow(), message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        /// <inheritdoc/>
        public void ShowSystemAttachmentView(SystemAttachment attachment)
        {
            ArgumentNullException.ThrowIfNull(attachment);

            if (attachment.FileContent == null || attachment.FileContent.Length == 0)
            {
                ShowMessage("附件内容为空，无法查看。");
                return;
            }

            try
            {
                if (SystemAttachmentViewSupport.IsImageAttachment(attachment))
                {
                    SystemAttachmentViewSupport.OpenWithDefaultApplication(attachment);
                    return;
                }

                string displayFileName = SystemAttachmentViewSupport.ResolveDisplayFileName(attachment);
                var choiceDialog = new AttachmentViewChoiceDialog(displayFileName)
                {
                    Owner = GetOwnerWindow()
                };

                if (choiceDialog.ShowDialog() != true || choiceDialog.Result == null)
                {
                    return;
                }

                switch (choiceDialog.Result.Value)
                {
                    case SystemAttachmentViewAction.OpenWithDefaultApp:
                        SystemAttachmentViewSupport.OpenWithDefaultApplication(attachment);
                        break;

                    case SystemAttachmentViewAction.SaveAs:
                        SaveSystemAttachmentAs(attachment);
                        break;
                }
            }
            catch (Exception ex)
            {
                ShowError($"查看附件失败：{ex.Message}");
            }
        }

        private void SaveSystemAttachmentAs(SystemAttachment attachment)
        {
            string displayFileName = SystemAttachmentViewSupport.ResolveDisplayFileName(attachment);
            string? savePath = SaveFileDialog("所有文件|*.*", "另存附件", displayFileName);
            if (string.IsNullOrWhiteSpace(savePath) || attachment.FileContent == null)
            {
                return;
            }

            File.WriteAllBytes(savePath, attachment.FileContent);
            ShowMessage("附件已保存。", "提示");
        }

        public SheetSelectionResult? ShowSheetSelectionDialog(
            List<string> sheetNames,
            string title = "选择Sheet",
            bool showExpandItemsByTextLineOption = false,
            string? expandItemsByTextLineContent = null,
            string? expandItemsByTextLineToolTip = null)
        {
            var dialog = new SheetSelectionDialog
            {
                Owner = GetOwnerWindow(),
                Title = string.IsNullOrWhiteSpace(title) ? "选择Sheet" : title.Trim()
            };
            var (scope, viewModel) = CreateScopedViewModel<SheetSelectionDialogViewModel>(
                new[] { typeof(IEnumerable<string>), typeof(IDialogService), typeof(bool), typeof(string), typeof(string) },
                sheetNames,
                this,
                showExpandItemsByTextLineOption,
                expandItemsByTextLineContent,
                expandItemsByTextLineToolTip);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => SetModalDialogResult(dialog, result);
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true
                    ? new SheetSelectionResult(viewModel.SelectedSheet, viewModel.ExpandItemsByTextLine)
                    : null;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public IReadOnlyList<HardDiskMedium>? ShowHardDiskMediumSelectionDialog(IEnumerable<string>? initialSelectedCodes = null, int? currentElectronicArchiveUnitId = null, string? selectionMode = null)
        {
            var dialog = new HardDiskMediumSelectionDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<HardDiskMediumSelectionDialogViewModel>(
                new[] { typeof(IEnumerable<string>), typeof(int?), typeof(string) },
                initialSelectedCodes,
                currentElectronicArchiveUnitId,
                selectionMode ?? string.Empty);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true ? viewModel.SelectedMedia : null;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public ImportMode? ShowImportOptionDialog(string tableName)
        {
            var dialog = new ImportOptionDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<ImportOptionDialogViewModel>(tableName);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true ? viewModel.SelectedMode : null;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public void SetBusyState(bool isBusy)
        {
            Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
        }

        /// <inheritdoc/>
        public IOperationProgressSession ShowOperationProgress(string title, string initialStatus)
        {
            var viewModel = new OperationProgressDialogViewModel
            {
                Title = string.IsNullOrWhiteSpace(title) ? "处理中" : title.Trim()
            };
            viewModel.ApplyIndeterminate(
                string.IsNullOrWhiteSpace(initialStatus) ? "请稍候…" : initialStatus.Trim());

            Window host = ResolveProgressHostWindow();
            return OperationProgressSession.Attach(host, viewModel);
        }

        private static Window ResolveProgressHostWindow()
        {
            Window? owner = GetOwnerWindow();
            if (owner is SheetSelectionDialog)
            {
                owner = owner.Owner;
            }

            return owner
                ?? Application.Current?.MainWindow
                ?? throw new InvalidOperationException("没有可显示进度条的父窗口。");
        }

        public bool ShowUserEditDialog(User? userToEdit)
        {
            var dialog = new UserEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<UserEditDialogViewModel>(new[] { typeof(User) }, userToEdit);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public bool ShowCabinetEditDialog(Cabinet cabinetToEdit)
        {
            var dialog = new CabinetEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<CabinetEditDialogViewModel>(cabinetToEdit, this);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public CabinetArchiveBoxPlacementMode? ShowCabinetArchiveBoxPlacementEditDialog(string title, string summary, CabinetArchiveBoxPlacementMode initialMode)
        {
            var dialog = new CabinetArchiveBoxPlacementEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<CabinetArchiveBoxPlacementEditDialogViewModel>(title, summary, initialMode);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true ? viewModel.SelectedMode : null;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public CabinetHardDiskSlotCategoryEditResult? ShowCabinetHardDiskSlotCategoryEditDialog(string title, string summary, string? initialCategoryName)
        {
            var dialog = new CabinetHardDiskSlotCategoryEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<CabinetHardDiskSlotCategoryEditDialogViewModel>(title, summary, initialCategoryName);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true ? viewModel.Result : null;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public CabinetArchiveSlotCategoryEditResult? ShowCabinetArchiveSlotCategoryEditDialog(string title, string summary, string? initialCategoryName)
        {
            var dialog = new CabinetArchiveSlotCategoryEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<CabinetArchiveSlotCategoryEditDialogViewModel>(title, summary, initialCategoryName);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true ? viewModel.Result : null;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public void ShowCabinetOpenDialog(CabinetOpenRequest request)
        {
            var dialog = new CabinetOpenDialog();
            var (scope, viewModel) = CreateScopedViewModel<CabinetOpenViewModel>(request, this);

            dialog.DataContext = viewModel;
            dialog.Owner = GetOwnerWindow();

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                viewModel.Detach();
                scope.Dispose();
            }
        }

        public void ShowCabinetSlotDetailDialog(CabinetOpenRequest request, CabinetSlotViewModel slot, bool canShowSlotZoom)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(slot);

            var dialog = new CabinetSlotDetailDialog();
            var viewModel = new CabinetSlotDetailViewModel(request, slot, this, canShowSlotZoom);

            dialog.DataContext = viewModel;
            dialog.Owner = GetOwnerWindow();

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
            }
        }

        public void ShowCabinetArchiveBoxContentDialog(string boxCode)
        {
            ShowArchiveContainerContentDialog(dialogFactory: (dialogService, searchService) =>
            {
                var contents = _cabinetArchiveBoxContentService.GetContents(boxCode);
                bool isYearlyArchiveBox = _cabinetArchiveBoxContentService.IsYearlyArchiveBoxAtLocation(boxCode);
                var occupationLockSummary = _cabinetArchiveBoxContentService.GetArchiveBoxOccupationLockSummary(boxCode);
                return CabinetArchiveBoxContentViewModel.CreateArchiveBoxView(
                    boxCode,
                    contents,
                    isYearlyArchiveBox,
                    dialogService,
                    searchService,
                    occupationLockSummary);
            }, errorMessage: "查看档案盒内容失败");
        }

        public void ShowCabinetArchiveBoxPendingReturnDetailDialog(string boxCode, string boxLabel, int pendingReturnCopyCount)
        {
            try
            {
                var details = _cabinetArchiveBoxContentService.GetSimulatedArchiveBoxPendingReturnDetails(boxCode);
                if (details.Count == 0)
                {
                    ShowMessage("该档案盒当前没有待还资料明细。", "提示");
                    return;
                }

                var dialog = new CabinetArchiveBoxPendingReturnDetailDialog
                {
                    DataContext = new CabinetArchiveBoxPendingReturnDetailViewModel(
                        boxCode,
                        boxLabel,
                        pendingReturnCopyCount,
                        details),
                    Owner = GetOwnerWindow()
                };

                if (dialog.DataContext is CabinetArchiveBoxPendingReturnDetailViewModel viewModel)
                {
                    void HandleRequestClose() => dialog.Close();
                    viewModel.RequestClose += HandleRequestClose;

                    try
                    {
                        dialog.ShowDialog();
                    }
                    finally
                    {
                        viewModel.RequestClose -= HandleRequestClose;
                    }
                }
                else
                {
                    dialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                ShowError($"查看待还资料详情失败：{ex.Message}");
            }
        }

        public void ShowCabinetElectronicBagContentDialog(int electronicArchiveUnitId)
        {
            ShowArchiveContainerContentDialog(dialogFactory: (dialogService, searchService) =>
            {
                var header = _cabinetArchiveBoxContentService.GetElectronicBagHeader(electronicArchiveUnitId);
                var contents = _cabinetArchiveBoxContentService.GetElectronicBagContents(electronicArchiveUnitId);
                var occupationLockSummary = _cabinetArchiveBoxContentService.GetElectronicBagOccupationLockSummary(electronicArchiveUnitId);
                string locationCode = header?.StorageLocation ?? string.Empty;
                if (string.IsNullOrWhiteSpace(locationCode))
                {
                    locationCode = header?.ElectronicArchiveNo ?? $"袋#{electronicArchiveUnitId}";
                }

                return CabinetArchiveBoxContentViewModel.CreateElectronicBagView(
                    locationCode,
                    contents,
                    header,
                    dialogService,
                    searchService,
                    occupationLockSummary);
            }, errorMessage: "查看电子介质袋内容失败");
        }

        public void ShowCabinetElectronicBagContentDialogByLocation(string storageLocationCode)
        {
            ShowArchiveContainerContentDialog(dialogFactory: (dialogService, searchService) =>
            {
                var header = _cabinetArchiveBoxContentService.GetElectronicBagHeaderByLocation(storageLocationCode);
                var contents = _cabinetArchiveBoxContentService.GetElectronicBagContentsByLocation(storageLocationCode);
                var occupationLockSummary = _cabinetArchiveBoxContentService.GetElectronicBagOccupationLockSummaryByLocation(storageLocationCode);
                return CabinetArchiveBoxContentViewModel.CreateElectronicBagView(
                    storageLocationCode,
                    contents,
                    header,
                    dialogService,
                    searchService,
                    occupationLockSummary);
            }, errorMessage: "查看电子介质袋内容失败");
        }

        private void ShowArchiveContainerContentDialog(
            Func<IDialogService, IArchiveFilingSearchService, CabinetArchiveBoxContentViewModel> dialogFactory,
            string errorMessage)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var searchService = scope.ServiceProvider.GetRequiredService<IArchiveFilingSearchService>();
                var dialog = new CabinetArchiveBoxContentDialog();
                var viewModel = dialogFactory(this, searchService);
                ShowArchiveContainerContentDialog(dialog, viewModel);
            }
            catch (Exception ex)
            {
                ShowError($"{errorMessage}：{ex.Message}");
            }
        }

        private void ShowArchiveContainerContentDialog(CabinetArchiveBoxContentDialog dialog, CabinetArchiveBoxContentViewModel viewModel)
        {
            dialog.DataContext = viewModel;
            dialog.Owner = GetOwnerWindow();

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
            }
        }

        public bool ShowTopoMapEditDialog(TopoMap mapToEdit)
        {
            var dialog = new TopoMapEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<TopoMapEditDialogViewModel>(mapToEdit);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public bool ShowAerialPhotoEditDialog(AerialPhoto photoToEdit)
        {
            var dialog = new AerialPhotoEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<AerialPhotoEditDialogViewModel>(photoToEdit);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public bool ShowOtherMapEditDialog(OtherMap mapToEdit)
        {
            var dialog = new OtherMapEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<OtherMapEditDialogViewModel>(mapToEdit);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public bool ShowProjectEditDialog(ProjectInfo? projectToEdit)
        {
            var dialog = new ProjectEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<ProjectEditDialogViewModel>(new[] { typeof(ProjectInfo) }, projectToEdit);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public bool ShowHardDiskMediumEditDialog(HardDiskMedium mediumToEdit, bool persistOnConfirm = true)
        {
            ArgumentNullException.ThrowIfNull(mediumToEdit);
            HasHardDiskMediumCommittedChanges = false;

            var dialog = new HardDiskMediumEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<HardDiskMediumEditDialogViewModel>(mediumToEdit, persistOnConfirm);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                dialog.ShowDialog();
                HasHardDiskMediumCommittedChanges = viewModel.HasCommittedChanges;
                return HasHardDiskMediumCommittedChanges;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public LocalPhysicalDiskInfo? ShowLocalPhysicalDiskPickerDialog()
        {
            var dialog = new LocalPhysicalDiskPickerDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<LocalPhysicalDiskPickerDialogViewModel>();

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true ? viewModel.SelectedDisk : null;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public DocumentCameraCaptureResult? ShowDocumentCameraCaptureDialog()
        {
            var dialog = new DocumentCameraCaptureDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<DocumentCameraCaptureDialogViewModel>();
            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true ? viewModel.CaptureResult : null;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                _ = viewModel.ShutdownAsync();
                scope.Dispose();
            }
        }

        public bool ShowHardDiskMediaOutboundApplicationEditDialog(HardDiskMediaApplication applicationToEdit)
        {
            ArgumentNullException.ThrowIfNull(applicationToEdit);
            HasHardDiskMediaOutboundApplicationCommittedChanges = false;

            var dialog = new HardDiskMediaOutboundApplicationEditDialog
            {
                Owner = GetOwnerWindow()
            };

            IServiceScope? scope = null;
            HardDiskMediaOutboundApplicationEditDialogViewModel? viewModel = null;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;

            try
            {
                (scope, viewModel) = CreateScopedViewModel<HardDiskMediaOutboundApplicationEditDialogViewModel>(
                    new[] { typeof(HardDiskMediaApplication) },
                    applicationToEdit);

                dialog.DataContext = viewModel;
                viewModel.RequestClose += HandleRequestClose;

                dialog.ShowDialog();
                HasHardDiskMediaOutboundApplicationCommittedChanges = viewModel.HasCommittedChanges;
                return HasHardDiskMediaOutboundApplicationCommittedChanges;
            }
            catch (Exception ex)
            {
                ShowError($"打开硬盘借出申请编辑窗口失败：{ex.Message}");
                return false;
            }
            finally
            {
                if (viewModel != null)
                {
                    viewModel.RequestClose -= HandleRequestClose;
                }

                scope?.Dispose();
            }
        }

        public bool ShowHardDiskDisposalEditDialog(HardDiskDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var dialog = new HardDiskDisposalEditDialog
            {
                Owner = GetOwnerWindow()
            };

            IServiceScope? scope = null;
            HardDiskDisposalEditDialogViewModel? viewModel = null;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;

            try
            {
                (scope, viewModel) = CreateScopedViewModel<HardDiskDisposalEditDialogViewModel>(
                    new[] { typeof(HardDiskDisposalRecord) },
                    record);

                dialog.DataContext = viewModel;
                viewModel.RequestClose += HandleRequestClose;
                dialog.ShowDialog();
                return viewModel.HasCommittedChanges;
            }
            catch (Exception ex)
            {
                ShowError($"打开硬盘离库处置窗口失败：{ex.Message}");
                return false;
            }
            finally
            {
                if (viewModel != null)
                {
                    viewModel.RequestClose -= HandleRequestClose;
                }

                scope?.Dispose();
            }
        }

        public bool ShowNetworkInboundEditDialog(NetworkInboundRecord record, NetworkTransferWorkspaceMode mode)
        {
            ArgumentNullException.ThrowIfNull(record);
            var dialog = new NetworkInboundEditDialog { Owner = GetOwnerWindow() };
            IServiceScope? scope = null;
            NetworkInboundEditDialogViewModel? viewModel = null;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            try
            {
                (scope, viewModel) = CreateScopedViewModel<NetworkInboundEditDialogViewModel>(
                    new[] { typeof(NetworkInboundRecord), typeof(NetworkTransferWorkspaceMode) },
                    record,
                    mode);
                dialog.DataContext = viewModel;
                viewModel.SetDialogMode(true);
                viewModel.RequestClose += HandleRequestClose;
                dialog.ShowDialog();
                return viewModel.HasCommittedChanges;
            }
            catch (Exception ex)
            {
                ShowError($"打开入网申请窗口失败：{ex.Message}");
                return false;
            }
            finally
            {
                if (viewModel != null) viewModel.RequestClose -= HandleRequestClose;
                scope?.Dispose();
            }
        }

        public bool ShowNetworkOutboundEditDialog(NetworkOutboundRecord record, NetworkTransferWorkspaceMode mode)
        {
            ArgumentNullException.ThrowIfNull(record);
            var dialog = new NetworkOutboundEditDialog { Owner = GetOwnerWindow() };
            IServiceScope? scope = null;
            NetworkOutboundEditDialogViewModel? viewModel = null;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            try
            {
                (scope, viewModel) = CreateScopedViewModel<NetworkOutboundEditDialogViewModel>(
                    new[] { typeof(NetworkOutboundRecord), typeof(NetworkTransferWorkspaceMode) },
                    record,
                    mode);
                dialog.DataContext = viewModel;
                viewModel.SetDialogMode(true);
                viewModel.RequestClose += HandleRequestClose;
                dialog.ShowDialog();
                return viewModel.HasCommittedChanges;
            }
            catch (Exception ex)
            {
                ShowError($"打开出网申请窗口失败：{ex.Message}");
                return false;
            }
            finally
            {
                if (viewModel != null) viewModel.RequestClose -= HandleRequestClose;
                scope?.Dispose();
            }
        }

        public bool ShowNetworkOnNetDisposalEditDialog(NetworkOnNetDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            var dialog = new NetworkOnNetDisposalEditDialog { Owner = GetOwnerWindow() };
            IServiceScope? scope = null;
            NetworkOnNetDisposalEditDialogViewModel? viewModel = null;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            try
            {
                (scope, viewModel) = CreateScopedViewModel<NetworkOnNetDisposalEditDialogViewModel>(
                    new[] { typeof(NetworkOnNetDisposalRecord) },
                    record);
                dialog.DataContext = viewModel;
                viewModel.RequestClose += HandleRequestClose;
                dialog.ShowDialog();
                return viewModel.HasCommittedChanges;
            }
            catch (Exception ex)
            {
                ShowError($"打开在网处置窗口失败：{ex.Message}");
                return false;
            }
            finally
            {
                if (viewModel != null) viewModel.RequestClose -= HandleRequestClose;
                scope?.Dispose();
            }
        }

        public bool ShowHistoryArchiveDisposalEditDialog(HistoryArchiveDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            var dialog = new HistoryArchiveDisposalEditDialog { Owner = GetOwnerWindow() };
            IServiceScope? scope = null;
            HistoryArchiveDisposalEditDialogViewModel? viewModel = null;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            try
            {
                (scope, viewModel) = CreateScopedViewModel<HistoryArchiveDisposalEditDialogViewModel>(
                    new[] { typeof(HistoryArchiveDisposalRecord) },
                    record);
                dialog.DataContext = viewModel;
                viewModel.RequestClose += HandleRequestClose;
                dialog.ShowDialog();
                return viewModel.HasCommittedChanges;
            }
            catch (Exception ex)
            {
                ShowError($"打开历史存档离库处置窗口失败：{ex.Message}");
                return false;
            }
            finally
            {
                if (viewModel != null) viewModel.RequestClose -= HandleRequestClose;
                scope?.Dispose();
            }
        }

        public bool ShowArchiveDisposalEditDialog(YearlyArchiveDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var dialog = new ArchiveDisposalEditDialog
            {
                Owner = GetOwnerWindow()
            };

            IServiceScope? scope = null;
            ArchiveDisposalEditDialogViewModel? viewModel = null;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;

            try
            {
                (scope, viewModel) = CreateScopedViewModel<ArchiveDisposalEditDialogViewModel>(
                    new[] { typeof(YearlyArchiveDisposalRecord) },
                    record);

                dialog.DataContext = viewModel;
                viewModel.RequestClose += HandleRequestClose;
                dialog.ShowDialog();
                return viewModel.HasCommittedChanges;
            }
            catch (Exception ex)
            {
                ShowError($"打开资料离库处置窗口失败：{ex.Message}");
                return false;
            }
            finally
            {
                if (viewModel != null)
                {
                    viewModel.RequestClose -= HandleRequestClose;
                }

                scope?.Dispose();
            }
        }

        public bool ShowHardDiskInventoryRegisterEditDialog(HardDiskInventoryRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var dialog = new HardDiskInventoryRegisterEditDialog
            {
                Owner = GetOwnerWindow()
            };

            IServiceScope? scope = null;
            HardDiskInventoryRegisterEditDialogViewModel? viewModel = null;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;

            try
            {
                (scope, viewModel) = CreateScopedViewModel<HardDiskInventoryRegisterEditDialogViewModel>(
                    new[] { typeof(HardDiskInventoryRegisterRecord) },
                    record);

                dialog.DataContext = viewModel;
                viewModel.RequestClose += HandleRequestClose;
                dialog.ShowDialog();
                return viewModel.HasCommittedChanges;
            }
            catch (Exception ex)
            {
                ShowError($"打开硬盘盘库登记窗口失败：{ex.Message}");
                return false;
            }
            finally
            {
                if (viewModel != null)
                {
                    viewModel.RequestClose -= HandleRequestClose;
                }

                scope?.Dispose();
            }
        }

        public bool ShowSimulatedArchiveInventoryRegisterEditDialog(YearlyArchiveInventoryRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var dialog = new SimulatedArchiveInventoryRegisterEditDialog
            {
                Owner = GetOwnerWindow()
            };

            IServiceScope? scope = null;
            SimulatedArchiveInventoryRegisterEditDialogViewModel? viewModel = null;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;

            try
            {
                (scope, viewModel) = CreateScopedViewModel<SimulatedArchiveInventoryRegisterEditDialogViewModel>(
                    new[] { typeof(YearlyArchiveInventoryRegisterRecord) },
                    record);

                dialog.DataContext = viewModel;
                viewModel.RequestClose += HandleRequestClose;
                dialog.ShowDialog();
                return viewModel.HasCommittedChanges;
            }
            catch (Exception ex)
            {
                ShowError($"打开模拟资料盘库登记窗口失败：{ex.Message}");
                return false;
            }
            finally
            {
                if (viewModel != null)
                {
                    viewModel.RequestClose -= HandleRequestClose;
                }

                scope?.Dispose();
            }
        }

        public bool ShowElectronicArchiveInventoryRegisterEditDialog(YearlyArchiveInventoryRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var dialog = new ElectronicArchiveInventoryRegisterEditDialog
            {
                Owner = GetOwnerWindow()
            };

            IServiceScope? scope = null;
            ElectronicArchiveInventoryRegisterEditDialogViewModel? viewModel = null;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;

            try
            {
                (scope, viewModel) = CreateScopedViewModel<ElectronicArchiveInventoryRegisterEditDialogViewModel>(
                    new[] { typeof(YearlyArchiveInventoryRegisterRecord) },
                    record);

                dialog.DataContext = viewModel;
                viewModel.RequestClose += HandleRequestClose;
                dialog.ShowDialog();
                return viewModel.HasCommittedChanges;
            }
            catch (Exception ex)
            {
                ShowError($"打开电子资料盘库登记窗口失败：{ex.Message}");
                return false;
            }
            finally
            {
                if (viewModel != null)
                {
                    viewModel.RequestClose -= HandleRequestClose;
                }

                scope?.Dispose();
            }
        }

        public bool ShowArchiveRegisterEditDialog(ArchiveRegisterWorkspaceMode workspaceMode, out int? committedRecordId, int? initialRecordId = null)
        {
            committedRecordId = null;
            var dialog = new ArchiveRegisterEditDialog
            {
                Owner = GetOwnerWindow()
            };

            var (scope, viewModel) = CreateScopedViewModel<ArchiveRegisterViewModel>();
            viewModel.SetWorkspaceMode(workspaceMode);
            viewModel.SetDialogMode(true);
            dialog.DataContext = viewModel;

            async void HandleLoaded(object? _, RoutedEventArgs __)
            {
                dialog.Loaded -= HandleLoaded;
                await viewModel.InitializeAsync(initialRecordId);
            }

            dialog.Loaded += HandleLoaded;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                dialog.ShowDialog();
                committedRecordId = viewModel.CurrentRecord?.Id;
                return viewModel.HasCommittedChanges;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                dialog.Loaded -= HandleLoaded;
                scope.Dispose();
            }
        }

        public void ShowArchiveRegisterApplicationViewDialog(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var dialog = new ArchiveRegisterApplicationViewDialog
            {
                Owner = GetOwnerWindow()
            };

            var (scope, viewModel) = CreateScopedViewModel<ArchiveRegisterApplicationViewDialogViewModel>(
                new[] { typeof(YearlyArchiveRegisterRecord) },
                record);
            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public int? ShowSearchResultSetPickDialog(IEnumerable<int>? excludedResultSetIds = null)
        {
            var dialog = new ArchiveSearchResultSetPickDialog
            {
                Owner = GetOwnerWindow()
            };

            var (scope, viewModel) = CreateScopedViewModel<ArchiveSearchResultSetPickDialogViewModel>(
                new[] { typeof(IEnumerable<int>) },
                excludedResultSetIds ?? Array.Empty<int>());

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true ? viewModel.SelectedResultSetId : null;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public bool ShowStockTextArchiveExcelImportDialog(IReadOnlyList<StockTextArchiveExcelBoxDraft> boxes)
        {
            var dialog = new StockTextArchiveExcelImportDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<StockTextArchiveExcelImportDialogViewModel>(
                new[] { typeof(IReadOnlyList<StockTextArchiveExcelBoxDraft>) },
                boxes ?? Array.Empty<StockTextArchiveExcelBoxDraft>());
            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true && viewModel.Imported;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public ProjectInfo? ShowYearProjectPickDialog(string year, IReadOnlyList<ProjectInfo> projects)
        {
            var viewModel = new StockHardDiskYearProjectPickDialogViewModel(year, projects);
            var dialog = new StockHardDiskYearProjectPickDialog
            {
                Owner = GetOwnerWindow(),
                DataContext = viewModel
            };

            return dialog.ShowDialog() == true ? viewModel.SelectedProject : null;
        }

        public void ShowArchiveDetailWindow(ArchiveDetailOpenRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.RegisterRecordId <= 0)
            {
                ShowMessage("无效的记录编号。", "提示");
                return;
            }

            var window = new ArchiveDetailWindow(
                request.RegisterRecordId,
                request.SearchHighlight,
                request.FilterPoolMediaKind,
                request.FilingFactId)
            {
                Owner = GetOwnerWindow()
            };

            window.Show();
        }

        public bool ShowArchiveOutboundEditDialog(
            ArchiveOutboundWorkspaceMode workspaceMode,
            out int? committedRecordId,
            int? initialRecordId = null,
            YearlyArchiveOutboundRecord? initialDraft = null)
        {
            committedRecordId = null;
            var dialog = new ArchiveOutboundEditDialog
            {
                Owner = GetOwnerWindow()
            };

            var (scope, viewModel) = CreateScopedViewModel<ArchiveOutboundViewModel>();
            viewModel.SetWorkspaceMode(workspaceMode);
            dialog.DataContext = viewModel;

            async void HandleLoaded(object? _, RoutedEventArgs __)
            {
                dialog.Loaded -= HandleLoaded;
                await viewModel.InitializeAsync(initialRecordId, initialDraft);
            }

            dialog.Loaded += HandleLoaded;
            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                dialog.ShowDialog();
                committedRecordId = viewModel.Record.Id > 0 ? viewModel.Record.Id : null;
                return viewModel.HasCommittedChanges;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                dialog.Loaded -= HandleLoaded;
                scope.Dispose();
            }
        }

        public void ShowArchiveOutboundApplicationViewDialog(YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var dialog = new ArchiveOutboundApplicationViewDialog
            {
                Owner = GetOwnerWindow()
            };

            var (scope, viewModel) = CreateScopedViewModel<ArchiveOutboundApplicationViewDialogViewModel>(
                new[] { typeof(YearlyArchiveOutboundRecord) },
                record);
            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public bool ShowHardDiskMediaApprovalEditDialog(HardDiskMediaApplication application, User? currentUser, out HardDiskMediaApprovalInput? approvalInput)
        {
            ArgumentNullException.ThrowIfNull(application);
            HasHardDiskMediaApprovalCommittedChanges = false;

            var dialog = new HardDiskMediaApprovalEditDialog
            {
                Owner = GetOwnerWindow()
            };

            var (scope, viewModel) = CreateScopedViewModel<HardDiskMediaApprovalEditDialogViewModel>(
                new[] { typeof(HardDiskMediaApplication), typeof(User) },
                application,
                currentUser);
            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => SetModalDialogResult(dialog, result);
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                dialog.ShowDialog();
                HasHardDiskMediaApprovalCommittedChanges = viewModel.HasCommittedChanges;
                approvalInput = viewModel.Result;
                return HasHardDiskMediaApprovalCommittedChanges;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public void ShowHardDiskMediaApplicationViewDialog(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);

            var dialog = new HardDiskMediaApplicationViewDialog
            {
                Owner = GetOwnerWindow()
            };

            var (scope, viewModel) = CreateScopedViewModel<HardDiskMediaApplicationViewDialogViewModel>(
                new[] { typeof(HardDiskMediaApplication) },
                application);
            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public bool ShowDeptEditDialog(Department? deptToEdit)
        {
            var dialog = new DeptEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<DeptEditDialogViewModel>(new[] { typeof(Department) }, deptToEdit);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public bool ShowRoleEditDialog(Role? roleToEdit)
        {
            var dialog = new RoleEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<RoleEditDialogViewModel>(new[] { typeof(Role) }, roleToEdit);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        public bool ShowServerPathSettingEditDialog(ServerPathSetting? settingToEdit)
        {
            var dialog = new ServerPathSettingEditDialog
            {
                Owner = GetOwnerWindow()
            };
            var (scope, viewModel) = CreateScopedViewModel<ServerPathSettingEditDialogViewModel>(
                new[] { typeof(ServerPathSetting) },
                settingToEdit);

            dialog.DataContext = viewModel;

            void HandleRequestClose(bool? result) => dialog.DialogResult = result;
            viewModel.RequestClose += HandleRequestClose;

            try
            {
                return dialog.ShowDialog() == true;
            }
            finally
            {
                viewModel.RequestClose -= HandleRequestClose;
                scope.Dispose();
            }
        }

        //private (IServiceScope Scope, TViewModel ViewModel) CreateScopedViewModel<TViewModel>(params object?[] parameters)
        //    where TViewModel : class
        //{
        //    var scope = _serviceScopeFactory.CreateScope();

        //    try
        //    {
        //        var viewModel = ActivatorUtilities.CreateInstance<TViewModel>(scope.ServiceProvider, parameters);
        //        return (scope, viewModel);
        //    }
        //    catch
        //    {
        //        scope.Dispose();
        //        throw;
        //    }
        //}


        private (IServiceScope Scope, TViewModel ViewModel) CreateScopedViewModel<TViewModel>(Type[] argumentTypes, params object?[] parameters)
            where TViewModel : class
        {
            ArgumentNullException.ThrowIfNull(argumentTypes);

            var scope = _serviceScopeFactory.CreateScope();

            try
            {
                var factory = ActivatorUtilities.CreateFactory(typeof(TViewModel), argumentTypes);
                var viewModel = (TViewModel)factory(scope.ServiceProvider, parameters ?? Array.Empty<object?>());
                return (scope, viewModel);
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }

        private (IServiceScope Scope, TViewModel ViewModel) CreateScopedViewModel<TViewModel>(params object?[] parameters)
            where TViewModel : class
        {
            var normalizedParameters = parameters ?? Array.Empty<object?>();

            var argumentTypes = normalizedParameters
                .Select(parameter => parameter?.GetType())
                .ToArray();

            if (argumentTypes.Any(type => type == null))
            {
                throw new InvalidOperationException($"无法根据 null 参数推断 {typeof(TViewModel).FullName} 的构造函数参数类型，请显式提供参数类型。");
            }

            return CreateScopedViewModel<TViewModel>(argumentTypes!, normalizedParameters);
        }


        private static Window? GetOwnerWindow()
        {
            var app = Application.Current;
            if (app == null)
            {
                return null;
            }

            // 待办提醒窗为非模态且曾设 Topmost，不宜作为业务弹窗 Owner，否则易出现“窗体在上、按钮不可点”。
            var visibleWindows = app.Windows.OfType<Window>()
                .Where(window => window.IsVisible && window is not ToDoNotificationWindow)
                .ToList();
            if (visibleWindows.Count == 0)
            {
                return app.MainWindow;
            }

            // async 续体后模态窗常失去 IsActive，若仍用主窗口作 Owner，MessageBox/文件框会被挡在后面，表现为“卡死”。
            var activeWindow = visibleWindows.FirstOrDefault(window => window.IsActive);
            if (activeWindow != null)
            {
                return activeWindow;
            }

            return visibleWindows.LastOrDefault() ?? app.MainWindow;
        }

        /// <summary>
        /// 安全设置模态窗 DialogResult。最大化时先还原再关闭，避免仅还原不关闭；
        /// 若 DialogResult 已是目标值但仍可见，则强制 Close。
        /// </summary>
        private static void SetModalDialogResult(Window dialog, bool? result)
        {
            ArgumentNullException.ThrowIfNull(dialog);

            if (dialog.WindowState == WindowState.Maximized)
            {
                dialog.WindowState = WindowState.Normal;
            }

            try
            {
                if (dialog.DialogResult != result)
                {
                    dialog.DialogResult = result;
                }
            }
            catch (InvalidOperationException)
            {
                dialog.Close();
                return;
            }

            if (dialog.IsVisible)
            {
                dialog.Close();
            }
        }
    }
}