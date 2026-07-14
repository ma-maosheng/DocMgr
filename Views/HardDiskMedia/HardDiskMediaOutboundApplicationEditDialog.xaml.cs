using DocMgr.ViewModels.HardDiskMedia;
using System;
using System.Windows;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskMediaOutboundApplicationEditDialog : Window
    {
        public HardDiskMediaOutboundApplicationEditDialog()
        {
            InitializeComponent();
            Loaded += HardDiskMediaOutboundApplicationEditDialog_Loaded;
        }

        private async void HardDiskMediaOutboundApplicationEditDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HardDiskMediaOutboundApplicationEditDialog_Loaded;

            if (DataContext is not HardDiskMediaOutboundApplicationEditDialogViewModel viewModel)
            {
                return;
            }

            try
            {
                await viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"加载申请单失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                DialogResult = false;
            }
        }
    }
}
