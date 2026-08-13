using DocMgr.Models.NetworkTransfer;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.NetworkTransfer;

/// <summary>
/// 在网台账候选勾选项（出网/处置等业务共用）。
/// </summary>
public sealed class NetworkOnNetAssetCandidate : ViewModelBase
{
    private bool _isSelected;

    public NetworkOnNetAssetCandidate(NetworkOnNetAsset asset) => Asset = asset;

    public NetworkOnNetAsset Asset { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Display => $"{Asset.AssetNo} | {Asset.AssetName} | {Asset.ServerPath}";
}
