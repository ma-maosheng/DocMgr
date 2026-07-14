using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料归还明细编辑行：借出份数只读，完好归还份数可编辑，灭失份数自动计算。
    /// </summary>
    public sealed class ArchiveReturnItemEditRowViewModel : ViewModelBase
    {
        public event EventHandler? ReturnCopyCountsChanged;

        public ArchiveReturnItemEditRowViewModel(YearlyArchiveReturnItem source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ArchiveReturnDomainValues.NormalizeReturnCopyCounts(Source);
        }

        public YearlyArchiveReturnItem Source { get; }

        public int SortOrder => Source.SortOrder;

        public int? ItemArchiveYear => Source.ItemArchiveYear;

        public string MediaKind => Source.MediaKind;

        public string MediaType => Source.MediaType;

        public string UsageModeDisplay => Source.UsageModeDisplay;

        public string MaterialName => Source.MaterialName;

        public string ItemName => Source.ItemName;

        public string SelectionScopeDisplay => Source.SelectionScopeDisplay;

        public string ContainerCode => Source.ContainerCode;

        public string StorageLocation => Source.StorageLocation;

        public string CurrentContainerCode => Source.CurrentContainerCode;

        public string CurrentStorageLocation => Source.CurrentStorageLocation;

        public string ContainerStatusDisplay => Source.ContainerStatusDisplay;

        public string ContainerStatusWarning => Source.ContainerStatusWarning;

        public bool NeedsRehome => Source.BlocksWithoutRehome;

        public string RehomeTargetBoxDisplay =>
            string.IsNullOrWhiteSpace(Source.RehomeTargetBoxDisplay)
                ? (Source.RehomeTargetBoxId is int id && id > 0 ? $"#{id}" : string.Empty)
                : Source.RehomeTargetBoxDisplay;

        public string ContainerStatusHintText
        {
            get
            {
                if (Source.BlocksWithoutRehome)
                {
                    return string.IsNullOrWhiteSpace(RehomeTargetBoxDisplay)
                        ? "盒已失效（需指定目标盒）"
                        : $"盒已失效 → {RehomeTargetBoxDisplay}";
                }

                if (string.Equals(
                        Source.ContainerStatusKind,
                        ArchiveReturnContainerAssessment.StatusLocationChanged,
                        StringComparison.Ordinal))
                {
                    return "盒位已变";
                }

                return Source.ContainerStatusDisplay;
            }
        }

        public int ReturnCopyCount => ArchiveReturnDomainValues.ResolveBorrowedCopyCount(Source);

        public int LossCopyCount => ArchiveReturnDomainValues.ResolveLossCopyCount(Source);

        public string ConfidentialLevelDisplay => Source.ConfidentialLevelDisplay;

        public string DiskInfo => Source.DiskInfo;

        public string Remark
        {
            get => Source.Remark;
            set
            {
                if (!string.Equals(Source.Remark, value, StringComparison.Ordinal))
                {
                    Source.Remark = value;
                    OnPropertyChanged();
                }
            }
        }

        public int IntactReturnCopyCount
        {
            get => ArchiveReturnDomainValues.ResolveIntactReturnCopyCount(Source);
            set
            {
                int borrowed = ReturnCopyCount;
                int clamped = Math.Clamp(value, 0, borrowed);
                if (Source.IntactReturnCopyCount == clamped
                    && Source.LossCopyCount == borrowed - clamped)
                {
                    return;
                }

                Source.IntactReturnCopyCount = clamped;
                Source.LossCopyCount = borrowed - clamped;
                ArchiveReturnDomainValues.SyncItemConditionFromCopyCounts(Source);
                OnPropertyChanged();
                OnPropertyChanged(nameof(LossCopyCount));
                ReturnCopyCountsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
