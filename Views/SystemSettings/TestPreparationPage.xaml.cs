using System.Windows;
using System.Windows.Controls;
using DocMgr.ViewModels.SystemSettings;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.SystemSettings
{
    public partial class TestPreparationPage : Page
    {
        private readonly IServiceScope _pageScope;

        public TestPreparationPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<TestPreparationPageViewModel>();

            Unloaded += TestPreparationPage_Unloaded;
        }

        private void TestPreparationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= TestPreparationPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
