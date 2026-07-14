using System;
using System.Collections.ObjectModel;
using System.Linq;
using DocMgr.Models.Cabinets;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Cabinets
{
    public class CabinetHardDiskSlotCategoryEditDialogViewModel : ViewModelBase
    {
        private CabinetHardDiskSlotCategoryOption? _selectedOption;

        public CabinetHardDiskSlotCategoryEditDialogViewModel(string title, string summary, string? initialCategoryName)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "设置档口用途" : title.Trim();
            Summary = string.IsNullOrWhiteSpace(summary) ? "请选择档口专用用途。" : summary.Trim();
            Options = new ObservableCollection<CabinetHardDiskSlotCategoryOption>(BuildOptions());
            _selectedOption = ResolveInitialOption(initialCategoryName);
            ConfirmCommand = new RelayCommand(_ => RequestClose?.Invoke(true));
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Title { get; }

        public string Summary { get; }

        public ObservableCollection<CabinetHardDiskSlotCategoryOption> Options { get; }

        public CabinetHardDiskSlotCategoryOption? SelectedOption
        {
            get => _selectedOption;
            set => SetProperty(ref _selectedOption, value);
        }

        public CabinetHardDiskSlotCategoryEditResult? Result =>
            SelectedOption == null
                ? null
                : new CabinetHardDiskSlotCategoryEditResult(SelectedOption.CategoryName);

        public RelayCommand ConfirmCommand { get; }

        public RelayCommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private static ObservableCollection<CabinetHardDiskSlotCategoryOption> BuildOptions()
        {
            return
            [
                new CabinetHardDiskSlotCategoryOption(string.Empty, "通用（无专用用途）"),
                new CabinetHardDiskSlotCategoryOption(CabinetHardDiskSlotCategoryAssignment.CategoryDamaged, "损坏硬盘专用"),
                new CabinetHardDiskSlotCategoryOption(CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc, "损坏光盘专用"),
                new CabinetHardDiskSlotCategoryOption(CabinetHardDiskSlotCategoryAssignment.CategoryData, "年度数据硬盘专用"),
                new CabinetHardDiskSlotCategoryOption(CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc, "年度数据光盘专用"),
                new CabinetHardDiskSlotCategoryOption(CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataHardDisk, "历史数据硬盘专用"),
                new CabinetHardDiskSlotCategoryOption(CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataOpticalDisc, "历史数据光盘专用"),
                new CabinetHardDiskSlotCategoryOption(CabinetHardDiskSlotCategoryAssignment.CategoryBlank, "空白硬盘专用")
            ];
        }

        private CabinetHardDiskSlotCategoryOption? ResolveInitialOption(string? initialCategoryName)
        {
            string normalized = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(initialCategoryName);
            foreach (var option in Options)
            {
                if (string.IsNullOrEmpty(normalized) && string.IsNullOrEmpty(option.CategoryName))
                {
                    return option;
                }

                if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(normalized, option.CategoryName))
                {
                    return option;
                }
            }

            return Options.FirstOrDefault();
        }
    }

    public sealed class CabinetHardDiskSlotCategoryOption
    {
        public CabinetHardDiskSlotCategoryOption(string categoryName, string displayName)
        {
            CategoryName = categoryName;
            DisplayName = displayName;
        }

        public string CategoryName { get; }

        public string DisplayName { get; }
    }
}
