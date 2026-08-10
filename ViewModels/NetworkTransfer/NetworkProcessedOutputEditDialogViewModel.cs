using DocMgr.Models.NetworkTransfer;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.NetworkTransfer
{
    /// <summary>
    /// 加工产出登记弹窗。
    /// </summary>
    public sealed class NetworkProcessedOutputEditDialogViewModel : ViewModelBase
    {
        private readonly INetworkTransferService _service;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private bool _hasCommittedChanges;
        private string _assetKind = NetworkTransferDomainValues.AssetKindJobData;
        private string _assetName = string.Empty;
        private string _projectName = string.Empty;
        private string _year = string.Empty;
        private string _serverPath = string.Empty;
        private string _confidentialLevel = string.Empty;
        private string _dataSizeText = string.Empty;
        private string _versionText = string.Empty;
        private string _remark = string.Empty;
        private string _parentAssetIdText = string.Empty;

        public NetworkProcessedOutputEditDialogViewModel(
            INetworkTransferService service,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _service = service;
            _dialogService = dialogService;
            _userContextService = userContextService;
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public event Action<bool?>? RequestClose;
        public bool HasCommittedChanges => _hasCommittedChanges;
        public System.Collections.ObjectModel.ObservableCollection<string> AssetKindOptions { get; } =
            new(NetworkTransferDomainValues.AssetKindOptions);

        public string AssetKind { get => _assetKind; set => SetProperty(ref _assetKind, value); }
        public string AssetName { get => _assetName; set => SetProperty(ref _assetName, value); }
        public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value); }
        public string Year { get => _year; set => SetProperty(ref _year, value); }
        public string ServerPath { get => _serverPath; set => SetProperty(ref _serverPath, value); }
        public string ConfidentialLevel { get => _confidentialLevel; set => SetProperty(ref _confidentialLevel, value); }
        public string DataSizeText { get => _dataSizeText; set => SetProperty(ref _dataSizeText, value); }
        public string VersionText { get => _versionText; set => SetProperty(ref _versionText, value); }
        public string Remark { get => _remark; set => SetProperty(ref _remark, value); }
        public string ParentAssetIdText { get => _parentAssetIdText; set => SetProperty(ref _parentAssetIdText, value); }

        private bool CanSave =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser)
            && !string.IsNullOrWhiteSpace(AssetName)
            && !string.IsNullOrWhiteSpace(ServerPath);

        public RelayCommand SaveCommand { get; }
        public RelayCommand CloseCommand { get; }

        private async Task SaveAsync()
        {
            try
            {
                int? parentId = null;
                if (!string.IsNullOrWhiteSpace(ParentAssetIdText)
                    && int.TryParse(ParentAssetIdText.Trim(), out int parsed)
                    && parsed > 0)
                {
                    parentId = parsed;
                }

                await _service.RegisterProcessedOutputAsync(new NetworkOnNetAsset
                {
                    AssetKind = AssetKind,
                    AssetName = AssetName,
                    ProjectName = ProjectName,
                    Year = Year,
                    ServerPath = ServerPath,
                    ConfidentialLevel = ConfidentialLevel,
                    DataSizeText = DataSizeText,
                    VersionText = VersionText,
                    Remark = Remark,
                    ParentAssetId = parentId
                }, _userContextService.CurrentUser ?? throw new InvalidOperationException("当前用户无效。"));
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("加工产出已登记到在网台账。");
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }
    }
}
