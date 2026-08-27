using System.Windows;

namespace DocMgr.Views.HistoryArchive
{
    public partial class HistoryArchiveDisposalEditDialog : Window
    {
        private bool _layoutInitialized;

        public HistoryArchiveDisposalEditDialog()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_layoutInitialized)
            {
                Height = HistoryArchiveDisposalLayoutSupport.ResolveWindowHeight();
                _layoutInitialized = true;
            }

            UpdateSelectedItemsGridHeight();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdateSelectedItemsGridHeight();
            }
        }

        private void UpdateSelectedItemsGridHeight()
        {
            if (SelectedItemsGrid == null)
            {
                return;
            }

            ContentScrollViewer?.UpdateLayout();
            double viewportHeight = ContentScrollViewer?.ViewportHeight ?? 0;
            SelectedItemsGrid.Height = HistoryArchiveDisposalLayoutSupport.ResolveSelectedItemsGridHeight(
                ActualHeight,
                viewportHeight);
        }
    }
}
