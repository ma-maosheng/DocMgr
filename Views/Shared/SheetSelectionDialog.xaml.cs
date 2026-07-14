using System.Windows;
using System.Windows.Input;
using DocMgr.ViewModels.Shared;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// SheetSelectionDialog.xaml 的交互逻辑
    /// </summary>
    public partial class SheetSelectionDialog : Window
    {
        public SheetSelectionDialog()
        {
            InitializeComponent();
            PreviewKeyDown += SheetSelectionDialog_PreviewKeyDown;
        }

        private void SheetSelectionDialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (DataContext is SheetSelectionDialogViewModel vm &&
                    vm.CancelCommand.CanExecute(null))
                {
                    vm.CancelCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}