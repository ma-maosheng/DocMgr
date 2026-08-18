using System.Windows;

namespace DocMgr.Views.NetworkTransfer
{
    public partial class NetworkOnNetDisposalEditDialog : Window
    {
        private bool _layoutInitialized;

        public NetworkOnNetDisposalEditDialog()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_layoutInitialized)
            {
                Height = NetworkOnNetDisposalLayoutSupport.ResolveWindowHeight();
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
            SelectedItemsGrid.Height = NetworkOnNetDisposalLayoutSupport.ResolveSelectedItemsGridHeight(
                ActualHeight,
                viewportHeight);
        }
    }
}
