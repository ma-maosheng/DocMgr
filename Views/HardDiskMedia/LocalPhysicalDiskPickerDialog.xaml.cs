using System.Windows;
using DocMgr.ViewModels.HardDiskMedia;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class LocalPhysicalDiskPickerDialog : Window
    {
        private bool _isInitialized;

        public LocalPhysicalDiskPickerDialog()
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
            if (DataContext is LocalPhysicalDiskPickerDialogViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }
    }
}
