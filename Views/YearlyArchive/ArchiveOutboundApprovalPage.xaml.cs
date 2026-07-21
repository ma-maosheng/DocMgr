using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveOutboundApprovalPage : Page
    {
        private readonly IServiceScope _pageScope;
        private ArchiveOutboundWorkbenchPageViewModel? _viewModel;

        public ArchiveOutboundApprovalPage(int initialRecordId = 0)
        {
            InitializeComponent();
            _pageScope = App.CurrentProvider.CreateScope();
            var vmFactory = _pageScope.ServiceProvider
                .GetRequiredService<Func<ArchiveOutboundWorkspaceMode, int, ArchiveOutboundWorkbenchPageViewModel>>();
            _viewModel = vmFactory(ArchiveOutboundWorkspaceMode.Approval, initialRecordId);
            DataContext = _viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            Loaded += async (_, _) =>
            {
                if (_viewModel != null)
                {
                    await _viewModel.InitializeAsync();
                    ApplySplitLayout(_viewModel.HasEditingViewModel);
                }
            };
            Unloaded += (_, _) =>
            {
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }

                _pageScope.Dispose();
            };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ArchiveOutboundWorkbenchPageViewModel.HasEditingViewModel)
                && _viewModel != null)
            {
                ApplySplitLayout(_viewModel.HasEditingViewModel);
            }
        }

        /// <summary>
        /// 打开办理区时：左侧列表约占 2/5、右侧办理区约占 3/5，并启用左右拖拽分隔条；
        /// 关闭办理区后列表恢复占满整行。
        /// </summary>
        private void ApplySplitLayout(bool showDetail)
        {
            if (showDetail)
            {
                ListColumnDefinition.Width = new GridLength(2, GridUnitType.Star);
                ListColumnDefinition.MinWidth = 220;
                SplitterColumnDefinition.Width = new GridLength(8);
                DetailColumnDefinition.Width = new GridLength(3, GridUnitType.Star);
                DetailColumnDefinition.MinWidth = 420;
                ListDetailSplitter.Visibility = Visibility.Visible;
                DetailPaneBorder.Visibility = Visibility.Visible;
            }
            else
            {
                ListColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
                ListColumnDefinition.MinWidth = 220;
                SplitterColumnDefinition.Width = new GridLength(0);
                DetailColumnDefinition.Width = new GridLength(0);
                DetailColumnDefinition.MinWidth = 0;
                ListDetailSplitter.Visibility = Visibility.Collapsed;
                DetailPaneBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void DgRecords_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ArchiveOutboundWorkbenchPageViewModel vm && vm.OpenCommand.CanExecute(null))
            {
                vm.OpenCommand.Execute(null);
            }
        }
    }
}
