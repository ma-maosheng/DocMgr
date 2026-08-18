using DocMgr.Models.NetworkTransfer;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.NetworkTransfer;

/// <summary>
/// 在网台账候选勾选项（出网/处置等业务共用），字段与 NT-DSP 资产表一致。
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

    public string AssetNo => Asset.AssetNo;
    public string ApplicationNo => Asset.ApplicationNo;
    public string OriginKind => Asset.OriginKind;
    public string ApplicantDept => Asset.ApplicantDept;
    public string Year => Asset.Year;
    public string ProjectName => Asset.ProjectName;
    public string MaterialName => Asset.MaterialName;
    public string AssetName => Asset.AssetName;
    public string ProvideUnit => Asset.ProvideUnit;
    public string DepartmentName => Asset.DepartmentName;
    public string PhysicalPath => Asset.PhysicalPath;
    public string ServerPath => Asset.ServerPath;
    public string MaterialPath => Asset.MaterialPath;
    public string FullStorageAddress => Asset.FullStorageAddress;
    public string AssetKind => Asset.AssetKind;
    public string ConfidentialLevel => Asset.ConfidentialLevel;
    public string DataOrganizationForm => Asset.DataOrganizationForm;
    public string EntryCountDisplay => Asset.EntryCountDisplay;
    public string DataSizeText => Asset.DataSizeText;
    public string LifecycleStatus => Asset.LifecycleStatus;
}
