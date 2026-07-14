using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质模块首页 ViewModel。
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

        private string _businessReference = "参考：年度资料档案化管理 / 资料登记";
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

        private int _transferOutCount;
        public int TransferOutCount
        {
            get => _transferOutCount;
            set => SetProperty(ref _transferOutCount, value);
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

        private int _submittedApplicationCount;
        public int SubmittedApplicationCount
        {
            get => _submittedApplicationCount;
            set => SetProperty(ref _submittedApplicationCount, value);
        }

        private int _pendingSignedFileCount;
        public int PendingSignedFileCount
        {
            get => _pendingSignedFileCount;
            set => SetProperty(ref _pendingSignedFileCount, value);
        }

        private int _pendingProcessApplicationCount;
        public int PendingProcessApplicationCount
        {
            get => _pendingProcessApplicationCount;
            set => SetProperty(ref _pendingProcessApplicationCount, value);
        }

        public ObservableCollection<string> WorkflowSteps { get; } = new();
        public ObservableCollection<string> SectionHighlights { get; } = new();
        public ObservableCollection<string> LocationInsights { get; } = new();
        public ObservableCollection<string> OutboundCapacityInsights { get; } = new();
        public ObservableCollection<string> HandoverInsights { get; } = new();
        public ObservableCollection<string> LifecycleInsights { get; } = new();
        public ObservableCollection<string> RiskInsights { get; } = new();

        public ICommand RefreshCommand { get; }

        public HardDiskMediaPageViewModel(IHardDiskMediaService hardDiskMediaService, IDialogService dialogService)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;

            RefreshCommand = new RelayCommand(async _ => await RefreshOverviewAsync());
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
            BlankInStockCount = overview.BlankInStockCount;
            BorrowedCount = overview.BorrowedCount;
            DataCarrierInStockCount = overview.DataCarrierInStockCount;
            DamagedInStockCount = overview.DamagedInStockCount;
            TransferOutCount = overview.TransferOutCount;
            NeedReturnMediumCount = overview.NeedReturnMediumCount;
            LongTermNeedReturnMediumCount = overview.LongTermNeedReturnMediumCount;
            TemporaryNeedReturnMediumCount = overview.TemporaryNeedReturnMediumCount;
            MissingLocationMediumCount = overview.MissingLocationMediumCount;
            OutboundWithoutKeeperMediumCount = overview.OutboundWithoutKeeperMediumCount;
            SubmittedApplicationCount = overview.SubmittedApplicationCount;
            PendingSignedFileCount = overview.PendingSignedFileCount;
            PendingProcessApplicationCount = overview.PendingProcessApplicationCount;

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

            switch (section)
            {
                case HardDiskMediaWorkbenchSection.Ledger:
                    SectionHighlights.Add("线上申请");
                    SectionHighlights.Add("打印审批单");
                    SectionHighlights.Add("线下签字");
                    SectionHighlights.Add("拍照上传");
                    SectionHighlights.Add("业务办理");
                    SectionTitle = "初始登记";
                    SectionDescription = "统一查看硬盘基础信息、当前位置、当前状态和介质属性，后续作为所有业务办理的基础台账入口。";
                    SectionFocus = "首版重点：先打通台账、状态、位置与生命周期主线，不直接在界面层写业务规则。";
                    WorkflowSteps.Add("新增或导入空白硬盘基础信息，默认进入“在库空白”。");
                    WorkflowSteps.Add("同一块硬盘始终保留一条主记录，通过申请和流转台账追踪后续变化。");
                    WorkflowSteps.Add("载体转化后仍保留原编号，避免“空白硬盘”和“有数据硬盘”拆成两条孤立记录。");
                    break;

                case HardDiskMediaWorkbenchSection.OutboundApplication:
                    SectionHighlights.Add("线上申请");
                    SectionHighlights.Add("打印审批单");
                    SectionHighlights.Add("线下签字");
                    SectionHighlights.Add("拍照上传");
                    SectionHighlights.Add("业务办理");
                    SectionTitle = "介质出库申请";
                    SectionDescription = "临时、长期、永久和销毁等出库动作统一从申请入口发起，并进入审批办理闭环。";
                    SectionFocus = "首版重点：形成申请单主档、打印页、签字件上传和办理前置校验。";
                    WorkflowSteps.Add("申请人在线填写介质出库申请并提交。");
                    WorkflowSteps.Add("系统打印审批单，线下完成签字确认。");
                    WorkflowSteps.Add("上传签字后的照片或扫描件，进入“待办理”。");
                    WorkflowSteps.Add("资料室按签字件办理实际流转，并回写主表状态与流转台账。");
                    break;

                case HardDiskMediaWorkbenchSection.Approval:
                    SectionHighlights.Add("线上申请");
                    SectionHighlights.Add("打印审批单");
                    SectionHighlights.Add("线下签字");
                    SectionHighlights.Add("拍照上传");
                    SectionHighlights.Add("业务办理");
                    SectionTitle = "审批办理";
                    SectionDescription = "审批办理区同时承接“线上审批留痕”和“线下签字回传后的业务执行”，适应当前网络环境的混合办公模式。";
                    SectionFocus = "首版重点：将“审批通过”和“业务已办结”分开，避免纯线上审批直接改状态。";
                    WorkflowSteps.Add("资料室管理员接收已提交申请，完成线上审批结论记录。");
                    WorkflowSteps.Add("确认纸质签字件已回传后，申请单状态转为“待办理”。");
                    WorkflowSteps.Add("只有“待办理”状态的申请单允许正式执行业务动作。");
                    WorkflowSteps.Add("办理完成后自动写入流转台账，并将申请单置为“已办结”。");
                    break;

                case HardDiskMediaWorkbenchSection.ReturnRegistration:
                    SectionHighlights.Add("归还登记");
                    SectionHighlights.Add("挂失登记");
                    SectionHighlights.Add("打印登记单");
                    SectionHighlights.Add("线下签字");
                    SectionHighlights.Add("业务办理");
                    SectionTitle = "介质归还登记";
                    SectionDescription = "临时或长期出库的介质，由申请人发起归还/挂失登记，登记无审批环节，签字回传后直接办理。";
                    SectionFocus = "首版重点：登记单与审批申请分离，确保“登记类无审批、签字后办理”的流程边界。";
                    WorkflowSteps.Add("登记人选择归还(空盘/资料/损坏)或挂失登记类型。");
                    WorkflowSteps.Add("打印登记单并完成申请人与资料室管理员交接签字。");
                    WorkflowSteps.Add("上传签字件后进入“待办理”并执行状态回写。");
                    WorkflowSteps.Add("办理完成后自动写入流转台账，形成闭环痕迹。");
                    break;

                case HardDiskMediaWorkbenchSection.Transaction:
                    SectionHighlights.Add("线上申请");
                    SectionHighlights.Add("打印审批单");
                    SectionHighlights.Add("线下签字");
                    SectionHighlights.Add("拍照上传");
                    SectionHighlights.Add("业务办理");
                    SectionTitle = "流转台账";
                    SectionDescription = "按时间线查询每块硬盘从登记、借出、归还、转化、移交到销毁的全过程。";
                    SectionFocus = "首版重点：主表看当前，流转表看历史，避免靠备注拼业务过程。";
                    WorkflowSteps.Add("每次状态变化都落一条流转台账记录。");
                    WorkflowSteps.Add("对外移交单独建语义，不等同于“借出未归还”。");
                    WorkflowSteps.Add("转为资料载体是独立业务动作，不通过手工改状态代替。");
                    break;

                case HardDiskMediaWorkbenchSection.Overview:
                    SectionHighlights.Add("初始登记");
                    SectionHighlights.Add("介质出库申请");
                    SectionHighlights.Add("审批办理");
                    SectionHighlights.Add("介质归还登记");
                    SectionHighlights.Add("流转台账");
                    SectionTitle = "硬盘概览";
                    SectionDescription = "汇总展示硬盘介质模块的核心入口、位置分布、容量占用、生命周期结构和交接办理主线。";
                    SectionFocus = "本页侧重全局分析：既看介质当前位置，也看出库容量体量、生命周期结构与借出、归还、挂失等交接环节风险。";
                    WorkflowSteps.Add("初始登记：维护基础信息，执行模板导出、模板说明和台账导入。");
                    WorkflowSteps.Add("介质出库申请：发起临时/长期/永久/销毁等出库申请，并打印审批单。");
                    WorkflowSteps.Add("审批办理：记录审批结论，回传签字件，办结实际业务。");
                    WorkflowSteps.Add("介质归还登记：办理归还/挂失登记并回传签字件后办结。");
                    WorkflowSteps.Add("流转台账：按时间线追踪状态、位置和办理过程。");
                    break;

                default:
                    SectionTitle = "介质管理（硬盘）";
                    SectionDescription = "硬盘介质全生命周期工作台。";
                    SectionFocus = string.Empty;
                    break;
            }
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
