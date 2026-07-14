using System;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Shared
{
    public class ImportOptionDialogViewModel : ViewModelBase
    {
        private readonly ImportOptionModel _model;

        public ImportOptionDialogViewModel(string tableName)
        {
            _model = new ImportOptionModel(tableName);

            ConfirmCommand = new RelayCommand(_ => RequestClose?.Invoke(true));
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Message => $"数据表 [{_model.TableName}] 已存在，请选择导入方式：";

        public ImportMode SelectedMode
        {
            get => _model.SelectedMode;
            set
            {
                if (_model.SelectedMode == value) return;

                _model.SelectedMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAppendMode));
                OnPropertyChanged(nameof(IsRecreateMode));
            }
        }

        public bool IsAppendMode
        {
            get => SelectedMode == ImportMode.Append;
            set
            {
                if (value)
                {
                    SelectedMode = ImportMode.Append;
                }
            }
        }

        public bool IsRecreateMode
        {
            get => SelectedMode == ImportMode.Recreate;
            set
            {
                if (value)
                {
                    SelectedMode = ImportMode.Recreate;
                }
            }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;
    }
}