using DocMgr.Models.Cabinets;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 历史存档资料（地形图/航片/其他图件）档口迁移：交互式与整档口批量。
    /// 历史资料无独立盒实体，位置以台账行 BoxNumber（四段盒号）表达；
    /// 迁移即改写盒号并同步统一摆放登记表。
    /// </summary>
    public sealed partial class ArchiveRelocationService
    {
        /// <summary>迁档明细 SourceLinkType 前缀：HistoryArchive:TopoMap 等。</summary>
        private const string HistorySourceLinkTypePrefix = "HistoryArchive";

        private static string BuildHistorySourceLinkType(string materialKind)
            => $"{HistorySourceLinkTypePrefix}:{materialKind}";

        /// <summary>历史资料整档口批量搬迁预览。</summary>
        public Task<ArchiveRelocationPreview> PreviewBatchHistorySlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            ArgumentNullException.ThrowIfNull(request);
            return BuildBatchHistorySlotPreviewAsync(request);
        }

        /// <summary>历史资料整档口批量搬迁执行。</summary>
        public async Task<ArchiveRelocationResult> ExecuteBatchHistorySlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            ArgumentNullException.ThrowIfNull(request);
            var preview = await BuildBatchHistorySlotPreviewAsync(request);
            if (!preview.CanExecute)
            {
                return ArchiveRelocationResult.Fail(preview.BlockReason);
            }

            return await ExecuteBatchHistorySlotPhysicalMoveCoreAsync(request);
        }

        #region 交互式迁移

        private async Task<ArchiveRelocationPreview> BuildInteractiveHistoryPreviewAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var boxCodes = NormalizeHistoryBoxCodes(request.SourceHistoryBoxCodes);
            if (boxCodes.Count == 0)
            {
                return Blocked("未指定源历史资料盒号。");
            }

            var groups = await _relocationRepository.GetHistoryLedgerReferencesByBoxCodesAsync(boxCodes);
            string? issue = await ValidateHistoryMoveSourcesAsync(boxCodes, groups, requireSingleSlot: true);
            if (!string.IsNullOrWhiteSpace(issue))
            {
                return Blocked(issue);
            }

            issue = await ValidateHistoryTargetSlotAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn,
                boxCodes);
            if (!string.IsNullOrWhiteSpace(issue))
            {
                return Blocked(issue);
            }

            string sourceSlotKey = ResolveHistorySlotKeyOfBoxCode(boxCodes[0]);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName, request.TargetFace, request.TargetRow, request.TargetColumn);
            int affectedRecords = groups.Select(group => group.RecordId).Distinct().Count();
            string label = boxCodes.Count == 1
                ? $"历史资料盒 [{boxCodes[0]}]"
                : $"{boxCodes.Count} 个历史资料盒";
            return Ready(
                $"【交互式历史资料迁档】{label} 将由 [{sourceSlotKey}] 迁至 [{targetSlotKey}] 空余位，涉及 {affectedRecords} 条历史台账；盒号与摆放登记将同步改写。",
                affectedRecords);
        }

        private async Task<ArchiveRelocationResult> ExecuteInteractiveHistoryPhysicalMoveAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var boxCodes = NormalizeHistoryBoxCodes(request.SourceHistoryBoxCodes);
            if (boxCodes.Count == 0)
            {
                return ArchiveRelocationResult.Fail("未指定源历史资料盒号。");
            }

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                var groups = await _relocationRepository.GetHistoryLedgerReferencesByBoxCodesAsync(boxCodes);
                string? issue = await ValidateHistoryMoveSourcesAsync(boxCodes, groups, requireSingleSlot: true);
                if (!string.IsNullOrWhiteSpace(issue))
                {
                    return ArchiveRelocationResult.Fail(issue);
                }

                issue = await ValidateHistoryTargetSlotAsync(
                    request.TargetCabinetName,
                    request.TargetFace,
                    request.TargetRow,
                    request.TargetColumn,
                    boxCodes);
                if (!string.IsNullOrWhiteSpace(issue))
                {
                    return ArchiveRelocationResult.Fail(issue);
                }

                string sourceSlotKey = ResolveHistorySlotKeyOfBoxCode(boxCodes[0]);
                var relocationOutcome = await ExecuteHistoryMoveCoreAsync(
                    request.TargetCabinetName,
                    request.TargetFace,
                    request.TargetRow,
                    request.TargetColumn,
                    boxCodes,
                    groups,
                    ArchiveRelocationMode.PhysicalMove,
                    sourceSlotKey,
                    request.Remarks);

                await transaction.CommitAsync();

                string label = boxCodes.Count == 1
                    ? $"历史资料盒 [{boxCodes[0]}] 已"
                    : $"{boxCodes.Count} 个历史资料盒已";
                return ArchiveRelocationResult.Ok(
                    relocationOutcome.RelocationNo,
                    $"{label}迁至 [{relocationOutcome.TargetSlotKey}]（{relocationOutcome.Items.Count} 条台账改写）。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region 整档口批量迁移

        private async Task<ArchiveRelocationPreview> BuildBatchHistorySlotPreviewAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            string? issue = ValidateHistoryBatchEndpointInputs(request);
            if (!string.IsNullOrWhiteSpace(issue))
            {
                return Blocked(issue);
            }

            string sourceSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.SourceCabinetName, request.SourceFace, request.SourceRow, request.SourceColumn);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName, request.TargetFace, request.TargetRow, request.TargetColumn);
            if (string.Equals(sourceSlotKey, targetSlotKey, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked("源档口与目标档口相同，无需搬迁。");
            }

            var groups = await _relocationRepository.GetHistoryLedgerReferencesInSlotAsync(
                request.SourceCabinetName, request.SourceFace, request.SourceRow, request.SourceColumn);
            var boxCodes = ExtractHistoryBoxCodesInSlot(groups, sourceSlotKey);
            if (boxCodes.Count == 0)
            {
                return Blocked("源档口内没有在库的历史资料。");
            }

            issue = await ValidateHistoryMoveSourcesAsync(boxCodes, groups, requireSingleSlot: false);
            if (!string.IsNullOrWhiteSpace(issue))
            {
                return Blocked(issue);
            }

            issue = await ValidateHistoryTargetSlotForBatchMoveAsync(request, boxCodes);
            if (!string.IsNullOrWhiteSpace(issue))
            {
                return Blocked(issue);
            }

            int affectedRecords = groups.Select(group => group.RecordId).Distinct().Count();
            return Ready(
                $"【整档口历史资料批量搬迁】源档口 [{sourceSlotKey}] 内 {boxCodes.Count} 个历史资料盒（{affectedRecords} 条台账）整体迁至目标档口 [{targetSlotKey}]，按剩余容量依次放置。",
                affectedRecords);
        }

        private async Task<ArchiveRelocationResult> ExecuteBatchHistorySlotPhysicalMoveCoreAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            string? issue = ValidateHistoryBatchEndpointInputs(request);
            if (!string.IsNullOrWhiteSpace(issue))
            {
                return ArchiveRelocationResult.Fail(issue);
            }

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                string sourceSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                    request.SourceCabinetName, request.SourceFace, request.SourceRow, request.SourceColumn);

                var groups = await _relocationRepository.GetHistoryLedgerReferencesInSlotAsync(
                    request.SourceCabinetName, request.SourceFace, request.SourceRow, request.SourceColumn);
                var boxCodes = ExtractHistoryBoxCodesInSlot(groups, sourceSlotKey);
                if (boxCodes.Count == 0)
                {
                    return ArchiveRelocationResult.Fail("源档口内没有在库的历史资料。");
                }

                issue = await ValidateHistoryMoveSourcesAsync(boxCodes, groups, requireSingleSlot: false);
                if (!string.IsNullOrWhiteSpace(issue))
                {
                    return ArchiveRelocationResult.Fail(issue);
                }

                issue = await ValidateHistoryTargetSlotForBatchMoveAsync(request, boxCodes);
                if (!string.IsNullOrWhiteSpace(issue))
                {
                    return ArchiveRelocationResult.Fail(issue);
                }

                var relocationOutcome = await ExecuteHistoryMoveCoreAsync(
                    request.TargetCabinetName,
                    request.TargetFace,
                    request.TargetRow,
                    request.TargetColumn,
                    boxCodes,
                    groups,
                    ArchiveRelocationMode.BatchPhysicalMove,
                    sourceSlotKey,
                    request.Remarks);

                await transaction.CommitAsync();

                return ArchiveRelocationResult.Ok(
                    relocationOutcome.RelocationNo,
                    $"档口批量搬迁完成，共迁移 {boxCodes.Count} 个历史资料盒（{relocationOutcome.Items.Count} 条台账改写）。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region 核心落库

        /// <summary>
        /// 历史资料迁移核心：为每个源盒号分配目标档口空闲序号，改写台账 BoxNumber 与摆放登记，写迁档单。
        /// </summary>
        private async Task<HistoryRelocationOutcome> ExecuteHistoryMoveCoreAsync(
            string targetCabinetName,
            string targetFace,
            int targetRow,
            int targetColumn,
            IReadOnlyList<string> sourceBoxCodes,
            IReadOnlyList<HistoryArchiveLedgerReferenceGroup> groups,
            string relocationMode,
            string sourceSlotKey,
            string remarks)
        {
            DateTime operatedAt = DateTime.Now;
            string operatorName = ResolveOperatorName();
            string relocationNo = await GenerateRelocationNoAsync(ArchiveRegisterDomainValues.MediaKindHistory, operatedAt.Year);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                targetCabinetName, targetFace, targetRow, targetColumn);

            var occupiedIndexes = await ResolveHistoryOccupiedIndexesInSlotAsync(
                targetCabinetName, targetFace, targetRow, targetColumn);
            var relocationItems = new List<YearlyArchiveRelocationItem>();

            // 预加载待迁移盒实体，迁移时同步改写盒号与结构化位置
            var boxesByCode = (await _relocationRepository.GetHistoryArchiveBoxesByCodesForUpdateAsync(sourceBoxCodes))
                .ToDictionary(box => box.BoxCode, StringComparer.OrdinalIgnoreCase);

            foreach (string sourceBoxCode in sourceBoxCodes)
            {
                int sequence = ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(occupiedIndexes);
                occupiedIndexes.Add(sequence);
                string newBoxCode = $"{targetSlotKey}-{sequence:D2}";
                ApplyHistoryBoxCodeRewrite(groups, sourceBoxCode, newBoxCode, operatedAt, operatorName, relocationItems);

                if (boxesByCode.TryGetValue(sourceBoxCode, out var box))
                {
                    RewriteHistoryBoxEntity(box, newBoxCode);
                }
            }

            var record = new YearlyArchiveRelocationRecord
            {
                RelocationNo = relocationNo,
                MediaKind = ArchiveRegisterDomainValues.MediaKindHistory,
                RelocationMode = relocationMode,
                SourceContainerId = 0,
                SourceContainerCode = $"批量({sourceBoxCodes.Count}盒)",
                SourceStorageLocation = sourceSlotKey,
                TargetContainerId = null,
                TargetContainerCode = $"批量({sourceBoxCodes.Count}盒)",
                TargetStorageLocation = targetSlotKey,
                SourceMediumDisposition = ArchiveRelocationSourceDisposition.None,
                OperatedBy = operatorName,
                OperatedAt = operatedAt,
                Remarks = remarks?.Trim() ?? string.Empty,
                PreviewReport = string.Empty
            };
            record.Items = relocationItems;

            _relocationRepository.AddRelocationRecord(record);
            await _relocationRepository.SaveChangesAsync();

            return new HistoryRelocationOutcome(relocationNo, targetSlotKey, relocationItems);
        }

        /// <summary>改写引用该盒号的台账引用组记录（盒号改写由盒实体承载，台账与摆放零触碰）。</summary>
        private void ApplyHistoryBoxCodeRewrite(
            IReadOnlyList<HistoryArchiveLedgerReferenceGroup> groups,
            string oldBoxCode,
            string newBoxCode,
            DateTime operatedAt,
            string operatorName,
            List<YearlyArchiveRelocationItem> relocationItems)
        {
            foreach (var group in groups)
            {
                if (!group.BoxCodes.Contains(oldBoxCode, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                relocationItems.Add(new YearlyArchiveRelocationItem
                {
                    FilingFactId = 0,
                    SourceLinkId = group.RecordId,
                    SourceLinkType = BuildHistorySourceLinkType(group.MaterialKind),
                    BeforeContainerCode = oldBoxCode,
                    BeforeStorageLocation = oldBoxCode,
                    AfterContainerCode = newBoxCode,
                    AfterStorageLocation = newBoxCode
                });
            }
        }

        /// <summary>改写盒实体：仅更新盒号（结构化位置由盒号解析，不再冗余存储）。</summary>
        private static void RewriteHistoryBoxEntity(HistoryArchiveBox box, string newBoxCode)
        {
            box.BoxCode = newBoxCode;
        }

        private sealed record HistoryRelocationOutcome(
            string RelocationNo,
            string TargetSlotKey,
            List<YearlyArchiveRelocationItem> Items);

        #endregion

        #region 校验

        /// <summary>源校验：盒号在库、未锁、（交互式）同档口。混放盒可迁（盒实体改号，台账投影自动跟随）。</summary>
        private async Task<string?> ValidateHistoryMoveSourcesAsync(
            IReadOnlyList<string> boxCodes,
            IReadOnlyList<HistoryArchiveLedgerReferenceGroup> groups,
            bool requireSingleSlot)
        {
            var missing = boxCodes
                .Where(code => !groups.Any(group => group.BoxCodes.Contains(code, StringComparer.OrdinalIgnoreCase)))
                .ToList();
            if (missing.Count > 0)
            {
                return $"以下历史盒号在台账中不存在或不在库：{string.Join("、", missing)}。";
            }

            if (requireSingleSlot)
            {
                string firstSlotKey = ResolveHistorySlotKeyOfBoxCode(boxCodes[0]);
                if (string.IsNullOrWhiteSpace(firstSlotKey))
                {
                    return $"历史盒号 [{boxCodes[0]}] 无法解析出档口信息。";
                }

                if (boxCodes.Any(code => !string.Equals(
                        ResolveHistorySlotKeyOfBoxCode(code),
                        firstSlotKey,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    return "所选历史资料盒不在同一档口，无法一次迁档。";
                }
            }

            // 混放盒（同一台账行登记多个盒号）不再禁迁：迁档仅改盒实体 BoxCode，
            // 链接表按盒 ID 关联且台账 BoxNumber 为投影属性，迁移后混放关系原样保留。

            var locked = await _relocationRepository.GetHistoryDisposalLockedBoxCodesAsync();
            var lockedConflicts = boxCodes
                .Where(code => locked.Contains(code))
                .ToList();
            if (lockedConflicts.Count > 0)
            {
                return $"以下历史盒存在未办结的离库处置锁定：{string.Join("、", lockedConflicts)}，暂不可迁档。";
            }

            return null;
        }

        /// <summary>目标档口校验：用途匹配（历史专用或混用）、无历史占用（源除外）。</summary>
        private async Task<string?> ValidateHistoryTargetSlotAsync(
            string targetCabinetName,
            string targetFace,
            int targetRow,
            int targetColumn,
            IReadOnlyList<string> sourceBoxCodes)
        {
            if (string.IsNullOrWhiteSpace(targetCabinetName)
                || string.IsNullOrWhiteSpace(targetFace)
                || targetRow <= 0
                || targetColumn <= 0)
            {
                return "请提供完整的目标档口信息。";
            }

            var targetCabinet = (await _filingRepository.GetNonMagneticCabinetsAsync())
                .FirstOrDefault(item => string.Equals(item.Name, targetCabinetName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (targetCabinet == null)
            {
                return $"未找到目标档案柜 [{targetCabinetName}]，历史资料只能迁入滑道式/立式/卧式档案柜。";
            }

            if (targetCabinet.Type == CabinetType.Standard)
            {
                string slotCode = ArchiveStorageSlotCategorySupport.BuildSlotCode(targetRow, targetColumn);
                string? storedCategory = await _filingRepository.GetArchiveSlotCategoryNameAsync(
                    targetCabinet.Id, targetFace.Trim(), slotCode);
                string? categoryIssue = ArchiveStorageSlotCategorySupport.TryValidateStandardSlotCategory(
                    targetCabinet,
                    targetFace.Trim(),
                    slotCode,
                    storedCategory,
                    ArchiveStorageSlotCategorySupport.ExpectedHistoricalMaterialsCategory,
                    $"{targetCabinetName.Trim()}{targetFace.Trim()}-{slotCode}");
                if (!string.IsNullOrWhiteSpace(categoryIssue))
                {
                    return categoryIssue;
                }
            }

            // 目标档口容量校验：已有历史盒（源除外）按规格厚度折算，须有足够剩余宽度
            var targetBoxes = await _relocationRepository.GetHistoryArchiveBoxesInSlotForUpdateAsync(
                targetCabinetName, targetFace, targetRow, targetColumn);
            var residentBoxes = targetBoxes
                .Where(box => !sourceBoxCodes.Contains(box.BoxCode, StringComparer.OrdinalIgnoreCase))
                .ToList();

            // 档口已有非历史内容（年度盒）时拒绝：历史与年度盒不混档
            int yearlyCount = (await _filingRepository.GetInUseYearlyArchiveBoxesInSlotAsync(
                targetCabinetName, targetFace, targetRow, targetColumn)).Count;
            if (yearlyCount > 0)
            {
                return "目标档口已有年度档案盒占用，历史资料只可迁入无年度盒的档口。";
            }

            if (residentBoxes.Count > 0)
            {
                string? capacityIssue = await ValidateHistoryTargetSlotCapacityAsync(
                    targetCabinet, targetFace.Trim(), targetRow, targetColumn, residentBoxes.Count + sourceBoxCodes.Count);
                if (!string.IsNullOrWhiteSpace(capacityIssue))
                {
                    return capacityIssue;
                }
            }

            return null;
        }

        /// <summary>
        /// 历史目标档口容量校验：按标准盒厚折算容量，校验迁入后总数不超容量。
        /// </summary>
        private async Task<string?> ValidateHistoryTargetSlotCapacityAsync(
            Cabinet targetCabinet,
            string targetFace,
            int targetRow,
            int targetColumn,
            int totalBoxCountAfterMove)
        {
            var slotSpecificationLookup = (await _filingRepository.GetCabinetSlotSpecificationsAsync())
                .ToDictionary(item => item.CabinetTypeCode, item => item, StringComparer.OrdinalIgnoreCase);
            string cabinetTypeCode = GetCabinetTypeCodeForBatchMove(targetCabinet.Type);
            if (!slotSpecificationLookup.TryGetValue(cabinetTypeCode, out var slotSpecification))
            {
                return null;
            }

            var specificationLookup = (await _filingRepository.GetArchiveBoxSpecificationsAsync())
                .ToDictionary(item => item.Name, item => item, StringComparer.OrdinalIgnoreCase);
            decimal standardThickness = specificationLookup.TryGetValue("标准(10cm)", out var standardSpec)
                ? standardSpec.ThicknessCm
                : 10m;
            if (standardThickness <= 0m)
            {
                return null;
            }

            int capacity = (int)Math.Floor(slotSpecification.WidthCm / standardThickness);
            if (capacity > 0 && totalBoxCountAfterMove > capacity)
            {
                return $"目标档口容量不足（容量 {capacity} 盒，迁入后将达 {totalBoxCountAfterMove} 盒）。";
            }

            return null;
        }

        /// <summary>批量目标档口校验：须无年度盒、无历史盒占用冲突且用途匹配。</summary>
        private Task<string?> ValidateHistoryTargetSlotForBatchMoveAsync(
            BatchSimulatedSlotPhysicalMoveRequest request,
            IReadOnlyList<string> sourceBoxCodes)
        {
            return ValidateHistoryTargetSlotAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn,
                sourceBoxCodes);
        }

        private static string? ValidateHistoryBatchEndpointInputs(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SourceCabinetName)
                || string.IsNullOrWhiteSpace(request.SourceFace)
                || string.IsNullOrWhiteSpace(request.TargetCabinetName)
                || string.IsNullOrWhiteSpace(request.TargetFace)
                || request.SourceRow <= 0
                || request.SourceColumn <= 0
                || request.TargetRow <= 0
                || request.TargetColumn <= 0)
            {
                return "请提供完整的源档口与目标档口信息。";
            }

            return null;
        }

        #endregion

        #region 辅助

        private static List<string> NormalizeHistoryBoxCodes(IEnumerable<string>? codes)
        {
            return (codes ?? [])
                .Select(code => code?.Trim() ?? string.Empty)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ResolveHistorySlotKeyOfBoxCode(string boxCode)
            => ArchiveSlotLocationSupport.BuildSlotKey(boxCode);

        /// <summary>提取台账组中位于指定档口的全部盒号。</summary>
        private static List<string> ExtractHistoryBoxCodesInSlot(
            IReadOnlyList<HistoryArchiveLedgerReferenceGroup> groups,
            string slotKey)
        {
            return groups
                .SelectMany(group => group.BoxCodes)
                .Where(code => string.Equals(
                    ArchiveSlotLocationSupport.BuildSlotKey(code),
                    slotKey,
                    StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>收集目标档口内已占用序号（历史盒盒号末段 + 年度盒位置编码末段）。</summary>
        private async Task<List<int>> ResolveHistoryOccupiedIndexesInSlotAsync(
            string cabinetName,
            string face,
            int row,
            int column)
        {
            var occupied = new List<int>();

            var historyBoxes = await _relocationRepository.GetHistoryArchiveBoxesInSlotForUpdateAsync(cabinetName, face, row, column);
            foreach (var box in historyBoxes)
            {
                if (ArchiveSlotLocationSupport.TryParseSequenceIndex(box.BoxCode, out int index))
                {
                    occupied.Add(index);
                }
            }

            var yearlyBoxes = await _filingRepository.GetInUseYearlyArchiveBoxesInSlotAsync(cabinetName, face, row, column);
            foreach (var box in yearlyBoxes)
            {
                if (ArchiveSlotLocationSupport.TryParseSequenceIndex(box.BoxLocationCode, out int index))
                {
                    occupied.Add(index);
                }
            }

            return occupied;
        }

        #endregion
    }
}
