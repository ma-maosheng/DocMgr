using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using DocMgr.ViewModels.HistoryArchive;
using System.Windows;

namespace DocMgr.Views.HistoryArchive
{
    public partial class AerialPhotoPage : Page
    {
        private readonly IServiceScope _pageScope;
        public AerialPhotoPage()
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<AerialPhotoViewModel>();

            Unloaded += AerialPhotoPage_Unloaded;
        }

        private void AerialPhotoPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= AerialPhotoPage_Unloaded;
            _pageScope.Dispose();
        }



        // 纯 UI 逻辑：DataGrid 自动生成行号
        private void DgData_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            int pageStartIndex = 0;
            if (DataContext is AerialPhotoViewModel viewModel)
            {
                pageStartIndex = viewModel.PageStartIndex;
            }

            e.Row.Header = (pageStartIndex + e.Row.GetIndex() + 1).ToString();
        }
    }

}