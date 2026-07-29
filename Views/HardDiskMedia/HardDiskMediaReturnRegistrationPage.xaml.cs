using DocMgr.Models.HardDiskMedia;
using DocMgr.ViewModels.HardDiskMedia;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.HardDiskMedia
{
    public partial class HardDiskMediaReturnRegistrationPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly bool _overdueOnly;
        private readonly bool _matchAllYears;

        public HardDiskReturnWorkspaceMode WorkspaceMode { get; }

        public HardDiskMediaReturnRegistrationPage(
            HardDiskReturnWorkspaceMode workspaceMode = HardDiskReturnWorkspaceMode.Application)
            : this(workspaceMode, overdueOnly: false, matchAllYears: false)
        {
        }

        public HardDiskMediaReturnRegistrationPage(
            HardDiskReturnWorkspaceMode workspaceMode,
            bool overdueOnly,
            bool matchAllYears = false)
        {
            InitializeComponent();
            WorkspaceMode = workspaceMode;
            _overdueOnly = overdueOnly;
            _matchAllYears = matchAllYears;

            _pageScope = App.CurrentProvider.CreateScope();
            var vmFactory = _pageScope.ServiceProvider
                .GetRequiredService<Func<HardDiskReturnWorkspaceMode, HardDiskMediaReturnRegistrationPageViewModel>>();
            DataContext = vmFactory(workspaceMode);

            Loaded += HardDiskMediaReturnRegistrationPage_Loaded;
            Unloaded += HardDiskMediaReturnRegistrationPage_Unloaded;
        }

        private async void HardDiskMediaReturnRegistrationPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardDiskMediaReturnRegistrationPageViewModel viewModel)
            {
                await viewModel.InitializeAsync(_overdueOnly, _matchAllYears);
            }
        }

        private void HardDiskMediaReturnRegistrationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HardDiskMediaReturnRegistrationPage_Loaded;
            Unloaded -= HardDiskMediaReturnRegistrationPage_Unloaded;
            _pageScope.Dispose();
        }

        private void DgApplications_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is HardDiskMediaReturnRegistrationPageViewModel viewModel &&
                viewModel.OpenReturnCommand.CanExecute(null))
            {
                viewModel.OpenReturnCommand.Execute(null);
            }
        }

        private void DgCandidates_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is HardDiskMediaReturnRegistrationPageViewModel viewModel &&
                viewModel.StartReturnCommand.CanExecute(null))
            {
                viewModel.StartReturnCommand.Execute(null);
            }
        }
    }
}
