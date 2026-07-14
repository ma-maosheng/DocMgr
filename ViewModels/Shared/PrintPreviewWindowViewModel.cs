using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using System;
using System.IO;
using System.IO.Packaging;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;

namespace DocMgr.ViewModels.Shared
{
    public class PrintPreviewWindowViewModel : ViewModelBase, IDisposable
    {
        private MemoryStream? _xpsStream;
        private Package? _xpsPackage;
        private Uri? _packageUri;
        private XpsDocument? _xpsDocument;
        private IDocumentPaginatorSource? _previewDocument;

        public PrintPreviewWindowViewModel(FlowDocument document, PrintPreviewExportOptions? exportOptions = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            ShowExportWord = exportOptions != null;
            if (exportOptions != null)
            {
                ExportWordCommand = new RelayCommand(async _ => await exportOptions.ExportAsync());
            }

            LoadDocument(document);
        }

        public IDocumentPaginatorSource? PreviewDocument
        {
            get => _previewDocument;
            private set => SetProperty(ref _previewDocument, value);
        }

        public bool ShowExportWord { get; }

        public ICommand? ExportWordCommand { get; }

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
