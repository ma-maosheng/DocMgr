using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 异常归还：选择已有在用档案盒作为归还目标。
    /// </summary>
    public sealed class ArchiveReturnRehomeTargetPickViewModel : ViewModelBase
    {
        private ArchiveReturnRehomeTargetOption? _selectedOption;

        public ArchiveReturnRehomeTargetPickViewModel(IReadOnlyList<ArchiveReturnRehomeTargetOption> options)
        {
            Options = new ObservableCollection<ArchiveReturnRehomeTargetOption>(options ?? Array.Empty<ArchiveReturnRehomeTargetOption>());
            SelectedOption = Options.FirstOrDefault();
            ConfirmCommand = new RelayCommand(_ => { }, _ => SelectedOption != null);
        }

        public ObservableCollection<ArchiveReturnRehomeTargetOption> Options { get; }

        public ArchiveReturnRehomeTargetOption? SelectedOption
        {
            get => _selectedOption;
            set => SetProperty(ref _selectedOption, value);
        }

        public ICommand ConfirmCommand { get; }
    }
}
