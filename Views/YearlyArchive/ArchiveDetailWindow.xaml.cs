using DocMgr.ViewModels.YearlyArchive;
using System.Windows;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveDetailWindow : Window
    {
        public ArchiveDetailWindow(
            int recordId,
            ArchiveDetailHighlightContext? searchHighlight = null,
            string? filterPoolMediaKind = null,
            int? filingFactId = null)
        {
            InitializeComponent();

            ContentFrame.Navigate(new ArchiveDetailPage(recordId, searchHighlight, filterPoolMediaKind, filingFactId));
        }
    }
}
