using System.Windows;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveReturnOutboundDetailWindow : Window
    {
        public ArchiveReturnOutboundDetailWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            if (DataContext is ViewModels.YearlyArchive.ArchiveReturnOutboundDetailViewModel viewModel)
            {
                viewModel.RequestClose += Close;
            }
        }
    }
}
