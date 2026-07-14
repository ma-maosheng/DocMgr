using System.Collections.ObjectModel;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Cabinets
{
    /// <summary>
    /// 档案柜检索：在平面布局中打开档案柜，查看档口与存放内容。
    /// </summary>
    public class CabinetSearchViewModel : ViewModelBase, ICabinetLayoutInteractionHost
    {
        private readonly ICabinetService _cabinetService;
        private readonly IDialogService _dialogService;

        private ObservableCollection<Cabinet> _cabinets = new();

        public CabinetSearchViewModel(ICabinetService cabinetService, IDialogService dialogService)
        {
            _cabinetService = cabinetService;
            _dialogService = dialogService;

            RefreshCommand = new RelayCommand(_ => LoadData());
            OpenSelectedFaceACommand = new RelayCommand(_ => OpenCabinet(SelectedCabinet, CabinetFace.A), _ => SelectedCabinet != null);
            OpenSelectedFaceBCommand = new RelayCommand(_ => OpenCabinet(SelectedCabinet, CabinetFace.B), _ => SelectedCabinet?.HasMultipleFaces == true);
            OpenCabinetCommand = new RelayCommand<Cabinet>(cabinet => OpenCabinet(cabinet, CabinetFace.A));
            OpenCabinetFaceACommand = new RelayCommand<Cabinet>(cabinet => OpenCabinet(cabinet, CabinetFace.A));
            OpenCabinetFaceBCommand = new RelayCommand<Cabinet>(cabinet => OpenCabinet(cabinet, CabinetFace.B));
            SelectCabinetCommand = new RelayCommand<Cabinet>(cab =>
            {
                if (cab != null)
                {
                    SelectedCabinet = cab;
                }
            });
            ClearSelectionCommand = new RelayCommand(_ => SelectedCabinet = null);
            SaveLocationCommand = new RelayCommand<Cabinet>(_ => { });

            LoadData();
        }

        public CabinetLayoutWorkspaceMode WorkspaceMode => CabinetLayoutWorkspaceMode.Search;

        public bool AllowOpenOnDoubleClick => true;

        public bool AllowLayoutEdit => false;

        public ObservableCollection<Cabinet> Cabinets
        {
            get => _cabinets;
            set => SetProperty(ref _cabinets, value);
        }

        private Cabinet? _selectedCabinet;

        public Cabinet? SelectedCabinet
        {
            get => _selectedCabinet;
            set
            {
                if (SetProperty(ref _selectedCabinet, value))
                {
                    foreach (var cab in Cabinets)
                    {
                        cab.IsSelected = cab == value;
                    }

                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public RelayCommand RefreshCommand { get; }

        public RelayCommand OpenSelectedFaceACommand { get; }

        public RelayCommand OpenSelectedFaceBCommand { get; }

        public RelayCommand<Cabinet> SelectCabinetCommand { get; }

        public RelayCommand ClearSelectionCommand { get; }

        public RelayCommand<Cabinet> SaveLocationCommand { get; }

        public RelayCommand<Cabinet>? OpenCabinetCommand { get; }

        public RelayCommand<Cabinet>? OpenCabinetFaceACommand { get; }

        public RelayCommand<Cabinet>? OpenCabinetFaceBCommand { get; }

        public RelayCommand? EditCommand => null;

        public RelayCommand<Cabinet>? RotateCommand => null;

        public RelayCommand<Cabinet>? DeleteCabinetCommand => null;

        private void LoadData()
        {
            var list = _cabinetService.GetAllCabinets();
            Cabinets = new ObservableCollection<Cabinet>(list);
            SelectedCabinet = null;
        }

        private void OpenCabinet(Cabinet? cabinet, CabinetFace face)
        {
            if (cabinet == null)
            {
                return;
            }

            SelectedCabinet = cabinet;

            _dialogService.ShowCabinetOpenDialog(new CabinetOpenRequest
            {
                CabinetId = cabinet.Id,
                CabinetName = cabinet.Name,
                CabinetType = cabinet.Type,
                Face = cabinet.HasMultipleFaces ? face : CabinetFace.A,
                LayerCount = cabinet.LayerCount,
                ColumnCount = cabinet.ColumnCount,
                WidthCm = cabinet.Width,
                HeightCm = cabinet.Height,
                DepthCm = cabinet.Depth
            });
        }
    }
}
