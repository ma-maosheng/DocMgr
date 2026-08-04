using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        public const string YearlyArchiveBoxCategoryName = "年度资料";

        public Task<ArchiveRelocationPreview> PreviewBatchSimulatedSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            return BuildBatchSimulatedSlotPreviewAsync(request);
        }

        public async Task<ArchiveRelocationResult> ExecuteBatchSimulatedSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            var preview = await BuildBatchSimulatedSlotPreviewAsync(request);
            if (!preview.CanExecute)
            {
                return ArchiveRelocationResult.Fail(preview.BlockReason);
            }

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                var sourceBoxes = await LoadValidatedSourceBoxesForBatchMoveAsync(request);
                DateTime operatedAt = DateTime.Now;
                string operatorName = ResolveOperatorName();
                string relocationNo = await GenerateRelocationNoAsync(ArchiveRegisterDomainValues.MediaKindSimulated, operatedAt.Year);
                string sourceSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                    request.SourceCabinetName,
                    request.SourceFace,
                    request.SourceRow,
                    request.SourceColumn);
                string remark = $"档口批量搬迁：由 [{sourceSlotKey}] 迁至 [{ArchiveSlotLocationSupport.BuildSlotKey(request.TargetCabinetName, request.TargetFace, request.TargetRow, request.TargetColumn)}]。";

                var context = new ArchiveRelocationExecutionContext
                {
                    SourceMediumDisposition = ArchiveRelocationSourceDisposition.None
                };

                var movedBoxLocations = new List<(int BoxId, string ContainerCode, string NewLocation)>();
                var occupiedTargetSequences = new List<int>();
                foreach (var box in sourceBoxes)
                {
                    int sequence = ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(occupiedTargetSequences);
                    occupiedTargetSequences.Add(sequence);
                    string newLocation = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                        request.TargetCabinetName,
                        request.TargetFace,
                        request.TargetRow,
                        request.TargetColumn,
                        sequence);

                    ApplySimulatedBoxPhysicalLocation(
                        box,
                        newLocation,
                        request.TargetCabinetName,
                        request.TargetFace,
                        request.TargetRow,
                        request.TargetColumn,
                        sequence,
                        operatedAt,
                        operatorName);

                    await UpdateFilingFactsForPhysicalMoveAsync(
                        ArchiveRegisterDomainValues.MediaKindSimulated,
                        box.Id,
                        newLocation,
                        operatedAt,
                        remark,
                        context.RelocationItems);

                    movedBoxLocations.Add((box.Id, box.ArchiveSequenceNo, newLocation));
                }

                var firstBox = sourceBoxes[0];
                string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                    request.TargetCabinetName,
                    request.TargetFace,
                    request.TargetRow,
                    request.TargetColumn);
                string targetContainerCode = string.Join("、", sourceBoxes.Select(item => item.ArchiveSequenceNo));
                string targetStorageLocation = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                    request.TargetCabinetName,
                    request.TargetFace,
                    request.TargetRow,
                    request.TargetColumn,
                    1);

                var record = BuildBatchSlotRelocationRecord(
                    relocationNo,
                    firstBox,
                    sourceSlotKey,
                    targetSlotKey,
                    sourceBoxes.Count,
                    firstBox.Id,
                    targetContainerCode,
                    targetStorageLocation,
                    context,
                    operatorName,
                    operatedAt,
                    request.Remarks,
                    preview.SummaryText);

                record.Items = context.RelocationItems;
                _relocationRepository.AddRelocationRecord(record);
                await _materialTransactionWriter.AppendRelocationTransactionsAsync(record);
                await _relocationRepository.SaveChangesAsync();

                foreach (var moved in movedBoxLocations)
                {
                    await _pendingReturnContainerService.MarkPendingReturnsLocationChangedAsync(
                        moved.BoxId,
                        moved.ContainerCode,
                        moved.NewLocation);
                }

                await transaction.CommitAsync();

                return ArchiveRelocationResult.Ok(
                    relocationNo,
                    $"档口批量搬迁完成，共迁移 {sourceBoxes.Count} 个档案盒。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<ArchiveRelocationPreview> BuildBatchSimulatedSlotPreviewAsync(BatchSimulatedSlotPhysicalMoveRequest request)
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
                return Blocked("请提供完整的源档口与目标档口信息。");
            }

            string sourceSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.SourceCabinetName,
                request.SourceFace,
                request.SourceRow,
                request.SourceColumn);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);

            if (string.Equals(sourceSlotKey, targetSlotKey, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked("源档口与目标档口相同，无需搬迁。");
            }

            List<YearlyArchiveBox> sourceBoxes;
            try
            {
                sourceBoxes = await LoadValidatedSourceBoxesForBatchMoveAsync(request);
            }
            catch (InvalidOperationException ex)
            {
                return Blocked(ex.Message);
            }

            string? targetIssue = await ValidateTargetSlotForBatchMoveAsync(request, sourceBoxes);
            if (!string.IsNullOrWhiteSpace(targetIssue))
            {
                return Blocked(targetIssue);
            }

            return Ready(
                $"【档口批量物理搬迁】将源档口 [{sourceSlotKey}] 内 {sourceBoxes.Count} 个年度模拟档案盒整体迁至空档口 [{targetSlotKey}]，按源顺序优先占用目标档口空闲序号；源档口余下实体序号不重排。",
                sourceBoxes.Sum(box => box.MediaItemLinks.Count));
        }

        private async Task<List<YearlyArchiveBox>> LoadValidatedSourceBoxesForBatchMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            int historyCount = await _filingRepository.CountHistoryArchiveOccupanciesInSlotAsync(
                request.SourceCabinetName,
                request.SourceFace,
                request.SourceRow,
                request.SourceColumn);
            if (historyCount > 0)
            {
                throw new InvalidOperationException("源档口存在历史库资料占用，属于混放档口，禁止批量搬迁。");
            }

            var boxes = await _filingRepository.GetInUseYearlyArchiveBoxesInSlotAsync(
                request.SourceCabinetName,
                request.SourceFace,
                request.SourceRow,
                request.SourceColumn);

            if (boxes.Count == 0)
            {
                throw new InvalidOperationException("源档口内没有在用的年度模拟档案盒。");
            }

            if (boxes.Any(box => box.MediaItemLinks.Count == 0))
            {
                throw new InvalidOperationException("源档口存在无资料子项的档案盒，请先整理后再批量搬迁。");
            }

            var withdrawalLocks = _cabinetOpenLayoutRepository.GetActiveWithdrawalLocksByArchiveBoxIds(
                boxes.Select(box => box.Id).ToList());
            var relocatable = boxes
                .Where(box => !withdrawalLocks.TryGetValue(box.Id, out var occupation) || !occupation.HasLock)
                .ToList();
            if (relocatable.Count == 0)
            {
                throw new InvalidOperationException("源档口内档案盒均存在出库预订/征用占用，暂不可批量迁档。");
            }

            return relocatable;
        }

        private async Task<string?> ValidateTargetSlotForBatchMoveAsync(
            BatchSimulatedSlotPhysicalMoveRequest request,
            IReadOnlyList<YearlyArchiveBox> sourceBoxes)
        {
            int yearlyCount = (await _filingRepository.GetInUseYearlyArchiveBoxesInSlotAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn)).Count;
            if (yearlyCount > 0)
            {
                return "目标档口已有年度档案盒占用，请选择全空档口。";
            }

            int historyCount = await _filingRepository.CountHistoryArchiveOccupanciesInSlotAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            if (historyCount > 0)
            {
                return "目标档口存在历史库资料占用，请选择全空档口。";
            }

            string? capacityIssue = await ValidateTargetSlotCapacityForBatchMoveAsync(request, sourceBoxes);
            if (!string.IsNullOrWhiteSpace(capacityIssue))
            {
                return capacityIssue;
            }

            return await ValidateTargetSlotCategoryForYearlyBatchMoveAsync(request);
        }

        private async Task<string?> ValidateTargetSlotCategoryForYearlyBatchMoveAsync(
            BatchSimulatedSlotPhysicalMoveRequest request)
        {
            var cabinets = await _filingRepository.GetNonMagneticCabinetsAsync();
            var targetCabinet = cabinets.FirstOrDefault(item =>
                string.Equals(item.Name, request.TargetCabinetName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (targetCabinet == null)
            {
                return $"未找到目标柜 [{request.TargetCabinetName}]。";
            }

            if (targetCabinet.Type != CabinetType.Standard)
            {
                return null;
            }

            string slotCode = ArchiveStorageSlotCategorySupport.BuildSlotCode(request.TargetRow, request.TargetColumn);
            string faceCode = request.TargetFace.Trim();
            string? storedCategory = await _filingRepository.GetArchiveSlotCategoryNameAsync(
                targetCabinet.Id,
                faceCode,
                slotCode);
            return ArchiveStorageSlotCategorySupport.TryValidateStandardSlotCategory(
                targetCabinet,
                faceCode,
                slotCode,
                storedCategory,
                ArchiveStorageSlotCategorySupport.ExpectedYearlyMaterialsCategory,
                $"{request.TargetCabinetName.Trim()}{faceCode}-{slotCode}");
        }

        private async Task<string?> ValidateTargetSlotCapacityForBatchMoveAsync(
            BatchSimulatedSlotPhysicalMoveRequest request,
            IReadOnlyList<YearlyArchiveBox> sourceBoxes)
        {
            var cabinets = await _filingRepository.GetNonMagneticCabinetsAsync();
            var targetCabinet = cabinets.FirstOrDefault(item =>
                string.Equals(item.Name, request.TargetCabinetName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (targetCabinet == null)
            {
                return $"未找到目标柜 [{request.TargetCabinetName}]。";
            }

            var specificationLookup = (await _filingRepository.GetArchiveBoxSpecificationsAsync())
                .ToDictionary(item => item.Name, item => item, StringComparer.OrdinalIgnoreCase);
            var slotSpecificationLookup = (await _filingRepository.GetCabinetSlotSpecificationsAsync())
                .ToDictionary(item => item.CabinetTypeCode, item => item, StringComparer.OrdinalIgnoreCase);

            string cabinetTypeCode = GetCabinetTypeCodeForBatchMove(targetCabinet.Type);
            if (!slotSpecificationLookup.TryGetValue(cabinetTypeCode, out var slotSpecification))
            {
                return $"未找到柜型 [{cabinetTypeCode}] 的档口规格配置。";
            }

            decimal totalWidth = sourceBoxes.Sum(box => ResolveArchiveBoxThickness(specificationLookup, box.Specs));
            if (totalWidth > slotSpecification.WidthCm)
            {
                return $"目标档口可用宽度不足（需要 {totalWidth:0.##}cm，档口 {slotSpecification.WidthCm:0.##}cm）。";
            }

            return null;
        }

        private static decimal ResolveArchiveBoxThickness(
            IReadOnlyDictionary<string, ArchiveBoxSpecification> specificationLookup,
            string? specs)
        {
            string normalized = NormalizeArchiveBoxSpecification(specs);
            if (specificationLookup.TryGetValue(normalized, out var specification))
            {
                return specification.ThicknessCm;
            }

            return 5m;
        }

        private static string GetCabinetTypeCodeForBatchMove(CabinetType cabinetType)
        {
            return cabinetType switch
            {
                CabinetType.Standard => "Standard",
                CabinetType.Vertical => "Vertical",
                CabinetType.Horizontal => "Horizontal",
                _ => "Standard"
            };
        }

        private void ApplySimulatedBoxPhysicalLocation(
            YearlyArchiveBox box,
            string newLocation,
            string cabinetName,
            string side,
            int row,
            int column,
            int boxIndex,
            DateTime operatedAt,
            string operatorName)
        {
            box.CabinetName = cabinetName.Trim();
            box.Side = side.Trim();
            box.Row = row;
            box.Column = column;
            box.BoxIndex = boxIndex;
            box.BoxLocationCode = newLocation;
            box.ContainerLifecycleStatus = ArchiveContainerLifecycleStatus.InUse;
            UpsertArchiveBoxPlacement(box, operatedAt, operatorName);
        }

        private static YearlyArchiveRelocationRecord BuildBatchSlotRelocationRecord(
            string relocationNo,
            YearlyArchiveBox firstBox,
            string sourceSlotKey,
            string targetSlotKey,
            int movedBoxCount,
            int targetContainerId,
            string targetContainerCode,
            string targetStorageLocation,
            ArchiveRelocationExecutionContext context,
            string operatorName,
            DateTime operatedAt,
            string remarks,
            string previewReport)
        {
            return new YearlyArchiveRelocationRecord
            {
                RelocationNo = relocationNo,
                MediaKind = ArchiveRegisterDomainValues.MediaKindSimulated,
                RelocationMode = ArchiveRelocationMode.BatchPhysicalMove,
                SourceContainerId = firstBox.Id,
                SourceContainerCode = $"批量({movedBoxCount}盒)",
                SourceStorageLocation = sourceSlotKey,
                TargetContainerId = targetContainerId,
                TargetContainerCode = targetContainerCode,
                TargetStorageLocation = targetStorageLocation,
                SourceMediumDisposition = context.SourceMediumDisposition,
                OperatedBy = operatorName,
                OperatedAt = operatedAt,
                Remarks = remarks?.Trim() ?? string.Empty,
                PreviewReport = previewReport
            };
        }

        public Task<string?> GetSimulatedPendingReturnConfirmMessageAsync(int sourceBoxId, string actionLabel)
        {
            EnsureArchiveAdmin();
            return _pendingReturnContainerService.BuildPendingReturnConfirmMessageAsync(sourceBoxId, actionLabel);
        }

        public async Task<string?> GetBatchSimulatedPendingReturnConfirmMessageAsync(
            BatchSimulatedSlotPhysicalMoveRequest request,
            string actionLabel)
        {
            EnsureArchiveAdmin();
            try
            {
                var sourceBoxes = await LoadValidatedSourceBoxesForBatchMoveAsync(request);
                var boxIds = sourceBoxes.Select(box => box.Id).ToList();
                return await _pendingReturnContainerService.BuildPendingReturnConfirmMessageForBoxesAsync(boxIds, actionLabel);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
