using DocMgr.Models.YearlyArchive;
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

        public ArchiveReturnWorkspaceMode WorkspaceMode { get; }

        public ArchiveReturnWorkbenchViewModel ViewModel { get; }

        public ArchiveReturnWorkbenchPage(ArchiveReturnWorkspaceMode workspaceMode = ArchiveReturnWorkspaceMode.Handover)
        {
            InitializeComponent();
            WorkspaceMode = workspaceMode;
            _pageScope = App.CurrentProvider.CreateScope();
            var vmFactory = _pageScope.ServiceProvider
                .GetRequiredService<Func<ArchiveReturnWorkspaceMode, ArchiveReturnWorkbenchViewModel>>();
            ViewModel = vmFactory(workspaceMode);
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
            _pageScope.Dispose();
        }
    }
}
