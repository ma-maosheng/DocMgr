using DocMgr.Models.NetworkTransfer;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.NetworkTransfer;

/// <summary>
/// 在网处置已选明细：字段与候选在网对象表一致，另存原因/方式。
/// </summary>
public sealed class NetworkOnNetDisposalItemRow : ViewModelBase
{
    private NetworkOnNetAsset _asset;
    private string _disposalReason;
    private string _dispositionMethod;

    public NetworkOnNetDisposalItemRow(NetworkOnNetAsset asset, string disposalReason, string dispositionMethod)
    {
        ArgumentNullException.ThrowIfNull(asset);
        _asset = asset;
        _disposalReason = disposalReason?.Trim() ?? string.Empty;
        _dispositionMethod = dispositionMethod?.Trim() ?? string.Empty;
    }

    public NetworkOnNetAsset Asset => _asset;

    public int OnNetAssetId => _asset.Id;

    public string AssetNo => _asset.AssetNo;
    public string ApplicationNo => _asset.ApplicationNo;
    public string OriginKind => _asset.OriginKind;
    public string ApplicantDept => _asset.ApplicantDept;
    public string Year => _asset.Year;
    public string ProjectName => _asset.ProjectName;
    public string MaterialName => _asset.MaterialName;
    public string AssetName => _asset.AssetName;
    public string ProvideUnit => _asset.ProvideUnit;
    public string DepartmentName => _asset.DepartmentName;
    public string PhysicalPath => _asset.PhysicalPath;
    public string ServerPath => _asset.ServerPath;
    public string MaterialPath => _asset.MaterialPath;
    public string FullStorageAddress => _asset.FullStorageAddress;
    public string AssetKind => _asset.AssetKind;
    public string ConfidentialLevel => _asset.ConfidentialLevel;
    public string DataOrganizationForm => _asset.DataOrganizationForm;
    public string EntryCountDisplay => _asset.EntryCountDisplay;
    public string DataSizeText => _asset.DataSizeText;
    public string LifecycleStatus => _asset.LifecycleStatus;

    public string DisposalReason
    {
        get => _disposalReason;
        set => SetProperty(ref _disposalReason, value?.Trim() ?? string.Empty);
    }

    public string DispositionMethod
    {
        get => _dispositionMethod;
        set => SetProperty(ref _dispositionMethod, value?.Trim() ?? string.Empty);
    }

    /// <summary>从候选勾选加入明细。</summary>
    public static NetworkOnNetDisposalItemRow FromCandidate(
        NetworkOnNetAssetCandidate candidate,
        string disposalReason,
        string dispositionMethod)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new NetworkOnNetDisposalItemRow(candidate.Asset, disposalReason, dispositionMethod);
    }

    /// <summary>从已保存明细还原；若有实时台账则补全展示字段。</summary>
    public static NetworkOnNetDisposalItemRow FromPersisted(
        NetworkOnNetDisposalItem item,
        NetworkOnNetAsset? asset = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        NetworkOnNetAsset source = asset ?? CreateSnapshotAsset(item);
        return new NetworkOnNetDisposalItemRow(source, item.DisposalReason, item.DispositionMethod);
    }

    /// <summary>用实时台账替换快照，便于移回候选时带全字段。</summary>
    public void ReplaceAsset(NetworkOnNetAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.Id != OnNetAssetId)
        {
            return;
        }

        _asset = asset;
        OnPropertyChanged(nameof(Asset));
        OnPropertyChanged(nameof(AssetNo));
        OnPropertyChanged(nameof(ApplicationNo));
        OnPropertyChanged(nameof(OriginKind));
        OnPropertyChanged(nameof(ApplicantDept));
        OnPropertyChanged(nameof(Year));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(MaterialName));
        OnPropertyChanged(nameof(AssetName));
        OnPropertyChanged(nameof(ProvideUnit));
        OnPropertyChanged(nameof(DepartmentName));
        OnPropertyChanged(nameof(PhysicalPath));
        OnPropertyChanged(nameof(ServerPath));
        OnPropertyChanged(nameof(MaterialPath));
        OnPropertyChanged(nameof(FullStorageAddress));
        OnPropertyChanged(nameof(AssetKind));
        OnPropertyChanged(nameof(ConfidentialLevel));
        OnPropertyChanged(nameof(DataOrganizationForm));
        OnPropertyChanged(nameof(EntryCountDisplay));
        OnPropertyChanged(nameof(DataSizeText));
        OnPropertyChanged(nameof(LifecycleStatus));
    }

    /// <summary>批量写入原因和方式，并强制刷新表格绑定。</summary>
    public void ApplyDisposition(string disposalReason, string dispositionMethod)
    {
        _disposalReason = disposalReason?.Trim() ?? string.Empty;
        _dispositionMethod = dispositionMethod?.Trim() ?? string.Empty;
        OnPropertyChanged(nameof(DisposalReason));
        OnPropertyChanged(nameof(DispositionMethod));
    }

    /// <summary>移回候选列表时复用同一在网对象。</summary>
    public NetworkOnNetAssetCandidate ToCandidate() => new(Asset);

    private static NetworkOnNetAsset CreateSnapshotAsset(NetworkOnNetDisposalItem item) =>
        new()
        {
            Id = item.OnNetAssetId,
            AssetNo = item.AssetNo,
            AssetKind = item.AssetKind,
            AssetName = item.AssetName,
            ServerPath = item.ServerPath,
            LifecycleStatus = item.BeforeLifecycleStatus
        };
}
