using DocMgr.ViewModels.Shared;
using System;
using System.Windows;
using System.Windows.Documents;

namespace DocMgr.Views.Shared
{
    public partial class PrintPreviewWindow : Window
    {
        public PrintPreviewWindow(FlowDocument document, PrintPreviewExportOptions? exportOptions = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            InitializeComponent();
            DataContext = new PrintPreviewWindowViewModel(document, exportOptions);
            Closed += PrintPreviewWindow_Closed;
        }

        private void PrintPreviewWindow_Closed(object? sender, EventArgs e)
        {
            Closed -= PrintPreviewWindow_Closed;

            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
