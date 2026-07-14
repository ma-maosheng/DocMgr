using System;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HardDiskMedia
{
    public sealed record HardDiskMediaApplicantOption
    {
        public string ApplicantName { get; init; } = string.Empty;

        public string ApplicantDept { get; init; } = string.Empty;

        public string DisplayText => string.IsNullOrWhiteSpace(ApplicantDept)
            ? ApplicantName
            : $"{ApplicantName} / {ApplicantDept}";
    }

    /// <summary>
    /// 出库申请可选硬盘列表项。
    /// </summary>
    public sealed class HardDiskMediaOutboundMediumOption : ViewModelBase
    {
        private bool _isSelected;

        public int Id { get; init; }

        public string DisplayText { get; init; } = string.Empty;

        public string CurrentLocation { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public sealed record HardDiskMediaReturnMediumOption
    {
        public int Id { get; init; }

        public string DisplayText { get; init; } = string.Empty;

        public string CurrentLocation { get; init; } = string.Empty;

        public string OriginalLocation { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string ApplicantDept { get; init; } = string.Empty;

        public int? SourceApplicationId { get; init; }

        public int? SourceOutboundRecordId { get; init; }

        public string SourceApplicationNo { get; init; } = string.Empty;

        public DateTime? ExpectedReturnDate { get; init; }
    }
}
