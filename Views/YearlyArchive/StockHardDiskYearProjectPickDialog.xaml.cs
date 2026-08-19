using System.Windows;
using System.Windows.Input;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Views.YearlyArchive
{
    public partial class StockHardDiskYearProjectPickDialog : Window
    {
        public StockHardDiskYearProjectPickDialog()
        {
            InitializeComponent();
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            TryAcceptSelection();
        }

        private void OnGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TryAcceptSelection();
        }

        private void TryAcceptSelection()
        {
            if (DataContext is StockHardDiskYearProjectPickDialogViewModel { SelectedProject: not null })
            {
                DialogResult = true;
            }
        }
    }
}
