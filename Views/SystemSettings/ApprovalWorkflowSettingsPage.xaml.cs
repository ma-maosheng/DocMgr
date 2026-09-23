using System.Windows.Controls;
using DocMgr.ViewModels.SystemSettings;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.SystemSettings
{
    public partial class ApprovalWorkflowSettingsPage : Page
    {
        private readonly IServiceScope _pageScope;

        public ApprovalWorkflowSettingsPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<ApprovalWorkflowSettingsViewModel>();

            Loaded += ApprovalWorkflowSettingsPage_Loaded;
            Unloaded += ApprovalWorkflowSettingsPage_Unloaded;
        }

        private async void ApprovalWorkflowSettingsPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ApprovalWorkflowSettingsViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void ApprovalWorkflowSettingsPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Loaded -= ApprovalWorkflowSettingsPage_Loaded;
            Unloaded -= ApprovalWorkflowSettingsPage_Unloaded;
            _pageScope.Dispose();
        }
    }
}
