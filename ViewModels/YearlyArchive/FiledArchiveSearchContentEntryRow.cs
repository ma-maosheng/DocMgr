using System;
using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class FiledArchiveSearchContentEntryRow : ViewModelBase
    {
        public FiledArchiveSearchContentEntryRow(
            MatchedContentEntryInfo entry,
            int filingFactId,
            Action? selectionChanged = null)
        {
            Entry = entry;
            FilingFactId = filingFactId;
            SelectionChanged = selectionChanged;
        }

        public MatchedContentEntryInfo Entry { get; }

        public int FilingFactId { get; }

        public Action? SelectionChanged { get; }

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    SelectionChanged?.Invoke();
                }
            }
        }

        public int EntryId => Entry.EntryId;

        public string EntryKind => Entry.EntryKind;

        public string EntryName => Entry.EntryName;

        public string FilingPath => Entry.FilingPath;

        public string CreatedDateText => Entry.CreatedDateText;

        public string ModifiedDateText => Entry.ModifiedDateText;

        public string SizeText => Entry.SizeText;

        public string DisplayLabel => ContentEntrySearchSupport.FormatEntryLabel(Entry);
    }
}
