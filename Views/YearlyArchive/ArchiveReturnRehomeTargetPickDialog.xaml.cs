using System.Windows;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveReturnRehomeTargetPickDialog : Window
    {
        public ArchiveReturnRehomeTargetPickDialog()
        {
            InitializeComponent();
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is ArchiveReturnRehomeTargetPickViewModel { SelectedOption: not null })
            {
                DialogResult = true;
            }
        }
    }
}
