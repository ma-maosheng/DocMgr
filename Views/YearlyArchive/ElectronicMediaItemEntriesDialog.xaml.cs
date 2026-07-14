using System.Windows;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ElectronicMediaItemEntriesDialog : Window
    {
        public ElectronicMediaItemEntriesDialog()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ElectronicMediaItemEntriesDialogViewModel viewModel)
            {
                viewModel.LoadCurrentPage();
            }
        }
    }
}
