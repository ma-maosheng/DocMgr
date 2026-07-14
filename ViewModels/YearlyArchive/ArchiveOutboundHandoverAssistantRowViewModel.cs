using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class ArchiveOutboundHandoverAssistantRowViewModel : ViewModelBase
    {
        public ArchiveOutboundHandoverAssistantRowViewModel(string category, string text)
        {
            Category = category;
            Text = text;
        }

        public string Category { get; }

        public string Text { get; }

        private bool _isChecked;

        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }
    }
}
