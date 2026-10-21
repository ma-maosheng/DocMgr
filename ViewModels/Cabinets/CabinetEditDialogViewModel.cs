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
        private const double MagneticDiskWidth = 140d;
        private const double MagneticDiskHeight = 150d;
        private const double MagneticDiskDepth = 52d;
        private const int DefaultMagneticLayerCount = 9;
        private const int DefaultMagneticColumnCount = 6;
        private const int DefaultVerticalLayerCount = 4;
        private const int DefaultVerticalColumnCount = 1;
        private const int DefaultHorizontalLayerCount = 1;
        private const int DefaultHorizontalColumnCount = 1;

        private readonly IDialogService _dialogService;
        private readonly ICabinetService _cabinetService;
        private readonly Cabinet _cabinet;
        private readonly IReadOnlyList<CabinetStoredContentSlot> _storedContentSlots;

        private string _selectedName;
        private CabinetType _selectedCabinetType;
        private int _faceCount;
        private int _layerCount;
        private int _columnCount;
        private string _description = string.Empty;
        private int _selectedStructureLayerCount;
        private int _selectedStructureColumnCount;
        private double _editableWidth;
        private double _editableHeight;
        private double _editableDepth;
        private int _hardDiskSlotCapacity;
        private int _opticalDiscSlotCapacity;

        public CabinetEditDialogViewModel(Cabinet cabinetToEdit, IDialogService dialogService, ICabinetService cabinetService)
        {
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(cabinetService);

            _dialogService = dialogService;
            _cabinetService = cabinetService;
            _cabinet = cabinetToEdit ?? new Cabinet();
            _storedContentSlots = _cabinet.Id > 0
                ? _cabinetService.GetStoredContentSlots(_cabinet.Id)
                : Array.Empty<CabinetStoredContentSlot>();

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

            LayerCountOptions = Enumerable.Range(1, 18).ToList();
            ColumnCountOptions = Enumerable.Range(1, 12).ToList();

            _selectedName = NameOptions.Contains(_cabinet.Name) ? _cabinet.Name : NameOptions[0];
            _selectedCabinetType = _cabinet.Type;
            _selectedStructureLayerCount = ResolveInitialLayerCount(_cabinet);
            _selectedStructureColumnCount = ResolveInitialColumnCount(_cabinet);
            (_editableWidth, _editableHeight, _editableDepth) = ResolveInitialDimensions(_cabinet, _selectedCabinetType);
            _hardDiskSlotCapacity = ResolveInitialHardDiskSlotCapacity(_cabinet);
            _opticalDiscSlotCapacity = ResolveInitialOpticalDiscSlotCapacity(_cabinet);

            SyncDisplayFromType(_selectedCabinetType, preserveDimensions: true);

            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public List<string> NameOptions { get; }

        public List<KeyValuePair<CabinetType, string>> CabinetTypeOptions { get; }

        public List<int> LayerCountOptions { get; }

        public List<int> ColumnCountOptions { get; }

        /// <summary>防磁磁盘柜、立式文件柜、卧式文件柜的门数固定为双门。</summary>
        public string DoorCountDisplay => "双门";

        /// <summary>双门共同组成一个 A 面。</summary>
        public string DoorCountHint => "双门共同组成 A 面，不设 B 面。";

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
                NotifyStructurePresentationChanged();
            }
        }

        public int SelectedStructureLayerCount
        {
            get => _selectedStructureLayerCount;
            set
            {
                if (!SetProperty(ref _selectedStructureLayerCount, value))
                {
                    return;
                }

                RefreshStructureDisplayCounts();
            }
        }

        public int SelectedStructureColumnCount
        {
            get => _selectedStructureColumnCount;
            set
            {
                if (!SetProperty(ref _selectedStructureColumnCount, value))
                {
                    return;
                }

                RefreshStructureDisplayCounts();
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

        /// <summary>防磁磁盘柜：硬盘类档口每格最大块数。</summary>
        public int HardDiskSlotCapacity
        {
            get => _hardDiskSlotCapacity;
            set => SetProperty(ref _hardDiskSlotCapacity, value);
        }

        /// <summary>防磁磁盘柜：光盘类档口每格最大张数。</summary>
        public int OpticalDiscSlotCapacity
        {
            get => _opticalDiscSlotCapacity;
            set => SetProperty(ref _opticalDiscSlotCapacity, value);
        }

        public bool IsMagneticDiskCabinetSelected => SelectedCabinetType == CabinetType.MagneticDisk;

        public bool IsStructureCabinetSelected => IsStructureCabinet(SelectedCabinetType);

        public Visibility DefaultSpecificationVisibility => IsStructureCabinetSelected ? Visibility.Collapsed : Visibility.Visible;

        public Visibility StructureSpecificationVisibility => IsStructureCabinetSelected ? Visibility.Visible : Visibility.Collapsed;

        public Visibility MagneticCapacityVisibility => IsMagneticDiskCabinetSelected ? Visibility.Visible : Visibility.Collapsed;

        public string StructureLayerEditorLabel => IsMagneticDiskCabinetSelected ? "抽屉层数：" : "层数：";

        public string SpecificationSectionTitle => SelectedCabinetType switch
        {
            CabinetType.MagneticDisk => "规格参数（可按防磁磁盘柜设置）",
            CabinetType.Vertical or CabinetType.Horizontal => "规格参数（可按文件柜设置）",
            _ => "规格参数（系统自动设定）"
        };

        public string DimensionSummary
        {
            get => $"柜体尺寸：高 {EditableHeight:0}cm × 宽 {EditableWidth:0}cm × 深 {EditableDepth:0}cm";
        }

        public bool HasStoredContent => _storedContentSlots.Count > 0;

        public Visibility StoredContentNoticeVisibility => HasStoredContent ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>柜内已有内容时，在编辑窗中列出档口并说明不得改写已有存放记录。</summary>
        public string StoredContentNotice => HasStoredContent
            ? CabinetStoredContentEditNotice.Build(_cabinet.Name, _storedContentSlots)
            : string.Empty;

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

            if (IsMagneticDiskCabinetSelected)
            {
                if (HardDiskSlotCapacity <= 0 || OpticalDiscSlotCapacity <= 0)
                {
                    _dialogService.ShowMessage("档口最大容量必须为大于 0 的整数。", "提示");
                    return;
                }

                if (HardDiskSlotCapacity > 200 || OpticalDiscSlotCapacity > 200)
                {
                    _dialogService.ShowMessage("档口最大容量不宜超过 200，请核对后重试。", "提示");
                    return;
                }
            }

            Cabinet proposed = CreateProposedCabinet();
            try
            {
                _cabinetService.EnsureCabinetEditKeepsStoredContent(proposed);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowMessage(ex.Message, "无法保存");
                return;
            }

            CopyProposedCabinet(_cabinet, proposed);
            RequestClose?.Invoke(true);
        }

        private Cabinet CreateProposedCabinet()
        {
            var proposed = new Cabinet
            {
                Id = _cabinet.Id,
                Name = SelectedName?.Trim() ?? string.Empty,
                CanvasLeft = _cabinet.CanvasLeft,
                CanvasTop = _cabinet.CanvasTop,
                RotationAngle = _cabinet.RotationAngle
            };
            ApplyTypeRules(proposed, SelectedCabinetType, SelectedStructureLayerCount, SelectedStructureColumnCount);
            proposed.Width = EditableWidth;
            proposed.Height = EditableHeight;
            proposed.Depth = EditableDepth;
            if (IsMagneticDiskCabinetSelected)
            {
                proposed.HardDiskSlotCapacity = HardDiskSlotCapacity;
                proposed.OpticalDiscSlotCapacity = OpticalDiscSlotCapacity;
            }
            else
            {
                proposed.HardDiskSlotCapacity = CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity;
                proposed.OpticalDiscSlotCapacity = CabinetHardDiskSlotCategoryAssignment.DedicatedOpticalDiscSlotCapacity;
            }

            return proposed;
        }

        private static void CopyProposedCabinet(Cabinet target, Cabinet proposed)
        {
            target.Name = proposed.Name;
            target.Type = proposed.Type;
            target.FaceCount = proposed.FaceCount;
            target.LayerCount = proposed.LayerCount;
            target.ColumnCount = proposed.ColumnCount;
            target.Width = proposed.Width;
            target.Height = proposed.Height;
            target.Depth = proposed.Depth;
            target.HardDiskSlotCapacity = proposed.HardDiskSlotCapacity;
            target.OpticalDiscSlotCapacity = proposed.OpticalDiscSlotCapacity;
        }

        private static void ApplyTypeRules(Cabinet cab, CabinetType type, int layerCount, int columnCount)
        {
            ArgumentNullException.ThrowIfNull(cab);

            cab.Type = type;
            var (width, height, depth) = ResolveDimensions(type);

            switch (type)
            {
                case CabinetType.Standard:
                    cab.FaceCount = 2;
                    cab.LayerCount = 6;
                    cab.ColumnCount = 3;
                    break;
                case CabinetType.Vertical:
                case CabinetType.Horizontal:
                case CabinetType.MagneticDisk:
                    cab.FaceCount = 1;
                    cab.LayerCount = Math.Max(1, layerCount);
                    cab.ColumnCount = Math.Max(1, columnCount);
                    break;
            }

            cab.Width = width;
            cab.Height = height;
            cab.Depth = depth;
        }

        private void SyncDisplayFromType(CabinetType type, bool preserveDimensions = false)
        {
            if (!preserveDimensions && IsStructureCabinet(type))
            {
                SetStructureCounts(DefaultLayerCount(type), DefaultColumnCount(type));
            }
            else
            {
                RefreshDisplayedCounts(type);
            }

            Description = type switch
            {
                CabinetType.Standard => "双面密集架，容量大，底座带导轨标识。",
                CabinetType.Vertical => "双门共同组成 A 面。可设置层数与每层格数，并在下方微调柜体尺寸。",
                CabinetType.Horizontal => "双门共同组成 A 面，适用于图纸或特殊资料平铺。可设置层数与每层格数，并在下方微调柜体尺寸。",
                CabinetType.MagneticDisk => "双门共同组成 A 面。按抽屉层数与每层格数生成开柜格口；可设置硬盘/光盘档口最大容量，并在下方微调柜体尺寸。",
                _ => string.Empty
            };

            if (!preserveDimensions)
            {
                ApplyDefaultDimensions(type);
                if (type == CabinetType.MagneticDisk)
                {
                    HardDiskSlotCapacity = CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity;
                    OpticalDiscSlotCapacity = CabinetHardDiskSlotCategoryAssignment.DedicatedOpticalDiscSlotCapacity;
                }
            }

            OnPropertyChanged(nameof(DimensionSummary));
            OnPropertyChanged(nameof(StructureLayerEditorLabel));
        }

        private void RefreshDisplayedCounts(CabinetType type)
        {
            if (IsStructureCabinet(type))
            {
                RefreshStructureDisplayCounts();
                return;
            }

            if (type == CabinetType.Standard)
            {
                FaceCount = 2;
                LayerCount = 6;
                ColumnCount = 3;
                return;
            }

            FaceCount = 0;
            LayerCount = 0;
            ColumnCount = 0;
        }

        private void RefreshStructureDisplayCounts()
        {
            if (!IsStructureCabinetSelected)
            {
                return;
            }

            FaceCount = 1;
            LayerCount = SelectedStructureLayerCount;
            ColumnCount = SelectedStructureColumnCount;
        }

        private void SetStructureCounts(int layerCount, int columnCount)
        {
            bool layerChanged = _selectedStructureLayerCount != layerCount;
            bool columnChanged = _selectedStructureColumnCount != columnCount;
            _selectedStructureLayerCount = layerCount;
            _selectedStructureColumnCount = columnCount;
            if (layerChanged)
            {
                OnPropertyChanged(nameof(SelectedStructureLayerCount));
            }

            if (columnChanged)
            {
                OnPropertyChanged(nameof(SelectedStructureColumnCount));
            }

            RefreshStructureDisplayCounts();
        }

        private void ApplyDefaultDimensions(CabinetType type)
        {
            var (width, height, depth) = ResolveDimensions(type);
            EditableWidth = width;
            EditableHeight = height;
            EditableDepth = depth;
        }

        private void NotifyStructurePresentationChanged()
        {
            OnPropertyChanged(nameof(IsMagneticDiskCabinetSelected));
            OnPropertyChanged(nameof(IsStructureCabinetSelected));
            OnPropertyChanged(nameof(DefaultSpecificationVisibility));
            OnPropertyChanged(nameof(StructureSpecificationVisibility));
            OnPropertyChanged(nameof(MagneticCapacityVisibility));
            OnPropertyChanged(nameof(SpecificationSectionTitle));
            OnPropertyChanged(nameof(StructureLayerEditorLabel));
            OnPropertyChanged(nameof(DimensionSummary));
        }

        private static bool IsStructureCabinet(CabinetType type)
        {
            return type is CabinetType.Vertical or CabinetType.Horizontal or CabinetType.MagneticDisk;
        }

        private static int DefaultLayerCount(CabinetType type)
        {
            return type switch
            {
                CabinetType.Vertical => DefaultVerticalLayerCount,
                CabinetType.Horizontal => DefaultHorizontalLayerCount,
                CabinetType.MagneticDisk => DefaultMagneticLayerCount,
                CabinetType.Standard => 6,
                _ => 1
            };
        }

        private static int DefaultColumnCount(CabinetType type)
        {
            return type switch
            {
                CabinetType.Vertical => DefaultVerticalColumnCount,
                CabinetType.Horizontal => DefaultHorizontalColumnCount,
                CabinetType.MagneticDisk => DefaultMagneticColumnCount,
                CabinetType.Standard => 3,
                _ => 1
            };
        }

        private static int ResolveInitialLayerCount(Cabinet cabinet)
        {
            ArgumentNullException.ThrowIfNull(cabinet);
            if (IsStructureCabinet(cabinet.Type) && cabinet.LayerCount > 0)
            {
                return cabinet.LayerCount;
            }

            return DefaultLayerCount(cabinet.Type);
        }

        private static int ResolveInitialColumnCount(Cabinet cabinet)
        {
            ArgumentNullException.ThrowIfNull(cabinet);
            if (IsStructureCabinet(cabinet.Type) && cabinet.ColumnCount > 0)
            {
                return cabinet.ColumnCount;
            }

            return DefaultColumnCount(cabinet.Type);
        }

        private static (double Width, double Height, double Depth) ResolveInitialDimensions(Cabinet cabinet, CabinetType type)
        {
            ArgumentNullException.ThrowIfNull(cabinet);

            var defaults = ResolveDimensions(type);
            return (
                cabinet.Width > 0d ? cabinet.Width : defaults.Width,
                cabinet.Height > 0d ? cabinet.Height : defaults.Height,
                cabinet.Depth > 0d ? cabinet.Depth : defaults.Depth);
        }

        private static (double Width, double Height, double Depth) ResolveDimensions(CabinetType type)
        {
            return type switch
            {
                CabinetType.Standard => (StandardWidth, StandardHeight, StandardDepth),
                CabinetType.Vertical => (VerticalWidth, VerticalHeight, VerticalDepth),
                CabinetType.Horizontal => (HorizontalWidth, HorizontalHeight, HorizontalDepth),
                CabinetType.MagneticDisk => (MagneticDiskWidth, MagneticDiskHeight, MagneticDiskDepth),
                _ => (0d, 0d, 0d)
            };
        }

        private static int ResolveInitialHardDiskSlotCapacity(Cabinet cabinet)
        {
            ArgumentNullException.ThrowIfNull(cabinet);
            return cabinet.HardDiskSlotCapacity > 0
                ? cabinet.HardDiskSlotCapacity
                : CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity;
        }

        private static int ResolveInitialOpticalDiscSlotCapacity(Cabinet cabinet)
        {
            ArgumentNullException.ThrowIfNull(cabinet);
            return cabinet.OpticalDiscSlotCapacity > 0
                ? cabinet.OpticalDiscSlotCapacity
                : CabinetHardDiskSlotCategoryAssignment.DedicatedOpticalDiscSlotCapacity;
        }
    }
}
