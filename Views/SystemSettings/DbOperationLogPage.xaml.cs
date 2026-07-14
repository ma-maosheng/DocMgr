using System.Windows.Controls;
using System.Windows.Input;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.SystemSettings;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.SystemSettings
{
    public partial class DbOperationLogPage : Page
    {
        private readonly IServiceScope _pageScope;

        public DbOperationLogPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<DbOperationLogPageViewModel>();

            var logContext = _pageScope.ServiceProvider.GetRequiredService<IDbOperationLogContextService>();
            logContext.SetCurrentPage("系统设置（数据库操作日志）");

            Loaded += DbOperationLogPage_Loaded;
            Unloaded += DbOperationLogPage_Unloaded;
        }

        private async void DbOperationLogPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Loaded -= DbOperationLogPage_Loaded;

            if (DataContext is DbOperationLogPageViewModel viewModel)
            {
                await viewModel.LoadOnPageDisplayedAsync();
            }
        }

        private void DbOperationLogPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Unloaded -= DbOperationLogPage_Unloaded;
            _pageScope.Dispose();
        }

        private void DgLogs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DbOperationLogPageViewModel viewModel
                && viewModel.ViewDetailCommand.CanExecute(null))
            {
                viewModel.ViewDetailCommand.Execute(null);
            }
        }
    }
}
