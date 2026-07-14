using System.Windows.Controls;
using DocMgr.ViewModels.SystemSettings;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.SystemSettings
{
    public partial class BusinessLogicSettingsPage : Page
    {
        private readonly IServiceScope _pageScope;

        public BusinessLogicSettingsPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<BusinessLogicSettingsViewModel>();

            Loaded += BusinessLogicSettingsPage_Loaded;
            Unloaded += BusinessLogicSettingsPage_Unloaded;
        }

        private async void BusinessLogicSettingsPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is BusinessLogicSettingsViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void BusinessLogicSettingsPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Loaded -= BusinessLogicSettingsPage_Loaded;
            Unloaded -= BusinessLogicSettingsPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
