using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using DocMgr.ViewModels.Cabinets;

namespace DocMgr.Views.Cabinets
{
    public partial class CabinetEditDialog : Window
    {
        private CabinetEditDialogViewModel? _viewModel;

        public CabinetEditDialog()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void CabinetEditDialog_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshWindowHeight();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = e.NewValue as CabinetEditDialogViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }

            ScheduleRefreshWindowHeight();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(CabinetEditDialogViewModel.SelectedCabinetType)
                or nameof(CabinetEditDialogViewModel.DefaultSpecificationVisibility)
                or nameof(CabinetEditDialogViewModel.StructureSpecificationVisibility)
                or nameof(CabinetEditDialogViewModel.MagneticCapacityVisibility)
                or nameof(CabinetEditDialogViewModel.SelectedStructureLayerCount)
                or nameof(CabinetEditDialogViewModel.SelectedStructureColumnCount))
            {
                ScheduleRefreshWindowHeight();
            }
        }

        private void ScheduleRefreshWindowHeight()
        {
            // 等 Visibility 折叠/展开完成后再量高，避免量到旧布局。
            Dispatcher.BeginInvoke(RefreshWindowHeight, DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 切换柜种或规格区显隐后，重新按内容计算窗体高度。
        /// </summary>
        private void RefreshWindowHeight()
        {
            // 先清掉本地 Height：若先切 Manual，会把当前 ActualHeight 写进 Height，
            // 再开 SizeToContent 时窗体无法随内容收缩（标准柜 ↔ 防磁柜切换时尤甚）。
            ClearValue(HeightProperty);
            SizeToContent = SizeToContent.Height;

            ContentRoot.InvalidateMeasure();
            ContentRoot.UpdateLayout();
            InvalidateMeasure();
            UpdateLayout();
        }
    }
}
