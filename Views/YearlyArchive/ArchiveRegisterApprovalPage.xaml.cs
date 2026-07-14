using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveRegisterApprovalPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly int? _initialRecordId;

        public ArchiveRegisterWorkspaceMode WorkspaceMode => ArchiveRegisterWorkspaceMode.Approval;

        public ArchiveRegisterApprovalPage(int? initialRecordId = null)
        {
            InitializeComponent();
            _initialRecordId = initialRecordId;
            _pageScope = App.CurrentProvider.CreateScope();
            var vmFactory = _pageScope.ServiceProvider
                .GetRequiredService<Func<ArchiveRegisterWorkspaceMode, int, ArchiveRegisterWorkbenchPageViewModel>>();
            DataContext = vmFactory(
                ArchiveRegisterWorkspaceMode.Approval,
                _initialRecordId.GetValueOrDefault());
            Loaded += ArchiveRegisterApprovalPage_Loaded;
            Unloaded += ArchiveRegisterApprovalPage_Unloaded;
        }

        private async void ArchiveRegisterApprovalPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ArchiveRegisterWorkbenchPageViewModel vm)
                await vm.InitializeAsync();
        }

        private void ArchiveRegisterApprovalPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ArchiveRegisterApprovalPage_Loaded;
            Unloaded -= ArchiveRegisterApprovalPage_Unloaded;
            _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ArchiveRegisterWorkbenchPageViewModel vm &&
                vm.OpenCommand.CanExecute(null))
            {
                vm.OpenCommand.Execute(null);
            }
        }
    }
}
