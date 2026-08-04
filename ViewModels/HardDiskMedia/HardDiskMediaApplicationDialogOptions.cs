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

        public string DiskCode { get; init; } = string.Empty;

        public string SerialNumber { get; init; } = string.Empty;

        public string DiskType { get; init; } = string.Empty;

        public string Brand { get; init; } = string.Empty;

        public string Capacity { get; init; } = string.Empty;

        public string InterfaceType { get; init; } = string.Empty;

        public string RegisterPerson { get; init; } = string.Empty;

        public DateTime RegisterDate { get; init; }

        public DateTime? FactoryDate { get; init; }

        public string RegistrationMethod { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;

        /// <summary>
        /// 台账存放位置原文（空白盘通常即为档口键）。
        /// </summary>
        public string CurrentLocation { get; init; } = string.Empty;

        /// <summary>
        /// 所在档口编号（柜面-层-列，不含档内序号）。
        /// </summary>
        public string SlotCode { get; init; } = string.Empty;

        public string RegisterDateDisplay => RegisterDate == default
            ? string.Empty
            : RegisterDate.ToString("yyyy-MM-dd");

        public string FactoryDateDisplay => FactoryDate?.ToString("yyyy-MM-dd") ?? string.Empty;

        public string DisplayText =>
            $"{DiskCode} / {SerialNumber} / {Capacity} / {InterfaceType}";

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
