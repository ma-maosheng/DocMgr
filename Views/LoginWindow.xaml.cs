using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels;
using DocMgr.Views.Shared;
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
            EnterAsTabNavigationSupport.Attach(this);

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
            ApplyEnglishInput(this);
            ApplyEnglishInput(TxtUsername);
            ApplyEnglishInput(TxtPassword);
            TxtUsername.GotKeyboardFocus += (_, _) => ApplyEnglishInput(TxtUsername);
            TxtPassword.GotKeyboardFocus += (_, _) => ApplyEnglishInput(TxtPassword);
            TxtUsername.Focus();

            var logContext = _viewScope.ServiceProvider.GetService<IDbOperationLogContextService>();
            logContext?.SetCurrentPage("用户登录");
        }

        /// <summary>
        /// 登录框默认英文：关闭中文输入法，并尽量切到已安装的英语键盘。
        /// </summary>
        private static void ApplyEnglishInput(DependencyObject target)
        {
            InputMethod.SetIsInputMethodEnabled(target, true);
            InputMethod.SetPreferredImeState(target, InputMethodState.Off);
            InputMethod.SetPreferredImeConversionMode(target, ImeConversionModeValues.Alphanumeric);

            CultureInfo? english = InputLanguageManager.Current.AvailableInputLanguages
                .OfType<CultureInfo>()
                .FirstOrDefault(item => item.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            if (english == null)
            {
                return;
            }

            InputLanguageManager.SetInputLanguage(target, english);
            InputLanguageManager.Current.CurrentInputLanguage = english;
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
