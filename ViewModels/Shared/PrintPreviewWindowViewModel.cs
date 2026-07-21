using DocMgr.ViewModels.Base;
using DocMgr.Services.Shared;
using Microsoft.Win32;
using System;
using System.IO;
using System.IO.Packaging;
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
        private MemoryStream? _xpsStream;
        private Package? _xpsPackage;
        private Uri? _packageUri;
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

        private void LoadDocument(FlowDocument document)
        {
            DisposeInternal();

            _xpsStream = new MemoryStream();
            _xpsPackage = Package.Open(_xpsStream, FileMode.Create, FileAccess.ReadWrite);

            var uriString = $"pack://temp_print_preview_{Guid.NewGuid():N}.xps";
            _packageUri = new Uri(uriString);

            PackageStore.AddPackage(_packageUri, _xpsPackage);

            _xpsDocument = new XpsDocument(_xpsPackage, CompressionOption.SuperFast, uriString);
            var writer = XpsDocument.CreateXpsDocumentWriter(_xpsDocument);
            writer.Write(((IDocumentPaginatorSource)document).DocumentPaginator);

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

            if (_packageUri != null)
            {
                PackageStore.RemovePackage(_packageUri);
                _packageUri = null;
            }

            _xpsDocument?.Close();
            _xpsDocument = null;

            _xpsPackage?.Close();
            _xpsPackage = null;

            _xpsStream?.Dispose();
            _xpsStream = null;
        }
    }
}
