using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveFilingPage : Page
    {
        private readonly IServiceScope _pageScope;
        private bool _isInitialized;

        public ArchiveFilingViewModel ViewModel { get; }

        public ArchiveFilingPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            ViewModel = _pageScope.ServiceProvider.GetRequiredService<ArchiveFilingViewModel>();
            DataContext = ViewModel;
            ViewModel.SimulatedPendingSelectionRestoreRequested += RestoreSimulatedPendingSelection;
            ViewModel.RequestClearPendingListSelections += ClearPendingPoolListSelections;

            Unloaded += ArchiveFilingPage_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            // 先让页面与 Tab 完成布局与首帧绘制，再加载待立档池，避免长时间空白像“死机”
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => _ = LoadArchiveFilingDataAsync());
        }

        private async Task LoadArchiveFilingDataAsync()
        {
            try
            {
                await Task.Yield();
                await ViewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "资料立档页面数据加载失败，请关闭页面后重试。若数据库文件在网络盘或体积很大，也可能较慢。\n\n" + ex.Message,
                    "加载失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ArchiveFilingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= ArchiveFilingPage_Unloaded;
            ViewModel.SimulatedPendingSelectionRestoreRequested -= RestoreSimulatedPendingSelection;
            ViewModel.RequestClearPendingListSelections -= ClearPendingPoolListSelections;
            _pageScope.Dispose();
        }

        private void RestoreSimulatedPendingSelection(IReadOnlyList<int> recordIds)
        {
            if (recordIds == null || recordIds.Count == 0)
            {
                return;
            }

            var selectedIds = recordIds.ToHashSet();
            LvSimulatedPendingRecords.SelectedItems.Clear();
            foreach (var record in LvSimulatedPendingRecords.Items.Cast<YearlyArchiveRegisterRecord>().Where(item => selectedIds.Contains(item.Id)))
            {
                LvSimulatedPendingRecords.SelectedItems.Add(record);
            }
        }

        private void ArchiveFilingTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not TabControl tab || ViewModel == null)
            {
                return;
            }

            if (!ReferenceEquals(e.Source, tab))
            {
                return;
            }

            int idx = tab.SelectedIndex;
            if (idx < 0)
            {
                return;
            }

            if (ViewModel.SelectedTrackIndex != idx)
            {
                ViewModel.SelectedTrackIndex = idx;
            }
        }

        private void ClearPendingPoolListSelections()
        {
            LvSimulatedPendingRecords.SelectedItems.Clear();
            LvElectronicPendingRecords.SelectedItems.Clear();
        }

        private void LvSimulatedPendingRecords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedRecordsFromSender(sender);
        }

        private void LvElectronicPendingRecords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedRecordsFromSender(sender);
        }

        private void UpdateSelectedRecordsFromSender(object sender)
        {
            if (ViewModel.SuppressPendingListSelectionSync)
            {
                return;
            }

            if (sender is not ListView listView)
            {
                return;
            }

            ViewModel.SelectedRecords = listView.SelectedItems
                .Cast<YearlyArchiveRegisterRecord>()
                .ToList();
        }

        private void DgElectronicRecordItemsStepTwo_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Height = double.NaN;
        }
    }
}