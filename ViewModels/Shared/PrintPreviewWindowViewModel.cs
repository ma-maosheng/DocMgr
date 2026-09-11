using DocMgr.ViewModels.Base;
using DocMgr.Services.Shared;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;

namespace DocMgr.ViewModels.Shared
{
    public class PrintPreviewWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly FlowDocument _sourceDocument;
        private readonly PrintPreviewExportOptions? _exportOptions;
        private string? _xpsFilePath;
        private XpsDocument? _xpsDocument;
        private IDocumentPaginatorSource? _previewDocument;

        public PrintPreviewWindowViewModel(FlowDocument document, PrintPreviewExportOptions? exportOptions = null)
        {
            ArgumentNullException.ThrowIfNull(document);

            _sourceDocument = document;
            _exportOptions = exportOptions;
            ShowExportWord = true;
            ExportWordCommand = new RelayCommand(_ => ExportWord());

            LoadDocument(document);
        }

        public IDocumentPaginatorSource? PreviewDocument
        {
            get => _previewDocument;
            private set => SetProperty(ref _previewDocument, value);
        }

        /// <summary>所有打印预览均提供导出 Word。</summary>
        public bool ShowExportWord { get; }

        public ICommand ExportWordCommand { get; }

        private void ExportWord()
        {
            try
            {
                if (_exportOptions?.ExportAsync != null)
                {
                    _exportOptions.ExportAsync().GetAwaiter().GetResult();
                    return;
                }

                ExportFlowDocumentAsWord();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "导出 Word 失败：" + ex.Message,
                    "导出 Word",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportFlowDocumentAsWord()
        {
            string defaultName = string.IsNullOrWhiteSpace(_exportOptions?.DefaultFileName)
                ? FlowDocumentWordExportSupport.SuggestDefaultFileName(_sourceDocument)
                : _exportOptions!.DefaultFileName!.Trim();

            if (!defaultName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                defaultName += ".docx";
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出 Word",
                Filter = "Word 文档|*.docx",
                FileName = defaultName,
                AddExtension = true,
                DefaultExt = ".docx"
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return;
            }

            string path = dialog.FileName;
            if (!path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                path += ".docx";
            }

            FlowDocumentWordExportSupport.ExportToFile(_sourceDocument, path);
            MessageBox.Show(
                $"Word 文档已保存：\n{path}",
                "导出 Word",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// 写入临时 .xps 文件后按只读方式打开回读。
        /// 2026-08 桌面运行时 servicing 更新后，XPS 从内存包（PackageStore + MemoryStream）
        /// 回读时解析内嵌 ODTTF 字体会抛 FormatException（FixedDocument 初始化失败），
        /// 从文件打开回读不受影响，故改用临时文件承载。
        /// </summary>
        private void LoadDocument(FlowDocument document)
        {
            DisposeInternal();

            _xpsFilePath = Path.Combine(Path.GetTempPath(), $"docmgr-print-preview-{Guid.NewGuid():N}.xps");

            using (var writeDocument = new XpsDocument(_xpsFilePath, FileAccess.ReadWrite))
            {
                var writer = XpsDocument.CreateXpsDocumentWriter(writeDocument);
                writer.Write(((IDocumentPaginatorSource)document).DocumentPaginator);
            }

            _xpsDocument = new XpsDocument(_xpsFilePath, FileAccess.Read);
            PreviewDocument = _xpsDocument.GetFixedDocumentSequence();
        }

        public void Dispose()
        {
            DisposeInternal();
            GC.SuppressFinalize(this);
        }

        private void DisposeInternal()
        {
            PreviewDocument = null;

            _xpsDocument?.Close();
            _xpsDocument = null;

            if (_xpsFilePath != null)
            {
                try
                {
                    File.Delete(_xpsFilePath);
                }
                catch (IOException)
                {
                    // 文件仍被占用时忽略，留在临时目录由系统清理
                }
                _xpsFilePath = null;
            }
        }
    }
}
