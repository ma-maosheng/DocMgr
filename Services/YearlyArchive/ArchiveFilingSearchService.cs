using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    public sealed class ArchiveFilingSearchService : IArchiveFilingSearchService
    {
        private readonly IArchiveFilingFactRepository _filingFactRepository;
        private readonly IArchiveFilingRepository _archiveFilingRepository;
        private readonly IArchiveOutboundRepository _outboundRepository;

        public ArchiveFilingSearchService(
            IArchiveFilingFactRepository filingFactRepository,
            IArchiveFilingRepository archiveFilingRepository,
            IArchiveOutboundRepository outboundRepository)
        {
            _filingFactRepository = filingFactRepository;
            _archiveFilingRepository = archiveFilingRepository;
            _outboundRepository = outboundRepository;
        }

        public async Task<List<FiledArchiveSearchHit>> SearchByRegisterAsync(
            string mediaKind,
            RegisterDirectionSearchCriteria criteria)
        {
            var facts = await _filingFactRepository.SearchByRegisterCriteriaAsync(mediaKind, criteria);
            var stockCountsByRegisterMediaId = await LoadRegisterMediaStockCountsAsync(facts);
            var supplementsByMediaItemId = await LoadRegisterSupplementsByMediaItemIdAsync(facts);
            var archivePurposeByRegisterRecordId = await LoadArchivePurposeByRegisterRecordIdAsync(facts);

            if (!ContentEntrySearchSupport.HasActiveFilter(criteria))
            {
                return facts
                    .Select(fact => MapHit(
                        fact,
                        stockCountsByRegisterMediaId,
                        supplementsByMediaItemId: supplementsByMediaItemId,
                        archivePurposeByRegisterRecordId: archivePurposeByRegisterRecordId))
                    .ToList();
            }

            var mediaItemIds = facts
                .Select(fact => fact.MediaItemId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var entries = await _filingFactRepository.GetElectronicContentEntriesByMediaItemIdsAsync(mediaItemIds);

            return facts
                .Select(fact =>
                {
                    var matchedEntries = entries
                        .Where(entry => entry.ElectronicMediaItemDetailId == fact.MediaItemId
                            && ContentEntrySearchSupport.MatchesEntry(entry, criteria))
                        .Select(entry => ContentEntrySearchSupport.ToMatchedInfo(entry, fact.FilingStoragePath))
                        .ToList() as IReadOnlyList<MatchedContentEntryInfo>;

                    return MapHit(
                        fact,
                        stockCountsByRegisterMediaId,
                        criteria,
                        matchedEntries ?? Array.Empty<MatchedContentEntryInfo>(),
                        supplementsByMediaItemId,
                        archivePurposeByRegisterRecordId);
                })
                .ToList();
        }

        public async Task<List<FiledArchiveSearchGroupHit>> SearchByRegisterGroupedAsync(
            string mediaKind,
            RegisterDirectionSearchCriteria criteria)
        {
            var matchedHits = await SearchByRegisterAsync(mediaKind, criteria);
            if (matchedHits.Count == 0)
            {
                return [];
            }

            var rootIds = matchedHits
                .Select(hit => hit.PrimaryFilingFactId ?? hit.FilingFactId)
                .Distinct()
                .ToList();

            var primaryFacts = await _filingFactRepository.GetFactsByIdsAsync(rootIds);
            var backupFacts = await _filingFactRepository.GetBackupFactsByPrimaryIdsAsync(rootIds);
            var stockCountsByRegisterMediaId = await LoadRegisterMediaStockCountsAsync(
                primaryFacts.Concat(backupFacts));
            var supplementsByMediaItemId = await LoadRegisterSupplementsByMediaItemIdAsync(
                primaryFacts.Concat(backupFacts));
            var archivePurposeByRegisterRecordId = await LoadArchivePurposeByRegisterRecordIdAsync(
                primaryFacts.Concat(backupFacts));

            return FiledArchiveSearchGroupingSupport.GroupRegisterHits(
                matchedHits,
                primaryFacts,
                backupFacts,
                fact => MapHit(
                    fact,
                    stockCountsByRegisterMediaId,
                    criteria,
                    supplementsByMediaItemId: supplementsByMediaItemId,
                    archivePurposeByRegisterRecordId: archivePurposeByRegisterRecordId));
        }

        public async Task<List<FiledArchiveSearchBoxGroupHit>> SearchByRegisterGroupedByArchiveBoxAsync(
            string mediaKind,
            RegisterDirectionSearchCriteria criteria)
        {
            if (!string.Equals(
                    mediaKind,
                    ArchiveRegisterDomainValues.MediaKindSimulated,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("仅模拟介质检索支持按档案盒归组。");
            }

            var itemGroups = await SearchByRegisterGroupedAsync(mediaKind, criteria);
            if (itemGroups.Count == 0)
            {
                return [];
            }

            var boxKeys = itemGroups
                .Select(group => FiledArchiveSearchGroupingSupport.ResolveArchiveBoxKey(group.PrimaryHit))
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var boxes = await _archiveFilingRepository.GetArchiveBoxesBySequenceNosAsync(boxKeys);
            var boxesBySequenceNo = boxes.ToDictionary(
                box => box.ArchiveSequenceNo.Trim(),
                StringComparer.Ordinal);

            return FiledArchiveSearchGroupingSupport.GroupRegisterHitsByArchiveBox(itemGroups, boxesBySequenceNo);
        }

        public async Task<List<FiledArchiveSearchHit>> SearchByContainerAsync(
            string mediaKind,
            ContainerDirectionSearchCriteria criteria)
        {
            var facts = await _filingFactRepository.SearchByContainerCriteriaAsync(mediaKind, criteria);
            var stockCountsByRegisterMediaId = await LoadRegisterMediaStockCountsAsync(facts);
            var supplementsByMediaItemId = await LoadRegisterSupplementsByMediaItemIdAsync(facts);
            var archivePurposeByRegisterRecordId = await LoadArchivePurposeByRegisterRecordIdAsync(facts);
            return facts
                .Select(fact => MapHit(
                    fact,
                    stockCountsByRegisterMediaId,
                    supplementsByMediaItemId: supplementsByMediaItemId,
                    archivePurposeByRegisterRecordId: archivePurposeByRegisterRecordId))
                .ToList();
        }

        public async Task<SearchResultSetSaveResult> SaveResultSetAsync(
            SaveArchiveSearchResultSetRequest request,
            User currentUser,
            bool isArchiveAdmin)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(currentUser);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new InvalidOperationException("请填写结果集名称。");
            }

            if (request.Selections == null || request.Selections.Count == 0)
            {
                throw new InvalidOperationException("筛选池为空，无法保存。");
            }

            var autoRemovedNames = await EnforceUserResultSetLimitAsync(currentUser.Id, request.MediaKind);

            var factIds = request.Selections.Select(item => item.FilingFactId).Distinct().ToList();
            var facts = await _filingFactRepository.GetFactsByIdsAsync(factIds);
            if (facts.Count == 0)
            {
                throw new InvalidOperationException("未找到可保存的立档记录。");
            }

            var factById = facts.ToDictionary(fact => fact.Id);

            if (string.Equals(
                    request.MediaKind,
                    ArchiveRegisterDomainValues.MediaKindSimulated,
                    StringComparison.Ordinal))
            {
                var copyCountErrors = new List<string>();
                foreach (var selection in request.Selections.Where(item => item.IsWholeMediaItem))
                {
                    if (!factById.TryGetValue(selection.FilingFactId, out var fact))
                    {
                        continue;
                    }

                    string? error = ArchiveSearchPoolCopyCountSupport.ValidateSimulatedRequestedCopyCount(
                        selection.RequestedCopyCount,
                        fact.ContentCount,
                        fact.ItemName);
                    if (error != null)
                    {
                        copyCountErrors.Add(error);
                    }
                }

                if (copyCountErrors.Count > 0)
                {
                    throw new InvalidOperationException(
                        "筛选池份数校验未通过：" + Environment.NewLine + string.Join(Environment.NewLine, copyCountErrors));
                }
            }

            var contentEntryIds = request.Selections
                .Where(item => item.IsContentEntry)
                .Select(item => item.ContentEntryId!.Value)
                .Distinct()
                .ToList();

            var contentEntries = contentEntryIds.Count == 0
                ? new Dictionary<int, YearlyArchiveRegisterElectronicMediaItemEntry>()
                : (await _filingFactRepository.GetElectronicContentEntriesByIdsAsync(contentEntryIds))
                    .ToDictionary(entry => entry.Id);

            int year = DateTime.Now.Year;
            string prefix = $"检索集-{request.MediaKind}-{year}-";
            string? lastNo = await _filingFactRepository.GetLastResultSetNoByPrefixAsync(prefix);
            int nextSequence = 1;
            if (!string.IsNullOrWhiteSpace(lastNo) && lastNo.Length > prefix.Length
                && int.TryParse(lastNo[prefix.Length..], out int parsed) && parsed > 0)
            {
                nextSequence = parsed + 1;
            }

            DateTime now = DateTime.Now;
            var resultSet = new YearlyArchiveSearchResultSet
            {
                ResultSetNo = $"{prefix}{nextSequence:D4}",
                Name = request.Name.Trim(),
                MediaKind = request.MediaKind,
                Status = ArchiveSearchResultSetStatus.Confirmed,
                CreatedByUserId = currentUser.Id,
                CreatedByName = string.IsNullOrWhiteSpace(currentUser.RealName)
                    ? currentUser.LoginName
                    : currentUser.RealName,
                CreatedAt = now,
                UpdatedAt = now,
                Remarks = request.Remarks?.Trim() ?? string.Empty
            };

            int order = 0;
            foreach (var selection in request.Selections
                         .OrderBy(item => factById.TryGetValue(item.FilingFactId, out var fact) ? fact.FormNo : string.Empty)
                         .ThenBy(item => factById.TryGetValue(item.FilingFactId, out var fact) ? fact.ItemName : string.Empty)
                         .ThenBy(item => item.ContentEntryId))
            {
                if (!factById.TryGetValue(selection.FilingFactId, out var fact))
                {
                    continue;
                }

                YearlyArchiveRegisterElectronicMediaItemEntry? scopedEntry = null;
                if (selection.IsContentEntry)
                {
                    if (!contentEntries.TryGetValue(selection.ContentEntryId!.Value, out scopedEntry))
                    {
                        throw new InvalidOperationException(
                            $"目录/文件条目 [{selection.ContentEntryId}] 不存在，无法保存。");
                    }

                    if (scopedEntry.ElectronicMediaItemDetailId != fact.MediaItemId)
                    {
                        throw new InvalidOperationException(
                            $"目录/文件条目 [{scopedEntry.EntryName}] 与立档子项不匹配，无法保存。");
                    }
                }

                resultSet.Items.Add(new YearlyArchiveSearchResultSetItem
                {
                    FilingFactId = fact.Id,
                    SelectionScopeKind = selection.IsContentEntry
                        ? ArchiveSearchSelectionScopeKind.ContentEntry
                        : ArchiveSearchSelectionScopeKind.WholeMediaItem,
                    ContentEntryId = selection.IsContentEntry ? selection.ContentEntryId : null,
                    ContentEntryKind = scopedEntry?.EntryKind?.Trim() ?? string.Empty,
                    ContentEntryName = scopedEntry?.EntryName?.Trim() ?? string.Empty,
                    ContentEntryRelativePath = scopedEntry?.RelativePath?.Trim() ?? string.Empty,
                    SortOrder = order++,
                    FormNo = fact.FormNo,
                    MaterialName = fact.MaterialName,
                    ItemName = fact.ItemName,
                    ContainerCode = fact.ContainerCode,
                    StorageLocation = fact.StorageLocation,
                    LifecycleStatus = fact.LifecycleStatus,
                    BorrowHintLevel = fact.BorrowHintLevel,
                    BorrowHintText = fact.BorrowHintText,
                    RequestedCopyCount = string.Equals(
                        request.MediaKind,
                        ArchiveRegisterDomainValues.MediaKindSimulated,
                        StringComparison.Ordinal)
                        && selection.IsWholeMediaItem
                        ? Math.Max(1, selection.RequestedCopyCount)
                        : 1,
                    AddedAt = now
                });
            }

            _filingFactRepository.AddResultSet(resultSet);
            await _filingFactRepository.SaveChangesAsync();
            return new SearchResultSetSaveResult
            {
                ResultSet = resultSet,
                AutoRemovedResultSetNames = autoRemovedNames
            };
        }

        private async Task<List<string>> EnforceUserResultSetLimitAsync(int userId, string mediaKind)
        {
            int existingCount = await _filingFactRepository.CountUserResultSetsByMediaKindAsync(userId, mediaKind);
            int needRemove = Math.Max(0, existingCount - SearchPoolLimits.MaxResultSetsPerUserPerMediaKind + 1);
            if (needRemove <= 0)
            {
                return new List<string>();
            }

            var oldestSets = await _filingFactRepository.GetOldestUserResultSetsByMediaKindAsync(
                userId,
                mediaKind,
                needRemove);

            var removedNames = new List<string>();
            foreach (var resultSet in oldestSets)
            {
                removedNames.Add(resultSet.Name);
                if (!await _filingFactRepository.DeleteResultSetAsync(resultSet.Id))
                {
                    throw new InvalidOperationException($"自动清理最早结果集失败：{resultSet.Name}");
                }
            }

            if (removedNames.Count > 0)
            {
                await _filingFactRepository.SaveResultSetChangesAsync();
            }

            return removedNames;
        }

        public Task<List<SearchPoolListItem>> ListSearchPoolsAsync(
            SearchPoolListCriteria criteria,
            User currentUser,
            bool isArchiveAdmin)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            ArgumentNullException.ThrowIfNull(currentUser);

            if (string.IsNullOrWhiteSpace(criteria.MediaKind))
            {
                throw new InvalidOperationException("未指定介质类型。");
            }

            return _filingFactRepository.SearchResultSetsAsync(
                criteria.MediaKind,
                criteria,
                currentUser.Id,
                isArchiveAdmin);
        }

        public async Task<YearlyArchiveSearchResultSet?> GetSearchPoolAsync(
            int resultSetId,
            User currentUser,
            bool isArchiveAdmin)
        {
            ArgumentNullException.ThrowIfNull(currentUser);

            var resultSet = await _filingFactRepository.GetResultSetWithItemsAsync(resultSetId);
            if (resultSet == null)
            {
                return null;
            }

            EnsureCanAccessResultSet(resultSet, currentUser, isArchiveAdmin);
            return resultSet;
        }

        public async Task<YearlyArchiveSearchResultSet> UpdateSearchPoolAsync(
            UpdateSearchPoolRequest request,
            User currentUser,
            bool isArchiveAdmin)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(currentUser);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new InvalidOperationException("请填写检索池名称。");
            }

            var resultSet = await _filingFactRepository.GetResultSetWithItemsAsync(request.ResultSetId);
            if (resultSet == null)
            {
                throw new InvalidOperationException("未找到指定的检索池。");
            }

            EnsureCanAccessResultSet(resultSet, currentUser, isArchiveAdmin);

            var remainingIds = request.RemainingResultSetItemIds?.ToHashSet() ?? new HashSet<int>();
            var itemsToRemove = resultSet.Items.Where(item => !remainingIds.Contains(item.Id)).ToList();
            foreach (var item in itemsToRemove)
            {
                resultSet.Items.Remove(item);
            }

            if (resultSet.Items.Count == 0)
            {
                throw new InvalidOperationException("检索池至少应保留一条立档记录。");
            }

            resultSet.Name = request.Name.Trim();
            resultSet.Remarks = request.Remarks?.Trim() ?? string.Empty;
            resultSet.Status = NormalizeResultSetStatus(request.Status);
            resultSet.UpdatedAt = DateTime.Now;

            int order = 0;
            foreach (var item in resultSet.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            {
                item.SortOrder = order++;
            }

            await _filingFactRepository.SaveResultSetChangesAsync();
            return resultSet;
        }

        public async Task DeleteSearchPoolAsync(
            int resultSetId,
            User currentUser,
            bool isArchiveAdmin)
        {
            ArgumentNullException.ThrowIfNull(currentUser);

            var resultSet = await _filingFactRepository.GetResultSetWithItemsAsync(resultSetId);
            if (resultSet == null)
            {
                throw new InvalidOperationException("未找到指定的检索池。");
            }

            EnsureCanAccessResultSet(resultSet, currentUser, isArchiveAdmin);

            if (!await _filingFactRepository.DeleteResultSetAsync(resultSetId))
            {
                throw new InvalidOperationException("删除检索池失败。");
            }

            await _filingFactRepository.SaveResultSetChangesAsync();
        }

        public async Task<IReadOnlyList<MatchedContentEntryInfo>> GetContentEntriesByMediaItemIdAsync(
            int mediaItemId,
            string? filingStoragePath = null)
        {
            if (mediaItemId <= 0)
            {
                return Array.Empty<MatchedContentEntryInfo>();
            }

            var entries = await _filingFactRepository.GetElectronicContentEntriesByMediaItemIdsAsync(
                new[] { mediaItemId });

            return entries
                .Select(entry => ContentEntrySearchSupport.ToMatchedInfo(entry, filingStoragePath))
                .ToList();
        }

        public async Task<FiledArchiveSearchHit?> GetSearchHitByFilingFactIdAsync(int filingFactId)
        {
            if (filingFactId <= 0)
            {
                return null;
            }

            var facts = await _filingFactRepository.GetFactsByIdsAsync(new[] { filingFactId });
            if (facts.Count == 0)
            {
                return null;
            }

            var stockCountsByRegisterMediaId = await LoadRegisterMediaStockCountsAsync(facts);
            var supplementsByMediaItemId = await LoadRegisterSupplementsByMediaItemIdAsync(facts);
            var archivePurposeByRegisterRecordId = await LoadArchivePurposeByRegisterRecordIdAsync(facts);
            return MapHit(
                facts[0],
                stockCountsByRegisterMediaId,
                supplementsByMediaItemId: supplementsByMediaItemId,
                archivePurposeByRegisterRecordId: archivePurposeByRegisterRecordId);
        }

        public async Task<IReadOnlyDictionary<int, string>> GetStockCopyCountDisplaysByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds)
        {
            if (filingFactIds == null || filingFactIds.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var facts = await _filingFactRepository.GetFactsByIdsAsync(filingFactIds);
            var stockCountsByRegisterMediaId = await LoadRegisterMediaStockCountsAsync(facts);

            return facts.ToDictionary(
                fact => fact.Id,
                fact => ArchiveStockCopyCountSupport.FormatDisplay(
                    fact.MediaKind,
                    ResolveStockCopyCount(fact, stockCountsByRegisterMediaId)));
        }

        public async Task<IReadOnlyDictionary<int, string>> GetCurrentStorageLocationsByFilingFactIdsAsync(
            IReadOnlyList<int> filingFactIds)
        {
            if (filingFactIds == null || filingFactIds.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var facts = await _filingFactRepository.GetFactsByIdsAsync(filingFactIds);
            var ledgerLocations = await LoadElectronicMediumLedgerLocationsAsync(facts);

            return facts.ToDictionary(
                fact => fact.Id,
                fact => ledgerLocations.TryGetValue(fact.Id, out string? ledgerLocation)
                    ? FiledElectronicArchiveLocationSupport.ResolveCurrentDisplayLocation(fact, ledgerLocation)
                    : string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                        ? fact.StorageLocation
                        : fact.CurrentStorageLocation);
        }

        private async Task<IReadOnlyDictionary<int, string>> LoadElectronicMediumLedgerLocationsAsync(
            IReadOnlyList<YearlyArchiveFilingFact> facts)
        {
            var result = new Dictionary<int, string>();
            foreach (var fact in facts)
            {
                if (!string.Equals(fact.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
                {
                    continue;
                }

                string? ledgerLocation = null;
                if (ArchiveFilingBusinessRules.IsOpticalDiscArchiveCarrierType(fact.StorageCarrierType))
                {
                    ledgerLocation = await TryLoadOpticalDiscLedgerLocationAsync(fact);
                }
                else if (ArchiveFilingBusinessRules.IsHardDiskArchiveCarrierType(fact.StorageCarrierType))
                {
                    ledgerLocation = await TryLoadHardDiskLedgerLocationAsync(fact);
                }

                if (!string.IsNullOrWhiteSpace(ledgerLocation))
                {
                    result[fact.Id] = ledgerLocation.Trim();
                }
            }

            return result;
        }

        private async Task<string?> TryLoadOpticalDiscLedgerLocationAsync(YearlyArchiveFilingFact fact)
        {
            if (fact.ContainerId > 0)
            {
                var discs = await _outboundRepository.GetOpticalDiscMediaByElectronicUnitIdForUpdateAsync(fact.ContainerId);
                string? location = discs
                    .Select(disc => disc.Ledger?.StorageLocation?.Trim())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                if (!string.IsNullOrWhiteSpace(location))
                {
                    return location;
                }
            }

            if (!string.IsNullOrWhiteSpace(fact.MediumCode))
            {
                var disc = await _outboundRepository.GetOpticalDiscMediumByCodeForUpdateAsync(fact.MediumCode);
                if (!string.IsNullOrWhiteSpace(disc?.Ledger?.StorageLocation))
                {
                    return disc.Ledger.StorageLocation.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(fact.ContainerCode))
            {
                var disc = await _outboundRepository.GetOpticalDiscMediumByCodeForUpdateAsync(fact.ContainerCode);
                if (!string.IsNullOrWhiteSpace(disc?.Ledger?.StorageLocation))
                {
                    return disc.Ledger.StorageLocation.Trim();
                }
            }

            return null;
        }

        private async Task<string?> TryLoadHardDiskLedgerLocationAsync(YearlyArchiveFilingFact fact)
        {
            if (fact.ContainerId > 0)
            {
                var media = await _outboundRepository.GetHardDiskMediaByElectronicUnitIdForUpdateAsync(fact.ContainerId);
                string? location = media
                    .Select(medium => medium.Ledger?.StorageLocation?.Trim())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                if (!string.IsNullOrWhiteSpace(location))
                {
                    return location;
                }
            }

            if (!string.IsNullOrWhiteSpace(fact.MediumCode))
            {
                var medium = await _outboundRepository.GetHardDiskMediumByCodeForUpdateAsync(fact.MediumCode);
                if (!string.IsNullOrWhiteSpace(medium?.Ledger?.StorageLocation))
                {
                    return medium.Ledger.StorageLocation.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(fact.ContainerCode))
            {
                var medium = await _outboundRepository.GetHardDiskMediumByCodeForUpdateAsync(fact.ContainerCode);
                if (!string.IsNullOrWhiteSpace(medium?.Ledger?.StorageLocation))
                {
                    return medium.Ledger.StorageLocation.Trim();
                }
            }

            return null;
        }

        public async Task<IReadOnlyDictionary<int, SimulatedInArchiveCopyCountInfo>> GetSimulatedInArchiveCopyCountInfoByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds)
        {
            if (filingFactIds == null || filingFactIds.Count == 0)
            {
                return new Dictionary<int, SimulatedInArchiveCopyCountInfo>();
            }

            var facts = await _filingFactRepository.GetFactsByIdsAsync(filingFactIds);
            var simulatedFacts = facts
                .Where(fact => string.Equals(
                    fact.MediaKind,
                    ArchiveRegisterDomainValues.MediaKindSimulated,
                    StringComparison.Ordinal))
                .ToList();

            if (simulatedFacts.Count == 0)
            {
                return new Dictionary<int, SimulatedInArchiveCopyCountInfo>();
            }

            var withdrawnCopyCounts = await _outboundRepository
                .GetCompletedOutstandingWithdrawalCopyCountsByFilingFactIdsAsync(
                    simulatedFacts.Select(fact => fact.Id).ToList());

            return simulatedFacts.ToDictionary(
                fact => fact.Id,
                fact =>
                {
                    int filedCopyCount = SimulatedInArchiveCopyCountSupport.ResolveFiledCopyCount(fact.ContentCount);
                    int withdrawnCopyCount = withdrawnCopyCounts.GetValueOrDefault(fact.Id);
                    int currentInArchiveCopyCount = SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                        filedCopyCount,
                        withdrawnCopyCount);

                    return new SimulatedInArchiveCopyCountInfo
                    {
                        FiledCopyCount = filedCopyCount,
                        WithdrawnCopyCount = withdrawnCopyCount,
                        CurrentInArchiveCopyCount = currentInArchiveCopyCount,
                        Display = SimulatedInArchiveCopyCountSupport.FormatDisplay(filedCopyCount, withdrawnCopyCount)
                    };
                });
        }

        private static void EnsureCanAccessResultSet(
            YearlyArchiveSearchResultSet resultSet,
            User currentUser,
            bool isArchiveAdmin)
        {
            if (isArchiveAdmin || resultSet.CreatedByUserId == currentUser.Id)
            {
                return;
            }

            throw new InvalidOperationException("无权访问该检索池。");
        }

        private static string NormalizeResultSetStatus(string? status)
        {
            return status switch
            {
                ArchiveSearchResultSetStatus.Draft => ArchiveSearchResultSetStatus.Draft,
                ArchiveSearchResultSetStatus.Confirmed => ArchiveSearchResultSetStatus.Confirmed,
                ArchiveSearchResultSetStatus.Referenced => ArchiveSearchResultSetStatus.Referenced,
                _ => ArchiveSearchResultSetStatus.Confirmed
            };
        }

        private static FiledArchiveSearchHit MapHit(
            YearlyArchiveFilingFact fact,
            IReadOnlyDictionary<int, int> stockCountsByRegisterMediaId,
            RegisterDirectionSearchCriteria? contentSearchCriteria = null,
            IReadOnlyList<MatchedContentEntryInfo>? matchedContentEntries = null,
            IReadOnlyDictionary<int, RegisterSearchSupplement>? supplementsByMediaItemId = null,
            IReadOnlyDictionary<int, string>? archivePurposeByRegisterRecordId = null)
        {
            int stockCopyCount = ResolveStockCopyCount(fact, stockCountsByRegisterMediaId);
            var supplement = fact.MediaItemId > 0
                && supplementsByMediaItemId != null
                && supplementsByMediaItemId.TryGetValue(fact.MediaItemId, out var resolvedSupplement)
                ? resolvedSupplement
                : RegisterSearchSupplement.Empty;
            string archivePurpose = fact.RegisterRecordId > 0
                && archivePurposeByRegisterRecordId != null
                && archivePurposeByRegisterRecordId.TryGetValue(fact.RegisterRecordId, out string? purpose)
                ? purpose
                : string.Empty;

            return new FiledArchiveSearchHit
            {
                FilingFactId = fact.Id,
                FilingFactNo = fact.FilingFactNo,
                MediaKind = fact.MediaKind,
                RegisterRecordId = fact.RegisterRecordId,
                RegisterMediaId = fact.RegisterMediaId,
                MediaItemId = fact.MediaItemId,
                FormNo = fact.FormNo,
                MaterialName = fact.MaterialName,
                ProjectName = fact.ProjectName,
                ProvideUnit = fact.ProvideUnit,
                ApplicantName = fact.ApplicantName,
                ItemType = fact.ItemType,
                ItemName = fact.ItemName,
                ConfidentialLevel = fact.ConfidentialLevel,
                ContentCount = fact.ContentCount,
                ContainerKind = fact.ContainerKind,
                ContainerCode = fact.ContainerCode,
                StorageLocation = fact.StorageLocation,
                StorageCarrierType = fact.StorageCarrierType,
                MediumCode = fact.MediumCode,
                FilingStoragePath = fact.FilingStoragePath,
                FiledAt = fact.FiledAt,
                FiledBy = fact.FiledBy,
                LifecycleStatus = fact.LifecycleStatus,
                CurrentContainerCode = string.IsNullOrWhiteSpace(fact.CurrentContainerCode)
                    ? fact.ContainerCode
                    : fact.CurrentContainerCode,
                CurrentStorageLocation = string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                    ? fact.StorageLocation
                    : fact.CurrentStorageLocation,
                BorrowHintLevel = fact.BorrowHintLevel,
                BorrowHintText = fact.BorrowHintText,
                PrimaryFilingFactId = fact.PrimaryFilingFactId,
                ArchiveCopyRole = string.IsNullOrWhiteSpace(fact.ArchiveCopyRole)
                    ? FilingFactArchiveCopyRole.Original
                    : fact.ArchiveCopyRole,
                StockCopyCount = stockCopyCount,
                StockCopyCountDisplay = ArchiveStockCopyCountSupport.FormatDisplay(fact.MediaKind, stockCopyCount),
                RegisterMediaType = supplement.RegisterMediaType,
                MaterialCategory = supplement.MaterialCategory,
                SubCategory = supplement.SubCategory,
                DataOrganizationForm = supplement.DataOrganizationForm,
                ArchivePurpose = archivePurpose,
                ContentSearchKeyword = contentSearchCriteria?.ContentEntryKeyword?.Trim() ?? string.Empty,
                ContentSearchKindFilter = contentSearchCriteria?.ContentEntryKindFilter?.Trim() ?? string.Empty,
                MatchedContentEntries = matchedContentEntries ?? Array.Empty<MatchedContentEntryInfo>()
            };
        }

        private async Task<IReadOnlyDictionary<int, RegisterSearchSupplement>> LoadRegisterSupplementsByMediaItemIdAsync(
            IEnumerable<YearlyArchiveFilingFact> facts)
        {
            var mediaItemIds = facts
                .Select(fact => fact.MediaItemId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (mediaItemIds.Count == 0)
            {
                return new Dictionary<int, RegisterSearchSupplement>();
            }

            var registerMediaIds = facts
                .Select(fact => fact.RegisterMediaId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var mediaItems = await _filingFactRepository.GetRegisterMediaItemsWithSupplementsAsync(mediaItemIds);
            var registerMedias = await _filingFactRepository.GetRegisterMediasByIdsAsync(registerMediaIds);
            var mediaTypeByRegisterMediaId = registerMedias.ToDictionary(
                media => media.Id,
                media => media.MediaType?.Trim() ?? string.Empty);

            return mediaItems.ToDictionary(
                item => item.Id,
                item => BuildRegisterSearchSupplement(item, mediaTypeByRegisterMediaId));
        }

        private async Task<IReadOnlyDictionary<int, string>> LoadArchivePurposeByRegisterRecordIdAsync(
            IEnumerable<YearlyArchiveFilingFact> facts)
        {
            var registerRecordIds = facts
                .Select(fact => fact.RegisterRecordId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            return await _filingFactRepository.GetArchivePurposesByRegisterRecordIdsAsync(registerRecordIds);
        }

        private static RegisterSearchSupplement BuildRegisterSearchSupplement(
            YearlyArchiveRegisterMediaItem mediaItem,
            IReadOnlyDictionary<int, string> mediaTypeByRegisterMediaId)
        {
            var detail = mediaItem.ElectronicDetail;
            string registerMediaType = !string.IsNullOrWhiteSpace(mediaItem.MediaEntry?.MediaType)
                ? mediaItem.MediaEntry.MediaType.Trim()
                : mediaTypeByRegisterMediaId.TryGetValue(mediaItem.YearlyArchiveRegisterMediaId, out string? mediaType)
                    ? mediaType
                    : string.Empty;

            return new RegisterSearchSupplement(
                registerMediaType,
                detail?.MaterialCategory?.Trim() ?? string.Empty,
                detail?.SubCategory?.Trim() ?? string.Empty,
                detail?.DataOrganizationForm?.Trim() ?? string.Empty);
        }

        private readonly record struct RegisterSearchSupplement(
            string RegisterMediaType,
            string MaterialCategory,
            string SubCategory,
            string DataOrganizationForm)
        {
            public static RegisterSearchSupplement Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);
        }

        private async Task<IReadOnlyDictionary<int, int>> LoadRegisterMediaStockCountsAsync(
            IEnumerable<YearlyArchiveFilingFact> facts)
        {
            var registerMediaIds = facts
                .Select(fact => fact.RegisterMediaId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            return await _filingFactRepository.GetRegisterMediaStockCountsByIdsAsync(registerMediaIds);
        }

        private static int ResolveStockCopyCount(
            YearlyArchiveFilingFact fact,
            IReadOnlyDictionary<int, int> stockCountsByRegisterMediaId)
        {
            int registerMediaCount = fact.RegisterMediaId > 0
                && stockCountsByRegisterMediaId.TryGetValue(fact.RegisterMediaId, out int count)
                ? count
                : 0;

            return ArchiveStockCopyCountSupport.ResolveStockCopyCount(fact.MediaKind, registerMediaCount);
        }
    }
}
