using DocMgr.Views.Shared;
using System;
using System.Threading.Tasks;
using System.Windows;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    // 使用 partial 关键字
    public partial class ArchiveRegisterViewModel : ViewModelBase
    {
        private void PrintApplication()
        {
            try
            {
                var data = CollectPrintData();
                string blankDt = "______年___月___日";

                // 2026-02-26 修改：申请人打印申请时，使用审批页的格式，但将审批签字内容置空。
                data.DeptLeaderApproval = $"|{blankDt}";
                data.ProdFull = $"||{blankDt}";
                data.RndFull = $"||{blankDt}";
                data.DeputyFull = $"||{blankDt}";
                data.DeliverFull = $"|{blankDt}";
                data.AdminFull = $"|{blankDt}";
                data.ProdOpinion = "|";
                data.RndOpinion = "|";

                // 修改点1：传入 true，表示这是“打印申请”模式
                var doc = ArchiveRegisterPrintDocumentFactory.Create(data, isApplicationPrint: true);
                ShowArchiveRegisterPrintPreview(doc, data);

                if (IsDialogMode && WorkspaceMode == ArchiveRegisterWorkspaceMode.Application)
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

                var data = CollectPrintData();

                // 传入 false（默认）表示“审批/交接单打印”模式
                var doc = ArchiveRegisterPrintDocumentFactory.Create(data, isApplicationPrint: false);

                ShowArchiveRegisterPrintPreview(doc, data);

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

        private ArchiveRegisterPrintData CollectPrintData()
        {
            if (CurrentRecord == null) return new ArchiveRegisterPrintData();

            var sourceType = string.IsNullOrWhiteSpace(CurrentRecord.SourceType)
                ? (SelectedSourceType ?? ArchiveRegisterDomainValues.SourceTypeInternal)
                : CurrentRecord.SourceType;

            CurrentRecord.SourceType = sourceType;

            return _archiveRegisterService.BuildPrintData(
                CurrentRecord,
                SelectedSourceType,
                BuildMediaEntries());
        }

        private async Task<ArchiveRegisterPrintData> CollectPrintDataAsync()
        {
            var data = CollectPrintData();
            if (CurrentRecord == null)
            {
                return data;
            }

            data.OpticalDiscLedgerSummary = await _archiveRegisterService.BuildOpticalDiscLedgerSummaryAsync(CurrentRecord);
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
                _dialogService.ShowMessage($"Word 文档已保存：\n{path}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出 Word 失败：" + ex.Message);
            }

            return Task.CompletedTask;
        }
    }
}
