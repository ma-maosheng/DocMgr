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
        private IReadOnlyList<StockTextArchiveExcelBoxValidation> _validations =
            Array.Empty<StockTextArchiveExcelBoxValidation>();

        private bool _isBusy;
        private string _summaryText = "正在校验…";
        private bool _imported;
        private string _importProgressStatus = string.Empty;
        private string _importProgressPercentText = string.Empty;
        private double _importProgressValue;
        private bool _importProgressIsIndeterminate;
        private bool _expandByBox;

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
            ConfirmCommand = new RelayCommand(async _ => await ConfirmAsync(), _ => !IsBusy && CanConfirmImport);
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(_imported), _ => !IsBusy);
        }

        public ObservableCollection<StockTextArchiveExcelImportRowViewModel> Rows { get; }

        public ICommand ConfirmCommand { get; }

        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        public bool Imported => _imported;

        /// <summary>
        /// 为 true 时按档案盒一行展示（子项字段汇总）；为 false 时按资料子项逐行展开。
        /// </summary>
        public bool ExpandByBox
        {
            get => _expandByBox;
            set
            {
                if (SetProperty(ref _expandByBox, value))
                {
                    RebuildRows();
                    RefreshSummary();
                }
            }
        }

        /// <summary>全部档案盒均通过逻辑校验时才允许确认导入。</summary>
        public bool CanConfirmImport =>
            _validations.Count > 0 && _validations.All(item => item.CanImport);

        public int InvalidCount => _validations.Count(item => !item.CanImport);

        public int BoxCount => _validations.Count;

        public int ItemCount => _validations.Sum(item => item.Box.Items.Count);

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

        public async Task InitializeAsync()
        {
            IsBusy = true;
            try
            {
                var validations = await _filingService.ValidateExcelImportAsync(
                    _boxes,
                    _userContextService.CurrentUser);
                _validations = validations;
                RebuildRows();
                RefreshSummary();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RebuildRows()
        {
            Rows.Clear();
            foreach (var validation in _validations)
            {
                if (ExpandByBox)
                {
                    Rows.Add(StockTextArchiveExcelImportRowViewModel.CreateForBox(validation));
                    continue;
                }

                var items = validation.Box.Items;
                if (items.Count == 0)
                {
                    Rows.Add(StockTextArchiveExcelImportRowViewModel.CreateForItem(validation, null));
                    continue;
                }

                foreach (var item in items)
                {
                    Rows.Add(StockTextArchiveExcelImportRowViewModel.CreateForItem(validation, item));
                }
            }
        }

        private async Task ConfirmAsync()
        {
            if (!CanConfirmImport)
            {
                _dialogService.ShowMessage(
                    $"当前有 {InvalidCount} 盒未通过逻辑校验，须全部修正通过后才能开始导入。",
                    "无法导入");
                return;
            }

            var importable = _validations.Select(item => item.Box).ToList();
            if (!_dialogService.ShowConfirm(
                    $"全部 {importable.Count} 盒、{ItemCount} 条子项已通过逻辑校验，将按表内物理位置整批导入（跳过审批）。\n"
                    + "导入过程中若某一盒写入失败，将停止后续盒，请确认后继续。",
                    "确认 Excel 导入立档"))
            {
                return;
            }

            IsBusy = true;
            BeginImportProgress("正在整批复核并立档…");
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
                    result.SucceededCount == importable.Count && result.FailedCount == 0
                        ? $"导入完成：成功 {result.SucceededCount} 盒。"
                        : $"导入结束：成功 {result.SucceededCount}，失败 {result.FailedCount}。");
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
            if (BoxCount == 0)
            {
                SummaryText = "未解析到档案盒。";
                return;
            }

            string viewHint = ExpandByBox
                ? "当前按档案盒汇总展示。"
                : "当前按资料子项逐行展开。";

            if (CanConfirmImport)
            {
                SummaryText =
                    $"共 {BoxCount} 盒、{ItemCount} 条子项，全部通过逻辑校验，可以开始导入。"
                    + $" {viewHint} 档口使用表内档案盒编号。";
                return;
            }

            SummaryText =
                $"共 {BoxCount} 盒、{ItemCount} 条子项；未通过 {InvalidCount} 盒。"
                + $" {viewHint} 须全部通过逻辑校验后才能开始导入，请按「校验」列修正 Excel 后重新选择文件。";
        }
    }

    /// <summary>
    /// Excel 导入预览行（可按子项展开，或按档案盒汇总）。
    /// </summary>
    public sealed class StockTextArchiveExcelImportRowViewModel
    {
        private StockTextArchiveExcelImportRowViewModel()
        {
        }

        public static StockTextArchiveExcelImportRowViewModel CreateForItem(
            StockTextArchiveExcelBoxValidation validation,
            StockTextArchiveMediaItemDraft? item)
        {
            var row = CreateBoxFields(validation);
            row.ItemName = item?.ContentDesc ?? string.Empty;
            row.CopyCountText = item == null ? string.Empty : item.ContentCount.ToString();
            row.ConfidentialLevel = item?.ConfidentialLevel ?? string.Empty;
            row.MaterialCategory = item?.MaterialCategory ?? string.Empty;
            row.SubCategory = item?.SubCategory ?? string.Empty;
            row.OrganizationForm = item?.OrganizationForm ?? string.Empty;
            row.ItemNote = item?.Note ?? string.Empty;
            return row;
        }

        public static StockTextArchiveExcelImportRowViewModel CreateForBox(
            StockTextArchiveExcelBoxValidation validation)
        {
            var row = CreateBoxFields(validation);
            var items = validation.Box.Items ?? Array.Empty<StockTextArchiveMediaItemDraft>();
            row.ItemName = string.Join("；", items
                .Select(item => item.ContentDesc?.Trim() ?? string.Empty)
                .Where(name => name.Length > 0));
            row.CopyCountText = items.Count == 0
                ? string.Empty
                : items.Sum(item => item.ContentCount).ToString();
            row.ConfidentialLevel = JoinDistinct(items.Select(item => item.ConfidentialLevel));
            row.MaterialCategory = JoinDistinct(items.Select(item => item.MaterialCategory));
            row.SubCategory = JoinDistinct(items.Select(item => item.SubCategory));
            row.OrganizationForm = JoinDistinct(items.Select(item => item.OrganizationForm));
            row.ItemNote = JoinDistinct(items.Select(item => item.Note));
            return row;
        }

        private static StockTextArchiveExcelImportRowViewModel CreateBoxFields(
            StockTextArchiveExcelBoxValidation validation)
        {
            return new StockTextArchiveExcelImportRowViewModel
            {
                Box = validation.Box,
                SequenceNo = validation.Box.SequenceNo,
                Year = validation.Box.Year,
                ProjectName = validation.Box.ProjectName,
                ProjectCode = validation.Box.ProjectCode,
                MaterialName = validation.Box.MaterialName,
                SourceType = validation.Box.SourceType,
                ProvideUnit = validation.Box.ProvideUnit,
                ArchivePurpose = validation.Box.ArchivePurpose,
                MediaType = validation.Box.MediaType,
                BoxCountText = validation.Box.ClaimedBoxCount?.ToString() ?? string.Empty,
                BoxLocation = string.IsNullOrWhiteSpace(validation.Box.NormalizedBoxLocationCode)
                    ? validation.Box.SourceBoxLocationCode
                    : validation.Box.NormalizedBoxLocationCode,
                BoxSpecification = validation.Box.BoxSpecification,
                BoxRemarks = validation.Box.Remarks,
                CanImport = validation.CanImport,
                StatusText = validation.CanImport ? "可导入" : "不可导入",
                ErrorText = validation.Errors.Count == 0
                    ? string.Empty
                    : string.Join("；", validation.Errors)
            };
        }

        private static string JoinDistinct(IEnumerable<string?> values)
        {
            return string.Join("；", values
                .Select(value => value?.Trim() ?? string.Empty)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal));
        }

        public StockTextArchiveExcelBoxDraft Box { get; private init; } = null!;

        public int SequenceNo { get; private init; }

        public string Year { get; private init; } = string.Empty;

        public string ProjectName { get; private init; } = string.Empty;

        public string ProjectCode { get; private init; } = string.Empty;

        public string MaterialName { get; private init; } = string.Empty;

        public string SourceType { get; private init; } = string.Empty;

        public string ProvideUnit { get; private init; } = string.Empty;

        public string ArchivePurpose { get; private init; } = string.Empty;

        public string MediaType { get; private init; } = string.Empty;

        public string BoxCountText { get; private init; } = string.Empty;

        public string BoxLocation { get; private init; } = string.Empty;

        public string BoxSpecification { get; private init; } = string.Empty;

        public string BoxRemarks { get; private init; } = string.Empty;

        public string ItemName { get; private set; } = string.Empty;

        public string CopyCountText { get; private set; } = string.Empty;

        public string ConfidentialLevel { get; private set; } = string.Empty;

        public string MaterialCategory { get; private set; } = string.Empty;

        public string SubCategory { get; private set; } = string.Empty;

        public string OrganizationForm { get; private set; } = string.Empty;

        public string ItemNote { get; private set; } = string.Empty;

        public bool CanImport { get; private init; }

        public string StatusText { get; private init; } = string.Empty;

        public string ErrorText { get; private init; } = string.Empty;
    }
}
