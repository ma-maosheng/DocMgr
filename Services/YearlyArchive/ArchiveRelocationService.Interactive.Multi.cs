using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        public Task<ArchiveRelocationPreview> PreviewInteractiveItemPhysicalMoveAsync(InteractiveItemPhysicalMoveRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return PreviewInteractiveItemsPhysicalMoveAsync(request.ToItemsRequest());
        }

        public Task<ArchiveRelocationResult> ExecuteInteractiveItemPhysicalMoveAsync(InteractiveItemPhysicalMoveRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return ExecuteInteractiveItemsPhysicalMoveAsync(request.ToItemsRequest());
        }

        public async Task<ArchiveRelocationPreview> PreviewInteractiveItemsPhysicalMoveAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            ArgumentNullException.ThrowIfNull(request);

            if (string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindBlankHardDisk, StringComparison.Ordinal))
            {
                return await BuildInteractiveBlankHardDisksPreviewAsync(request);
            }

            if (string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindDamagedHardDisk, StringComparison.Ordinal))
            {
                return await BuildInteractiveDamagedHardDisksPreviewAsync(request);
            }

            if (string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindDamagedOpticalDisc, StringComparison.Ordinal))
            {
                return await BuildInteractiveDamagedOpticalDiscsPreviewAsync(request);
            }

            return string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                ? await BuildInteractiveElectronicsPreviewAsync(request)
                : await BuildInteractiveSimulatedsPreviewAsync(request);
        }

        public async Task<ArchiveRelocationResult> ExecuteInteractiveItemsPhysicalMoveAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            ArgumentNullException.ThrowIfNull(request);

            var preview = await PreviewInteractiveItemsPhysicalMoveAsync(request);
            if (!preview.CanExecute)
            {
                return ArchiveRelocationResult.Fail(preview.BlockReason);
            }

            if (string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindBlankHardDisk, StringComparison.Ordinal))
            {
                return await ExecuteInteractiveBlankHardDisksPhysicalMoveAsync(request);
            }

            if (string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindDamagedHardDisk, StringComparison.Ordinal))
            {
                return await ExecuteInteractiveDamagedHardDisksPhysicalMoveAsync(request);
            }

            if (string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindDamagedOpticalDisc, StringComparison.Ordinal))
            {
                return await ExecuteInteractiveDamagedOpticalDiscsPhysicalMoveAsync(request);
            }

            if (string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
            {
                return await ExecuteInteractiveElectronicsPhysicalMoveAsync(request);
            }

            return await ExecuteInteractiveSimulatedsPhysicalMoveAsync(request);
        }

        private async Task<ArchiveRelocationPreview> BuildInteractiveSimulatedsPreviewAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var boxIds = NormalizePositiveIds(request.SourceBoxIds);
            if (boxIds.Count == 0)
            {
                return Blocked("未指定源档案盒。");
            }

            var sources = new List<YearlyArchiveBox>();
            foreach (int boxId in boxIds)
            {
                var source = await _relocationRepository.GetArchiveBoxForRelocationAsync(boxId);
                if (source == null)
                {
                    return Blocked($"未找到源档案盒（Id={boxId}）。");
                }

                if (source.MediaItemLinks.Count == 0)
                {
                    return Blocked($"源档案盒 [{source.ArchiveSequenceNo}] 内无资料子项，无法迁档。");
                }

                try
                {
                    EnsureSimulatedBoxAvailableAsRelocationSource(source);
                }
                catch (InvalidOperationException ex)
                {
                    return Blocked(ex.Message);
                }

                sources.Add(source);
            }

            var validation = await ValidateInteractiveSimulatedTargetsAsync(request, sources);
            if (!string.IsNullOrWhiteSpace(validation.Issue))
            {
                return Blocked(validation.Issue);
            }

            string label = sources.Count == 1
                ? $"档案盒 [{sources[0].ArchiveSequenceNo}]"
                : $"{sources.Count} 个档案盒";
            string fromText = sources.Count == 1
                ? sources[0].BoxLocationCode
                : "源档口";

            return Ready(
                $"【交互式物理迁档】{label} 将从 [{fromText}] 迁至目标档口空余位（示例位置 [{validation.NewLocation}]）。\n档口用途：{validation.SlotPurposeText}\n档口空间：{validation.SlotSpaceText}",
                sources.Sum(item => item.MediaItemLinks.Count));
        }

        private async Task<ArchiveRelocationPreview> BuildInteractiveElectronicsPreviewAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var unitIds = NormalizePositiveIds(request.SourceUnitIds);
            if (unitIds.Count == 0)
            {
                return Blocked("未指定源电子介质袋。");
            }

            var sources = new List<YearlyElectronicArchiveUnit>();
            foreach (int unitId in unitIds)
            {
                var source = await _relocationRepository.GetElectronicUnitForRelocationAsync(unitId);
                if (source == null)
                {
                    return Blocked($"未找到源电子介质袋（Id={unitId}）。");
                }

                if (source.MediaItemLinks.Count == 0)
                {
                    return Blocked($"源电子介质袋 [{source.ElectronicArchiveNo}] 内无立档资料，无法迁档。");
                }

                try
                {
                    EnsureElectronicUnitAvailableAsRelocationSource(source);
                }
                catch (InvalidOperationException ex)
                {
                    return Blocked(ex.Message);
                }

                sources.Add(source);
            }

            var validation = await ValidateInteractiveElectronicTargetsAsync(request, sources);
            if (!string.IsNullOrWhiteSpace(validation.Issue))
            {
                return Blocked(validation.Issue);
            }

            string label = sources.Count == 1
                ? $"电子介质袋 [{sources[0].ElectronicArchiveNo}]"
                : $"{sources.Count} 个电子介质袋";

            return Ready(
                $"【交互式物理迁档】{label} 将迁至目标档口空余位（示例位置 [{validation.NewLocation}]）；关联硬盘/光盘台账存放位置将同步更新。\n档口用途：{validation.SlotPurposeText}\n档口空间：{validation.SlotSpaceText}",
                sources.Sum(item => item.MediaItemLinks.Count));
        }

        private async Task<ArchiveRelocationPreview> BuildInteractiveBlankHardDisksPreviewAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var mediumIds = NormalizePositiveIds(request.SourceMediumIds);
            if (mediumIds.Count == 0)
            {
                return Blocked("未指定源空白硬盘。");
            }

            var media = new List<HardDiskMedium>();
            foreach (int mediumId in mediumIds)
            {
                try
                {
                    media.Add(await LoadValidatedInteractiveBlankHardDiskSourceAsync(mediumId));
                }
                catch (InvalidOperationException ex)
                {
                    return Blocked(ex.Message);
                }
            }

            string sourceSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(media[0].Ledger!.StorageLocation);
            if (media.Any(item => !string.Equals(
                    HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(item.Ledger!.StorageLocation),
                    sourceSlotKey,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return Blocked("所选空白硬盘不在同一档口，无法一次迁档。");
            }

            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            if (string.Equals(sourceSlotKey, targetSlotKey, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked("新位置与当前位置相同，无需迁移。");
            }

            var targetRequest = new BatchSimulatedSlotPhysicalMoveRequest
            {
                TargetCabinetName = request.TargetCabinetName,
                TargetFace = request.TargetFace,
                TargetRow = request.TargetRow,
                TargetColumn = request.TargetColumn
            };
            string? targetIssue = await ValidateTargetBlankHardDiskSlotForBatchMoveAsync(targetRequest, media.Count);
            if (!string.IsNullOrWhiteSpace(targetIssue))
            {
                return Blocked(targetIssue);
            }

            int occupiedCount = (await _hardDiskMediaRepository.GetInStockBlankHardDisksInSlotAsync(
                targetSlotKey,
                unlockedOnly: false)).Count;
            // 源盘若已在目标档口不会出现；此处目标与源不同
            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryBlank);
            string slotSpaceText = $"迁入后 {occupiedCount + media.Count} 盘 / 档口容量 {slotCapacity} 盘";
            string label = media.Count == 1 ? $"空白硬盘 [{media[0].DiskCode}]" : $"{media.Count} 块空白硬盘";

            return Ready(
                $"【交互式物理迁档】{label} 将从 [{sourceSlotKey}] 迁至 [{targetSlotKey}]；硬盘台账存放位置将同步更新。\n档口用途：{ResolveMagneticSlotCategoryDisplay(CabinetHardDiskSlotCategoryAssignment.CategoryBlank)}\n档口空间：{slotSpaceText}",
                media.Count);
        }

        private async Task<ArchiveRelocationResult> ExecuteInteractiveSimulatedsPhysicalMoveAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var boxIds = NormalizePositiveIds(request.SourceBoxIds);
            if (boxIds.Count == 0)
            {
                return ArchiveRelocationResult.Fail("未指定源档案盒。");
            }

            var sources = new List<YearlyArchiveBox>();
            foreach (int boxId in boxIds)
            {
                var source = await _relocationRepository.GetArchiveBoxForRelocationAsync(boxId)
                    ?? throw new InvalidOperationException($"未找到源档案盒（Id={boxId}）。");
                EnsureSimulatedBoxAvailableAsRelocationSource(source);
                sources.Add(source);
            }

            var preparedRequests = await BuildInteractiveSimulatedRelocationRequestsAsync(request, sources);
            var relocationNos = new List<string>();
            int successCount = 0;
            foreach (var simulatedRequest in preparedRequests)
            {
                var result = await ExecuteSimulatedRelocationAsync(simulatedRequest);
                if (!result.Success)
                {
                    string prefix = successCount > 0
                        ? $"已成功迁档 {successCount} 个档案盒，后续中断："
                        : string.Empty;
                    return ArchiveRelocationResult.Fail(prefix + result.Message);
                }

                successCount++;
                if (!string.IsNullOrWhiteSpace(result.RelocationNo))
                {
                    relocationNos.Add(result.RelocationNo.Trim());
                }
            }

            return ArchiveRelocationResult.Ok(
                string.Join("、", relocationNos),
                boxIds.Count == 1 ? "档案盒物理迁档完成。" : $"已完成 {boxIds.Count} 个档案盒物理迁档。");
        }

        private async Task<ArchiveRelocationResult> ExecuteInteractiveElectronicsPhysicalMoveAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var unitIds = NormalizePositiveIds(request.SourceUnitIds);
            if (unitIds.Count == 0)
            {
                return ArchiveRelocationResult.Fail("未指定源电子介质袋。");
            }

            var sources = new List<YearlyElectronicArchiveUnit>();
            foreach (int unitId in unitIds)
            {
                var source = await _relocationRepository.GetElectronicUnitForRelocationAsync(unitId)
                    ?? throw new InvalidOperationException($"未找到源电子介质袋（Id={unitId}）。");
                EnsureElectronicUnitAvailableAsRelocationSource(source);
                sources.Add(source);
            }

            var preparedRequests = await BuildInteractiveElectronicRelocationRequestsAsync(request, sources);
            var relocationNos = new List<string>();
            int successCount = 0;
            foreach (var electronicRequest in preparedRequests)
            {
                var result = await ExecuteElectronicRelocationAsync(electronicRequest);
                if (!result.Success)
                {
                    string prefix = successCount > 0
                        ? $"已成功迁档 {successCount} 个电子介质袋，后续中断："
                        : string.Empty;
                    return ArchiveRelocationResult.Fail(prefix + result.Message);
                }

                successCount++;
                if (!string.IsNullOrWhiteSpace(result.RelocationNo))
                {
                    relocationNos.Add(result.RelocationNo.Trim());
                }
            }

            return ArchiveRelocationResult.Ok(
                string.Join("、", relocationNos),
                unitIds.Count == 1 ? "电子介质袋物理迁档完成。" : $"已完成 {unitIds.Count} 个电子介质袋物理迁档。");
        }

        /// <summary>
        /// 为同批多盒预先分配互不冲突的目标序号，避免逐件执行时重复落到同一序号。
        /// </summary>
        private async Task<IReadOnlyList<SimulatedRelocationRequest>> BuildInteractiveSimulatedRelocationRequestsAsync(
            InteractiveItemsPhysicalMoveRequest request,
            IReadOnlyList<YearlyArchiveBox> sources)
        {
            var validation = await ValidateInteractiveSimulatedTargetsAsync(request, sources);
            if (!string.IsNullOrWhiteSpace(validation.Issue))
            {
                throw new InvalidOperationException(validation.Issue);
            }

            var sourceIds = sources.Select(item => item.Id).ToHashSet();
            var boxesInTarget = await _filingRepository.GetInUseYearlyArchiveBoxesInSlotAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            var usedIndexes = boxesInTarget
                .Where(box => !sourceIds.Contains(box.Id))
                .Select(box => box.BoxIndex)
                .Where(index => index > 0)
                .ToList();

            var prepared = new List<SimulatedRelocationRequest>(sources.Count);
            foreach (var source in sources)
            {
                int sequence = ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(usedIndexes);
                usedIndexes.Add(sequence);
                string newLocation = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                    request.TargetCabinetName,
                    request.TargetFace,
                    request.TargetRow,
                    request.TargetColumn,
                    sequence);

                prepared.Add(new SimulatedRelocationRequest
                {
                    RelocationMode = ArchiveRelocationMode.PhysicalMove,
                    SourceBoxId = source.Id,
                    NewStorageLocation = newLocation,
                    NewCabinetName = request.TargetCabinetName.Trim(),
                    NewSide = request.TargetFace.Trim(),
                    NewRow = request.TargetRow,
                    NewColumn = request.TargetColumn,
                    NewBoxIndex = sequence,
                    Remarks = request.Remarks
                });
            }

            return prepared;
        }

        /// <summary>
        /// 为同批多袋预先分配互不冲突的目标序号。
        /// </summary>
        private async Task<IReadOnlyList<ElectronicRelocationRequest>> BuildInteractiveElectronicRelocationRequestsAsync(
            InteractiveItemsPhysicalMoveRequest request,
            IReadOnlyList<YearlyElectronicArchiveUnit> sources)
        {
            var validation = await ValidateInteractiveElectronicTargetsAsync(request, sources);
            if (!string.IsNullOrWhiteSpace(validation.Issue))
            {
                throw new InvalidOperationException(validation.Issue);
            }

            var sourceIds = sources.Select(item => item.Id).ToHashSet();
            var unitsInSlot = await _relocationRepository.GetInUseElectronicArchiveUnitsInSlotForRelocationAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            var usedIndexes = unitsInSlot
                .Where(unit => !sourceIds.Contains(unit.Id))
                .Select(unit =>
                {
                    string location = ResolveElectronicUnitPhysicalStorageLocation(unit);
                    return ArchiveSlotLocationSupport.TryParseSequenceIndex(location, out int seq) ? seq : 0;
                })
                .Where(seq => seq > 0)
                .ToList();

            var prepared = new List<ElectronicRelocationRequest>(sources.Count);
            foreach (var source in sources)
            {
                int sequence = ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(usedIndexes);
                usedIndexes.Add(sequence);
                string newLocation = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                    request.TargetCabinetName,
                    request.TargetFace,
                    request.TargetRow,
                    request.TargetColumn,
                    sequence);

                prepared.Add(new ElectronicRelocationRequest
                {
                    RelocationMode = ArchiveRelocationMode.PhysicalMove,
                    SourceUnitId = source.Id,
                    NewStorageLocation = newLocation,
                    Remarks = request.Remarks
                });
            }

            return prepared;
        }

        private async Task<ArchiveRelocationResult> ExecuteInteractiveBlankHardDisksPhysicalMoveAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var mediumIds = NormalizePositiveIds(request.SourceMediumIds);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            string targetLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(targetSlotKey);
            DateTime operatedAt = DateTime.Now;
            string operatorName = ResolveOperatorName();

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                foreach (int mediumId in mediumIds)
                {
                    var medium = await LoadValidatedInteractiveBlankHardDiskSourceAsync(mediumId);
                    string sourceSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(medium.Ledger!.StorageLocation);
                    string remark = $"交互式迁档：由 [{sourceSlotKey}] 迁至 [{targetSlotKey}]。";
                    RelocateBlankHardDiskLedger(medium, targetLocation, operatedAt, operatorName, remark, "交互式迁档");
                }

                await _relocationRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                return ArchiveRelocationResult.Ok(
                    string.Empty,
                    mediumIds.Count == 1
                        ? $"空白硬盘已迁至 [{targetSlotKey}]。"
                        : $"已将 {mediumIds.Count} 块空白硬盘迁至 [{targetSlotKey}]。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<InteractiveTargetValidationResult> ValidateInteractiveSimulatedTargetsAsync(
            InteractiveItemsPhysicalMoveRequest request,
            IReadOnlyList<YearlyArchiveBox> sources)
        {
            if (string.IsNullOrWhiteSpace(request.TargetCabinetName)
                || string.IsNullOrWhiteSpace(request.TargetFace)
                || request.TargetRow <= 0
                || request.TargetColumn <= 0)
            {
                return InteractiveTargetValidationResult.Fail("请提供完整的目标档口信息。");
            }

            var targetCabinet = (await _filingRepository.GetNonMagneticCabinetsAsync())
                .FirstOrDefault(item => string.Equals(item.Name, request.TargetCabinetName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (targetCabinet == null)
            {
                return InteractiveTargetValidationResult.Fail($"未找到目标档案柜 [{request.TargetCabinetName}]，模拟介质只能迁入滑道式/立式/卧式档案柜。");
            }

            string slotPurposeText;
            if (targetCabinet.Type == CabinetType.Standard)
            {
                string slotCode = $"{request.TargetRow}-{request.TargetColumn}";
                string? storedCategory = await _filingRepository.GetArchiveSlotCategoryNameAsync(
                    targetCabinet.Id,
                    request.TargetFace.Trim(),
                    slotCode);
                slotPurposeText = string.IsNullOrWhiteSpace(storedCategory)
                    ? CabinetArchiveSlotCategoryAssignment.CategoryUnset
                    : CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(storedCategory);

                string? categoryIssue = ArchiveStorageSlotCategorySupport.TryValidateStandardSlotCategory(
                    targetCabinet,
                    request.TargetFace.Trim(),
                    slotCode,
                    storedCategory,
                    ArchiveStorageSlotCategorySupport.ExpectedYearlyMaterialsCategory,
                    $"{request.TargetCabinetName.Trim()}{request.TargetFace.Trim()}-{slotCode}");
                if (!string.IsNullOrWhiteSpace(categoryIssue))
                {
                    return InteractiveTargetValidationResult.Fail(categoryIssue);
                }
            }
            else
            {
                slotPurposeText = "标准档案柜档口";
            }

            var sourceIds = sources.Select(item => item.Id).ToHashSet();
            var boxesInTarget = await _filingRepository.GetInUseYearlyArchiveBoxesInSlotAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            var occupyingBoxes = boxesInTarget
                .Where(box => !sourceIds.Contains(box.Id))
                .ToList();

            var specificationLookup = (await _filingRepository.GetArchiveBoxSpecificationsAsync())
                .ToDictionary(item => item.Name, item => item, StringComparer.OrdinalIgnoreCase);
            var slotSpecificationLookup = (await _filingRepository.GetCabinetSlotSpecificationsAsync())
                .ToDictionary(item => item.CabinetTypeCode, item => item, StringComparer.OrdinalIgnoreCase);

            string cabinetTypeCode = GetCabinetTypeCodeForBatchMove(targetCabinet.Type);
            if (!slotSpecificationLookup.TryGetValue(cabinetTypeCode, out var slotSpecification))
            {
                return InteractiveTargetValidationResult.Fail($"未找到柜型 [{cabinetTypeCode}] 的档口规格配置。");
            }

            decimal incomingThickness = sources.Sum(box => ResolveArchiveBoxThickness(specificationLookup, box.Specs));
            decimal occupiedWidth = occupyingBoxes.Sum(box => ResolveArchiveBoxThickness(specificationLookup, box.Specs));
            decimal totalWidthAfterMove = occupiedWidth + incomingThickness;
            string slotSpaceText = $"迁入后约 {occupyingBoxes.Count + sources.Count} 盒，占用宽度 {totalWidthAfterMove:0.##}cm / 档口 {slotSpecification.WidthCm:0.##}cm";
            if (totalWidthAfterMove > slotSpecification.WidthCm)
            {
                return InteractiveTargetValidationResult.Fail($"目标档口可用宽度不足（迁入后需 {totalWidthAfterMove:0.##}cm，档口 {slotSpecification.WidthCm:0.##}cm）。");
            }

            var occupiedIndexes = occupyingBoxes
                .Select(box => box.BoxIndex)
                .Where(index => index > 0)
                .ToList();
            int firstSequence = ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(occupiedIndexes);
            string sampleLocation = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn,
                firstSequence);

            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            bool anyOutsideTarget = sources.Any(source =>
                !ArchiveSlotLocationSupport.IsSameSlot(source.BoxLocationCode, targetSlotKey));
            if (!anyOutsideTarget)
            {
                return InteractiveTargetValidationResult.Fail("源与目标为同一档口，无需迁移。");
            }

            return InteractiveTargetValidationResult.Ok(sampleLocation, slotPurposeText, slotSpaceText);
        }

        private async Task<InteractiveTargetValidationResult> ValidateInteractiveElectronicTargetsAsync(
            InteractiveItemsPhysicalMoveRequest request,
            IReadOnlyList<YearlyElectronicArchiveUnit> sources)
        {
            if (string.IsNullOrWhiteSpace(request.TargetCabinetName)
                || string.IsNullOrWhiteSpace(request.TargetFace)
                || request.TargetRow <= 0
                || request.TargetColumn <= 0)
            {
                return InteractiveTargetValidationResult.Fail("请提供完整的目标档口信息。");
            }

            var targetCabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(request.TargetCabinetName);
            if (targetCabinet == null)
            {
                return InteractiveTargetValidationResult.Fail($"未找到目标防磁磁盘柜 [{request.TargetCabinetName}]。");
            }

            string targetSlotCode = $"{request.TargetRow}-{request.TargetColumn}";
            string? sourceCategory = null;
            string sourcePhysicalLocation = ResolveElectronicUnitPhysicalStorageLocation(sources[0]);
            if (ArchiveSlotLocationSupport.TryParseSlotLocation(
                    sourcePhysicalLocation,
                    out string sourceCabinetName,
                    out string sourceFace,
                    out int sourceRow,
                    out int sourceColumn))
            {
                var sourceCabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(sourceCabinetName);
                if (sourceCabinet != null)
                {
                    sourceCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                        sourceCabinet.Id,
                        sourceFace,
                        $"{sourceRow}-{sourceColumn}");
                }
            }

            string? targetCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                targetCabinet.Id,
                request.TargetFace.Trim(),
                targetSlotCode);
            string normalizedSourceCategory = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(sourceCategory);
            string normalizedTargetCategory = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(targetCategory);

            if (!CabinetHardDiskSlotCategoryAssignment.IsRelocatableDedicatedSlotCategory(normalizedSourceCategory))
            {
                return InteractiveTargetValidationResult.Fail("仅支持已设置专用类别档口内的电子介质迁档。");
            }

            if (!CabinetHardDiskSlotCategoryAssignment.MatchesCategory(normalizedTargetCategory, normalizedSourceCategory))
            {
                return InteractiveTargetValidationResult.Fail(
                    $"目标档口专用类别须与源一致（源：{ResolveMagneticSlotCategoryDisplay(normalizedSourceCategory)}，目标：{ResolveMagneticSlotCategoryDisplay(normalizedTargetCategory)}）。");
            }

            var sourceIds = sources.Select(item => item.Id).ToHashSet();
            int occupiedCount = await CountInteractiveElectronicOccupancyInSlotAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn,
                excludeUnitIds: sourceIds);
            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(normalizedTargetCategory);
            int countAfterMove = occupiedCount + sources.Count;
            string slotSpaceText = $"迁入后 {countAfterMove} 袋 / 档口容量 {slotCapacity} 袋";
            if (countAfterMove > slotCapacity)
            {
                return InteractiveTargetValidationResult.Fail($"目标档口盘位不足（迁入后需 {countAfterMove} 袋，档口容量 {slotCapacity} 袋）。");
            }

            var unitsInSlot = await _relocationRepository.GetInUseElectronicArchiveUnitsInSlotForRelocationAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            var occupiedIndexes = unitsInSlot
                .Where(unit => !sourceIds.Contains(unit.Id))
                .Select(unit =>
                {
                    string location = ResolveElectronicUnitPhysicalStorageLocation(unit);
                    return ArchiveSlotLocationSupport.TryParseSequenceIndex(location, out int seq) ? seq : 0;
                })
                .Where(seq => seq > 0)
                .ToList();

            int firstSequence = ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(occupiedIndexes);
            string sampleLocation = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn,
                firstSequence);

            string slotCode = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            string sourceSlotKey = ArchiveSlotLocationSupport.TryParseSlotLocation(
                sourcePhysicalLocation,
                out string sc, out string sf, out int sr, out int scol)
                ? ArchiveSlotLocationSupport.BuildSlotKey(sc, sf, sr, scol)
                : string.Empty;
            if (string.Equals(sourceSlotKey, slotCode, StringComparison.OrdinalIgnoreCase)
                && sources.Count == 1
                && HardDiskLedgerSyncSupport.IsSameFullLocation(sourcePhysicalLocation, sampleLocation))
            {
                return InteractiveTargetValidationResult.Fail("新位置与当前位置相同，无需迁移。");
            }

            return InteractiveTargetValidationResult.Ok(
                sampleLocation,
                ResolveMagneticSlotCategoryDisplay(normalizedTargetCategory),
                slotSpaceText);
        }

        private async Task<int> CountInteractiveElectronicOccupancyInSlotAsync(
            string cabinetName,
            string face,
            int row,
            int column,
            IReadOnlySet<int> excludeUnitIds)
        {
            var units = await _relocationRepository.GetInUseElectronicArchiveUnitsInSlotForRelocationAsync(
                cabinetName,
                face,
                row,
                column);
            return units.Count(unit => !excludeUnitIds.Contains(unit.Id));
        }

        private static List<int> NormalizePositiveIds(IEnumerable<int>? ids)
            => (ids ?? []).Where(id => id > 0).Distinct().ToList();
    }
}
