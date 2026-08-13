using DocMgr.Models.NetworkTransfer;
using DocMgr.Services.NetworkTransfer;
using DocMgr.ViewModels.Base;
using System.Globalization;

namespace DocMgr.ViewModels.NetworkTransfer;

/// <summary>
/// 出网明细列表行（表格内手工录入）。
/// </summary>
public sealed class NetworkOutboundItemRowViewModel : ViewModelBase
{
    private NetworkOutboundItemRowViewModel(NetworkOutboundItem item) => Item = item;

    public NetworkOutboundItem Item { get; }

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

    public string AssetName
    {
        get => Item.AssetName;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(Item.AssetName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            Item.AssetName = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AssetNameDisplay));
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
        set => UpdateDataSizeText(value, DataSizeUnit);
    }

    public string DataSizeUnit
    {
        get => NetworkInboundItemDisplaySupport.GetDataSizeUnitPart(Item.DataSizeText);
        set => UpdateDataSizeText(DataSizeValue, value);
    }

    public string AssetKindDisplay =>
        string.IsNullOrWhiteSpace(Item.AssetKind) ? string.Empty : Item.AssetKind.Trim();

    public string AssetNameDisplay =>
        string.IsNullOrWhiteSpace(Item.AssetName) ? string.Empty : Item.AssetName.Trim();

    public string ItemNameDisplay =>
        string.IsNullOrWhiteSpace(Item.ItemName) ? string.Empty : Item.ItemName.Trim();

    public string ConfidentialLevelDisplay =>
        string.IsNullOrWhiteSpace(Item.ConfidentialLevel) ? string.Empty : Item.ConfidentialLevel.Trim();

    public string DataSizeTextDisplay =>
        string.IsNullOrWhiteSpace(Item.DataSizeText) ? string.Empty : Item.DataSizeText.Trim();

    public static NetworkOutboundItemRowViewModel Create(NetworkOutboundItem item) =>
        new(item);

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
