using System.Windows;
using System.Windows.Controls;
using DocMgr.ViewModels.SystemSettings;
using DocMgr.Views.Shared;

namespace DocMgr.Views.SystemSettings
{
    public partial class ChangePasswordDialog : Window
    {
        public ChangePasswordDialog()
        {
            InitializeComponent();
            EnterAsTabNavigationSupport.Attach(this);
        }

        private void PwdCurrent_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ChangePasswordDialogViewModel vm && sender is PasswordBox pb)
            {
                vm.CurrentPassword = pb.Password;
            }
        }

        private void PwdNew_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ChangePasswordDialogViewModel vm && sender is PasswordBox pb)
            {
                vm.NewPassword = pb.Password;
            }
        }

        private void PwdConfirm_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ChangePasswordDialogViewModel vm && sender is PasswordBox pb)
            {
                vm.ConfirmPassword = pb.Password;
            }
        }
    }
}
