using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using DocMgr.Models.HistoryArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Shared
{
    /// <summary>
    /// 工作表选择结果。
    /// </summary>
    public sealed class SheetSelectionResult
    {
        public SheetSelectionResult(string sheetName, bool expandItemsByTextLine, string? category = null)
        {
            SheetName = sheetName ?? string.Empty;
            ExpandItemsByTextLine = expandItemsByTextLine;
            Category = category?.Trim() ?? string.Empty;
        }

        public string SheetName { get; }

        /// <summary>
        /// 勾选时按内容单元格内的文本行拆分记录；否则按 Excel 表格行导入。
        /// </summary>
        public bool ExpandItemsByTextLine { get; }

        /// <summary>
        /// 历史存档导入的分类；未启用分类输入时为空。
        /// </summary>
        public string Category { get; }
    }

    public class SheetSelectionDialogViewModel : ViewModelBase
    {
        private const string DefaultExpandOptionContent = "以文本行为单位展开资料子项";
        private const string DefaultExpandOptionToolTip =
            "勾选后，「子项名称」单元格内每一非空文本行导入为一条资料子项；不勾选则以 Excel 表格行作为一条资料子项。";

        private readonly IDialogService _dialogService;
        private string _selectedSheet = string.Empty;
        private string _category = string.Empty;
        private bool _expandItemsByTextLine;
        private bool _syncCategoryFromSheet = true;

        public SheetSelectionDialogViewModel(
            IEnumerable<string> sheetNames,
            IDialogService dialogService,
            bool showExpandItemsByTextLineOption = false,
            string? expandItemsByTextLineContent = null,
            string? expandItemsByTextLineToolTip = null,
            bool showCategoryInput = false,
            string? categoryNamePrefix = null)
        {
            _dialogService = dialogService;
            SheetNames = (sheetNames ?? Enumerable.Empty<string>()).ToList();
            _selectedSheet = SheetNames.FirstOrDefault() ?? string.Empty;
            ShowExpandItemsByTextLineOption = showExpandItemsByTextLineOption;
            ExpandItemsByTextLineContent = string.IsNullOrWhiteSpace(expandItemsByTextLineContent)
                ? DefaultExpandOptionContent
                : expandItemsByTextLineContent.Trim();
            ExpandItemsByTextLineToolTip = string.IsNullOrWhiteSpace(expandItemsByTextLineToolTip)
                ? DefaultExpandOptionToolTip
                : expandItemsByTextLineToolTip.Trim();
            ShowCategoryInput = showCategoryInput;
            CategoryPrefix = categoryNamePrefix?.Trim() ?? string.Empty;
            if (ShowCategoryInput)
            {
                _category = BuildDefaultCategory(_selectedSheet);
            }

            ConfirmCommand = new RelayCommand(_ => Confirm(), _ => CanConfirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public List<string> SheetNames { get; }

        /// <summary>
        /// 是否显示「以文本行为单位拆分」勾选（存档文本 / 其他资料导入等场景）。
        /// </summary>
        public bool ShowExpandItemsByTextLineOption { get; }

        /// <summary>
        /// 勾选框显示文案。
        /// </summary>
        public string ExpandItemsByTextLineContent { get; }

        /// <summary>
        /// 勾选框提示。
        /// </summary>
        public string ExpandItemsByTextLineToolTip { get; }

        /// <summary>
        /// 是否显示「分类」输入（历史存档 Excel 导入）。
        /// </summary>
        public bool ShowCategoryInput { get; }

        /// <summary>
        /// 分类须符合的前缀（如「地形图：」），用于确定时校验。
        /// </summary>
        public string CategoryPrefix { get; }

        public string SelectedSheet
        {
            get => _selectedSheet;
            set
            {
                if (!SetProperty(ref _selectedSheet, value))
                {
                    return;
                }

                if (ShowCategoryInput && _syncCategoryFromSheet)
                {
                    Category = BuildDefaultCategory(value);
                    _syncCategoryFromSheet = true;
                }

                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// 分类全文（默认「前缀 + 工作表名」）；确定时校验须以规定前缀开头且后缀非空。
        /// </summary>
        public string Category
        {
            get => _category;
            set
            {
                string next = value ?? string.Empty;
                if (!SetProperty(ref _category, next))
                {
                    return;
                }

                string trimmed = next.Trim();
                string autoDefault = BuildDefaultCategory(_selectedSheet);
                _syncCategoryFromSheet = string.Equals(trimmed, autoDefault, StringComparison.Ordinal);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// 以文本行为单位拆分内容字段。
        /// </summary>
        public bool ExpandItemsByTextLine
        {
            get => _expandItemsByTextLine;
            set => SetProperty(ref _expandItemsByTextLine, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private bool CanConfirm()
            => !string.IsNullOrWhiteSpace(SelectedSheet);

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(SelectedSheet))
            {
                _dialogService.ShowMessage("请选择一个有效的工作表！");
                return;
            }

            if (ShowCategoryInput)
            {
                if (!HistoryArchiveImportTableNameSupport.TryValidateCategoryName(
                        Category,
                        CategoryPrefix,
                        out string? errorMessage))
                {
                    _dialogService.ShowMessage(errorMessage ?? "分类格式不正确。");
                    return;
                }

                Category = HistoryArchiveImportTableNameSupport.NormalizeCategoryName(Category);
            }

            RequestClose?.Invoke(true);
        }

        private string BuildDefaultCategory(string? sheetName)
        {
            if (string.IsNullOrWhiteSpace(sheetName))
            {
                return CategoryPrefix;
            }

            if (string.IsNullOrWhiteSpace(CategoryPrefix))
            {
                return sheetName.Trim();
            }

            return HistoryArchiveImportTableNameSupport.BuildDefaultCategoryName(CategoryPrefix, sheetName);
        }
    }
}
