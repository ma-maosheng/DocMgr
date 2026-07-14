using System.Windows;
using DocMgr.ViewModels.HardDiskMedia;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskMediumSelectionDialog : Window
    {
        private bool _isInitialized;

        public HardDiskMediumSelectionDialog()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            if (DataContext is HardDiskMediumSelectionDialogViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }
    }
}
