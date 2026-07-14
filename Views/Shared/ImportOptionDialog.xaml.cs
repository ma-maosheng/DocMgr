using System.Windows;
using System.Windows.Input;
using DocMgr.ViewModels.Shared;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// ImportOptionDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ImportOptionDialog : Window
    {
        public ImportOptionDialog()
        {
            InitializeComponent();
            PreviewKeyDown += ImportOptionDialog_PreviewKeyDown;
        }

        private void ImportOptionDialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (DataContext is ImportOptionDialogViewModel vm &&
                    vm.CancelCommand.CanExecute(null))
                {
                    vm.CancelCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}