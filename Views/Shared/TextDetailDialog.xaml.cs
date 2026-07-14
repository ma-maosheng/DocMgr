using System.Windows;

namespace DocMgr.Views.Shared
{
    public partial class TextDetailDialog : Window
    {
        public TextDetailDialog(string title, string detailText)
        {
            InitializeComponent();
            DialogTitle = title;
            DataContext = this;

            DetailTextBlock.Text = detailText ?? string.Empty;

            Loaded += TextDetailDialog_Loaded;
        }

        public string DialogTitle { get; }

        private void TextDetailDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= TextDetailDialog_Loaded;
            DetailScrollViewer.ScrollToTop();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
