using System;
using DocMgr.Models.Cabinets;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Cabinets
{
    public class CabinetArchiveBoxPlacementEditDialogViewModel : ViewModelBase
    {
        private CabinetArchiveBoxPlacementMode _selectedMode;

        public CabinetArchiveBoxPlacementEditDialogViewModel(string title, string summary, CabinetArchiveBoxPlacementMode initialMode)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "设置放置方式" : title.Trim();
            Summary = string.IsNullOrWhiteSpace(summary) ? "请选择档案盒放置方式。" : summary.Trim();
            _selectedMode = initialMode;
            ConfirmCommand = new RelayCommand(_ => RequestClose?.Invoke(true));
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Title { get; }

        public string Summary { get; }

        public CabinetArchiveBoxPlacementMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (!SetProperty(ref _selectedMode, value)) return;
                OnPropertyChanged(nameof(IsSpineOut));
                OnPropertyChanged(nameof(IsFrontOut));
            }
        }

        public bool IsSpineOut
        {
            get => SelectedMode == CabinetArchiveBoxPlacementMode.SpineOut;
            set
            {
                if (value)
                {
                    SelectedMode = CabinetArchiveBoxPlacementMode.SpineOut;
                }
            }
        }

        public bool IsFrontOut
        {
            get => SelectedMode == CabinetArchiveBoxPlacementMode.FrontOut;
            set
            {
                if (value)
                {
                    SelectedMode = CabinetArchiveBoxPlacementMode.FrontOut;
                }
            }
        }

        public RelayCommand ConfirmCommand { get; }

        public RelayCommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;
    }
}
