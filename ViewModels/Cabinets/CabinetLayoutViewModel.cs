using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Cabinets
{
    /// <summary>
    /// 档案柜登记：维护资料室平面布局中的柜体增删、编号、旋转与位置。
    /// </summary>
    public class CabinetLayoutViewModel : ViewModelBase, ICabinetLayoutInteractionHost
    {
        private readonly ICabinetService _cabinetService;
        private readonly IDialogService _dialogService;

        private ObservableCollection<Cabinet> _cabinets = new();

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

        public CabinetLayoutWorkspaceMode WorkspaceMode => CabinetLayoutWorkspaceMode.Register;

        public bool AllowOpenOnDoubleClick => false;

        public bool AllowLayoutEdit => true;

        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand<Cabinet> RotateCommand { get; }
        public RelayCommand<Cabinet>? OpenCabinetCommand => null;
        public RelayCommand<Cabinet>? OpenCabinetFaceACommand => null;
        public RelayCommand<Cabinet>? OpenCabinetFaceBCommand => null;
        public RelayCommand<Cabinet> DeleteCabinetCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand<Cabinet> SaveLocationCommand { get; }
        public RelayCommand<Cabinet> SelectCabinetCommand { get; }
        public RelayCommand ClearSelectionCommand { get; }

        private readonly List<string> _namePool =
        [
            "甲","乙","丙","丁","戊","己","庚","辛","壬","癸",
            "子","丑","寅","卯","辰","巳","午","未","申","酉","戌","亥"
        ];

        private const double StandardTrackLeft = 560;
        private const double StandardTrackWidth = 18;
        private const double StandardTrackRight = 1082;
        private const double StandardTrackTop = 70;
        private const double StandardCabinetThickness = 40;
        private const double StandardCabinetLengthOverflow = 20;
        private const int DefaultStandardCabinetCount = 7;
        private const double DefaultMagneticCabinetLeft = 410;
        private const double DefaultMagneticCabinetTop = 150;
        private const double DefaultMagneticCabinetWidth = 70;
        private const double DefaultMagneticCabinetHeight = 150;
        private const double DefaultMagneticCabinetDepth = 52;

        public CabinetLayoutViewModel(ICabinetService cabinetService, IDialogService dialogService)
        {
            _cabinetService = cabinetService;
            _dialogService = dialogService;

            RefreshCommand = new RelayCommand(_ => LoadData());
            AddCommand = new RelayCommand(_ => AddCabinet());
            EditCommand = new RelayCommand(_ => EditCabinet(), _ => SelectedCabinet != null);
            RotateCommand = new RelayCommand<Cabinet>(RotateCabinet);
            DeleteCabinetCommand = new RelayCommand<Cabinet>(DeleteCabinetByKey);
            DeleteCommand = new RelayCommand(_ => DeleteCabinet(), _ => SelectedCabinet != null);
            SaveLocationCommand = new RelayCommand<Cabinet>(SaveCabinetLocation);

            SelectCabinetCommand = new RelayCommand<Cabinet>(cab =>
            {
                if (cab != null) SelectedCabinet = cab;
            });

            ClearSelectionCommand = new RelayCommand(_ => SelectedCabinet = null);

            LoadData();
        }

        private void LoadData()
        {
            var list = _cabinetService.GetAllCabinets();
            if (list.Count == 0)
            {
                list = CreateDefaultCabinets();
                foreach (var cabinet in list)
                {
                    _cabinetService.AddCabinet(cabinet);
                }

                list = _cabinetService.GetAllCabinets();
            }

            Cabinets = new ObservableCollection<Cabinet>(list);
            SelectedCabinet = null;
        }

        private List<Cabinet> CreateDefaultCabinets()
        {
            var generatedCabinets = new List<Cabinet>();

            for (int i = 0; i < DefaultStandardCabinetCount; i++)
            {
                generatedCabinets.Add(new Cabinet
                {
                    Name = _namePool[i],
                    Type = CabinetType.Standard,
                    FaceCount = 2,
                    LayerCount = 6,
                    ColumnCount = 3,
                    Width = GetStandardCabinetWidth(),
                    Height = StandardCabinetThickness,
                    Depth = 25,
                    CanvasLeft = GetStandardCabinetLeft(),
                    CanvasTop = StandardTrackTop + (i * StandardCabinetThickness),
                    RotationAngle = 0
                });
            }

            generatedCabinets.Add(new Cabinet
            {
                Name = _namePool[DefaultStandardCabinetCount],
                Type = CabinetType.MagneticDisk,
                FaceCount = 1,
                LayerCount = 9,
                ColumnCount = 4,
                Width = DefaultMagneticCabinetWidth,
                Height = DefaultMagneticCabinetHeight,
                Depth = DefaultMagneticCabinetDepth,
                CanvasLeft = DefaultMagneticCabinetLeft,
                CanvasTop = DefaultMagneticCabinetTop,
                RotationAngle = 0
            });

            return generatedCabinets;
        }

        private void AddCabinet()
        {
            string nextName = GenerateNextName();

            var newCab = new Cabinet
            {
                Name = nextName,
                Type = CabinetType.Standard,
                FaceCount = 2,
                LayerCount = 6,
                ColumnCount = 3,
                Width = 80,
                Height = 120,
                Depth = 25,
                CanvasLeft = 800,
                CanvasTop = 240
            };

            if (_dialogService.ShowCabinetEditDialog(newCab))
            {
                if (newCab.Type == CabinetType.Standard)
                {
                    ApplyStandardTrackPlacement(newCab);
                }

                _cabinetService.AddCabinet(newCab);
                LoadData();
                _dialogService.ShowMessage("新增资料柜成功！");
            }
        }

        private void ApplyStandardTrackPlacement(Cabinet cabinet)
        {
            ArgumentNullException.ThrowIfNull(cabinet);

            cabinet.Width = GetStandardCabinetWidth();
            cabinet.Height = StandardCabinetThickness;
            cabinet.CanvasLeft = GetStandardCabinetLeft();
            cabinet.CanvasTop = GetNextStandardTrackTop();
            cabinet.RotationAngle = 0;
        }

        private static double GetStandardCabinetWidth()
        {
            double leftTrackCenter = StandardTrackLeft + (StandardTrackWidth / 2d);
            double rightTrackCenter = StandardTrackRight + (StandardTrackWidth / 2d);
            return (rightTrackCenter - leftTrackCenter) + StandardCabinetLengthOverflow;
        }

        private static double GetStandardCabinetLeft()
        {
            double leftTrackCenter = StandardTrackLeft + (StandardTrackWidth / 2d);
            return leftTrackCenter - (StandardCabinetLengthOverflow / 2d);
        }

        private double GetNextStandardTrackTop()
        {
            var occupiedSlots = Cabinets
                .Where(cabinet => cabinet.Type == CabinetType.Standard)
                .Select(cabinet => (int)Math.Round((cabinet.CanvasTop - StandardTrackTop) / StandardCabinetThickness, MidpointRounding.AwayFromZero))
                .Where(slotIndex => slotIndex >= 0)
                .ToHashSet();

            int slotIndex = 0;
            while (occupiedSlots.Contains(slotIndex))
            {
                slotIndex++;
            }

            return StandardTrackTop + (slotIndex * StandardCabinetThickness);
        }

        private string GenerateNextName()
        {
            var existingNames = new HashSet<string>(CabsNames());

            foreach (var name in _namePool)
            {
                if (!existingNames.Contains(name))
                {
                    return name;
                }
            }

            int i = 1;
            while (true)
            {
                string name = $"新{i}";
                if (!existingNames.Contains(name)) return name;
                i++;
            }

            IEnumerable<string> CabsNames() => Cabinets.Select(c => c.Name);
        }

        private void EditCabinet()
        {
            if (SelectedCabinet == null)
            {
                _dialogService.ShowMessage("请先选择一个资料柜。", "提示");
                return;
            }

            var originalType = SelectedCabinet.Type;
            var editableCabinet = CloneCabinet(SelectedCabinet);

            if (_dialogService.ShowCabinetEditDialog(editableCabinet))
            {
                ApplyCabinetEdits(SelectedCabinet, editableCabinet);

                if (originalType != CabinetType.Standard && SelectedCabinet.Type == CabinetType.Standard)
                {
                    ApplyStandardTrackPlacement(SelectedCabinet);
                }

                _cabinetService.UpdateCabinet(SelectedCabinet);
                LoadData();
                _dialogService.ShowMessage("更新资料柜成功！");
            }
        }

        private static Cabinet CloneCabinet(Cabinet source)
        {
            ArgumentNullException.ThrowIfNull(source);

            return new Cabinet
            {
                Id = source.Id,
                Name = source.Name,
                Type = source.Type,
                Width = source.Width,
                Height = source.Height,
                Depth = source.Depth,
                CanvasLeft = source.CanvasLeft,
                CanvasTop = source.CanvasTop,
                FaceCount = source.FaceCount,
                LayerCount = source.LayerCount,
                ColumnCount = source.ColumnCount,
                RotationAngle = source.RotationAngle,
                IsSelected = source.IsSelected
            };
        }

        private static void ApplyCabinetEdits(Cabinet target, Cabinet source)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(source);

            target.Name = source.Name;
            target.Type = source.Type;
            target.Width = source.Width;
            target.Height = source.Height;
            target.Depth = source.Depth;
            target.CanvasLeft = source.CanvasLeft;
            target.CanvasTop = source.CanvasTop;
            target.FaceCount = source.FaceCount;
            target.LayerCount = source.LayerCount;
            target.ColumnCount = source.ColumnCount;
            target.RotationAngle = source.RotationAngle;
        }

        private void DeleteCabinet()
        {
            if (SelectedCabinet == null)
            {
                _dialogService.ShowMessage("请先选择一个资料柜。", "提示");
                return;
            }

            if (_dialogService.ShowConfirm($"确定要删除资料柜 [{SelectedCabinet.Name}] 吗？", "警告"))
            {
                _cabinetService.DeleteCabinet(SelectedCabinet.Id);
                LoadData();
            }
        }

        private void RotateCabinet(Cabinet cab)
        {
            if (cab == null) return;

            cab.RotationAngle += 90;
            if (cab.RotationAngle >= 360) cab.RotationAngle = 0;
            _cabinetService.UpdateCabinet(cab);
        }

        private void DeleteCabinetByKey(Cabinet cab)
        {
            if (cab == null) return;
            SelectedCabinet = cab;
            DeleteCabinet();
        }

        private void SaveCabinetLocation(Cabinet cab)
        {
            if (cab != null)
            {
                _cabinetService.UpdateCabinet(cab);
            }
        }
    }
}
