using System.Windows;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveSearchResultSetPickDialog : Window
    {
        private bool _isInitialized;

        public ArchiveSearchResultSetPickDialog()
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
            if (DataContext is ArchiveSearchResultSetPickDialogViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }
    }
}
