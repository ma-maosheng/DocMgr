using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Shared
{
    public class SheetSelectionDialogViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private string _selectedSheet = string.Empty;

        public SheetSelectionDialogViewModel(IEnumerable<string> sheetNames, IDialogService dialogService)
        {
            _dialogService = dialogService;
            SheetNames = (sheetNames ?? Enumerable.Empty<string>()).ToList();
            _selectedSheet = SheetNames.FirstOrDefault() ?? string.Empty;

            ConfirmCommand = new RelayCommand(_ => Confirm(), _ => CanConfirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public List<string> SheetNames { get; }

        public string SelectedSheet
        {
            get => _selectedSheet;
            set
            {
                if (SetProperty(ref _selectedSheet, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private bool CanConfirm()
            => !string.IsNullOrWhiteSpace(SelectedSheet);

        private void Confirm()
        {
            if (!CanConfirm())
            {
                _dialogService.ShowMessage("请选择一个有效的工作表！");
                return;
            }

            RequestClose?.Invoke(true);
        }
    }
}