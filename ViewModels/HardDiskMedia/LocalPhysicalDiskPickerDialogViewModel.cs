using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 本机物理磁盘选择弹窗 ViewModel。
    /// </summary>
    public sealed class LocalPhysicalDiskPickerDialogViewModel : ViewModelBase
    {
        private readonly ILocalPhysicalDiskHardwareService _localPhysicalDiskHardwareService;
        private readonly IDialogService _dialogService;
        private LocalPhysicalDiskInfo? _selectedDisk;
        private string _statusText = "正在读取本机硬盘…";
        private bool _isBusy;

        public LocalPhysicalDiskPickerDialogViewModel(
            ILocalPhysicalDiskHardwareService localPhysicalDiskHardwareService,
            IDialogService dialogService)
        {
            _localPhysicalDiskHardwareService = localPhysicalDiskHardwareService;
            _dialogService = dialogService;

            Disks = new ObservableCollection<LocalPhysicalDiskInfo>();
            RefreshCommand = new RelayCommand(async _ => await LoadDisksAsync(), _ => !IsBusy);
            ConfirmCommand = new RelayCommand(_ => Confirm(), _ => CanConfirm);
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public ObservableCollection<LocalPhysicalDiskInfo> Disks { get; }

        public LocalPhysicalDiskInfo? SelectedDisk
        {
            get => _selectedDisk;
            set
            {
                if (SetProperty(ref _selectedDisk, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                    OnPropertyChanged(nameof(CanConfirm));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanConfirm => SelectedDisk != null && SelectedDisk.CanRegister && !IsBusy;

        public ICommand RefreshCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        public Task InitializeAsync() => LoadDisksAsync();

        private async Task LoadDisksAsync()
        {
            IsBusy = true;
            StatusText = "正在读取本机硬盘…";
            Disks.Clear();
            SelectedDisk = null;

            try
            {
                IReadOnlyList<LocalPhysicalDiskInfo> disks = await _localPhysicalDiskHardwareService.GetPhysicalDisksAsync();
                foreach (LocalPhysicalDiskInfo disk in disks)
                {
                    Disks.Add(disk);
                }

                SelectedDisk = Disks.FirstOrDefault(item => item.CanRegister);
                int registrableCount = Disks.Count(item => item.CanRegister);
                StatusText = registrableCount == 0
                    ? "未找到可登记硬盘。请接入拟登记硬盘后点击「刷新」；系统盘与虚拟磁盘不可用于登记。"
                    : $"共 {Disks.Count} 块物理盘，其中 {registrableCount} 块可登记。请选择刚接入、拟写入台账的那一块。";
            }
            catch (InvalidOperationException ex)
            {
                StatusText = ex.Message;
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                StatusText = "读取本机硬盘失败，请改用手工录入。";
                _dialogService.ShowError($"读取本机硬盘失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Confirm()
        {
            if (SelectedDisk == null)
            {
                _dialogService.ShowMessage("请选择一块本机硬盘。");
                return;
            }

            if (!SelectedDisk.CanRegister)
            {
                _dialogService.ShowMessage("系统盘与虚拟磁盘不可用于登记，请选择拟入库的那块硬盘。");
                return;
            }

            RequestClose?.Invoke(true);
        }
    }
}
