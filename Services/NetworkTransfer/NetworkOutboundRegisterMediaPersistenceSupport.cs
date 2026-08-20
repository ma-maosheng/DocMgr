using DocMgr.Data;
using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网单登记介质树的持久化（与入网档外同构，归属 <c>NetworkOutboundRecordId</c>）。
/// </summary>
internal static class NetworkOutboundRegisterMediaPersistenceSupport
{
    internal static async Task ReplaceMediaEntriesAsync(
        AppDbContext dbContext,
        int outboundRecordId,
        IReadOnlyList<YearlyArchiveRegisterMedia> mediaEntries)
    {
        List<YearlyArchiveRegisterMedia> existing = await dbContext.YearlyArchiveRegisterMedias
            .Include(media => media.Items)
                .ThenInclude(item => item.ElectronicDetail)
                    .ThenInclude(detail => detail!.Entries)
            .Include(media => media.Items)
                .ThenInclude(item => item.SimulatedDetail)
            .Where(media => media.NetworkOutboundRecordId == outboundRecordId)
            .ToListAsync();

        if (existing.Count > 0)
        {
            foreach (YearlyArchiveRegisterMedia media in existing)
            {
                if (media.Items is { Count: > 0 })
                {
                    foreach (YearlyArchiveRegisterMediaItem item in media.Items)
                    {
                        if (item.ElectronicDetail != null)
                        {
                            dbContext.YearlyArchiveRegisterElectronicMediaItemDetails.Remove(item.ElectronicDetail);
                        }
                    }

                    dbContext.YearlyArchiveRegisterMediaItems.RemoveRange(media.Items);
                }
            }

            dbContext.YearlyArchiveRegisterMedias.RemoveRange(existing);
        }

        if (mediaEntries == null || mediaEntries.Count == 0)
        {
            return;
        }

        foreach (YearlyArchiveRegisterMedia media in mediaEntries)
        {
            media.Id = 0;
            media.NetworkOutboundRecordId = outboundRecordId;
            media.NetworkInboundRecordId = null;
            media.YearlyArchiveRegisterRecordId = null;
            if (media.Items != null)
            {
                foreach (YearlyArchiveRegisterMediaItem item in media.Items)
                {
                    item.Id = 0;
                    item.YearlyArchiveRegisterMediaId = 0;
                    if (item.ElectronicDetail != null)
                    {
                        item.ElectronicDetail.MediaItemId = 0;
                        foreach (YearlyArchiveRegisterElectronicMediaItemEntry entry in item.ElectronicDetail.Entries)
                        {
                            entry.Id = 0;
                            entry.ElectronicMediaItemDetailId = 0;
                        }
                    }

                    if (item.SimulatedDetail != null)
                    {
                        item.SimulatedDetail.MediaItemId = 0;
                    }
                }
            }

            dbContext.YearlyArchiveRegisterMedias.Add(media);
        }
    }
}
