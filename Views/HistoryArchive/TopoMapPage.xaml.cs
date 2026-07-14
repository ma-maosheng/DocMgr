using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using DocMgr.ViewModels.HistoryArchive;
using System.Windows;

namespace DocMgr.Views.HistoryArchive
{
    public partial class TopoMapPage : Page
    {
        private readonly IServiceScope _pageScope;
        public TopoMapPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            DataContext = _pageScope.ServiceProvider.GetRequiredService<TopoMapViewModel>();

            Unloaded += TopoMapPage_Unloaded;
        }

        private void TopoMapPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= TopoMapPage_Unloaded;
            _pageScope.Dispose();
        }

        // 纯 UI 逻辑：DataGrid 自动生成行号
        // 这属于视图层的表现逻辑，不属于业务逻辑，保留在 View 中是合理的
        private void DgTopoMaps_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }
    }
}