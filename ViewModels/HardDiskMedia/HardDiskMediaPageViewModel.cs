using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.ViewModels.Base;
using DocMgr.Views;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质模块首页 ViewModel（概览页，对齐现行硬盘业务菜单与统计口径）。
    /// </summary>
    public class HardDiskMediaPageViewModel : ViewModelBase
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

        private int _missingLedgerMediumCount;
        public int MissingLedgerMediumCount
        {
            get => _missingLedgerMediumCount;
            set => SetProperty(ref _missingLedgerMediumCount, value);
        }

        private int _blankInStockCount;
        public int BlankInStockCount
        {
            get => _blankInStockCount;
            set => SetProperty(ref _blankInStockCount, value);
        }

        private int _borrowedCount;
        public int BorrowedCount
        {
            get => _borrowedCount;
            set => SetProperty(ref _borrowedCount, value);
        }

        private int _dataCarrierInStockCount;
        public int DataCarrierInStockCount
        {
            get => _dataCarrierInStockCount;
            set => SetProperty(ref _dataCarrierInStockCount, value);
        }

        private int _damagedInStockCount;
        public int DamagedInStockCount
        {
            get => _damagedInStockCount;
            set => SetProperty(ref _damagedInStockCount, value);
        }

        private int _inStockLostCount;
        public int InStockLostCount
        {
            get => _inStockLostCount;
            set => SetProperty(ref _inStockLostCount, value);
        }

        private int _inStockScrapCount;
        public int InStockScrapCount
        {
            get => _inStockScrapCount;
            set => SetProperty(ref _inStockScrapCount, value);
        }

        private int _permanentTransferCount;
        public int PermanentTransferCount
        {
            get => _permanentTransferCount;
            set => SetProperty(ref _permanentTransferCount, value);
        }

        private int _disposedCount;
        public int DisposedCount
        {
            get => _disposedCount;
            set => SetProperty(ref _disposedCount, value);
        }

        private int _outLostCount;
        public int OutLostCount
        {
            get => _outLostCount;
            set => SetProperty(ref _outLostCount, value);
        }

        private int _needReturnMediumCount;
        public int NeedReturnMediumCount
        {
            get => _needReturnMediumCount;
            set => SetProperty(ref _needReturnMediumCount, value);
        }

        private int _longTermNeedReturnMediumCount;
        public int LongTermNeedReturnMediumCount
        {
            get => _longTermNeedReturnMediumCount;
            set => SetProperty(ref _longTermNeedReturnMediumCount, value);
        }

        private int _temporaryNeedReturnMediumCount;
        public int TemporaryNeedReturnMediumCount
        {
            get => _temporaryNeedReturnMediumCount;
            set => SetProperty(ref _temporaryNeedReturnMediumCount, value);
        }

        private int _overdueNeedReturnCount;
        public int OverdueNeedReturnCount
        {
            get => _overdueNeedReturnCount;
            set => SetProperty(ref _overdueNeedReturnCount, value);
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

        private int _lockedMediumCount;
        public int LockedMediumCount
        {
            get => _lockedMediumCount;
            set => SetProperty(ref _lockedMediumCount, value);
        }

        private int _submittedApplicationCount;
        public int SubmittedApplicationCount
        {
            get => _submittedApplicationCount;
            set => SetProperty(ref _submittedApplicationCount, value);
        }

        private int _pendingHandoverApplicationCount;
        public int PendingHandoverApplicationCount
        {
            get => _pendingHandoverApplicationCount;
            set => SetProperty(ref _pendingHandoverApplicationCount, value);
        }

        private int _pendingSignedFileCount;
        public int PendingSignedFileCount
        {
            get => _pendingSignedFileCount;
            set => SetProperty(ref _pendingSignedFileCount, value);
        }

        private int _pendingCompleteApplicationCount;
        public int PendingCompleteApplicationCount
        {
            get => _pendingCompleteApplicationCount;
            set => SetProperty(ref _pendingCompleteApplicationCount, value);
        }

        private int _pendingDisposalCount;
        public int PendingDisposalCount
        {
            get => _pendingDisposalCount;
            set => SetProperty(ref _pendingDisposalCount, value);
        }

        private int _draftInventoryRegisterCount;
        public int DraftInventoryRegisterCount
        {
            get => _draftInventoryRegisterCount;
            set => SetProperty(ref _draftInventoryRegisterCount, value);
        }

        public ObservableCollection<string> LocationInsights { get; } = new();
        public ObservableCollection<string> OutboundCapacityInsights { get; } = new();
        public ObservableCollection<string> HandoverInsights { get; } = new();
        public ObservableCollection<string> LifecycleInsights { get; } = new();
        public ObservableCollection<string> RiskInsights { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand NavigateKpiCommand { get; }

        public HardDiskMediaPageViewModel(IHardDiskMediaService hardDiskMediaService, IDialogService dialogService)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;

            RefreshCommand = new RelayCommand(async _ => await RefreshOverviewAsync());
            NavigateKpiCommand = new RelayCommand(parameter => NavigateKpi(parameter));
        }

        public async Task InitializeAsync(HardDiskMediaWorkbenchSection section)
        {
            _ = section;

            try
            {
                if (!_isInitialized)
                {
                    await LoadOverviewAsync();
                    _isInitialized = true;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载硬盘介质模块失败：{ex.Message}");
            }
        }

        private async Task LoadOverviewAsync()
        {
            var overview = await _hardDiskMediaService.GetOverviewAsync();
            TotalMediumCount = overview.TotalMediumCount;
            MissingLedgerMediumCount = overview.MissingLedgerMediumCount;
            BlankInStockCount = overview.BlankInStockCount;
            BorrowedCount = overview.BorrowedCount;
            DataCarrierInStockCount = overview.DataCarrierInStockCount;
            DamagedInStockCount = overview.DamagedInStockCount;
            InStockLostCount = overview.InStockLostCount;
            InStockScrapCount = overview.InStockScrapCount;
            PermanentTransferCount = overview.PermanentTransferCount;
            DisposedCount = overview.DisposedCount;
            OutLostCount = overview.OutLostCount;
            NeedReturnMediumCount = overview.NeedReturnMediumCount;
            LongTermNeedReturnMediumCount = overview.LongTermNeedReturnMediumCount;
            TemporaryNeedReturnMediumCount = overview.TemporaryNeedReturnMediumCount;
            OverdueNeedReturnCount = overview.OverdueNeedReturnCount;
            MissingLocationMediumCount = overview.MissingLocationMediumCount;
            OutboundWithoutKeeperMediumCount = overview.OutboundWithoutKeeperMediumCount;
            LockedMediumCount = overview.LockedMediumCount;
            SubmittedApplicationCount = overview.SubmittedApplicationCount;
            PendingHandoverApplicationCount = overview.PendingHandoverApplicationCount;
            PendingSignedFileCount = overview.PendingSignedFileCount;
            PendingCompleteApplicationCount = overview.PendingCompleteApplicationCount;
            PendingDisposalCount = overview.PendingDisposalCount;
            DraftInventoryRegisterCount = overview.DraftInventoryRegisterCount;

            ReplaceCollection(LocationInsights, overview.LocationInsights);
            ReplaceCollection(OutboundCapacityInsights, overview.OutboundCapacityInsights);
            ReplaceCollection(HandoverInsights, overview.HandoverInsights);
            ReplaceCollection(LifecycleInsights, overview.LifecycleInsights);
            ReplaceCollection(RiskInsights, overview.RiskInsights);
        }

        private void NavigateKpi(object? parameter)
        {
            HardDiskOverviewKpiKind? kind = parameter switch
            {
                HardDiskOverviewKpiKind value => value,
                string text when Enum.TryParse(text, ignoreCase: true, out HardDiskOverviewKpiKind parsed) => parsed,
                _ => null
            };

            if (kind == null)
            {
                return;
            }

            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateFromHardDiskOverviewKpi(kind.Value);
                return;
            }

            _dialogService.ShowError("无法跳转到业务列表：主窗口不可用。");
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
                _dialogService.ShowError($"刷新介质管理概览失败：{ex.Message}");
            }
        }
    }
}
