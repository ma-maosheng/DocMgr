using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveRegisterApplicationPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly int? _initialRecordId;

        public ArchiveRegisterWorkspaceMode WorkspaceMode => ArchiveRegisterWorkspaceMode.Application;

        public ArchiveRegisterApplicationPage(int? initialRecordId = null)
        {
            InitializeComponent();
            _initialRecordId = initialRecordId;
            _pageScope = App.CurrentProvider.CreateScope();
            var vmFactory = _pageScope.ServiceProvider
                .GetRequiredService<Func<ArchiveRegisterWorkspaceMode, int, ArchiveRegisterWorkbenchPageViewModel>>();
            DataContext = vmFactory(
                ArchiveRegisterWorkspaceMode.Application,
                _initialRecordId.GetValueOrDefault());
            Loaded += ArchiveRegisterApplicationPage_Loaded;
            Unloaded += ArchiveRegisterApplicationPage_Unloaded;
        }

        private async void ArchiveRegisterApplicationPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ArchiveRegisterWorkbenchPageViewModel vm)
                await vm.InitializeAsync();
        }

        private void ArchiveRegisterApplicationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ArchiveRegisterApplicationPage_Loaded;
            Unloaded -= ArchiveRegisterApplicationPage_Unloaded;
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
