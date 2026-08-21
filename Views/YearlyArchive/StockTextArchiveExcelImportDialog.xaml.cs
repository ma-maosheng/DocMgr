using System.Windows;
using System.Windows.Input;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Views.YearlyArchive
{
    public partial class StockTextArchiveExcelImportDialog : Window
    {
        public StockTextArchiveExcelImportDialog()
        {
            InitializeComponent();
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is StockTextArchiveExcelImportDialogViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            if (DataContext is StockTextArchiveExcelImportDialogViewModel viewModel
                && viewModel.CancelCommand.CanExecute(null))
            {
                viewModel.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
