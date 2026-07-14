using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.HardDiskMedia;
using DocMgr.ViewModels.Base;
using System.Windows;
using DocMgr.Services.SystemSettings;

namespace DocMgr.ViewModels.SystemSettings
{
    public class TestPreparationPageViewModel : ViewModelBase
    {
        private readonly TestPreparationService _testPreparationService;
        private readonly IDialogService _dialogService;
        private bool _isBusy;
        private string _statusText = "请按需执行测试数据填充。";

        public TestPreparationPageViewModel(TestPreparationService testPreparationService, IDialogService dialogService)
        {
            _testPreparationService = testPreparationService;
            _dialogService = dialogService;

            ImportTopoMapsCommand = new RelayCommand(async _ => await ExecuteImportAsync(
                "填入历史存档资料表（地形图）",
                _testPreparationService.ImportTopoMapsAsync), _ => !IsBusy);
            ImportAerialPhotosCommand = new RelayCommand(async _ => await ExecuteImportAsync(
                "填入历史存档资料表（航摄影像）",
                _testPreparationService.ImportAerialPhotosAsync), _ => !IsBusy);
            ImportBlankHardDisksCommand = new RelayCommand(async _ => await ExecuteImportAsync(
                "填入硬盘（无数据）",
                _testPreparationService.ImportBlankHardDisksAsync), _ => !IsBusy);
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

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public ICommand ImportTopoMapsCommand { get; }
        public ICommand ImportAerialPhotosCommand { get; }
        public ICommand ImportBlankHardDisksCommand { get; }

        private async Task ExecuteImportAsync(string actionName, Func<Task<string>> action)
        {
            IsBusy = true;
            StatusText = $"正在执行：{actionName}...";
            _dialogService.SetBusyState(true);

            try
            {
                string summary = await action();
                StatusText = summary;
                _dialogService.ShowMessage(summary, "完成");
            }
            catch (FileNotFoundException ex)
            {
                ShowError(actionName, ex.Message);
            }
            catch (HardDiskMediaImportException ex)
            {
                ShowError(actionName, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                ShowError(actionName, ex.Message);
            }
            catch (ArgumentException ex)
            {
                ShowError(actionName, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowError(actionName, ex.Message);
            }
            catch (IOException ex)
            {
                ShowError(actionName, ex.Message);
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
            }
        }

        private void ShowError(string actionName, string message)
        {
            StatusText = $"{actionName}失败：{message}";
            _dialogService.ShowError(StatusText, "执行失败");
        }
    }
}
