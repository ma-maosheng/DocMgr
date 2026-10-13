using DocMgr.Views.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Shared;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    // 使用 partial 关键字
    public partial class ArchiveRegisterViewModel : ViewModelBase
    {
        private async Task PrintApplicationAsync()
        {
            try
            {
                var data = await CollectPrintDataAsync();
                string blankDt = "______年___月___日";

                // 2026-02-26 修改：申请人打印申请时，使用审批页的格式，但将审批签字内容置空。
                data.DeptHeadApproval = $"|{blankDt}";
                data.ProdFull = $"||{blankDt}";
                data.RndFull = $"||{blankDt}";
                data.ArchiveDeputyPresidentFull = $"||{blankDt}";
                data.ProductionVicePresidentFull = $"||{blankDt}";
                data.DeliverFull = $"|{blankDt}";
                data.AdminFull = $"|{blankDt}";
                data.ProdOpinion = "|";
                data.RndOpinion = "|";

                // 修改点1：传入 true，表示这是“打印申请”模式
                var doc = ArchiveRegisterPrintDocumentFactory.Create(data, isApplicationPrint: true);
                ShowArchiveRegisterPrintPreview(doc, data);
                // 申请打印后不关窗（与出网/入网一致）；关窗会触发工作台反复重开。
                MarkCommitted();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("打印生成失败: " + ex.Message);
            }
        }

        private async void PrintApprovalPage()
        {
            try
            {
                if (CurrentRecord == null)
                {
                    _dialogService.ShowMessage("当前记录为空，无法打印。");
                    return;
                }

                if (!CurrentRecord.IsArchived)
                {
                    _dialogService.ShowMessage("请先执行“确认办结”，再打印交接单。");
                    return;
                }

                try
                {
                    CurrentRecord.MediaEntries = BuildMediaEntries();
                    await _archiveRegisterService.SaveOrUpdateAsync(CurrentRecord);
                    OnPropertyChanged(nameof(CurrentRecord));
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError("保存交接信息失败，已取消打印： " + ex.Message);
                    return;
                }

                var data = await CollectPrintDataAsync();

                // 传入 false（默认）表示“审批/交接单打印”模式
                var doc = ArchiveRegisterPrintDocumentFactory.Create(data, isApplicationPrint: false);

                ShowArchiveRegisterPrintPreview(doc, data);

                try
                {
                    int printCount = CurrentRecord.PrintCount;
                    DateTime? firstPrintedAt = CurrentRecord.FirstPrintedAt;
                    DateTime? lastPrintedAt = CurrentRecord.LastPrintedAt;
                    ApprovalSignatureDateSupport.RecordPrint(ref printCount, ref firstPrintedAt, ref lastPrintedAt);
                    CurrentRecord.PrintCount = printCount;
                    CurrentRecord.FirstPrintedAt = firstPrintedAt;
                    CurrentRecord.LastPrintedAt = lastPrintedAt;
                    await _archiveRegisterService.SaveOrUpdateAsync(CurrentRecord);
                    OnPropertyChanged(nameof(CurrentRecord));
                    NotifyShellAliasPropertiesChanged();
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError("打印计数保存失败：" + ex.Message);
                }

                if (IsDialogMode && WorkspaceMode == ArchiveRegisterWorkspaceMode.Approval)
                {
                    MarkCommitted();
                    RequestClose?.Invoke(true);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("打印生成失败: " + ex.Message);
            }
        }

        private async Task<ArchiveRegisterPrintData> CollectPrintDataAsync()
        {
            if (CurrentRecord == null)
            {
                return new ArchiveRegisterPrintData();
            }

            // 已提交等只读态：用库中已加载的介质图，避免 BuildMediaEntries→Ensure* 再改写工具栏/触发借出列表刷新。
            IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries =
                !CanEditForm
                && CurrentRecord.MediaEntries is { Count: > 0 } persisted
                    ? persisted
                    : BuildMediaEntries();

            var data = await _archiveRegisterService.BuildPrintDataAsync(CurrentRecord, mediaEntries);
            data.OpticalDiscLedgerSummary =
                await _archiveRegisterService.BuildOpticalDiscLedgerSummaryAsync(CurrentRecord);
            return data;
        }

        private void ShowArchiveRegisterPrintPreview(System.Windows.Documents.FlowDocument document, ArchiveRegisterPrintData data)
        {
            var exportOptions = new PrintPreviewExportOptions
            {
                ExportAsync = () => ExportArchiveRegisterWordAsync(data)
            };
            var previewWin = new PrintPreviewWindow(document, exportOptions)
            {
                Owner = Application.Current.MainWindow
            };
            previewWin.ShowDialog();
        }

        private Task ExportArchiveRegisterWordAsync(ArchiveRegisterPrintData data)
        {
            try
            {
                string defaultName = string.IsNullOrWhiteSpace(data.FormNo)
                    ? "年度资料入档申请审批单.docx"
                    : $"{data.FormNo}.docx";
                string? path = _dialogService.SaveFileDialog(
                    "Word 文档|*.docx",
                    "导出 Word",
                    defaultName);
                if (string.IsNullOrWhiteSpace(path))
                {
                    return Task.CompletedTask;
                }

                if (!path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".docx";
                }

                _archiveRegisterWordExportService.ExportToFile(data, path);
                WordExportOpenPromptSupport.NotifySavedAndOfferOpen(path, _dialogService);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出 Word 失败：" + ex.Message);
            }

            return Task.CompletedTask;
        }
    }
}
