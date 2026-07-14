using System.ComponentModel;
using System.Windows;
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

            RefreshWindowHeight();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(CabinetEditDialogViewModel.SelectedCabinetType)
                or nameof(CabinetEditDialogViewModel.DefaultSpecificationVisibility)
                or nameof(CabinetEditDialogViewModel.MagneticSpecificationVisibility)
                or nameof(CabinetEditDialogViewModel.SelectedMagneticDoorCount)
                or nameof(CabinetEditDialogViewModel.SelectedMagneticDrawerCount)
                or nameof(CabinetEditDialogViewModel.SelectedMagneticColumnCount))
            {
                Dispatcher.BeginInvoke(RefreshWindowHeight);
            }
        }

        /// <summary>
        /// 切换柜种或规格区显隐后，重新按内容计算窗体高度。
        /// </summary>
        private void RefreshWindowHeight()
        {
            ContentRoot.UpdateLayout();

            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.Height;
        }
    }
}
