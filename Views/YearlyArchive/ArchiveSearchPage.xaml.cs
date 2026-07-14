using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DocMgr.Models.YearlyArchive;
using DocMgr.Views;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveSearchPage : Page
    {
        private readonly IServiceScope _pageScope;
        private bool _preserveStateOnUnload;

        public ArchiveSearchPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<ArchiveSearchViewModel>();

            if (DataContext is ArchiveSearchViewModel vm)
            {
                vm.ViewDetailRequested += ArchiveSearchViewModel_ViewDetailRequested;
            }

            Loaded += ArchiveSearchPage_Loaded;
            Unloaded += ArchiveSearchPage_Unloaded;
        }

        private async void ArchiveSearchPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ArchiveSearchViewModel vm)
            {
                await vm.InitializeAsync();
            }
        }

        private void ArchiveSearchPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_preserveStateOnUnload)
            {
                _preserveStateOnUnload = false;
                return;
            }

            Loaded -= ArchiveSearchPage_Loaded;
            Unloaded -= ArchiveSearchPage_Unloaded;

            if (DataContext is ArchiveSearchViewModel vm)
            {
                vm.ViewDetailRequested -= ArchiveSearchViewModel_ViewDetailRequested;
            }

            _pageScope.Dispose();
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ArchiveSearchViewModel vm &&
                DgRecords.SelectedItem is YearlyArchiveRegisterRecord record &&
                vm.ViewDetailCommand.CanExecute(record))
            {
                vm.ViewDetailCommand.Execute(record);
            }
        }

        private void ArchiveSearchViewModel_ViewDetailRequested(YearlyArchiveRegisterRecord record)
        {
            OpenRecordDetail(record);
        }

        private void OpenRecordDetail(YearlyArchiveRegisterRecord? record)
        {
            if (record == null)
            {
                return;
            }

            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                _preserveStateOnUnload = true;
                mainWindow.NavigateToArchiveDetailPage(record.Id);
                return;
            }

            MessageBox.Show("当前无法打开资料查看页。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}