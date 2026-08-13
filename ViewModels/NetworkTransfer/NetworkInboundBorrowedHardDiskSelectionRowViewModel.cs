using DocMgr.Models.HardDiskMedia;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.NetworkTransfer
{
    /// <summary>
    /// 档外资料入网申请时，申请人可选的借出硬盘候选项。
    /// </summary>
    public sealed class NetworkInboundBorrowedHardDiskSelectionRowViewModel : ViewModelBase
    {
        private bool _isSelected;

        public NetworkInboundBorrowedHardDiskSelectionRowViewModel(
            HardDiskMediaReturnCandidate candidate,
            bool isSelected,
            bool isEditable)
        {
            Candidate = candidate;
            _isSelected = isSelected;
            IsEditable = isEditable;
        }

        public HardDiskMediaReturnCandidate Candidate { get; }

        public bool IsEditable { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string DiskCode => Candidate.DiskCode;

        public string SourceApplicationNo => Candidate.SourceApplicationNo;

        public string BorrowedLocation => Candidate.BorrowedLocation;

        public string CurrentStatus => Candidate.CurrentStatus;
    }
}
