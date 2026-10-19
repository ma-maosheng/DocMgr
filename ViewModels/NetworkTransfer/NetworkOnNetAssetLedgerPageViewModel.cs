using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;
using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.NetworkTransfer;

/// <summary>
/// 在网数据台账页：查询在网资产当前态。
/// </summary>
public sealed class NetworkOnNetAssetLedgerPageViewModel : ViewModelBase
{
    private readonly INetworkTransferService _service;
    private readonly PageDbOperationQueue _pageDb = new();
    private readonly IDialogService _dialogService;
    private readonly IServerPathSettingService _serverPathSettingService;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private int _refreshGeneration;
    private bool _isInitialized;
    private string _assetKeyword = string.Empty;
    private string _assetOriginKind = "全部";
    private string _assetLifecycle = "全部";
    private string _assetServerPath = "全部";
    private string _assetDepartment = "全部";
    private NetworkOnNetAsset? _selectedAsset;

    public NetworkOnNetAssetLedgerPageViewModel(
        INetworkTransferService service,
        IDialogService dialogService,
        IServerPathSettingService serverPathSettingService)
    {
        _service = service;
        _dialogService = dialogService;
        _serverPathSettingService = serverPathSettingService;

        RefreshCommand = new RelayCommand(async _ => await _pageDb.RunAsync(RefreshAssetsAsync));
        SearchCommand = new RelayCommand(async _ => await _pageDb.RunAsync(RefreshAssetsAsync));
        ViewAssetDetailCommand = new RelayCommand(async item => await ViewAssetDetailAsync(item), item =>
            NetworkOnNetAssetDetailTextSupport.Resolve(item, SelectedAsset) != null);
        ShowIntroCommand = new RelayCommand(_ => _dialogService.ShowLedgerIntro(LedgerIntroKind.OnNetAsset));
    }

    public ObservableCollection<NetworkOnNetAsset> Assets { get; } = new();

    public ObservableCollection<string> OriginKindOptions { get; } = new()
    {
        "全部",
        NetworkTransferDomainValues.OriginKindInbound,
        NetworkTransferDomainValues.OriginKindProcessedOutput
    };

    public ObservableCollection<string> ServerPathOptions { get; } = new() { "全部" };

    public ObservableCollection<string> DepartmentOptions { get; } = new() { "全部" };

    public ObservableCollection<string> LifecycleOptions { get; } = new()
    {
        "全部",
        NetworkTransferDomainValues.LifecycleOnNet,
        NetworkTransferDomainValues.LifecycleOutboundLocked,
        NetworkTransferDomainValues.LifecycleOutbounded,
        NetworkTransferDomainValues.LifecycleDisposalLocked,
        NetworkTransferDomainValues.LifecycleDisposed
    };

    public string AssetKeyword
    {
        get => _assetKeyword;
        set => SetProperty(ref _assetKeyword, value);
    }

    public string AssetOriginKind
    {
        get => _assetOriginKind;
        set
        {
            if (SetProperty(ref _assetOriginKind, value) && _isInitialized)
            {
                _pageDb.RunInBackground(RefreshAssetsAsync);
            }
        }
    }

    public string AssetLifecycle
    {
        get => _assetLifecycle;
        set
        {
            if (SetProperty(ref _assetLifecycle, value) && _isInitialized)
            {
                _pageDb.RunInBackground(RefreshAssetsAsync);
            }
        }
    }

    public string AssetServerPath
    {
        get => _assetServerPath;
        set
        {
            if (SetProperty(ref _assetServerPath, value) && _isInitialized)
            {
                _pageDb.RunInBackground(RefreshAssetsAsync);
            }
        }
    }

    public string AssetDepartment
    {
        get => _assetDepartment;
        set
        {
            if (SetProperty(ref _assetDepartment, value) && _isInitialized)
            {
                _pageDb.RunInBackground(RefreshAssetsAsync);
            }
        }
    }

    public NetworkOnNetAsset? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (SetProperty(ref _selectedAsset, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ViewAssetDetailCommand { get; }
    public RelayCommand ShowIntroCommand { get; }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            await RefreshAssetsAsync();
            return;
        }

        LoadAssetFilterOptions();
        await RefreshAssetsAsync();
        _isInitialized = true;
    }

    private void LoadAssetFilterOptions()
    {
        ServerPathOptions.Clear();
        ServerPathOptions.Add("全部");
        foreach (string pathName in _serverPathSettingService.GetAll()
                     .Select(item => item.PathName?.Trim() ?? string.Empty)
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(item => item, StringComparer.Ordinal))
        {
            ServerPathOptions.Add(pathName);
        }

        DepartmentOptions.Clear();
        DepartmentOptions.Add("全部");
        foreach (string department in _serverPathSettingService.GetAll()
                     .Select(item => item.DepartmentName?.Trim() ?? string.Empty)
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(item => item, StringComparer.Ordinal))
        {
            DepartmentOptions.Add(department);
        }
    }

    private async Task RefreshAssetsAsync()
    {
        int generation = Interlocked.Increment(ref _refreshGeneration);
        await _refreshGate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (generation != Volatile.Read(ref _refreshGeneration))
            {
                return;
            }

            try
            {
                string? origin = string.Equals(AssetOriginKind, "全部", StringComparison.Ordinal) ? null : AssetOriginKind;
                string? life = string.Equals(AssetLifecycle, "全部", StringComparison.Ordinal) ? null : AssetLifecycle;
                string? serverPath = string.Equals(AssetServerPath, "全部", StringComparison.Ordinal) ? null : AssetServerPath;
                string? department = string.Equals(AssetDepartment, "全部", StringComparison.Ordinal) ? null : AssetDepartment;
                string? keyword = string.IsNullOrWhiteSpace(AssetKeyword) ? null : AssetKeyword.Trim();
                IReadOnlyList<NetworkOnNetAsset> list =
                    await _service.SearchOnNetAssetsAsync(keyword, origin, life, serverPath, department);
                Assets.Clear();
                foreach (NetworkOnNetAsset item in list)
                {
                    Assets.Add(item);
                }

                if (SelectedAsset != null)
                {
                    SelectedAsset = Assets.FirstOrDefault(item => item.Id == SelectedAsset.Id);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task ViewAssetDetailAsync(object? parameter)
    {
        NetworkOnNetAsset? asset = NetworkOnNetAssetDetailTextSupport.Resolve(parameter, SelectedAsset);
        if (asset == null)
        {
            _dialogService.ShowMessage("请先选择一条在网对象。");
            return;
        }

        try
        {
            await NetworkOnNetAssetDetailTextSupport.ShowAsync(_service, _dialogService, asset);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }
}
