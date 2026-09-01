using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.ViewModels.Base;
using DocMgr.Views;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 数据光盘介质模块首页 ViewModel。
    /// </summary>
    public class OpticalDiscMediaPageViewModel : ViewModelBase
    {
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IDialogService _dialogService;
        private bool _isInitialized;

        private int _totalMediumCount;
        public int TotalMediumCount
        {
            get => _totalMediumCount;
            set => SetProperty(ref _totalMediumCount, value);
        }

        private int _inStockCount;
        public int InStockCount
        {
            get => _inStockCount;
            set => SetProperty(ref _inStockCount, value);
        }

        private int _outTemporaryCount;
        public int OutTemporaryCount
        {
            get => _outTemporaryCount;
            set => SetProperty(ref _outTemporaryCount, value);
        }

        private int _damagedInStockCount;
        public int DamagedInStockCount
        {
            get => _damagedInStockCount;
            set => SetProperty(ref _damagedInStockCount, value);
        }

        private int _lostInStockCount;
        public int LostInStockCount
        {
            get => _lostInStockCount;
            set => SetProperty(ref _lostInStockCount, value);
        }

        private int _scrapInStockCount;
        public int ScrapInStockCount
        {
            get => _scrapInStockCount;
            set => SetProperty(ref _scrapInStockCount, value);
        }

        private int _destroyedCount;
        public int DestroyedCount
        {
            get => _destroyedCount;
            set => SetProperty(ref _destroyedCount, value);
        }

        private int _needReturnMediumCount;
        public int NeedReturnMediumCount
        {
            get => _needReturnMediumCount;
            set => SetProperty(ref _needReturnMediumCount, value);
        }

        private int _missingLocationMediumCount;
        public int MissingLocationMediumCount
        {
            get => _missingLocationMediumCount;
            set => SetProperty(ref _missingLocationMediumCount, value);
        }

        private int _outboundWithoutKeeperMediumCount;
        public int OutboundWithoutKeeperMediumCount
        {
            get => _outboundWithoutKeeperMediumCount;
            set => SetProperty(ref _outboundWithoutKeeperMediumCount, value);
        }

        private int _recentTransactionCount;
        public int RecentTransactionCount
        {
            get => _recentTransactionCount;
            set => SetProperty(ref _recentTransactionCount, value);
        }

        public ObservableCollection<string> LocationInsights { get; } = new();
        public ObservableCollection<string> LifecycleInsights { get; } = new();
        public ObservableCollection<string> CirculationInsights { get; } = new();
        public ObservableCollection<string> RiskInsights { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand NavigateKpiCommand { get; }

        public OpticalDiscMediaPageViewModel(IHardDiskMediaService hardDiskMediaService, IDialogService dialogService)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;
            RefreshCommand = new RelayCommand(async _ => await RefreshOverviewAsync());
            NavigateKpiCommand = new RelayCommand(parameter => NavigateKpi(parameter));
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                await LoadOverviewAsync();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载光盘概览失败：{ex.Message}");
            }
        }

        private async Task LoadOverviewAsync()
        {
            var overview = await _hardDiskMediaService.GetOpticalDiscOverviewAsync();
            TotalMediumCount = overview.TotalMediumCount;
            InStockCount = overview.InStockCount;
            OutTemporaryCount = overview.OutTemporaryCount;
            DamagedInStockCount = overview.DamagedInStockCount;
            LostInStockCount = overview.LostInStockCount;
            ScrapInStockCount = overview.ScrapInStockCount;
            DestroyedCount = overview.DestroyedCount;
            NeedReturnMediumCount = overview.NeedReturnMediumCount;
            MissingLocationMediumCount = overview.MissingLocationMediumCount;
            OutboundWithoutKeeperMediumCount = overview.OutboundWithoutKeeperMediumCount;
            RecentTransactionCount = overview.RecentTransactionCount;

            ReplaceCollection(LocationInsights, overview.LocationInsights);
            ReplaceCollection(LifecycleInsights, overview.LifecycleInsights);
            ReplaceCollection(CirculationInsights, overview.CirculationInsights);
            ReplaceCollection(RiskInsights, overview.RiskInsights);
        }

        private void NavigateKpi(object? parameter)
        {
            OpticalDiscOverviewKpiKind? kind = parameter switch
            {
                OpticalDiscOverviewKpiKind value => value,
                string text when Enum.TryParse(text, ignoreCase: true, out OpticalDiscOverviewKpiKind parsed) => parsed,
                _ => null
            };

            if (kind == null)
            {
                return;
            }

            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateFromOpticalDiscOverviewKpi(kind.Value);
                return;
            }

            _dialogService.ShowError("无法跳转到流转台账：主窗口不可用。");
        }

        private static void ReplaceCollection(ObservableCollection<string> target, IReadOnlyList<string> source)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }

        private async Task RefreshOverviewAsync()
        {
            try
            {
                await LoadOverviewAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"刷新光盘概览失败：{ex.Message}");
            }
        }
    }
}
