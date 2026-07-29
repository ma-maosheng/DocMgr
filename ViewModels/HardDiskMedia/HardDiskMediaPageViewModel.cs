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

        private HardDiskMediaWorkbenchSection _currentSection;
        private bool _isInitialized;

        private string _sectionTitle = string.Empty;
        public string SectionTitle
        {
            get => _sectionTitle;
            set => SetProperty(ref _sectionTitle, value);
        }

        private string _sectionDescription = string.Empty;
        public string SectionDescription
        {
            get => _sectionDescription;
            set => SetProperty(ref _sectionDescription, value);
        }

        private string _sectionFocus = string.Empty;
        public string SectionFocus
        {
            get => _sectionFocus;
            set => SetProperty(ref _sectionFocus, value);
        }

        private string _businessReference = "参考：硬盘台账 / 出库申请 / 归还登记 / 盘库登记 / 离库处置";
        public string BusinessReference
        {
            get => _businessReference;
            set => SetProperty(ref _businessReference, value);
        }

        private string _applicationTypeSummary = string.Empty;
        public string ApplicationTypeSummary
        {
            get => _applicationTypeSummary;
            set => SetProperty(ref _applicationTypeSummary, value);
        }

        private string _applicationStatusSummary = string.Empty;
        public string ApplicationStatusSummary
        {
            get => _applicationStatusSummary;
            set => SetProperty(ref _applicationStatusSummary, value);
        }

        private string _transactionTypeSummary = string.Empty;
        public string TransactionTypeSummary
        {
            get => _transactionTypeSummary;
            set => SetProperty(ref _transactionTypeSummary, value);
        }

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

        public ObservableCollection<string> WorkflowSteps { get; } = new();
        public ObservableCollection<string> SectionHighlights { get; } = new();
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
            _currentSection = section;

            try
            {
                if (!_isInitialized)
                {
                    await LoadOverviewAsync();
                    await LoadDomainOptionsAsync();
                    _isInitialized = true;
                }

                ApplySection(section);
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

        private async Task LoadDomainOptionsAsync()
        {
            var applicationTypes = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskMediaApplication), nameof(HardDiskMediaApplication.ApplicationType));
            var applicationStatuses = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskMediaApplication), nameof(HardDiskMediaApplication.ApplicationStatus));
            var transactionTypes = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskMediaTransaction), nameof(HardDiskMediaTransaction.TransactionType));

            ApplicationTypeSummary = string.Join("、", applicationTypes);
            ApplicationStatusSummary = string.Join("、", applicationStatuses);
            TransactionTypeSummary = string.Join("、", transactionTypes);
        }

        private void ApplySection(HardDiskMediaWorkbenchSection section)
        {
            WorkflowSteps.Clear();
            SectionHighlights.Clear();

            // 各业务已拆为独立页面；本页仅保留概览。其余 section 枚举保留兼容导航参数。
            SectionHighlights.Add("初始登记");
            SectionHighlights.Add("出库申请/审批");
            SectionHighlights.Add("归还申请/审批入库");
            SectionHighlights.Add("盘库登记");
            SectionHighlights.Add("离库处置");
            SectionHighlights.Add("硬盘台账");

            SectionTitle = "硬盘概览";
            SectionDescription = "按现行台账状态、申请工作流、盘库登记与离库处置汇总库存结构、流程积压与风险指标。";
            SectionFocus = "口径说明：待上传签批 ≠ 待办结；永久移交 / 离库处置 / 挂失 / 盘失分列统计。点击上方 KPI 卡片可跳转到对应业务列表（带初始筛选）。";
            BusinessReference = "参考：介质管理 → 硬盘（初始登记、出库、归还、盘库、离库处置、台账）";

            WorkflowSteps.Add("初始登记：维护硬盘基础信息与在库空白台账，支持模板导入导出。");
            WorkflowSteps.Add("出库申请 → 审批 → 实物交接 → 上传签批交接单 → 办结回写台账与流转流水。");
            WorkflowSteps.Add("归还/挂失登记：对临时/长期借出介质办理归还或挂失，签字回传后办结。");
            WorkflowSteps.Add("盘库登记：损坏登记、盘失登记（草稿直接办结）。损坏盘档口调整请开柜迁档。");
            WorkflowSteps.Add("离库处置：淘汰/损坏/盘失等介质提交处置（按盘自动带出原因），办结后进入「离库(处置)」。");
            WorkflowSteps.Add("硬盘台账：按时间线查看登记、出库、归还、盘库、处置等流转履历。");

            _ = section;
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
                await LoadDomainOptionsAsync();
                ApplySection(_currentSection);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"刷新介质管理概览失败：{ex.Message}");
            }
        }
    }
}
