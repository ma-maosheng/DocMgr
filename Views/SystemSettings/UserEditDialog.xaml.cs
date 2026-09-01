using System.Windows;
using System.Windows.Controls;
using DocMgr.ViewModels.SystemSettings;

namespace DocMgr.Views.SystemSettings
{
    public partial class UserEditDialog : Window
    {
        public UserEditDialog()
        {
            InitializeComponent();
        }

        private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserEditDialogViewModel vm && sender is PasswordBox pb)
            {
                vm.Password = pb.Password;
            }
        }

        private void PwdConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserEditDialogViewModel vm && sender is PasswordBox pb)
            {
                vm.ConfirmPassword = pb.Password;
            }
        }
    }
}