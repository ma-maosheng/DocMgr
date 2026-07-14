using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 资料柜类型
    /// </summary>
    public enum CabinetType
    {
        Standard = 0,   // 标准滑道式 (A/B面, 6层, 3格)
        Vertical = 1,   // 立式文件柜 (A面, 4层, 1格)
        Horizontal = 2,  // 卧式文件柜 (A面, 1层, 1格)
        MagneticDisk = 3 // 防磁磁盘柜 (单/双门, 抽屉分层, 单面打开)
    }

    /// <summary>
    /// 资料柜实体模型
    /// </summary>
    public class Cabinet : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private CabinetType _type;
        private double _width;
        private double _height;
        private double _depth;
        private double _canvasLeft;
        private double _canvasTop;
        private int _faceCount;
        private int _layerCount;
        private int _columnCount;
        private double _rotationAngle;
        private bool _isSelected;

        public int Id { get; set; }

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        public CabinetType Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AllowLayoutResize));
                }
            }
        }

        // 尺寸属性
        public double Width
        {
            get => _width;
            set
            {
                if (_width != value)
                {
                    _width = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LayoutRenderWidth));
                }
            }
        }

        public double Height
        {
            get => _height;
            set
            {
                if (_height != value)
                {
                    _height = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LayoutRenderHeight));
                }
            }
        }

        public double Depth
        {
            get => _depth;
            set
            {
                if (_depth != value)
                {
                    _depth = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LayoutRenderHeight));
                }
            }
        }

        // 位置属性 (Canvas 布局关键)
        public double CanvasLeft
        {
            get => _canvasLeft;
            set { if (_canvasLeft != value) { _canvasLeft = value; OnPropertyChanged(); } }
        }

        public double CanvasTop
        {
            get => _canvasTop;
            set { if (_canvasTop != value) { _canvasTop = value; OnPropertyChanged(); } }
        }

        public int FaceCount
        {
            get => _faceCount;
            set
            {
                if (_faceCount != value)
                {
                    _faceCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasMultipleFaces));
                    OnPropertyChanged(nameof(HasSingleFace));
                }
            }
        }

        public int LayerCount
        {
            get => _layerCount;
            set { if (_layerCount != value) { _layerCount = value; OnPropertyChanged(); } }
        }

        public int ColumnCount
        {
            get => _columnCount;
            set { if (_columnCount != value) { _columnCount = value; OnPropertyChanged(); } }
        }

        public double RotationAngle
        {
            get => _rotationAngle;
            set { if (_rotationAngle != value) { _rotationAngle = value; OnPropertyChanged(); } }
        }

        // 仅用于 UI 交互的选中状态（不存数据库）
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public bool HasMultipleFaces => FaceCount > 1;

        public bool HasSingleFace => !HasMultipleFaces;

        public bool AllowLayoutResize => Type != CabinetType.Standard && Type != CabinetType.MagneticDisk;

        public double LayoutRenderWidth => Width;

        public double LayoutRenderHeight => Type == CabinetType.MagneticDisk ? Depth : Height;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
