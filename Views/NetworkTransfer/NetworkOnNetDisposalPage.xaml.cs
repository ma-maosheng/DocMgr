using DocMgr.ViewModels.NetworkTransfer;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.NetworkTransfer;

public partial class NetworkOnNetDisposalPage : Page
{
    private readonly IServiceScope _pageScope;
    private readonly bool _pendingInProgress;
    private readonly bool _matchAllYears;

    public NetworkOnNetDisposalPage()
        : this(pendingInProgress: false, matchAllYears: false)
    {
    }

    public NetworkOnNetDisposalPage(bool pendingInProgress, bool matchAllYears = false)
    {
        _pendingInProgress = pendingInProgress;
        _matchAllYears = matchAllYears;
        InitializeComponent();
        _pageScope = App.CurrentProvider.CreateScope();
        DataContext = _pageScope.ServiceProvider.GetRequiredService<NetworkOnNetDisposalPageViewModel>();
        Loaded += NetworkOnNetDisposalPage_Loaded;
        Unloaded += NetworkOnNetDisposalPage_Unloaded;
    }

    private async void NetworkOnNetDisposalPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is NetworkOnNetDisposalPageViewModel viewModel)
        {
            await viewModel.InitializeAsync(_pendingInProgress, _matchAllYears);
        }
    }

    private void NetworkOnNetDisposalPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= NetworkOnNetDisposalPage_Loaded;
        Unloaded -= NetworkOnNetDisposalPage_Unloaded;
        _pageScope.Dispose();
    }

    private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is NetworkOnNetDisposalPageViewModel viewModel &&
            viewModel.OpenCommand.CanExecute(null))
        {
            viewModel.OpenCommand.Execute(null);
        }
    }
}
