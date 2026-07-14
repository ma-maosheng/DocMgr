using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveReturnWorkbenchPage : Page
    {
        private readonly IServiceScope _pageScope;
        private bool _isUnloaded;

        public ArchiveReturnWorkbenchViewModel ViewModel { get; }

        public ArchiveReturnWorkbenchPage()
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            ViewModel = _pageScope.ServiceProvider.GetRequiredService<ArchiveReturnWorkbenchViewModel>();
            DataContext = ViewModel;
            Loaded += ArchiveReturnWorkbenchPage_Loaded;
            Unloaded += ArchiveReturnWorkbenchPage_Unloaded;
        }

        private async void ArchiveReturnWorkbenchPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await ViewModel.InitializeAsync();
            }
            catch (ObjectDisposedException)
            {
                // 页面已返回首页，作用域已释放，忽略。
            }
            catch (Exception ex) when (!_isUnloaded)
            {
                MessageBox.Show(
                    "资料归还页面数据加载失败，请关闭页面后重试。\n\n" + ex.Message,
                    "加载失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ArchiveReturnWorkbenchPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = true;
            Loaded -= ArchiveReturnWorkbenchPage_Loaded;
            Unloaded -= ArchiveReturnWorkbenchPage_Unloaded;
            ViewModel.Deactivate();
            DataContext = null;
            _pageScope.Dispose();
        }
    }
}
