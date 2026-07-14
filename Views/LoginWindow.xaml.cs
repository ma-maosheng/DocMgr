using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views
{
    public partial class LoginWindow : Window
    {
        private readonly IServiceScope _viewScope;
        private readonly LoginWindowViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();

            _viewScope = App.CurrentProvider.CreateScope();

            _viewModel = _viewScope.ServiceProvider.GetRequiredService<LoginWindowViewModel>();
            _viewModel.LoginSucceeded += OnLoginSucceeded;
            _viewModel.RequestShutdown += OnRequestShutdown;

            DataContext = _viewModel;

            Loaded += LoginWindow_Loaded;
            Closed += (_, _) =>
            {
                _viewModel.LoginSucceeded -= OnLoginSucceeded;
                _viewModel.RequestShutdown -= OnRequestShutdown;
                _viewScope.Dispose();
            };
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= LoginWindow_Loaded;
            TxtUsername.Focus();

            var logContext = _viewScope.ServiceProvider.GetService<IDbOperationLogContextService>();
            logContext?.SetCurrentPage("用户登录");
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb)
            {
                _viewModel.Password = pb.Password;
            }
        }

        private void OnLoginSucceeded(User user)
        {
            var mainWindow = new MainWindow(user);
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            Close();
        }

        private void OnRequestShutdown()
        {
            Application.Current.Shutdown();
        }
    }
}
