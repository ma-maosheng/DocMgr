using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.NetworkTransfer;
using DocMgr.ViewModels.Base;
using System.Globalization;

namespace DocMgr.ViewModels.NetworkTransfer;

/// <summary>
/// 入网明细列表行（立档只读展示；档外可表格内编辑）。
/// </summary>
public sealed class NetworkInboundItemRowViewModel : ViewModelBase
{
    private readonly YearlyArchiveFilingFact? _filingFact;
    private readonly bool _isExternalSource;

    private NetworkInboundItemRowViewModel(
        NetworkInboundItem item,
        YearlyArchiveFilingFact? filingFact,
        bool isExternalSource)
    {
        Item = item;
        _filingFact = filingFact;
        _isExternalSource = isExternalSource;
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
        get => Item.ConfidentialLevel;
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

    public static NetworkInboundItemRowViewModel Create(
        NetworkInboundItem item,
        IReadOnlyDictionary<int, YearlyArchiveFilingFact> filingFacts,
        bool isExternalSource)
    {
        YearlyArchiveFilingFact? filingFact = null;
        if (item.SourceFilingFactId is int factId && factId > 0)
        {
            filingFacts.TryGetValue(factId, out filingFact);
        }

        return new NetworkInboundItemRowViewModel(item, filingFact, isExternalSource);
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
