using System.Windows;
using System.Windows.Controls;
using DocMgr.ViewModels.HistoryArchive;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.HistoryArchive
{
    public partial class OtherMapPage : Page
    {
        private readonly IServiceScope _pageScope;

        public OtherMapPage()
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<OtherMapViewModel>();
            Unloaded += OtherMapPage_Unloaded;
        }

        private void OtherMapPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= OtherMapPage_Unloaded;
            _pageScope.Dispose();
        }

        private void DgOtherMaps_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            int pageStartIndex = 0;
            if (DataContext is OtherMapViewModel viewModel)
            {
                pageStartIndex = viewModel.PageStartIndex;
            }

            e.Row.Header = (pageStartIndex + e.Row.GetIndex() + 1).ToString();
        }
    }
}
