using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 入网明细数据量文本解析与组合（数值 + MB/GB/TB）。
/// </summary>
public static partial class NetworkInboundItemDisplaySupport
{
    public const string EmptyDisplay = "-";
    public const string DefaultDataSizeUnit = "MB";

    private static readonly string[] DataSizeUnits = ["MB", "GB", "TB"];

    [GeneratedRegex(@"^\s*(?<value>\d+(?:\.\d+)?)\s*(?<unit>MB|GB|TB)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DataSizePattern();

    public static IReadOnlyList<string> DataSizeUnitOptions { get; } = DataSizeUnits;

    public static string FormatOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? EmptyDisplay : value.Trim();

    public static string ResolveAssetKindDisplay(NetworkInboundItem item, bool isExternalSource) =>
        isExternalSource ? FormatOptional(item.AssetKind) : EmptyDisplay;

    public static string ResolveMaterialNameDisplay(NetworkInboundItem item, bool isExternalSource) =>
        isExternalSource ? FormatOptional(item.AssetName) : FormatOptional(item.MaterialName);

    public static string ResolveFormNoDisplay(NetworkInboundItem item, bool isExternalSource) =>
        isExternalSource ? EmptyDisplay : FormatOptional(item.FormNo);

    public static string ResolveItemNameDisplay(NetworkInboundItem item, bool isExternalSource) =>
        FormatOptional(item.ItemName);

    public static string ResolveContainerCodeDisplay(NetworkInboundItem item, bool isExternalSource) =>
        isExternalSource ? EmptyDisplay : FormatOptional(item.ContainerCode);

    public static string ResolveStorageLocationDisplay(NetworkInboundItem item, bool isExternalSource) =>
        isExternalSource ? EmptyDisplay : FormatOptional(item.StorageLocation);

    public static string ResolveHardDiskCodeDisplay(YearlyArchiveFilingFact? filingFact, bool isExternalSource)
    {
        if (isExternalSource || filingFact == null)
        {
            return EmptyDisplay;
        }

        if (ArchiveFilingBusinessRules.IsOpticalDiscArchiveCarrierType(filingFact.StorageCarrierType))
        {
            return EmptyDisplay;
        }

        return FormatOptional(filingFact.MediumCode);
    }

    public static string ResolveConfidentialLevelDisplay(
        NetworkInboundItem item,
        YearlyArchiveFilingFact? filingFact,
        bool isExternalSource)
    {
        string value = !string.IsNullOrWhiteSpace(item.ConfidentialLevel)
            ? item.ConfidentialLevel
            : isExternalSource ? string.Empty : filingFact?.ConfidentialLevel ?? string.Empty;
        return FormatOptional(value);
    }

    public static string ResolveDataSizeDisplay(
        NetworkInboundItem item,
        YearlyArchiveFilingFact? filingFact,
        bool isExternalSource)
    {
        if (TryParseDataSizeText(item.DataSizeText, out decimal value, out string unit))
        {
            return ComposeDataSizeText(value, unit);
        }

        if (!isExternalSource && filingFact is { DataSizeMb: > 0 })
        {
            return ComposeDataSizeText(filingFact.DataSizeMb, DefaultDataSizeUnit);
        }

        return EmptyDisplay;
    }

    public static string GetDataSizeValuePart(string? dataSizeText)
    {
        if (TryParseDataSizeText(dataSizeText, out decimal value, out _))
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    public static string GetDataSizeUnitPart(string? dataSizeText)
    {
        if (TryParseDataSizeText(dataSizeText, out _, out string unit))
        {
            return unit;
        }

        return DefaultDataSizeUnit;
    }

    public static string ComposeDataSizeText(decimal value, string? unit)
    {
        if (value <= 0)
        {
            return string.Empty;
        }

        string normalizedUnit = NormalizeDataSizeUnit(unit);
        return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {normalizedUnit}";
    }

    public static bool TryParseDataSizeText(string? dataSizeText, out decimal value, out string unit)
    {
        value = 0;
        unit = DefaultDataSizeUnit;
        if (string.IsNullOrWhiteSpace(dataSizeText))
        {
            return false;
        }

        Match match = DataSizePattern().Match(dataSizeText.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!decimal.TryParse(match.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            || value <= 0)
        {
            return false;
        }

        unit = NormalizeDataSizeUnit(match.Groups["unit"].Value);
        return true;
    }

    public static string NormalizeDataSizeUnit(string? unit)
    {
        string trimmed = unit?.Trim().ToUpperInvariant() ?? string.Empty;
        return DataSizeUnits.Contains(trimmed, StringComparer.Ordinal) ? trimmed : DefaultDataSizeUnit;
    }

    public static void ApplyFilingFactSnapshot(NetworkInboundItem item, YearlyArchiveFilingFact? filingFact)
    {
        if (filingFact == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.ConfidentialLevel)
            && !string.IsNullOrWhiteSpace(filingFact.ConfidentialLevel))
        {
            item.ConfidentialLevel = filingFact.ConfidentialLevel.Trim();
        }

        if (string.IsNullOrWhiteSpace(item.DataSizeText) && filingFact.DataSizeMb > 0)
        {
            item.DataSizeText = ComposeDataSizeText(filingFact.DataSizeMb, DefaultDataSizeUnit);
        }
    }
}
