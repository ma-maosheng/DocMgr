using DocMgr.Data;
using DocMgr.Infrastructure.Seeding;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.SystemSettings;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.YearlyArchive;

public class ArchiveRegisterRepository : IArchiveRegisterRepository
{
    private readonly AppDbContext _dbContext;

    private static readonly string[] RegisterElectronicDetailDomainFields =
    [
        nameof(YearlyArchiveRegisterElectronicMediaItemDetail.MaterialCategory),
        nameof(YearlyArchiveRegisterElectronicMediaItemDetail.SubCategory),
        nameof(YearlyArchiveRegisterElectronicMediaItemDetail.DataOrganizationForm),
    ];

    public ArchiveRegisterRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<YearlyArchiveRegisterRecord?> GetByFormNoWithDetailsAsync(string formNo)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(item => item.ElectronicDetail!)
                        .ThenInclude(detail => detail.Entries)
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(item => item.ArchiveBoxLinks)
                        .ThenInclude(link => link.ArchiveBox)
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.ElectronicArchiveUnitLinks)
                    .ThenInclude(link => link.ElectronicArchiveUnit)
            .Include(record => record.ArchiveBoxes)
                .ThenInclude(box => box.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item!.MediaEntry)
            .Include(record => record.ArchiveBoxes)
                .ThenInclude(box => box.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item!.ElectronicDetail!)
                            .ThenInclude(detail => detail.Entries)
            .Include(record => record.ElectronicArchiveUnits)
                .ThenInclude(unit => unit.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item!.MediaEntry)
            .Include(record => record.ElectronicArchiveUnits)
                .ThenInclude(unit => unit.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item!.ElectronicDetail!)
                            .ThenInclude(detail => detail.Entries)
            .FirstOrDefaultAsync(record => record.FormNo == formNo);
    }

    public Task<YearlyArchiveRegisterRecord?> GetByIdWithDetailsAsync(int id)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(item => item.ElectronicDetail!)
                        .ThenInclude(detail => detail.Entries)
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(item => item.ArchiveBoxLinks)
                        .ThenInclude(link => link.ArchiveBox)
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.ElectronicArchiveUnitLinks)
                    .ThenInclude(link => link.ElectronicArchiveUnit)
            .Include(record => record.ArchiveBoxes)
                .ThenInclude(box => box.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item!.MediaEntry)
            .Include(record => record.ArchiveBoxes)
                .ThenInclude(box => box.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item!.ElectronicDetail!)
                            .ThenInclude(detail => detail.Entries)
            .Include(record => record.ElectronicArchiveUnits)
                .ThenInclude(unit => unit.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item!.MediaEntry)
            .Include(record => record.ElectronicArchiveUnits)
                .ThenInclude(unit => unit.MediaItemLinks)
                    .ThenInclude(link => link.MediaItem)
                        .ThenInclude(item => item!.ElectronicDetail!)
                            .ThenInclude(detail => detail.Entries)
            .FirstOrDefaultAsync(record => record.Id == id);
    }

    public Task<List<YearlyArchiveRegisterRecord>> SearchRecordsAsync(string keyword, int? year, int? status, int? projectId)
    {
        IQueryable<YearlyArchiveRegisterRecord> query = _dbContext.YearlyArchiveRegisterRecords
            .Include(record => record.ArchiveBoxes)
            .Include(record => record.ElectronicArchiveUnits)
            .Include(record => record.MediaEntries)
            .AsQueryable();

        if (year.HasValue && year.Value > 0)
        {
            query = query.Where(record => record.CreatedDate.Year == year.Value);
        }

        if (status.HasValue && status.Value != -1)
        {
            query = query.Where(record => record.Status == status.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(record => record.ProjectId == projectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(record =>
                (record.MaterialName ?? string.Empty).Contains(keyword) ||
                (record.FormNo ?? string.Empty).Contains(keyword) ||
                (record.ApplicantName ?? string.Empty).Contains(keyword) ||
                (record.ProjectName ?? string.Empty).Contains(keyword) ||
                (record.ProvideUnit ?? string.Empty).Contains(keyword) ||
                record.ArchiveBoxes.Any(box =>
                    (box.ArchiveSequenceNo ?? string.Empty).Contains(keyword) ||
                    (box.BoxLocationCode ?? string.Empty).Contains(keyword)) ||
                record.ElectronicArchiveUnits.Any(unit =>
                    (unit.ElectronicArchiveNo ?? string.Empty).Contains(keyword) ||
                    (unit.StorageLocation ?? string.Empty).Contains(keyword)));
        }

        return query.OrderByDescending(record => record.CreatedDate).ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetSubmittedRecordsForToDoAsync(int takeCount)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .AsNoTracking()
            .Where(record => record.Status == YearlyArchiveRegisterRecord.Submitted
                || record.Status == YearlyArchiveRegisterRecord.Approved
                || record.Status == YearlyArchiveRegisterRecord.SignedUploaded)
            .OrderByDescending(record => record.CreatedDate)
            .Take(takeCount)
            .ToListAsync();
    }

    public async Task<int> SaveOrUpdateRecordGraphAsync(YearlyArchiveRegisterRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var existingRecord = await _dbContext.YearlyArchiveRegisterRecords
            .Include(item => item.MediaEntries)
                .ThenInclude(media => media.Items)
                    .ThenInclude(mediaItem => mediaItem.ElectronicDetail!)
                        .ThenInclude(detail => detail.Entries)
            .Include(item => item.ArchiveBoxes)
            .FirstOrDefaultAsync(item => item.Id == record.Id || (record.Id == 0 && item.FormNo == record.FormNo));

        if (existingRecord == null)
        {
            if (record.Id != 0)
            {
                record.Id = 0;
            }

            if (record.MediaEntries != null)
            {
                foreach (var media in record.MediaEntries)
                {
                    media.Id = 0;
                    if (media.Items != null)
                    {
                        foreach (var item in media.Items)
                        {
                            item.Id = 0;
                            if (item.ElectronicDetail != null)
                            {
                                item.ElectronicDetail.MediaItemId = 0;
                                foreach (var entry in item.ElectronicDetail.Entries)
                                {
                                    entry.Id = 0;
                                    entry.ElectronicMediaItemDetailId = 0;
                                }
                            }
                        }
                    }
                }
            }

            _dbContext.YearlyArchiveRegisterRecords.Add(record);
        }
        else
        {
            record.Id = existingRecord.Id;
            _dbContext.Entry(existingRecord).CurrentValues.SetValues(record);

            if (record.MediaEntries != null && record.MediaEntries.Any())
            {
                var newMediaData = record.MediaEntries.Select(media => new YearlyArchiveRegisterMedia
                {
                    Id = 0,
                    YearlyArchiveRegisterRecordId = existingRecord.Id,
                    MediaKind = media.MediaKind,
                    MediaType = media.MediaType,
                    MediaCount = media.MediaCount,
                    Disposition = media.Disposition,
                    IsBorrowedHardDisk = media.IsBorrowedHardDisk,
                    BorrowedHardDiskCode = media.BorrowedHardDiskCode ?? string.Empty,
                    Items = media.Items?.Select(item => MapMediaItem(item, media.MediaKind)).ToList()
                        ?? new List<YearlyArchiveRegisterMediaItem>()
                }).ToList();

                var mediasToRemove = existingRecord.MediaEntries.ToList();
                if (mediasToRemove.Any())
                {
                    foreach (var media in mediasToRemove)
                    {
                        if (media.Items is { Count: > 0 })
                        {
                            _dbContext.YearlyArchiveRegisterMediaItems.RemoveRange(media.Items.ToList());
                        }
                    }

                    _dbContext.YearlyArchiveRegisterMedias.RemoveRange(mediasToRemove);
                }

                _dbContext.YearlyArchiveRegisterMedias.AddRange(newMediaData);

                existingRecord.MediaEntries.Clear();
                foreach (var media in newMediaData)
                {
                    existingRecord.MediaEntries.Add(media);
                }
            }
        }

        await _dbContext.SaveChangesAsync();

        var recordId = existingRecord?.Id ?? record.Id;
        if (recordId <= 0)
        {
            return recordId;
        }

        bool needsFix = false;
        var medias = existingRecord?.MediaEntries ?? record.MediaEntries ?? new List<YearlyArchiveRegisterMedia>();
        foreach (var media in medias)
        {
            if (media.YearlyArchiveRegisterRecordId == 0)
            {
                media.YearlyArchiveRegisterRecordId = recordId;
                needsFix = true;
            }

            if (media.Items == null)
            {
                continue;
            }

            foreach (var item in media.Items)
            {
                if (item.YearlyArchiveRegisterMediaId == 0 && media.Id > 0)
                {
                    item.YearlyArchiveRegisterMediaId = media.Id;
                    needsFix = true;
                }
            }
        }

        if (needsFix)
        {
            await _dbContext.SaveChangesAsync();
        }

        return recordId;
    }

    public async Task<int> LinkOrphanAttachmentsToRecordAsync(string formNo, int recordId)
    {
        if (string.IsNullOrWhiteSpace(formNo) || recordId <= 0)
        {
            return 0;
        }

        var orphanAttachments = await GetOrphanAttachmentsByFormNoAsync(formNo);
        if (orphanAttachments.Count == 0)
        {
            return 0;
        }

        foreach (var attachment in orphanAttachments)
        {
            attachment.BusinessId = recordId;
        }

        await _dbContext.SaveChangesAsync();
        return orphanAttachments.Count;
    }

    public Task<List<User>> GetUsersAsync()
    {
        return _dbContext.Users.AsNoTracking().ToListAsync();
    }

    public Task<List<SystemAttachment>> GetAttachmentSummariesByFormNoAsync(string formNo)
    {
        return _dbContext.SystemAttachments
            .Where(attachment => attachment.BusinessNo == formNo && attachment.BusinessType == "YearlyArchiveRegister")
            .OrderByDescending(attachment => attachment.UploadTime)
            .Select(attachment => new SystemAttachment
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                Extension = attachment.Extension,
                FileSize = attachment.FileSize,
                UploadTime = attachment.UploadTime,
                UploaderName = attachment.UploaderName,
                FileCategory = attachment.FileCategory,
                BusinessType = attachment.BusinessType,
                BusinessNo = attachment.BusinessNo,
                BusinessId = attachment.BusinessId,
            })
            .ToListAsync();
    }

    public Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId)
    {
        return _dbContext.SystemAttachments.FindAsync(attachmentId).AsTask();
    }

    public void AddAttachment(SystemAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _dbContext.SystemAttachments.Add(attachment);
    }

    public void RemoveAttachment(SystemAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _dbContext.SystemAttachments.Remove(attachment);
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetRecordsByApplicantAsync(string applicantName)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .AsNoTracking()
            .Include(record => record.ArchiveBoxes)
            .Include(record => record.ElectronicArchiveUnits)
            .Where(record => record.ApplicantName == applicantName)
            .OrderByDescending(record => record.CreatedDate)
            .ToListAsync();
    }

    public Task<List<YearlyArchiveRegisterRecord>> GetRecordsByYearAsync(int year)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .AsNoTracking()
            .Include(record => record.ArchiveBoxes)
            .Include(record => record.ElectronicArchiveUnits)
            .Where(record => record.CreatedDate.Year == year)
            .OrderBy(record => record.Status)
            .ThenByDescending(record => record.CreatedDate)
            .ToListAsync();
    }

    public Task<List<string>> GetFormNosByPrefixAsync(string prefix)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .Where(record => record.FormNo.StartsWith(prefix))
            .Select(record => record.FormNo)
            .ToListAsync();
    }

    public Task<List<int>> GetDistinctCreatedYearsAsync()
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .Select(record => record.CreatedDate.Year)
            .Distinct()
            .OrderByDescending(year => year)
            .ToListAsync();
    }

    public Task<YearlyArchiveRegisterRecord?> GetRecordForRemovalAsync(int id)
    {
        return _dbContext.YearlyArchiveRegisterRecords
            .Include(record => record.MediaEntries)
                .ThenInclude(media => media.Items)
            .FirstOrDefaultAsync(record => record.Id == id);
    }

    public Task<List<SystemAttachment>> GetAttachmentsByBusinessIdAsync(int businessId)
    {
        return _dbContext.SystemAttachments
            .Where(attachment => attachment.BusinessId == businessId && attachment.BusinessType == "YearlyArchiveRegister")
            .ToListAsync();
    }

    public Task<List<SystemAttachment>> GetOrphanAttachmentsByFormNoAsync(string formNo)
    {
        return _dbContext.SystemAttachments
            .Where(attachment => attachment.BusinessNo == formNo && attachment.BusinessId == 0 && attachment.BusinessType == "YearlyArchiveRegister")
            .ToListAsync();
    }

    public void RemoveAttachments(IEnumerable<SystemAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        _dbContext.SystemAttachments.RemoveRange(attachments);
    }

    public void RemoveRegisterRecord(YearlyArchiveRegisterRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _dbContext.YearlyArchiveRegisterRecords.Remove(record);
    }

    public Task<List<FieldDomainDefinition>> GetPageDomainDefinitionsAsync(
        string registerRecordEntityName,
        IReadOnlyCollection<string> registerRecordFields,
        string registerMediaEntityName,
        IReadOnlyCollection<string> registerMediaFields,
        string registerMediaItemEntityName,
        IReadOnlyCollection<string> registerMediaItemFields)
    {
        return _dbContext.FieldDomainDefinitions
            .AsNoTracking()
            .Include(definition => definition.Options.Where(option => option.IsEnabled))
            .Where(definition => definition.IsDomainEnabled
                && ((definition.EntityName == registerRecordEntityName && registerRecordFields.Contains(definition.FieldName))
                    || (definition.EntityName == registerMediaEntityName && registerMediaFields.Contains(definition.FieldName))
                    || (definition.EntityName == registerMediaItemEntityName && registerMediaItemFields.Contains(definition.FieldName))
                    || (definition.EntityName == nameof(YearlyArchiveRegisterElectronicMediaItemDetail)
                        && RegisterElectronicDetailDomainFields.Contains(definition.FieldName))))
            .ToListAsync();
    }

    public List<FieldDomainDefinition> GetPageDomainDefinitions(
        string registerRecordEntityName,
        IReadOnlyCollection<string> registerRecordFields,
        string registerMediaEntityName,
        IReadOnlyCollection<string> registerMediaFields,
        string registerMediaItemEntityName,
        IReadOnlyCollection<string> registerMediaItemFields)
    {
        return _dbContext.FieldDomainDefinitions
            .AsNoTracking()
            .Include(definition => definition.Options.Where(option => option.IsEnabled))
            .Where(definition => definition.IsDomainEnabled
                && ((definition.EntityName == registerRecordEntityName && registerRecordFields.Contains(definition.FieldName))
                    || (definition.EntityName == registerMediaEntityName && registerMediaFields.Contains(definition.FieldName))
                    || (definition.EntityName == registerMediaItemEntityName && registerMediaItemFields.Contains(definition.FieldName))
                    || (definition.EntityName == nameof(YearlyArchiveRegisterElectronicMediaItemDetail)
                        && RegisterElectronicDetailDomainFields.Contains(definition.FieldName))))
            .ToList();
    }

    public void SeedFieldDomainDefaults()
    {
        FieldDomainSeedService.SeedDefaults(new FieldDomainSeedRepository(_dbContext));
    }

    public Task<List<int>> GetElectronicArchiveUnitIdsByRegisterRecordIdAsync(int registerRecordId)
    {
        return _dbContext.YearlyElectronicArchiveUnitMediaLinks
            .AsNoTracking()
            .Where(link => link.MediaEntry != null && link.MediaEntry.YearlyArchiveRegisterRecordId == registerRecordId)
            .Select(link => link.YearlyElectronicArchiveUnitId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<List<(string DiscCode, string Location, string BusinessNo, DateTime OperateTime)>> GetOpticalDiscLedgerRowsAsync(IReadOnlyCollection<int> unitIds)
    {
        var rows = await _dbContext.YearlyElectronicArchiveUnitDiscLinks
            .AsNoTracking()
            .Include(link => link.OpticalDiscMedium)
                .ThenInclude(disc => disc!.Ledger)
            .Include(link => link.ElectronicArchiveUnit)
            .Where(link => unitIds.Contains(link.YearlyElectronicArchiveUnitId))
            .Where(link => link.OpticalDiscMedium != null && !link.OpticalDiscMedium.IsDeleted)
            .OrderBy(link => link.OpticalDiscMedium.DiscCode)
            .Select(link => new
            {
                DiscCode = link.OpticalDiscMedium.DiscCode,
                Location = link.OpticalDiscMedium.Ledger != null ? link.OpticalDiscMedium.Ledger.StorageLocation : string.Empty,
                BusinessNo = link.ElectronicArchiveUnit.ElectronicArchiveNo,
                OperateTime = link.CreatedAt == default ? link.ElectronicArchiveUnit.ArchivedDate : link.CreatedAt
            })
            .ToListAsync();

        return rows
            .Select(item => (item.DiscCode, item.Location, item.BusinessNo, item.OperateTime))
            .ToList();
    }

    public Task<List<HardDiskMedium>> GetHardDiskMediaByRegisterLockAsync(int recordId, string formNo, bool onlyNotDeleted)
    {
        string trimmedFormNo = formNo?.Trim() ?? string.Empty;

        IQueryable<HardDiskMedium> query = _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => item.RegisterLock != null)
            .Where(item => item.RegisterLock!.BusinessType == HardDiskRegisterLock.BusinessTypeArchiveRegister)
            .Where(item => item.RegisterLock!.BusinessRecordId == recordId
                || (!string.IsNullOrWhiteSpace(trimmedFormNo) && item.RegisterLock.BusinessNo == trimmedFormNo));

        if (onlyNotDeleted)
        {
            query = query.Where(item => !item.IsDeleted);
        }

        return query.ToListAsync();
    }

    public Task<List<HardDiskMedium>> GetHardDiskMediaByDiskCodesAsync(IReadOnlyCollection<string> diskCodes)
    {
        return _dbContext.HardDiskMedia
            .Include(item => item.Ledger)
            .Include(item => item.RegisterLock)
            .Where(item => diskCodes.Contains(item.DiskCode))
            .ToListAsync();
    }

    public void RemoveHardDiskRegisterLock(HardDiskRegisterLock registerLock)
    {
        ArgumentNullException.ThrowIfNull(registerLock);
        _dbContext.HardDiskRegisterLocks.Remove(registerLock);
    }

    public Task<int> SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }

    private static YearlyArchiveRegisterMediaItem MapMediaItem(YearlyArchiveRegisterMediaItem source, string mediaKind)
    {
        var mapped = new YearlyArchiveRegisterMediaItem
        {
            Id = 0,
            ItemType = source.ItemType,
            ContentDesc = source.ContentDesc,
            ContentCount = source.ContentCount,
            StoragePath = source.StoragePath,
            Note = source.Note,
            ConfidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(source.ConfidentialLevel)
        };

        if (!string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase)
            || source.ElectronicDetail == null)
        {
            return mapped;
        }

        mapped.ElectronicDetail = new YearlyArchiveRegisterElectronicMediaItemDetail
        {
            MaterialCategory = source.ElectronicDetail.MaterialCategory,
            SubCategory = source.ElectronicDetail.SubCategory,
            DataOrganizationForm = source.ElectronicDetail.DataOrganizationForm,
            DataSizeMb = source.ElectronicDetail.DataSizeMb,
            Entries = source.ElectronicDetail.Entries
                .Select((entry, index) => new YearlyArchiveRegisterElectronicMediaItemEntry
                {
                    Id = 0,
                    EntryKind = entry.EntryKind,
                    EntryName = entry.EntryName,
                    RelativePath = entry.RelativePath,
                    SizeMb = entry.SizeMb,
                    CreatedAt = entry.CreatedAt,
                    ModifiedAt = entry.ModifiedAt,
                    SortOrder = entry.SortOrder > 0 ? entry.SortOrder : (index + 1) * 10
                })
                .ToList()
        };

        return mapped;
    }
}
