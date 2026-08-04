using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 借出申请从检索集登记拟领用资料（硬拷贝明细，不建立与检索集的引用关系）。
    /// </summary>
    public sealed partial class ArchiveOutboundService
    {
        public async Task<ArchiveOutboundFlowResult> AttachSearchResultSetAsync(int recordId, int resultSetId, User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的出库申请单。");
            }

            if (!CanEditApplicationItems(record, user))
            {
                return ArchiveOutboundFlowResult.Fail("当前状态不允许登记拟领用资料。");
            }

            bool isAdmin = IsArchiveAdminUser(user);
            var resultSet = await _searchService.GetSearchPoolAsync(resultSetId, user, isAdmin);
            if (resultSet == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的检索集。");
            }

            if (resultSet.Items.Count == 0)
            {
                return ArchiveOutboundFlowResult.Fail("检索集内没有可借出的明细。");
            }

            var currentLocations = await _searchService.GetCurrentStorageLocationsByFilingFactIdsAsync(
                resultSet.Items.Select(item => item.FilingFactId).Distinct().ToList());

            int sortOrder = record.Items.Count;
            int addedCount = 0;

            foreach (var poolItem in resultSet.Items.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
            {
                if (HasDuplicateOutboundItem(record.Items, poolItem))
                {
                    continue;
                }

                var fact = await _outboundRepository.GetFilingFactByIdAsync(poolItem.FilingFactId);
                currentLocations.TryGetValue(poolItem.FilingFactId, out string? currentLocation);
                var outboundItem = MapPoolItemToOutboundItem(poolItem, fact, currentLocation, sortOrder++, resultSetId);
                await EnrichOutboundItemFromFactAsync(outboundItem, fact);
                record.Items.Add(outboundItem);
                addedCount++;
            }

            if (addedCount == 0)
            {
                return ArchiveOutboundFlowResult.Fail("检索集明细均已存在于当前申请单，未新增条目。");
            }

            record.MaterialSummary = BuildMaterialSummaryFromOutboundItems(record.Items);
            record.UpdatedAt = DateTime.Now;

            await _outboundRepository.SaveOrUpdateRecordGraphAsync(record);
            return ArchiveOutboundFlowResult.Ok($"已从检索集「{resultSet.Name}」登记 {addedCount} 条拟领用资料。");
        }

        public async Task<ArchiveOutboundFlowResult> RemoveApplicationItemAsync(int recordId, int itemId, User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的出库申请单。");
            }

            if (!CanEditApplicationItems(record, user))
            {
                return ArchiveOutboundFlowResult.Fail("当前状态不允许撤销登记。");
            }

            var item = record.Items.FirstOrDefault(row => row.Id == itemId);
            if (item == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的资料明细。");
            }

            record.Items.Remove(item);
            for (int index = 0; index < record.Items.Count; index++)
            {
                record.Items[index].SortOrder = index;
            }

            record.MaterialSummary = record.Items.Count > 0
                ? BuildMaterialSummaryFromOutboundItems(record.Items)
                : string.Empty;
            record.UpdatedAt = DateTime.Now;

            await _outboundRepository.SaveOrUpdateRecordGraphAsync(record);
            return ArchiveOutboundFlowResult.Ok("已撤销登记。");
        }

        public async Task<ArchiveOutboundFlowResult> RemoveApplicationItemsAsync(
            int recordId,
            IReadOnlyCollection<int> itemIds,
            User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            if (itemIds == null || itemIds.Count == 0)
            {
                return ArchiveOutboundFlowResult.Fail("请指定要撤销登记的资料明细。");
            }

            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveOutboundFlowResult.Fail("未找到指定的出库申请单。");
            }

            if (!CanEditApplicationItems(record, user))
            {
                return ArchiveOutboundFlowResult.Fail("当前状态不允许撤销登记。");
            }

            var idSet = itemIds.Where(id => id > 0).ToHashSet();
            if (idSet.Count == 0)
            {
                return ArchiveOutboundFlowResult.Fail("请指定要撤销登记的资料明细。");
            }

            int removedCount = record.Items.RemoveAll(item => idSet.Contains(item.Id));
            if (removedCount == 0)
            {
                return ArchiveOutboundFlowResult.Fail("未找到可撤销登记的资料明细。");
            }

            for (int index = 0; index < record.Items.Count; index++)
            {
                record.Items[index].SortOrder = index;
            }

            record.MaterialSummary = record.Items.Count > 0
                ? BuildMaterialSummaryFromOutboundItems(record.Items)
                : string.Empty;
            record.UpdatedAt = DateTime.Now;

            await _outboundRepository.SaveOrUpdateRecordGraphAsync(record);
            return ArchiveOutboundFlowResult.Ok($"已撤销 {removedCount} 条登记资料。");
        }

        private bool CanEditApplicationItems(YearlyArchiveOutboundRecord record, User user)
        {
            if (!record.IsDraft)
            {
                return false;
            }

            return record.ApplicantUserId == user.Id || IsArchiveAdminUser(user);
        }

        private static bool HasDuplicateOutboundItem(
            IReadOnlyCollection<YearlyArchiveOutboundItem> items,
            YearlyArchiveSearchResultSetItem poolItem)
        {
            return items.Any(item =>
                item.FilingFactId == poolItem.FilingFactId
                && string.Equals(item.SelectionScopeKind, poolItem.SelectionScopeKind, StringComparison.Ordinal)
                && item.ContentEntryId == poolItem.ContentEntryId);
        }

        private static string BuildMaterialSummaryFromOutboundItems(IReadOnlyCollection<YearlyArchiveOutboundItem> items) =>
            ArchiveOutboundItemDescription.BuildMaterialSummary(items);

        private async Task EnrichOutboundItemFromFactAsync(YearlyArchiveOutboundItem item, YearlyArchiveFilingFact? fact)
        {
            if (fact == null)
            {
                return;
            }

            item.StorageCarrierType = fact.StorageCarrierType ?? string.Empty;
            await ApplyArchivePurposeFromFactAsync(item, fact);

            if (string.Equals(fact.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
            {
                item.MediaType = ResolveElectronicMediaTypeFromFilingFact(fact);
                item.CopyCount = 1;
                item.StockCopyCount = 1;
                item.UsageMode = ArchiveOutboundDomainValues.ResolveDefaultElectronicUsageMode(item.ArchivePurpose);
                if (item.UsageMode == ArchiveOutboundDomainValues.UsageModeDuplicate)
                {
                    item.NeedReturn = false;
                    ArchiveOutboundDomainValues.ApplyDuplicateMediumSelection(
                        item,
                        ArchiveOutboundDomainValues.DuplicateMediumSelfUsb);
                }
                else
                {
                    item.NeedReturn = true;
                }

                return;
            }

            if (fact.RegisterMediaId > 0)
            {
                item.MediaType = await _outboundRepository.GetRegisterMediaTypeAsync(fact.RegisterMediaId) ?? string.Empty;
                item.StockCopyCount = await _outboundRepository.GetRegisterMediaStockCopyCountAsync(fact.RegisterMediaId);
            }

            if (item.CopyCount is null or <= 0)
            {
                item.CopyCount = 1;
            }
        }

        private async Task ApplyArchivePurposeFromFactAsync(
            YearlyArchiveOutboundItem item,
            YearlyArchiveFilingFact fact)
        {
            if (fact.RegisterRecordId <= 0)
            {
                return;
            }

            var purposes = await _filingFactRepository.GetArchivePurposesByRegisterRecordIdsAsync(
                new[] { fact.RegisterRecordId });
            if (purposes.TryGetValue(fact.RegisterRecordId, out string? purpose))
            {
                item.ArchivePurpose = purpose;
            }
        }

        private async Task FillMissingOutboundItemArchivePurposesAsync(
            IReadOnlyCollection<YearlyArchiveOutboundItem> items)
        {
            var missingItems = items
                .Where(item => string.IsNullOrWhiteSpace(item.ArchivePurpose) && item.FilingFactId > 0)
                .ToList();
            if (missingItems.Count == 0)
            {
                return;
            }

            var factIds = missingItems.Select(item => item.FilingFactId).Distinct().ToList();
            var facts = new Dictionary<int, YearlyArchiveFilingFact>();
            foreach (int factId in factIds)
            {
                var fact = await _outboundRepository.GetFilingFactByIdAsync(factId);
                if (fact != null)
                {
                    facts[factId] = fact;
                }
            }

            var registerRecordIds = facts.Values
                .Select(fact => fact.RegisterRecordId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (registerRecordIds.Count == 0)
            {
                return;
            }

            var purposes = await _filingFactRepository.GetArchivePurposesByRegisterRecordIdsAsync(registerRecordIds);
            foreach (var item in missingItems)
            {
                if (!facts.TryGetValue(item.FilingFactId, out var fact))
                {
                    continue;
                }

                if (purposes.TryGetValue(fact.RegisterRecordId, out string? purpose))
                {
                    item.ArchivePurpose = purpose;
                }
            }
        }

        private static string ResolveElectronicMediaTypeFromFilingFact(YearlyArchiveFilingFact fact)
        {
            string normalized = ArchiveOutboundDomainValues.NormalizeElectronicStorageCarrierDisplay(fact.StorageCarrierType);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            return fact.StorageCarrierType?.Trim() ?? string.Empty;
        }

        private static YearlyArchiveOutboundItem MapPoolItemToOutboundItem(
            YearlyArchiveSearchResultSetItem poolItem,
            YearlyArchiveFilingFact? fact,
            string? currentLocation,
            int sortOrder,
            int? sourceResultSetId = null)
        {
            return new YearlyArchiveOutboundItem
            {
                SortOrder = sortOrder,
                FilingFactId = poolItem.FilingFactId,
                PrimaryFilingFactId = fact?.PrimaryFilingFactId,
                ArchiveCopyRole = fact?.ArchiveCopyRole ?? FilingFactArchiveCopyRole.Original,
                SourceResultSetItemId = poolItem.Id,
                SourceResultSetId = sourceResultSetId,
                ItemArchiveYear = fact?.FiledAt.Year,
                ItemProjectName = fact?.ProjectName ?? string.Empty,
                SelectionScopeKind = poolItem.SelectionScopeKind,
                ContentEntryId = poolItem.ContentEntryId,
                ContentEntryKind = poolItem.ContentEntryKind,
                ContentEntryName = poolItem.ContentEntryName,
                ContentEntryRelativePath = poolItem.ContentEntryRelativePath,
                FormNo = poolItem.FormNo,
                MaterialName = poolItem.MaterialName,
                ItemName = poolItem.ItemName,
                ContainerCode = poolItem.ContainerCode,
                StorageLocation = poolItem.StorageLocation,
                CurrentStorageLocation = currentLocation ?? poolItem.StorageLocation,
                ConfidentialLevel = fact?.ConfidentialLevel ?? ArchiveRegisterDomainValues.ConfidentialLevelNone,
                MediaKind = fact?.MediaKind ?? string.Empty,
                UsageMode = ArchiveOutboundDomainValues.UsageModeWithdrawal,
                NeedReturn = true,
                CopyCount = string.Equals(
                    fact?.MediaKind,
                    ArchiveRegisterDomainValues.MediaKindSimulated,
                    StringComparison.Ordinal)
                    && poolItem.RequestedCopyCount > 0
                    ? poolItem.RequestedCopyCount
                    : 1,
                StockCopyCount = 1,
                DataSizeMb = fact?.DataSizeMb,
                CreatedAt = DateTime.Now
            };
        }

        /// <summary>
        /// 为提档数据硬盘等明细回填硬盘编号展示快照，供查看/审批卡片展示。
        /// </summary>
        private async Task EnrichOutboundItemFiledHardDiskCodesAsync(
            IReadOnlyCollection<YearlyArchiveOutboundItem> items)
        {
            var candidates = items.Where(NeedsFiledHardDiskCodeLookup).ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var factIds = candidates.Select(item => item.FilingFactId).Where(id => id > 0).Distinct().ToList();
            if (factIds.Count == 0)
            {
                return;
            }

            var facts = await _outboundRepository.GetFilingFactsByIdsForUpdateAsync(factIds);
            var codesByFactId = new Dictionary<int, string>();

            foreach (var pair in facts)
            {
                var fact = pair.Value;
                var codes = new List<string>();
                AppendDistinctCode(codes, fact.MediumCode);

                if (fact.ContainerId > 0)
                {
                    var media = await _outboundRepository.GetHardDiskMediaByElectronicUnitIdForUpdateAsync(fact.ContainerId);
                    foreach (var medium in media)
                    {
                        AppendDistinctCode(codes, medium.DiskCode);
                    }

                    if (codes.Count == 0)
                    {
                        var unit = await _outboundRepository.GetElectronicArchiveUnitByIdForUpdateAsync(fact.ContainerId);
                        if (unit != null)
                        {
                            foreach (string code in SplitLinkedMediumCodes(unit.LinkedMediumCodes))
                            {
                                AppendDistinctCode(codes, code);
                            }
                        }
                    }
                }

                if (codes.Count > 0)
                {
                    codesByFactId[pair.Key] = string.Join("、", codes);
                }
            }

            foreach (var item in candidates)
            {
                if (codesByFactId.TryGetValue(item.FilingFactId, out string? codesText))
                {
                    item.FiledHardDiskCodes = codesText;
                }
            }
        }

        private static bool NeedsFiledHardDiskCodeLookup(YearlyArchiveOutboundItem item)
        {
            if (item.FilingFactId <= 0 || !string.IsNullOrWhiteSpace(item.RequisitionedDiskCode))
            {
                return false;
            }

            if (!string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
            {
                return false;
            }

            return ArchiveOutboundDomainValues.IsHardDiskStorageCarrier(item.StorageCarrierType)
                || item.MediaType?.Contains("硬盘", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static IEnumerable<string> SplitLinkedMediumCodes(string? linkedMediumCodes)
        {
            if (string.IsNullOrWhiteSpace(linkedMediumCodes))
            {
                yield break;
            }

            foreach (string part in linkedMediumCodes.Split(
                         ['、', ',', ';', '；', '|', '/', ' '],
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return part;
            }
        }

        private static void AppendDistinctCode(List<string> target, string? value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            if (target.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            target.Add(normalized);
        }
    }
}
