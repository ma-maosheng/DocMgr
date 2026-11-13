using DocMgr.Infrastructure.AgentDebugLogging;
using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveFilingPage : Page
    {
        // 不在 Unloaded 释放。Frame 后退会复用同一页实例，提前 Dispose 后页面上的 DbContext 已销毁。
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

        /// <summary>
        /// 轨道切换会清空操作台，列表行却可能仍保持选中，再次单击不会触发 SelectionChanged。
        /// 此时若操作台尚未写入申请信息，按当前列表选中项重新刷新。
        /// </summary>
        private void LvPendingRecords_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListView listView)
            {
                return;
            }

            if (FindClickedPendingRecord(listView, e.OriginalSource as DependencyObject) == null
                || listView.SelectedItems.Count == 0)
            {
                return;
            }

            if (ViewModel.SelectedRecords.Count > 0
                && !string.IsNullOrWhiteSpace(ViewModel.TargetProject))
            {
                return;
            }

            UpdateSelectedRecordsFromSender(listView);
        }

        private static YearlyArchiveRegisterRecord? FindClickedPendingRecord(ListView listView, DependencyObject? source)
        {
            while (source != null && !ReferenceEquals(source, listView))
            {
                if (source is ListViewItem item && item.Content is YearlyArchiveRegisterRecord record)
                {
                    return record;
                }

                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }

            return null;
        }

        private void UpdateSelectedRecordsFromSender(object sender)
        {
            // #region agent log
            try
            {
            // #endregion
            if (ViewModel.SuppressPendingListSelectionSync)
            {
                // #region agent log
                AgentDebugSessionLog.Write("E", "ArchiveFilingPage.UpdateSelectedRecordsFromSender", "suppressed", new { suppress = true });
                // #endregion
                return;
            }

            if (sender is not ListView listView)
            {
                return;
            }

            // 未显示页签在卸载时也会冒出 SelectionChanged，不能拿另一侧的选中项覆盖当前操作台。
            bool isSimulatedList = ReferenceEquals(listView, LvSimulatedPendingRecords);
            if (isSimulatedList != ViewModel.IsSimulatedTrack)
            {
                return;
            }

            var selected = listView.SelectedItems
                .Cast<YearlyArchiveRegisterRecord>()
                .ToList();
            // #region agent log
            AgentDebugSessionLog.Write("E", "ArchiveFilingPage.UpdateSelectedRecordsFromSender", "selection sync", new
            {
                count = selected.Count,
                ids = selected.Select(r => r.Id).ToArray(),
                listName = listView.Name,
                threadId = Environment.CurrentManagedThreadId,
                isUi = Dispatcher.CheckAccess()
            });
            // #endregion
            ViewModel.SelectedRecords = selected;
            // #region agent log
            }
            catch (Exception ex)
            {
                AgentDebugSessionLog.WriteException("E", "ArchiveFilingPage.UpdateSelectedRecordsFromSender", "selection sync threw", ex);
                throw;
            }
            // #endregion
        }

        private void DgElectronicRecordItemsStepTwo_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Height = double.NaN;
        }
    }
}