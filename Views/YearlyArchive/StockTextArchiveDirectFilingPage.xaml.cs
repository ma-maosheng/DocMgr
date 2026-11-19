using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.YearlyArchive
{
    public partial class StockTextArchiveDirectFilingPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly StockTextArchiveDirectFilingViewModel _viewModel;

        public StockTextArchiveDirectFilingPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            _viewModel = _pageScope.ServiceProvider.GetRequiredService<StockTextArchiveDirectFilingViewModel>();
            DataContext = _viewModel;

            Loaded += StockTextArchiveDirectFilingPage_Loaded;
            Unloaded += StockTextArchiveDirectFilingPage_Unloaded;
        }

        private async void StockTextArchiveDirectFilingPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void StockTextArchiveDirectFilingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= StockTextArchiveDirectFilingPage_Loaded;
            Unloaded -= StockTextArchiveDirectFilingPage_Unloaded;
            _pageScope.Dispose();
        }

        /// <summary>
        /// 子项卡片只做横向滚动，滚轮继续交给页面纵向滚动。
        /// </summary>
        private void SttItemScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scroller || e.Handled || scroller.ScrollableHeight > 0)
                return;

            e.Handled = true;
            if (scroller.Parent is not UIElement parent)
                return;

            var forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            parent.RaiseEvent(forwarded);
        }
    }
}
