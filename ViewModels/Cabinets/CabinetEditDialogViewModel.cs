using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Cabinets
{
    public class CabinetEditDialogViewModel : ViewModelBase
    {
        private const double StandardWidth = 80d;
        private const double StandardHeight = 120d;
        private const double StandardDepth = 25d;
        private const double VerticalWidth = 50d;
        private const double VerticalHeight = 50d;
        private const double VerticalDepth = 38d;
        private const double HorizontalWidth = 80d;
        private const double HorizontalHeight = 40d;
        private const double HorizontalDepth = 38d;
        private const double MagneticDiskDoubleDoorWidth = 140d;
        private const double MagneticDiskSingleDoorWidth = 70d;
        private const double MagneticDiskHeight = 150d;
        private const double MagneticDiskDepth = 52d;

        private readonly IDialogService _dialogService;
        private readonly Cabinet _cabinet;

        private string _selectedName;
        private CabinetType _selectedCabinetType;
        private int _faceCount;
        private int _layerCount;
        private int _columnCount;
        private string _description = string.Empty;
        private int _selectedMagneticDoorCount;
        private int _selectedMagneticDrawerCount;
        private int _selectedMagneticColumnCount;
        private double _editableWidth;
        private double _editableHeight;
        private double _editableDepth;

        public CabinetEditDialogViewModel(Cabinet cabinetToEdit, IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(dialogService);

            _dialogService = dialogService;
            _cabinet = cabinetToEdit ?? new Cabinet();

            NameOptions = new List<string>
            {
                "甲","乙","丙","丁","戊","己","庚","辛","壬","癸",
                "子","丑","寅","卯","辰","巳","午","未","申","酉","戌","亥"
            };

            CabinetTypeOptions = new List<KeyValuePair<CabinetType, string>>
            {
                new KeyValuePair<CabinetType, string>(CabinetType.Standard, "标准滑道式档案柜"),
                new KeyValuePair<CabinetType, string>(CabinetType.Vertical, "立式文件柜"),
                new KeyValuePair<CabinetType, string>(CabinetType.Horizontal, "卧式文件柜"),
                new KeyValuePair<CabinetType, string>(CabinetType.MagneticDisk, "防磁磁盘柜")
            };

            MagneticDoorOptions =
            [
                new KeyValuePair<int, string>(2, "双门（左门、右门）"),
                new KeyValuePair<int, string>(1, "单门")
            ];

            DrawerCountOptions = Enumerable.Range(1, 18).ToList();
            MagneticColumnOptions = Enumerable.Range(1, 12).ToList();

            _selectedName = NameOptions.Contains(_cabinet.Name) ? _cabinet.Name : NameOptions[0];
            _selectedCabinetType = _cabinet.Type;
            _selectedMagneticDoorCount = _cabinet.Type == CabinetType.MagneticDisk && _cabinet.FaceCount > 0 ? _cabinet.FaceCount : 2;
            _selectedMagneticDrawerCount = _cabinet.Type == CabinetType.MagneticDisk && _cabinet.LayerCount > 0 ? _cabinet.LayerCount : 9;
            _selectedMagneticColumnCount = _cabinet.Type == CabinetType.MagneticDisk && _cabinet.ColumnCount > 0 ? _cabinet.ColumnCount : 6;
            (_editableWidth, _editableHeight, _editableDepth) = ResolveInitialDimensions(_cabinet, _selectedCabinetType, _selectedMagneticDoorCount);

            SyncDisplayFromType(_selectedCabinetType, preserveDimensions: true);

            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public List<string> NameOptions { get; }

        public List<KeyValuePair<CabinetType, string>> CabinetTypeOptions { get; }

        public List<KeyValuePair<int, string>> MagneticDoorOptions { get; }

        public List<int> DrawerCountOptions { get; }

        public List<int> MagneticColumnOptions { get; }

        public string SelectedName
        {
            get => _selectedName;
            set => SetProperty(ref _selectedName, value);
        }

        public CabinetType SelectedCabinetType
        {
            get => _selectedCabinetType;
            set
            {
                if (!SetProperty(ref _selectedCabinetType, value)) return;

                SyncDisplayFromType(value);
                OnPropertyChanged(nameof(IsMagneticDiskCabinetSelected));
                OnPropertyChanged(nameof(DefaultSpecificationVisibility));
                OnPropertyChanged(nameof(MagneticSpecificationVisibility));
                OnPropertyChanged(nameof(SpecificationSectionTitle));
                OnPropertyChanged(nameof(DimensionSummary));
            }
        }

        public int SelectedMagneticDoorCount
        {
            get => _selectedMagneticDoorCount;
            set
            {
                if (!SetProperty(ref _selectedMagneticDoorCount, value))
                {
                    return;
                }

                RefreshMagneticDisplayCounts();
                ApplyMagneticDoorDimensions();
            }
        }

        public int SelectedMagneticDrawerCount
        {
            get => _selectedMagneticDrawerCount;
            set
            {
                if (!SetProperty(ref _selectedMagneticDrawerCount, value))
                {
                    return;
                }

                RefreshMagneticDisplayCounts();
            }
        }

        public int SelectedMagneticColumnCount
        {
            get => _selectedMagneticColumnCount;
            set
            {
                if (!SetProperty(ref _selectedMagneticColumnCount, value))
                {
                    return;
                }

                RefreshMagneticDisplayCounts();
            }
        }

        public int FaceCount
        {
            get => _faceCount;
            set => SetProperty(ref _faceCount, value);
        }

        public int LayerCount
        {
            get => _layerCount;
            set => SetProperty(ref _layerCount, value);
        }

        public int ColumnCount
        {
            get => _columnCount;
            set => SetProperty(ref _columnCount, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public double EditableWidth
        {
            get => _editableWidth;
            set
            {
                if (SetProperty(ref _editableWidth, value))
                {
                    OnPropertyChanged(nameof(DimensionSummary));
                }
            }
        }

        public double EditableHeight
        {
            get => _editableHeight;
            set
            {
                if (SetProperty(ref _editableHeight, value))
                {
                    OnPropertyChanged(nameof(DimensionSummary));
                }
            }
        }

        public double EditableDepth
        {
            get => _editableDepth;
            set
            {
                if (SetProperty(ref _editableDepth, value))
                {
                    OnPropertyChanged(nameof(DimensionSummary));
                }
            }
        }

        public bool IsMagneticDiskCabinetSelected => SelectedCabinetType == CabinetType.MagneticDisk;

        public Visibility DefaultSpecificationVisibility => IsMagneticDiskCabinetSelected ? Visibility.Collapsed : Visibility.Visible;

        public Visibility MagneticSpecificationVisibility => IsMagneticDiskCabinetSelected ? Visibility.Visible : Visibility.Collapsed;

        public string SpecificationSectionTitle => IsMagneticDiskCabinetSelected ? "规格参数（可按防磁磁盘柜设置）" : "规格参数（系统自动设定）";

        public string DimensionSummary
        {
            get => $"柜体尺寸：高 {EditableHeight:0}cm × 宽 {EditableWidth:0}cm × 深 {EditableDepth:0}cm";
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(SelectedName))
            {
                _dialogService.ShowMessage("请选择柜子编号！", "提示");
                return;
            }

            if (EditableWidth <= 0d || EditableHeight <= 0d || EditableDepth <= 0d)
            {
                _dialogService.ShowMessage("柜体尺寸必须为大于 0 的数值。", "提示");
                return;
            }

            _cabinet.Name = SelectedName;
            ApplyTypeRules(_cabinet, SelectedCabinetType, SelectedMagneticDoorCount, SelectedMagneticDrawerCount, SelectedMagneticColumnCount);
            _cabinet.Width = EditableWidth;
            _cabinet.Height = EditableHeight;
            _cabinet.Depth = EditableDepth;

            RequestClose?.Invoke(true);
        }

        private static void ApplyTypeRules(Cabinet cab, CabinetType type, int magneticDoorCount, int magneticDrawerCount, int magneticColumnCount)
        {
            ArgumentNullException.ThrowIfNull(cab);

            cab.Type = type;

            switch (type)
            {
                case CabinetType.Standard:
                    cab.FaceCount = 2;
                    cab.LayerCount = 6;
                    cab.ColumnCount = 3;
                    cab.Width = StandardWidth;
                    cab.Height = StandardHeight;
                    cab.Depth = StandardDepth;
                    break;
                case CabinetType.Vertical:
                    cab.FaceCount = 1;
                    cab.LayerCount = 4;
                    cab.ColumnCount = 1;
                    cab.Width = VerticalWidth;
                    cab.Height = VerticalHeight;
                    cab.Depth = VerticalDepth;
                    break;
                case CabinetType.Horizontal:
                    cab.FaceCount = 1;
                    cab.LayerCount = 1;
                    cab.ColumnCount = 1;
                    cab.Width = HorizontalWidth;
                    cab.Height = HorizontalHeight;
                    cab.Depth = HorizontalDepth;
                    break;
                case CabinetType.MagneticDisk:
                    cab.FaceCount = magneticDoorCount <= 1 ? 1 : 2;
                    cab.LayerCount = Math.Max(1, magneticDrawerCount);
                    cab.ColumnCount = Math.Max(1, magneticColumnCount);
                    cab.Width = ResolveMagneticDiskWidth(magneticDoorCount);
                    cab.Height = MagneticDiskHeight;
                    cab.Depth = MagneticDiskDepth;
                    break;
            }
        }

        private void SyncDisplayFromType(CabinetType type, bool preserveDimensions = false)
        {
            switch (type)
            {
                case CabinetType.Standard:
                    FaceCount = 2;
                    LayerCount = 6;
                    ColumnCount = 3;
                    break;
                case CabinetType.Vertical:
                    FaceCount = 1;
                    LayerCount = 4;
                    ColumnCount = 1;
                    break;
                case CabinetType.Horizontal:
                    FaceCount = 1;
                    LayerCount = 1;
                    ColumnCount = 1;
                    break;
                case CabinetType.MagneticDisk:
                    RefreshMagneticDisplayCounts();
                    break;
                default:
                    FaceCount = 0;
                    LayerCount = 0;
                    ColumnCount = 0;
                    break;
            }

            Description = type switch
            {
                CabinetType.Standard => "双面密集架，容量大，底座带导轨标识。",
                CabinetType.Vertical => "单面立式柜，常规办公文件存储。",
                CabinetType.Horizontal => "单面卧式柜，适用于图纸或特殊资料平铺。",
                CabinetType.MagneticDisk => "防磁磁盘柜按门数对应左门/右门，按抽屉层数与每抽屉格数生成开柜格口；可在下方继续微调柜体尺寸。",
                _ => string.Empty
            };

            if (!preserveDimensions)
            {
                ApplyDefaultDimensions(type);
            }

            OnPropertyChanged(nameof(DimensionSummary));
        }

        private void RefreshMagneticDisplayCounts()
        {
            if (!IsMagneticDiskCabinetSelected)
            {
                return;
            }

            FaceCount = SelectedMagneticDoorCount;
            LayerCount = SelectedMagneticDrawerCount;
            ColumnCount = SelectedMagneticColumnCount;
        }

        private void ApplyDefaultDimensions(CabinetType type)
        {
            var (width, height, depth) = ResolveDimensions(type, SelectedMagneticDoorCount);
            EditableWidth = width;
            EditableHeight = height;
            EditableDepth = depth;
        }

        private void ApplyMagneticDoorDimensions()
        {
            if (!IsMagneticDiskCabinetSelected)
            {
                return;
            }

            EditableWidth = ResolveMagneticDiskWidth(SelectedMagneticDoorCount);
            EditableHeight = MagneticDiskHeight;
            EditableDepth = MagneticDiskDepth;
        }

        private static (double Width, double Height, double Depth) ResolveInitialDimensions(Cabinet cabinet, CabinetType type, int magneticDoorCount)
        {
            ArgumentNullException.ThrowIfNull(cabinet);

            var defaults = ResolveDimensions(type, magneticDoorCount);
            return (
                cabinet.Width > 0d ? cabinet.Width : defaults.Width,
                cabinet.Height > 0d ? cabinet.Height : defaults.Height,
                cabinet.Depth > 0d ? cabinet.Depth : defaults.Depth);
        }

        private static (double Width, double Height, double Depth) ResolveDimensions(CabinetType type, int magneticDoorCount)
        {
            return type switch
            {
                CabinetType.Standard => (StandardWidth, StandardHeight, StandardDepth),
                CabinetType.Vertical => (VerticalWidth, VerticalHeight, VerticalDepth),
                CabinetType.Horizontal => (HorizontalWidth, HorizontalHeight, HorizontalDepth),
                CabinetType.MagneticDisk => (ResolveMagneticDiskWidth(magneticDoorCount), MagneticDiskHeight, MagneticDiskDepth),
                _ => (0d, 0d, 0d)
            };
        }

        private static double ResolveMagneticDiskWidth(int magneticDoorCount)
        {
            return magneticDoorCount <= 1 ? MagneticDiskSingleDoorWidth : MagneticDiskDoubleDoorWidth;
        }
    }
}