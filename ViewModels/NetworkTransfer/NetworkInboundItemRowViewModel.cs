using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.NetworkTransfer;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.YearlyArchive;
using System.Globalization;

namespace DocMgr.ViewModels.NetworkTransfer;

/// <summary>
/// 入网明细列表行（立档只读展示；档外可表格内编辑）。
/// </summary>
public sealed class NetworkInboundItemRowViewModel : ViewModelBase
{
    private readonly YearlyArchiveFilingFact? _filingFact;
    private readonly SearchPoolItemRow? _poolItem;
    private readonly bool _isExternalSource;

    private NetworkInboundItemRowViewModel(
        NetworkInboundItem item,
        YearlyArchiveFilingFact? filingFact,
        bool isExternalSource,
        SearchPoolItemRow? poolItem)
    {
        Item = item;
        _filingFact = filingFact;
        _isExternalSource = isExternalSource;
        _poolItem = poolItem;
    }

    public NetworkInboundItem Item { get; }

    public string AssetKind
    {
        get => Item.AssetKind;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(Item.AssetKind, normalized, StringComparison.Ordinal))
            {
                return;
            }

            Item.AssetKind = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AssetKindDisplay));
        }
    }

    /// <summary>档外资料时绑定资料名称（底层存 AssetName）。</summary>
    public string MaterialName
    {
        get => _isExternalSource ? Item.AssetName : Item.MaterialName;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (_isExternalSource)
            {
                if (string.Equals(Item.AssetName, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                Item.AssetName = normalized;
            }
            else
            {
                if (string.Equals(Item.MaterialName, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                Item.MaterialName = normalized;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(MaterialNameDisplay));
        }
    }

    public string ItemName
    {
        get => Item.ItemName;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(Item.ItemName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            Item.ItemName = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ItemNameDisplay));
        }
    }

    public string ConfidentialLevel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Item.ConfidentialLevel))
            {
                return Item.ConfidentialLevel;
            }

            return FirstNonEmpty(_poolItem?.ConfidentialLevel, _filingFact?.ConfidentialLevel);
        }
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(Item.ConfidentialLevel, normalized, StringComparison.Ordinal))
            {
                return;
            }

            Item.ConfidentialLevel = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfidentialLevelDisplay));
        }
    }

    public string DataSizeValue
    {
        get => NetworkInboundItemDisplaySupport.GetDataSizeValuePart(Item.DataSizeText);
        set
        {
            UpdateDataSizeText(value, DataSizeUnit);
        }
    }

    public string DataSizeUnit
    {
        get => NetworkInboundItemDisplaySupport.GetDataSizeUnitPart(Item.DataSizeText);
        set
        {
            UpdateDataSizeText(DataSizeValue, value);
        }
    }

    public string AssetKindDisplay =>
        NetworkInboundItemDisplaySupport.ResolveAssetKindDisplay(Item, _isExternalSource);

    public string FormNoDisplay =>
        NetworkInboundItemDisplaySupport.ResolveFormNoDisplay(Item, _isExternalSource);

    public string MaterialNameDisplay =>
        NetworkInboundItemDisplaySupport.ResolveMaterialNameDisplay(Item, _isExternalSource);

    public string ItemNameDisplay =>
        NetworkInboundItemDisplaySupport.ResolveItemNameDisplay(Item, _isExternalSource);

    public string ConfidentialLevelDisplay =>
        NetworkInboundItemDisplaySupport.ResolveConfidentialLevelDisplay(Item, _filingFact, _isExternalSource);

    public string DataSizeTextDisplay =>
        NetworkInboundItemDisplaySupport.ResolveDataSizeDisplay(Item, _filingFact, _isExternalSource);

    public string ContainerCodeDisplay =>
        NetworkInboundItemDisplaySupport.ResolveContainerCodeDisplay(Item, _isExternalSource);

    public string HardDiskCodeDisplay =>
        NetworkInboundItemDisplaySupport.ResolveHardDiskCodeDisplay(_filingFact, _isExternalSource);

    public string StorageLocationDisplay =>
        NetworkInboundItemDisplaySupport.ResolveStorageLocationDisplay(Item, _isExternalSource);

    /// <summary>立档检索集明细列：与 YA-FIL-POOL 右侧「池内立档记录」同名字段。</summary>
    public string FormNo => FirstNonEmpty(_poolItem?.FormNo, Item.FormNo);

    public string ProjectYear =>
        FirstNonEmpty(_poolItem?.ProjectYear, string.Empty);

    public string ProjectName => FirstNonEmpty(_poolItem?.ProjectName, _filingFact?.ProjectName);

    public string ArchivePurpose => _poolItem?.ArchivePurpose?.Trim() ?? string.Empty;

    public string StorageCarrierTypeDisplay =>
        FirstNonEmpty(
            _poolItem?.StorageCarrierTypeDisplay,
            _filingFact == null
                ? string.Empty
                : ArchiveOutboundDomainValues.NormalizeElectronicStorageCarrierDisplay(_filingFact.StorageCarrierType));

    public string FilingDirectoryDisplay =>
        FirstNonEmpty(_poolItem?.FilingDirectoryDisplay, _filingFact?.FilingStoragePath);

    public string MaterialCategory => _poolItem?.MaterialCategory?.Trim() ?? string.Empty;

    public string SubCategory => _poolItem?.SubCategory?.Trim() ?? string.Empty;

    public string DataOrganizationForm => _poolItem?.DataOrganizationForm?.Trim() ?? string.Empty;

    public int RequestedCopyCount => _poolItem?.RequestedCopyCount > 0 ? _poolItem.RequestedCopyCount : 1;

    public bool IsCopyCountEditable => _poolItem?.IsCopyCountEditable ?? false;

    public string SelectionScopeDisplay => _poolItem?.SelectionScopeDisplay ?? string.Empty;

    public string MatchedContentEntrySummary => _poolItem?.MatchedContentEntrySummary ?? string.Empty;

    public string DataSizeDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_poolItem?.DataSizeDisplay))
            {
                return _poolItem.DataSizeDisplay;
            }

            if (_filingFact is { DataSizeMb: > 0 })
            {
                return $"{_filingFact.DataSizeMb:0.##} MB";
            }

            if (NetworkInboundItemDisplaySupport.TryParseDataSizeText(Item.DataSizeText, out decimal value, out string unit))
            {
                return NetworkInboundItemDisplaySupport.ComposeDataSizeText(value, unit);
            }

            return string.Empty;
        }
    }

    public string ContainerCode => FirstNonEmpty(_poolItem?.ContainerCode, Item.ContainerCode);

    public string StorageLocation => FirstNonEmpty(_poolItem?.StorageLocation, Item.StorageLocation);

    public string CurrentStorageLocation =>
        FirstNonEmpty(
            _poolItem?.CurrentStorageLocation,
            FirstNonEmpty(_filingFact?.CurrentStorageLocation, _filingFact?.StorageLocation));

    public string BorrowHintDisplay =>
        FirstNonEmpty(_poolItem?.BorrowHintDisplay, _filingFact?.BorrowHintText);

    public string LifecycleStatusDisplay =>
        FirstNonEmpty(
            _poolItem?.LifecycleStatusDisplay,
            _filingFact == null
                ? string.Empty
                : MaterialTransactionDomainValues.MapLifecycleStatusDisplay(_filingFact.LifecycleStatus));

    public static NetworkInboundItemRowViewModel Create(
        NetworkInboundItem item,
        IReadOnlyDictionary<int, YearlyArchiveFilingFact> filingFacts,
        bool isExternalSource,
        SearchPoolItemRow? poolItem = null)
    {
        YearlyArchiveFilingFact? filingFact = null;
        if (item.SourceFilingFactId is int factId && factId > 0)
        {
            filingFacts.TryGetValue(factId, out filingFact);
        }

        return new NetworkInboundItemRowViewModel(item, filingFact, isExternalSource, poolItem);
    }

    private static string FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first.Trim();
        }

        return string.IsNullOrWhiteSpace(second) ? string.Empty : second.Trim();
    }

    private void UpdateDataSizeText(string? valueText, string? unit)
    {
        string trimmedValue = valueText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedValue))
        {
            if (string.IsNullOrWhiteSpace(Item.DataSizeText))
            {
                return;
            }

            Item.DataSizeText = string.Empty;
            NotifyDataSizeChanged();
            return;
        }

        if (!decimal.TryParse(trimmedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed) || parsed <= 0)
        {
            Item.DataSizeText = trimmedValue;
            NotifyDataSizeChanged();
            return;
        }

        string composed = NetworkInboundItemDisplaySupport.ComposeDataSizeText(
            parsed,
            NetworkInboundItemDisplaySupport.NormalizeDataSizeUnit(unit));
        if (string.Equals(Item.DataSizeText, composed, StringComparison.Ordinal))
        {
            return;
        }

        Item.DataSizeText = composed;
        NotifyDataSizeChanged();
    }

    private void NotifyDataSizeChanged()
    {
        OnPropertyChanged(nameof(DataSizeValue));
        OnPropertyChanged(nameof(DataSizeUnit));
        OnPropertyChanged(nameof(DataSizeTextDisplay));
    }
}
