using DocMgr.ViewModels.HardDiskMedia;
using System.Windows;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskMediumEditDialog : Window
    {
        public HardDiskMediumEditDialog()
        {
            InitializeComponent();
            Loaded += HardDiskMediumEditDialog_Loaded;
        }

        private async void HardDiskMediumEditDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HardDiskMediumEditDialog_Loaded;

            if (DataContext is HardDiskMediumEditDialogViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }
    }
}
