using System.Windows;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveOutboundHandoverAssistantWindow : Window
    {
        public ArchiveOutboundHandoverAssistantWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            if (DataContext is ViewModels.YearlyArchive.ArchiveOutboundHandoverAssistantViewModel viewModel)
            {
                viewModel.RequestClose += Close;
            }
        }
    }
}
