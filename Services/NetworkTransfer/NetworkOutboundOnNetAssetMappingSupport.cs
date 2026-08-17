using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网登记介质子项 → 出网台账 / 申请明细快照。
/// </summary>
internal static class NetworkOutboundOnNetAssetMappingSupport
{
    internal static IEnumerable<YearlyArchiveRegisterMediaItem> EnumerateElectronicMediaItems(
        IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries) =>
        NetworkInboundOnNetAssetMappingSupport.EnumerateElectronicMediaItems(mediaEntries ?? []);

    internal static List<NetworkOutboundItem> BuildItemSnapshots(
        NetworkOutboundRecord header,
        IReadOnlyList<YearlyArchiveRegisterMedia>? mediaEntries)
    {
        var result = new List<NetworkOutboundItem>();
        int sort = 1;
        foreach (YearlyArchiveRegisterMediaItem mediaItem in EnumerateElectronicMediaItems(mediaEntries))
        {
            YearlyArchiveRegisterElectronicMediaItemDetail? detail = mediaItem.ElectronicDetail;
            string dataSizeText = detail == null || detail.DataSizeMb <= 0
                ? string.Empty
                : NetworkInboundItemDisplaySupport.ComposeDataSizeText(
                    detail.DataSizeMb,
                    NetworkInboundItemDisplaySupport.DefaultDataSizeUnit);

            result.Add(new NetworkOutboundItem
            {
                SortOrder = sort++,
                AssetKind = NetworkInboundOnNetAssetMappingSupport.ResolveAssetKind(
                    detail?.MaterialCategory,
                    detail?.SubCategory),
                AssetName = header.MaterialName?.Trim() ?? string.Empty,
                ItemName = mediaItem.ContentDesc?.Trim() ?? string.Empty,
                ServerPath = header.ServerPath?.Trim() ?? string.Empty,
                ConfidentialLevel = mediaItem.ConfidentialLevel?.Trim() ?? string.Empty,
                DataSizeText = dataSizeText,
                ProjectName = header.ProjectName?.Trim() ?? string.Empty,
                Year = header.Year?.Trim() ?? string.Empty,
                CreatedAt = DateTime.Now
            });
        }

        return result;
    }

    internal static NetworkOnNetAsset CreateOutboundedAsset(
        NetworkOutboundRecord outbound,
        NetworkOutboundItem item,
        string assetNo,
        string operatorName,
        DateTime now)
    {
        return new NetworkOnNetAsset
        {
            AssetNo = assetNo,
            AssetKind = item.AssetKind?.Trim() ?? string.Empty,
            AssetName = string.IsNullOrWhiteSpace(item.ItemName)
                ? item.AssetName?.Trim() ?? string.Empty
                : item.ItemName.Trim(),
            ProjectName = outbound.ProjectName?.Trim() ?? string.Empty,
            Year = outbound.Year?.Trim() ?? string.Empty,
            ServerPath = outbound.ServerPath?.Trim() ?? string.Empty,
            ConfidentialLevel = item.ConfidentialLevel?.Trim() ?? string.Empty,
            DataSizeText = item.DataSizeText?.Trim() ?? string.Empty,
            OriginKind = NetworkTransferDomainValues.OriginKindProcessedOutput,
            OriginOutboundItemId = item.Id > 0 ? item.Id : null,
            LifecycleStatus = NetworkTransferDomainValues.LifecycleOutbounded,
            RegisteredBy = operatorName,
            RegisteredAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    internal static List<YearlyArchiveRegisterMedia> CloneMediaForArchiveRegister(
        IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries,
        string outboundNo)
    {
        var result = new List<YearlyArchiveRegisterMedia>();
        foreach (YearlyArchiveRegisterMedia source in mediaEntries ?? [])
        {
            if (!RegisterMediaTreeSupport.IsElectronicMediaEntity(source))
            {
                continue;
            }

            var clone = new YearlyArchiveRegisterMedia
            {
                MediaKind = source.MediaKind,
                MediaType = ArchiveRegisterDomainValues.ElectronicMediaTypeInnerNetwork,
                MediaCount = source.MediaCount > 0 ? source.MediaCount : 1,
                Disposition = ArchiveRegisterDomainValues.ElectronicDispositionNone
            };

            foreach (YearlyArchiveRegisterMediaItem item in source.Items ?? [])
            {
                YearlyArchiveRegisterElectronicMediaItemDetail? detail = item.ElectronicDetail;
                var clonedItem = new YearlyArchiveRegisterMediaItem
                {
                    ItemType = item.ItemType,
                    ContentDesc = item.ContentDesc?.Trim() ?? string.Empty,
                    ContentCount = item.ContentCount > 0 ? item.ContentCount : 1,
                    StoragePath = item.StoragePath?.Trim() ?? string.Empty,
                    Note = string.IsNullOrWhiteSpace(item.Note)
                        ? $"来源出网单 {outboundNo}"
                        : item.Note.Trim(),
                    ConfidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(item.ConfidentialLevel),
                    ElectronicDetail = detail == null
                        ? null
                        : new YearlyArchiveRegisterElectronicMediaItemDetail
                        {
                            MaterialCategory = detail.MaterialCategory,
                            SubCategory = detail.SubCategory,
                            DataOrganizationForm = detail.DataOrganizationForm,
                            DataSizeMb = detail.DataSizeMb,
                            Entries = (detail.Entries ?? [])
                                .Select(entry => new YearlyArchiveRegisterElectronicMediaItemEntry
                                {
                                    EntryKind = entry.EntryKind,
                                    EntryName = entry.EntryName,
                                    RelativePath = entry.RelativePath,
                                    SizeMb = entry.SizeMb,
                                    CreatedAt = entry.CreatedAt,
                                    ModifiedAt = entry.ModifiedAt,
                                    SortOrder = entry.SortOrder
                                })
                                .ToList()
                        }
                };
                clone.Items.Add(clonedItem);
            }

            result.Add(clone);
        }

        return result;
    }
}
