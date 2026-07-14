using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DocMgr.ViewModels.Cabinets;

namespace DocMgr.Views.Cabinets
{
    public partial class CabinetSlotDetailDialog : Window
    {
        public CabinetSlotDetailDialog()
        {
            InitializeComponent();
        }

        private void ArchiveBoxGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not CabinetSlotDetailViewModel viewModel
                || sender is not DataGrid grid
                || grid.SelectedItem is not CabinetSlotDetailArchiveBoxRowViewModel row)
            {
                return;
            }

            viewModel.OpenArchiveBoxDetail(row);
            e.Handled = true;
        }

        private void HardDiskGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not CabinetSlotDetailViewModel viewModel
                || sender is not DataGrid grid
                || grid.SelectedItem is not CabinetSlotDetailHardDiskRowViewModel row)
            {
                return;
            }

            viewModel.OpenHardDiskDetail(row);
            e.Handled = true;
        }
    }
}
