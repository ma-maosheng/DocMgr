using System.IO;
using DocMgr.Data;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Services.HistoryArchive;

/// <summary>
/// 历史存档杂项资料：交互录入、四段位置校验与实物照片。
/// </summary>
public sealed class HistoryMiscMaterialService : IHistoryMiscMaterialService
{
    private readonly AppDbContext _dbContext;
    private readonly IUserContextService _userContextService;

    public HistoryMiscMaterialService(AppDbContext dbContext, IUserContextService userContextService)
    {
        _dbContext = dbContext;
        _userContextService = userContextService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoryMiscMaterialListItem>> SearchAsync(
        string? majorCategory,
        string? subCategory,
        string? containerKind,
        string? lifecycleStatus,
        string? keyword)
    {
        List<HistoryMiscMaterial> materials = await _dbContext.HistoryMiscMaterials
            .AsNoTracking()
            .Include(item => item.Lines)
            .OrderBy(item => item.MajorCategory)
            .ThenBy(item => item.SubCategory)
            .ThenBy(item => item.ContainerCode)
            .ToListAsync();

        string major = majorCategory?.Trim() ?? string.Empty;
        string sub = subCategory?.Trim() ?? string.Empty;
        string kind = containerKind?.Trim() ?? string.Empty;
        string lifecycle = lifecycleStatus?.Trim() ?? string.Empty;
        string text = keyword?.Trim() ?? string.Empty;
        bool filterInCabinet = string.Equals(lifecycle, HistoryArchiveDisposalDomainValues.LifecycleInStock, StringComparison.Ordinal);
        bool filterDisposed = string.Equals(lifecycle, HistoryArchiveDisposalDomainValues.LifecycleDisposed, StringComparison.Ordinal)
            || string.Equals(lifecycle, "离库", StringComparison.Ordinal);

        var photoCounts = await _dbContext.SystemAttachments
            .AsNoTracking()
            .Where(item => item.BusinessType == HistoryArchiveDisposalDomainValues.MiscAttachmentBusinessType
                && item.FileCategory == HistoryArchiveDisposalDomainValues.MiscAttachmentCategoryPhoto)
            .GroupBy(item => item.BusinessId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count);

        var result = new List<HistoryMiscMaterialListItem>();
        foreach (HistoryMiscMaterial material in materials)
        {
            if (!string.IsNullOrWhiteSpace(major)
                && !string.Equals(major, "全部", StringComparison.Ordinal)
                && !string.Equals(material.MajorCategory, major, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(sub)
                && !string.Equals(sub, "全部", StringComparison.Ordinal)
                && !string.Equals(material.SubCategory, sub, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(kind)
                && !string.Equals(kind, "全部", StringComparison.Ordinal)
                && !string.Equals(material.ContainerKind, kind, StringComparison.Ordinal))
            {
                continue;
            }

            string status = HistoryArchiveDisposalDomainValues.NormalizeLifecycleStatus(material.LifecycleStatus);
            if (filterInCabinet && HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(status))
            {
                continue;
            }

            if (filterDisposed && !HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(status))
            {
                continue;
            }

            IEnumerable<string> lines = material.Lines
                .OrderBy(item => item.SortOrder)
                .Select(item => item.LineText);
            string summary = HistoryMiscMaterialTextSupport.FormatStoredDetailLines(lines);
            if (!Contains(material, summary, text))
            {
                continue;
            }

            result.Add(new HistoryMiscMaterialListItem
            {
                Id = material.Id,
                MajorCategory = material.MajorCategory,
                SubCategory = material.SubCategory,
                ContainerKind = material.ContainerKind,
                ContainerCode = material.ContainerCode,
                StorageLocation = material.StorageLocation,
                LastStorageLocation = material.LastStorageLocation,
                LifecycleStatus = status,
                DetailSummary = summary,
                Remark = material.Remark,
                PhotoCount = photoCounts.TryGetValue(material.Id, out int count) ? count : 0
            });
        }

        return result;
    }

    /// <inheritdoc />
    public Task<HistoryMiscMaterial?> GetAsync(int id) =>
        _dbContext.HistoryMiscMaterials
            .AsNoTracking()
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == id);

    /// <inheritdoc />
    public async Task<HistoryMiscVocabulary> GetVocabularyAsync()
    {
        var rows = await _dbContext.HistoryMiscMaterials
            .AsNoTracking()
            .Select(item => new { item.MajorCategory, item.SubCategory, item.ContainerKind })
            .ToListAsync();

        HistoryMiscVocabulary loaded = new()
        {
            MajorCategories = rows
                .Select(item => item.MajorCategory?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList(),
            ContainerKinds = rows
                .Select(item => item.ContainerKind?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList(),
            SubCategories = rows
                .Where(item => !string.IsNullOrWhiteSpace(item.MajorCategory) && !string.IsNullOrWhiteSpace(item.SubCategory))
                .Select(item => new HistoryMiscSubCategoryOption
                {
                    MajorCategory = item.MajorCategory.Trim(),
                    SubCategory = item.SubCategory.Trim()
                })
                .DistinctBy(item => item.MajorCategory + "\u001f" + item.SubCategory)
                .OrderBy(item => item.MajorCategory, StringComparer.Ordinal)
                .ThenBy(item => item.SubCategory, StringComparer.Ordinal)
                .ToList()
        };
        return HistoryMiscMaterialDefaults.MergeSeed(loaded);
    }

    /// <inheritdoc />
    public async Task<string> AllocateContainerCodeAsync(int? year = null)
    {
        int numberingYear = year ?? DateTime.Now.Year;
        string prefix = HistoryMiscMaterialDefaults.BuildContainerCodePrefix(numberingYear);
        List<string> codes = await _dbContext.HistoryMiscMaterials
            .AsNoTracking()
            .Where(item => item.ContainerCode.StartsWith(prefix))
            .Select(item => item.ContainerCode)
            .ToListAsync();

        int nextSequence = 1;
        foreach (string code in codes)
        {
            if (code.Length <= prefix.Length)
            {
                continue;
            }

            if (int.TryParse(code[prefix.Length..], out int sequence) && sequence >= nextSequence)
            {
                nextSequence = sequence + 1;
            }
        }

        return prefix + nextSequence.ToString("D3");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoryMiscSlotOption>> SuggestSlotsAsync()
    {
        List<Cabinet> cabinets = await _dbContext.Cabinets
            .AsNoTracking()
            .Where(item => item.Type == CabinetType.Vertical || item.Type == CabinetType.Horizontal)
            .OrderBy(item => item.Name)
            .ToListAsync();

        IReadOnlyList<LibraryMaterialRef> libraryMaterials = await LoadInLibraryMaterialsAsync(excludeMaterialId: 0);
        HashSet<string> occupied = libraryMaterials
            .Select(item => item.NormalizedLocation)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var occupiedBySlot = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (string location in occupied)
        {
            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(location, out string cabinetName, out string side, out int row, out int column)
                || !ArchiveSlotLocationSupport.TryParseSequenceIndex(location, out int sequence))
            {
                continue;
            }

            string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, side, row, column);
            if (!occupiedBySlot.TryGetValue(slotKey, out HashSet<int>? sequences))
            {
                sequences = new HashSet<int>();
                occupiedBySlot[slotKey] = sequences;
            }

            sequences.Add(sequence);
        }

        var options = new List<HistoryMiscSlotOption>();
        foreach (Cabinet cabinet in cabinets)
        {
            string cabinetName = CabinetNameNormalizer.Normalize(cabinet.Name);
            if (string.IsNullOrWhiteSpace(cabinetName))
            {
                continue;
            }

            int faceCount = Math.Max(1, cabinet.FaceCount);
            int layerCount = Math.Max(1, cabinet.LayerCount);
            int columnCount = Math.Max(1, cabinet.ColumnCount);
            string typeDisplay = cabinet.Type == CabinetType.Horizontal ? "卧式文件柜" : "立式文件柜";
            for (int faceIndex = 0; faceIndex < faceCount && faceIndex < 2; faceIndex++)
            {
                string face = faceIndex == 0 ? "A" : "B";
                for (int layer = 1; layer <= layerCount; layer++)
                {
                    for (int column = 1; column <= columnCount; column++)
                    {
                        string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, face, layer, column);
                        HashSet<int> sequences = occupiedBySlot.TryGetValue(slotKey, out HashSet<int>? found)
                            ? found
                            : new HashSet<int>();
                        int sequence;
                        try
                        {
                            sequence = ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(sequences);
                        }
                        catch (InvalidOperationException)
                        {
                            continue;
                        }

                        options.Add(new HistoryMiscSlotOption
                        {
                            CabinetName = cabinetName,
                            CabinetTypeDisplay = typeDisplay,
                            SlotKey = slotKey,
                            SuggestedSequence = sequence,
                            SuggestedLocation = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                                cabinetName, face, layer, column, sequence),
                            OccupiedCount = sequences.Count
                        });
                    }
                }
            }
        }

        return options;
    }

    /// <inheritdoc />
    public async Task<HistoryMiscMaterial> SaveAsync(HistoryMiscMaterialSaveRequest request, User currentUser)
    {
        HistoryArchiveLedgerPermissionSupport.EnsureCanMaintain(currentUser);
        ArgumentNullException.ThrowIfNull(request);

        HistoryMiscVocabulary vocabulary = await GetVocabularyAsync();
        string major = HistoryMiscMaterialTextSupport.Canonicalize(request.MajorCategory, vocabulary.MajorCategories);
        string sub = HistoryMiscMaterialTextSupport.Canonicalize(
            request.SubCategory,
            vocabulary.SubCategories
                .Where(item => string.Equals(item.MajorCategory, major, StringComparison.Ordinal))
                .Select(item => item.SubCategory));
        string containerKind = HistoryMiscMaterialTextSupport.Canonicalize(request.ContainerKind, vocabulary.ContainerKinds);
        string containerCode = request.ContainerCode?.Trim() ?? string.Empty;
        IReadOnlyList<LibraryMaterialRef> libraryMaterials = await LoadInLibraryMaterialsAsync(request.Id);
        string location = await NormalizeAndValidateLocationAsync(request.StorageLocation, libraryMaterials);
        List<string> lines = request.DetailLines
            .Select(item => item?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        if (string.IsNullOrWhiteSpace(major))
        {
            throw new InvalidOperationException("请填写资料大类。");
        }

        if (string.IsNullOrWhiteSpace(sub))
        {
            throw new InvalidOperationException("请填写资料子类。");
        }

        if (string.IsNullOrWhiteSpace(containerKind))
        {
            throw new InvalidOperationException("请填写存放容器。");
        }

        if (string.IsNullOrWhiteSpace(containerCode))
        {
            throw new InvalidOperationException("请填写容器编号。");
        }

        if (lines.Count == 0)
        {
            throw new InvalidOperationException("请至少填写一行资料详情。");
        }

        await EnsureHasPhysicalPhotoAsync(request);
        EnsureContainerCodeAvailable(containerKind, containerCode, libraryMaterials);

        DateTime now = DateTime.Now;
        string operatorName = ResolveOperatorName(currentUser);
        HistoryMiscMaterial material;
        if (request.Id > 0)
        {
            material = await _dbContext.HistoryMiscMaterials
                    .Include(item => item.Lines)
                    .FirstOrDefaultAsync(item => item.Id == request.Id)
                ?? throw new InvalidOperationException("未找到杂项资料。");
            if (!HistoryArchiveDisposalDomainValues.IsInStockLifecycle(material.LifecycleStatus))
            {
                throw new InvalidOperationException("离库锁定或已离库的杂项资料不能修改。");
            }
        }
        else
        {
            material = new HistoryMiscMaterial
            {
                Registrant = operatorName,
                RegisteredAt = now,
                LifecycleStatus = HistoryArchiveDisposalDomainValues.LifecycleInStock
            };
            _dbContext.HistoryMiscMaterials.Add(material);
        }

        material.MajorCategory = major;
        material.SubCategory = sub;
        material.ContainerKind = containerKind;
        material.ContainerCode = containerCode;
        material.StorageLocation = location;
        material.Remark = request.Remark?.Trim() ?? string.Empty;
        material.Modifier = operatorName;
        material.ModifiedAt = now;

        if (material.Lines.Count > 0)
        {
            _dbContext.HistoryMiscMaterialLines.RemoveRange(material.Lines);
            material.Lines.Clear();
        }

        for (int index = 0; index < lines.Count; index++)
        {
            material.Lines.Add(new HistoryMiscMaterialLine
            {
                SortOrder = index + 1,
                LineText = lines[index]
            });
        }

        if (request.Id > 0 && request.RemovedAttachmentIds.Count > 0)
        {
            List<SystemAttachment> removing = await _dbContext.SystemAttachments
                .Where(item => request.RemovedAttachmentIds.Contains(item.Id)
                    && item.BusinessId == material.Id
                    && item.BusinessType == HistoryArchiveDisposalDomainValues.MiscAttachmentBusinessType)
                .ToListAsync();
            _dbContext.SystemAttachments.RemoveRange(removing);
        }

        await _dbContext.SaveChangesAsync();

        if (request.AddedPhotos.Count > 0)
        {
            foreach (HistoryMiscPhotoDraft photo in request.AddedPhotos)
            {
                if (photo.Content == null || photo.Content.Length == 0)
                {
                    continue;
                }

                string fileName = string.IsNullOrWhiteSpace(photo.FileName) ? "实物照片.jpg" : photo.FileName.Trim();
                _dbContext.SystemAttachments.Add(new SystemAttachment
                {
                    BusinessType = HistoryArchiveDisposalDomainValues.MiscAttachmentBusinessType,
                    BusinessNo = material.ContainerCode,
                    BusinessId = material.Id,
                    FileName = fileName,
                    Extension = Path.GetExtension(fileName),
                    FileSize = photo.Content.Length,
                    FileContent = photo.Content,
                    FileCategory = HistoryArchiveDisposalDomainValues.MiscAttachmentCategoryPhoto,
                    UploadTime = now,
                    UploaderName = operatorName
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        return material;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, User currentUser)
    {
        HistoryArchiveLedgerPermissionSupport.EnsureCanMaintain(currentUser);
        HistoryMiscMaterial material = await _dbContext.HistoryMiscMaterials
                .Include(item => item.Lines)
                .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new InvalidOperationException("未找到杂项资料。");
        if (!HistoryArchiveDisposalDomainValues.IsInStockLifecycle(material.LifecycleStatus))
        {
            throw new InvalidOperationException("离库锁定或已离库的杂项资料不能删除。");
        }

        string sourceKey = HistoryArchiveDisposalDomainValues.BuildSourceRecordKey(
            HistoryArchiveDisposalDomainValues.MaterialKindMisc,
            material.Id);
        string sourceKeyWithSeparator = sourceKey + "|";
        string sourceKeyInMiddle = "|" + sourceKey + "|";
        string sourceKeyAtEnd = "|" + sourceKey;
        bool usedByDisposal = await _dbContext.HistoryArchiveDisposalItems.AnyAsync(item =>
            item.SourceRecordKeys == sourceKey
            || item.SourceRecordKeys.StartsWith(sourceKeyWithSeparator)
            || item.SourceRecordKeys.EndsWith(sourceKeyAtEnd)
            || item.SourceRecordKeys.Contains(sourceKeyInMiddle));
        if (usedByDisposal)
        {
            throw new InvalidOperationException("该容器已进入离库处置，不能删除。");
        }

        List<SystemAttachment> photos = await _dbContext.SystemAttachments
            .Where(item => item.BusinessType == HistoryArchiveDisposalDomainValues.MiscAttachmentBusinessType
                && item.BusinessId == material.Id)
            .ToListAsync();
        _dbContext.SystemAttachments.RemoveRange(photos);
        _dbContext.HistoryMiscMaterialLines.RemoveRange(material.Lines);
        _dbContext.HistoryMiscMaterials.Remove(material);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemAttachment>> GetPhotosAsync(int materialId)
    {
        return await _dbContext.SystemAttachments
            .AsNoTracking()
            .Where(item => item.BusinessType == HistoryArchiveDisposalDomainValues.MiscAttachmentBusinessType
                && item.BusinessId == materialId
                && item.FileCategory == HistoryArchiveDisposalDomainValues.MiscAttachmentCategoryPhoto)
            .OrderBy(item => item.UploadTime)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoryMiscPhotoCatalogItem>> GetPhotoCatalogAsync(int materialId)
    {
        if (materialId <= 0)
        {
            return Array.Empty<HistoryMiscPhotoCatalogItem>();
        }

        return await _dbContext.SystemAttachments
            .AsNoTracking()
            .Where(item => item.BusinessType == HistoryArchiveDisposalDomainValues.MiscAttachmentBusinessType
                && item.BusinessId == materialId
                && item.FileCategory == HistoryArchiveDisposalDomainValues.MiscAttachmentCategoryPhoto)
            .OrderBy(item => item.UploadTime)
            .ThenBy(item => item.Id)
            .Select(item => new HistoryMiscPhotoCatalogItem
            {
                AttachmentId = item.Id,
                FileName = item.FileName,
                UploadTime = item.UploadTime
            })
            .ToListAsync();
    }

    /// <inheritdoc />
    public Task<SystemAttachment?> GetPhotoAsync(int attachmentId) =>
        _dbContext.SystemAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == attachmentId
                && item.BusinessType == HistoryArchiveDisposalDomainValues.MiscAttachmentBusinessType);

    /// <summary>新增与编辑保存后，至少保留一张实物照片。</summary>
    private async Task EnsureHasPhysicalPhotoAsync(HistoryMiscMaterialSaveRequest request)
    {
        int addedCount = request.AddedPhotos.Count(photo => photo.Content is { Length: > 0 });
        int keptCount = 0;
        if (request.Id > 0)
        {
            HashSet<int> removingIds = request.RemovedAttachmentIds
                .Where(id => id > 0)
                .ToHashSet();
            List<int> existingIds = await _dbContext.SystemAttachments
                .AsNoTracking()
                .Where(item => item.BusinessType == HistoryArchiveDisposalDomainValues.MiscAttachmentBusinessType
                    && item.BusinessId == request.Id
                    && item.FileCategory == HistoryArchiveDisposalDomainValues.MiscAttachmentCategoryPhoto)
                .Select(item => item.Id)
                .ToListAsync();
            keptCount = existingIds.Count(id => !removingIds.Contains(id));
        }

        if (keptCount + addedCount < 1)
        {
            throw new InvalidOperationException("请至少上传一张实物照片。");
        }
    }

    private async Task<string> NormalizeAndValidateLocationAsync(
        string? location,
        IReadOnlyList<LibraryMaterialRef> libraryMaterials)
    {
        string normalized = ArchiveSlotLocationSupport.NormalizeFullLocationCode(location);
        if (string.IsNullOrWhiteSpace(normalized)
            || !HistoryArchiveBoxCodeSupport.TryParseBoxCode(normalized, out string cabinetName, out string faceCode, out string slotCode, out _))
        {
            throw new InvalidOperationException("存放位置须为四段编号，例如丙A-2-1-01。");
        }

        string normalizedCabinet = CabinetNameNormalizer.Normalize(cabinetName);
        Cabinet? cabinet = await _dbContext.Cabinets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Name.ToUpper() == normalizedCabinet);
        if (cabinet == null)
        {
            throw new InvalidOperationException($"未找到柜体「{cabinetName}」。");
        }

        if (cabinet.Type is not (CabinetType.Vertical or CabinetType.Horizontal))
        {
            throw new InvalidOperationException("杂项资料只能放在立式文件柜或卧式文件柜，不能使用防磁磁盘柜或标准滑道式档案柜。");
        }

        int layer = int.Parse(slotCode.Split('-')[0]);
        int column = int.Parse(slotCode.Split('-')[1]);
        int faceIndex = string.Equals(faceCode, "A", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        if (faceIndex > Math.Max(1, cabinet.FaceCount)
            || layer < 1 || layer > Math.Max(1, cabinet.LayerCount)
            || column < 1 || column > Math.Max(1, cabinet.ColumnCount))
        {
            throw new InvalidOperationException($"存放位置超出柜体「{cabinet.Name}」的面、层或列范围。");
        }

        normalized = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
            normalizedCabinet,
            faceCode.ToUpperInvariant(),
            layer,
            column,
            ArchiveSlotLocationSupport.TryParseSequenceIndex(normalized, out int sequence) ? sequence : 0);

        LibraryMaterialRef? conflict = libraryMaterials.FirstOrDefault(item =>
            string.Equals(item.NormalizedLocation, normalized, StringComparison.OrdinalIgnoreCase));
        if (conflict != null)
        {
            throw new InvalidOperationException($"存放位置 {normalized} 与库内{conflict.Display}冲突。");
        }

        return normalized;
    }

    /// <summary>在库杂项、在库历史档案盒、在用年度档案盒。已离库记录不占编号和位置。</summary>
    private async Task<IReadOnlyList<LibraryMaterialRef>> LoadInLibraryMaterialsAsync(int excludeMaterialId)
    {
        var materials = new List<LibraryMaterialRef>();
        var miscRows = await _dbContext.HistoryMiscMaterials
            .AsNoTracking()
            .Where(item => item.LifecycleStatus != HistoryArchiveDisposalDomainValues.LifecycleDisposed
                && (excludeMaterialId <= 0 || item.Id != excludeMaterialId))
            .Select(item => new { item.ContainerKind, item.ContainerCode, item.StorageLocation })
            .ToListAsync();
        foreach (var row in miscRows)
        {
            string code = row.ContainerCode?.Trim() ?? string.Empty;
            string kind = row.ContainerKind?.Trim() ?? string.Empty;
            materials.Add(new LibraryMaterialRef(
                string.IsNullOrWhiteSpace(kind) ? $"杂项资料「{code}」" : $"杂项资料「{kind} {code}」",
                [code],
                NormalizeLocation(row.StorageLocation)));
        }

        var historyCodes = await _dbContext.HistoryArchiveBoxes
            .AsNoTracking()
            .Where(item => item.BoxCode != ""
                && item.LifecycleStatus != HistoryArchiveDisposalDomainValues.LifecycleDisposed)
            .Select(item => item.BoxCode)
            .ToListAsync();
        foreach (string boxCode in historyCodes)
        {
            string code = boxCode.Trim();
            materials.Add(new LibraryMaterialRef(
                $"历史档案盒「{code}」",
                [code],
                NormalizeLocation(code)));
        }

        var yearlyBoxes = await _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .Where(item => item.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                && (item.BoxLocationCode != "" || item.ArchiveSequenceNo != ""))
            .Select(item => new { item.BoxLocationCode, item.ArchiveSequenceNo })
            .ToListAsync();
        foreach (var box in yearlyBoxes)
        {
            string locationCode = box.BoxLocationCode?.Trim() ?? string.Empty;
            string sequenceNo = box.ArchiveSequenceNo?.Trim() ?? string.Empty;
            string label = string.IsNullOrWhiteSpace(locationCode) ? sequenceNo : locationCode;
            materials.Add(new LibraryMaterialRef(
                $"年度档案盒「{label}」",
                [locationCode, sequenceNo],
                NormalizeLocation(locationCode)));
        }

        return materials;
    }

    private static void EnsureContainerCodeAvailable(
        string containerKind,
        string containerCode,
        IReadOnlyList<LibraryMaterialRef> libraryMaterials)
    {
        string normalizedCode = containerCode.Trim();
        string codeAsLocation = NormalizeLocation(normalizedCode);
        LibraryMaterialRef? conflict = libraryMaterials.FirstOrDefault(item =>
            item.Codes.Any(code => !string.IsNullOrWhiteSpace(code)
                && string.Equals(code.Trim(), normalizedCode, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(codeAsLocation)
                && string.Equals(item.NormalizedLocation, codeAsLocation, StringComparison.OrdinalIgnoreCase)));
        if (conflict != null)
        {
            throw new InvalidOperationException(
                $"容器编号「{normalizedCode}」（{containerKind}）与库内{conflict.Display}冲突。");
        }
    }

    private static string NormalizeLocation(string? location) =>
        ArchiveSlotLocationSupport.NormalizeFullLocationCode(location);

    private sealed record LibraryMaterialRef(string Display, IReadOnlyList<string> Codes, string NormalizedLocation);

    private static bool Contains(HistoryMiscMaterial material, string summary, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return ContainsText(material.ContainerCode, keyword)
            || ContainsText(material.StorageLocation, keyword)
            || ContainsText(material.LastStorageLocation, keyword)
            || ContainsText(material.MajorCategory, keyword)
            || ContainsText(material.SubCategory, keyword)
            || ContainsText(material.ContainerKind, keyword)
            || ContainsText(material.Remark, keyword)
            || ContainsText(summary, keyword)
            || material.Lines.Any(line => ContainsText(line.LineText, keyword));
    }

    private static bool ContainsText(string? source, string keyword) =>
        !string.IsNullOrWhiteSpace(source)
        && source.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static string ResolveOperatorName(User user)
    {
        string name = string.IsNullOrWhiteSpace(user.RealName) ? user.LoginName : user.RealName;
        return name?.Trim() ?? string.Empty;
    }
}
