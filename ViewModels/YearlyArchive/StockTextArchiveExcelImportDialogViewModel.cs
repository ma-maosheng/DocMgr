using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 存档文本直办 Excel 导入预览。
    /// </summary>
    public sealed class StockTextArchiveExcelImportDialogViewModel : ViewModelBase
    {
        private readonly IStockTextArchiveDirectFilingService _filingService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly IReadOnlyList<StockTextArchiveExcelBoxDraft> _boxes;

        private bool _isBusy;
        private string _summaryText = "正在校验…";
        private bool _imported;
        private string _importProgressStatus = string.Empty;
        private string _importProgressPercentText = string.Empty;
        private double _importProgressValue;
        private bool _importProgressIsIndeterminate;

        public StockTextArchiveExcelImportDialogViewModel(
            IReadOnlyList<StockTextArchiveExcelBoxDraft> boxes,
            IStockTextArchiveDirectFilingService filingService,
            IUserContextService userContextService,
            IDialogService dialogService)
        {
            _boxes = boxes ?? Array.Empty<StockTextArchiveExcelBoxDraft>();
            _filingService = filingService;
            _userContextService = userContextService;
            _dialogService = dialogService;
            Rows = new ObservableCollection<StockTextArchiveExcelImportRowViewModel>();
            ConfirmCommand = new RelayCommand(async _ => await ConfirmAsync(), _ => !IsBusy && ImportableCount > 0);
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(_imported), _ => !IsBusy);
        }

        public ObservableCollection<StockTextArchiveExcelImportRowViewModel> Rows { get; }

        public ICommand ConfirmCommand { get; }

        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        public bool Imported => _imported;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string SummaryText
        {
            get => _summaryText;
            private set => SetProperty(ref _summaryText, value);
        }

        /// <summary>
        /// 底部导入进度说明。
        /// </summary>
        public string ImportProgressStatus
        {
            get => _importProgressStatus;
            private set => SetProperty(ref _importProgressStatus, value);
        }

        /// <summary>
        /// 底部导入进度分数，如 3 / 10。
        /// </summary>
        public string ImportProgressPercentText
        {
            get => _importProgressPercentText;
            private set => SetProperty(ref _importProgressPercentText, value);
        }

        /// <summary>
        /// 底部导入进度 0–100。
        /// </summary>
        public double ImportProgressValue
        {
            get => _importProgressValue;
            private set => SetProperty(ref _importProgressValue, value);
        }

        /// <summary>
        /// 导入尚未给出明确总量时为不确定进度。
        /// </summary>
        public bool ImportProgressIsIndeterminate
        {
            get => _importProgressIsIndeterminate;
            private set => SetProperty(ref _importProgressIsIndeterminate, value);
        }

        public int ImportableCount => Rows.Count(item => item.CanImport);

        public async Task InitializeAsync()
        {
            IsBusy = true;
            try
            {
                var validations = await _filingService.ValidateExcelImportAsync(
                    _boxes,
                    _userContextService.CurrentUser);
                Rows.Clear();
                foreach (var item in validations)
                {
                    Rows.Add(new StockTextArchiveExcelImportRowViewModel(item));
                }

                RefreshSummary();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ConfirmAsync()
        {
            var importable = Rows.Where(item => item.CanImport).Select(item => item.Box).ToList();
            if (importable.Count == 0)
            {
                _dialogService.ShowMessage("没有可通过校验的档案盒。");
                return;
            }

            if (!_dialogService.ShowConfirm(
                    $"将按表内物理位置导入 {importable.Count} 盒（跳过审批）。失败盒不会阻断后续盒。是否继续？",
                    "确认 Excel 导入立档"))
            {
                return;
            }

            IsBusy = true;
            BeginImportProgress("正在按盒立档…");
            StockTextArchiveExcelImportCommitResult result;
            try
            {
                var progress = new ImmediateUiProgress<(int Current, int Total, string Status)>(item =>
                    ReportImportProgress(item.Current, item.Total, item.Status));
                result = await _filingService.CommitExcelImportAsync(
                    importable,
                    _userContextService.CurrentUser,
                    progress);

                ReportImportProgress(
                    importable.Count,
                    importable.Count,
                    $"导入完成：成功 {result.SucceededCount}，失败 {result.FailedCount}，跳过 {result.SkippedCount}。");
                _imported = result.SucceededCount > 0;
                _dialogService.ShowMessage(result.Summary, "导入结果");
                RequestClose?.Invoke(_imported);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BeginImportProgress(string status)
        {
            ImportProgressIsIndeterminate = true;
            ImportProgressValue = 0;
            ImportProgressPercentText = string.Empty;
            ImportProgressStatus = status;
            PumpDispatcher();
        }

        private void ReportImportProgress(int current, int total, string? status)
        {
            if (total <= 0)
            {
                BeginImportProgress(string.IsNullOrWhiteSpace(status) ? ImportProgressStatus : status.Trim());
                return;
            }

            int safeCurrent = current < 0 ? 0 : current;
            if (safeCurrent > total)
            {
                safeCurrent = total;
            }

            ImportProgressIsIndeterminate = false;
            ImportProgressValue = 100d * safeCurrent / total;
            ImportProgressPercentText = $"{safeCurrent} / {total}";
            if (!string.IsNullOrWhiteSpace(status))
            {
                ImportProgressStatus = status.Trim();
            }

            PumpDispatcher();
        }

        private static void PumpDispatcher()
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (!dispatcher.CheckAccess())
            {
                dispatcher.Invoke(PumpDispatcher, DispatcherPriority.Send);
                return;
            }

            var frame = new DispatcherFrame();
            dispatcher.BeginInvoke(
                DispatcherPriority.Render,
                new DispatcherOperationCallback(static state =>
                {
                    ((DispatcherFrame)state!).Continue = false;
                    return null;
                }),
                frame);
            Dispatcher.PushFrame(frame);
        }

        private sealed class ImmediateUiProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;

            public ImmediateUiProgress(Action<T> handler)
            {
                _handler = handler;
            }

            public void Report(T value)
            {
                Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                if (dispatcher.CheckAccess())
                {
                    _handler(value);
                    return;
                }

                dispatcher.Invoke(() => _handler(value), DispatcherPriority.Send);
            }
        }

        private void RefreshSummary()
        {
            int totalItems = Rows.Sum(item => item.ItemCount);
            SummaryText =
                $"共 {Rows.Count} 盒、{totalItems} 条子项；可通过校验 {ImportableCount} 盒。"
                + " 资料名称沿用表中值；子类按名称映射；档口使用表内档案盒编号。";
        }
    }

    /// <summary>
    /// Excel 导入预览行。
    /// </summary>
    public sealed class StockTextArchiveExcelImportRowViewModel
    {
        public StockTextArchiveExcelImportRowViewModel(StockTextArchiveExcelBoxValidation validation)
        {
            Box = validation.Box;
            SequenceNo = validation.Box.SequenceNo;
            Year = validation.Box.Year;
            ProjectName = validation.Box.ProjectName;
            MaterialName = validation.Box.MaterialName;
            BoxSpecification = validation.Box.BoxSpecification;
            Location = validation.Box.NormalizedBoxLocationCode;
            ItemCount = validation.Box.Items.Count;
            CanImport = validation.CanImport;
            StatusText = validation.CanImport ? "可导入" : "不可导入";
            ErrorText = validation.Errors.Count == 0
                ? string.Empty
                : string.Join("；", validation.Errors);
        }

        public StockTextArchiveExcelBoxDraft Box { get; }

        public int SequenceNo { get; }

        public string Year { get; }

        public string ProjectName { get; }

        public string MaterialName { get; }

        public string BoxSpecification { get; }

        public string Location { get; }

        public int ItemCount { get; }

        public bool CanImport { get; }

        public string StatusText { get; }

        public string ErrorText { get; }
    }
}
