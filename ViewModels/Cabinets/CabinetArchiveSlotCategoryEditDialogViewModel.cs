using System;
using System.Collections.ObjectModel;
using System.Linq;
using DocMgr.Models.Cabinets;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Cabinets
{
    /// <summary>
    /// 标准滑道式档案柜档口用途选择对话框。
    /// </summary>
    public class CabinetArchiveSlotCategoryEditDialogViewModel : ViewModelBase
    {
        private CabinetArchiveSlotCategoryOption? _selectedOption;

        public CabinetArchiveSlotCategoryEditDialogViewModel(string title, string summary, string? initialCategoryName)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "设置档口用途" : title.Trim();
            Summary = string.IsNullOrWhiteSpace(summary)
                ? "请选择档口用途。标准滑道式档案柜档口用于存放档案盒内的模拟介质资料。"
                : summary.Trim();
            Options = new ObservableCollection<CabinetArchiveSlotCategoryOption>(BuildOptions());
            _selectedOption = ResolveInitialOption(initialCategoryName);
            ConfirmCommand = new RelayCommand(_ => RequestClose?.Invoke(true));
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Title { get; }

        public string Summary { get; }

        public ObservableCollection<CabinetArchiveSlotCategoryOption> Options { get; }

        public CabinetArchiveSlotCategoryOption? SelectedOption
        {
            get => _selectedOption;
            set => SetProperty(ref _selectedOption, value);
        }

        public CabinetArchiveSlotCategoryEditResult? Result =>
            SelectedOption == null
                ? null
                : new CabinetArchiveSlotCategoryEditResult(SelectedOption.CategoryName);

        public RelayCommand ConfirmCommand { get; }

        public RelayCommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private static ObservableCollection<CabinetArchiveSlotCategoryOption> BuildOptions()
        {
            return
            [
                new CabinetArchiveSlotCategoryOption(
                    CabinetArchiveSlotCategoryAssignment.CategoryUnset,
                    "未定义（未设置）"),
                new CabinetArchiveSlotCategoryOption(
                    CabinetArchiveSlotCategoryAssignment.CategoryYearlyMaterials,
                    "年度资料专用档口"),
                new CabinetArchiveSlotCategoryOption(
                    CabinetArchiveSlotCategoryAssignment.CategoryHistoricalMaterials,
                    "历史资料专用档口"),
                new CabinetArchiveSlotCategoryOption(
                    CabinetArchiveSlotCategoryAssignment.CategoryMixed,
                    "混用档口")
            ];
        }

        private CabinetArchiveSlotCategoryOption? ResolveInitialOption(string? initialCategoryName)
        {
            string normalized = CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(initialCategoryName);
            foreach (var option in Options)
            {
                if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(normalized, option.CategoryName))
                {
                    return option;
                }
            }

            return Options.FirstOrDefault();
        }
    }

    public sealed class CabinetArchiveSlotCategoryOption
    {
        public CabinetArchiveSlotCategoryOption(string categoryName, string displayName)
        {
            CategoryName = categoryName;
            DisplayName = displayName;
        }

        public string CategoryName { get; }

        public string DisplayName { get; }
    }
}
