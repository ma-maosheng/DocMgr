using System.Windows;
using DocMgr.ViewModels.Shared;

namespace DocMgr.Views.Shared
{
    public partial class DocumentCameraCaptureDialog : Window
    {
        private bool _isInitialized;

        public DocumentCameraCaptureDialog()
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
            if (DataContext is DocumentCameraCaptureDialogViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            if (DataContext is DocumentCameraCaptureDialogViewModel viewModel)
            {
                await viewModel.ShutdownAsync();
            }
        }
    }
}
