using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        public async Task<ArchiveRelocationPreview> PreviewInteractiveItemPhysicalMoveAsync(InteractiveItemPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            if (string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindBlankHardDisk, StringComparison.Ordinal))
            {
                return await BuildInteractiveBlankHardDiskPreviewAsync(request);
            }

            return string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                ? await BuildInteractiveElectronicPreviewAsync(request)
                : await BuildInteractiveSimulatedPreviewAsync(request);
        }

        public async Task<ArchiveRelocationResult> ExecuteInteractiveItemPhysicalMoveAsync(InteractiveItemPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            var preview = await PreviewInteractiveItemPhysicalMoveAsync(request);
            if (!preview.CanExecute)
            {
                return ArchiveRelocationResult.Fail(preview.BlockReason);
            }

            if (string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindBlankHardDisk, StringComparison.Ordinal))
            {
                return await ExecuteInteractiveBlankHardDiskPhysicalMoveAsync(request);
            }

            if (string.Equals(request.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
            {
                var electronicRequest = await BuildInteractiveElectronicRelocationRequestAsync(request);
                return await ExecuteElectronicRelocationAsync(electronicRequest);
            }

            var simulatedRequest = await BuildInteractiveSimulatedRelocationRequestAsync(request);
            return await ExecuteSimulatedRelocationAsync(simulatedRequest);
        }

        private async Task<ArchiveRelocationPreview> BuildInteractiveSimulatedPreviewAsync(InteractiveItemPhysicalMoveRequest request)
        {
            if (request.SourceBoxId <= 0)
            {
                return Blocked("未指定源档案盒。");
            }

            var source = await _relocationRepository.GetArchiveBoxForRelocationAsync(request.SourceBoxId);
            if (source == null)
            {
                return Blocked("未找到源档案盒。");
            }

            if (source.MediaItemLinks.Count == 0)
            {
                return Blocked("源档案盒内无资料子项，无法迁档。");
            }

            var validation = await ValidateInteractiveSimulatedTargetAsync(request, source);
            if (!string.IsNullOrWhiteSpace(validation.Issue))
            {
                return Blocked(validation.Issue);
            }

            if (HardDiskLedgerSyncSupport.IsSameFullLocation(source.BoxLocationCode, validation.NewLocation))
            {
                return Blocked("新位置与当前位置相同，无需迁移。");
            }

            return Ready(
                $"【单件物理迁档】档案盒 [{source.ArchiveSequenceNo}] 将从 [{source.BoxLocationCode}] 迁至 [{validation.NewLocation}]。\n档口用途：{validation.SlotPurposeText}\n档口空间：{validation.SlotSpaceText}",
                source.MediaItemLinks.Count);
        }

        private async Task<ArchiveRelocationPreview> BuildInteractiveElectronicPreviewAsync(InteractiveItemPhysicalMoveRequest request)
        {
            if (request.SourceUnitId <= 0)
            {
                return Blocked("未指定源电子介质袋。");
            }

            var source = await _relocationRepository.GetElectronicUnitForRelocationAsync(request.SourceUnitId);
            if (source == null)
            {
                return Blocked("未找到源电子介质袋。");
            }

            if (source.MediaItemLinks.Count == 0)
            {
                return Blocked("源电子介质袋内无立档资料，无法迁档。");
            }

            var validation = await ValidateInteractiveElectronicTargetAsync(request, source);
            if (!string.IsNullOrWhiteSpace(validation.Issue))
            {
                return Blocked(validation.Issue);
            }

            string sourceLocation = ResolveElectronicUnitPhysicalStorageLocation(source);
            if (HardDiskLedgerSyncSupport.IsSameFullLocation(sourceLocation, validation.NewLocation))
            {
                return Blocked("新位置与当前位置相同，无需迁移。");
            }

            string mediumHint = IsHardDiskCarrier(source.StorageCarrierType)
                ? "，关联硬盘台账存放位置将同步更新"
                : IsOpticalDiscCarrier(source.StorageCarrierType)
                    ? "，关联光盘台账存放位置将同步更新"
                    : string.Empty;

            return Ready(
                $"【单件物理迁档】电子介质袋 [{source.ElectronicArchiveNo}] 将从 [{sourceLocation}] 迁至 [{validation.NewLocation}]{mediumHint}。\n档口用途：{validation.SlotPurposeText}\n档口空间：{validation.SlotSpaceText}",
                source.MediaItemLinks.Count);
        }

        private async Task<SimulatedRelocationRequest> BuildInteractiveSimulatedRelocationRequestAsync(InteractiveItemPhysicalMoveRequest request)
        {
            var source = await _relocationRepository.GetArchiveBoxForRelocationAsync(request.SourceBoxId)
                ?? throw new InvalidOperationException("未找到源档案盒。");

            var validation = await ValidateInteractiveSimulatedTargetAsync(request, source);
            if (!string.IsNullOrWhiteSpace(validation.Issue))
            {
                throw new InvalidOperationException(validation.Issue);
            }

            if (!ArchiveSlotLocationSupport.TryParseSequenceIndex(validation.NewLocation, out int boxIndex))
            {
                throw new InvalidOperationException("目标位置序号无效。");
            }

            return new SimulatedRelocationRequest
            {
                RelocationMode = ArchiveRelocationMode.PhysicalMove,
                SourceBoxId = source.Id,
                NewStorageLocation = validation.NewLocation,
                NewCabinetName = request.TargetCabinetName.Trim(),
                NewSide = request.TargetFace.Trim(),
                NewRow = request.TargetRow,
                NewColumn = request.TargetColumn,
                NewBoxIndex = boxIndex,
                Remarks = request.Remarks
            };
        }

        private async Task<ElectronicRelocationRequest> BuildInteractiveElectronicRelocationRequestAsync(InteractiveItemPhysicalMoveRequest request)
        {
            var source = await _relocationRepository.GetElectronicUnitForRelocationAsync(request.SourceUnitId)
                ?? throw new InvalidOperationException("未找到源电子介质袋。");

            var validation = await ValidateInteractiveElectronicTargetAsync(request, source);
            if (!string.IsNullOrWhiteSpace(validation.Issue))
            {
                throw new InvalidOperationException(validation.Issue);
            }

            return new ElectronicRelocationRequest
            {
                RelocationMode = ArchiveRelocationMode.PhysicalMove,
                SourceUnitId = source.Id,
                NewStorageLocation = validation.NewLocation,
                Remarks = request.Remarks
            };
        }

        private sealed class InteractiveTargetValidationResult
        {
            public string? Issue { get; init; }

            public string NewLocation { get; init; } = string.Empty;

            public string SlotPurposeText { get; init; } = string.Empty;

            public string SlotSpaceText { get; init; } = string.Empty;

            public static InteractiveTargetValidationResult Fail(string issue) =>
                new() { Issue = issue };

            public static InteractiveTargetValidationResult Ok(
                string newLocation,
                string slotPurposeText,
                string slotSpaceText) =>
                new()
                {
                    NewLocation = newLocation,
                    SlotPurposeText = slotPurposeText,
                    SlotSpaceText = slotSpaceText
                };
        }

        private async Task<InteractiveTargetValidationResult> ValidateInteractiveSimulatedTargetAsync(
            InteractiveItemPhysicalMoveRequest request,
            YearlyArchiveBox source)
        {
            string newLocation = string.Empty;
            string slotPurposeText = CabinetArchiveSlotCategoryAssignment.CategoryUnset;
            string slotSpaceText = string.Empty;

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

            int sequence = await ResolveInteractiveSimulatedTargetSequenceAsync(source, request);
            newLocation = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn,
                sequence);

            var boxesInTarget = await _filingRepository.GetInUseYearlyArchiveBoxesInSlotAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            var occupyingBoxes = boxesInTarget
                .Where(box => box.Id != source.Id)
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

            decimal sourceThickness = ResolveArchiveBoxThickness(specificationLookup, source.Specs);
            decimal occupiedWidth = occupyingBoxes.Sum(box => ResolveArchiveBoxThickness(specificationLookup, box.Specs));
            decimal totalWidthAfterMove = occupiedWidth + sourceThickness;

            slotSpaceText = $"迁入后约 {occupyingBoxes.Count + 1} 盒，占用宽度 {totalWidthAfterMove:0.##}cm / 档口 {slotSpecification.WidthCm:0.##}cm";
            if (totalWidthAfterMove > slotSpecification.WidthCm)
            {
                return InteractiveTargetValidationResult.Fail($"目标档口可用宽度不足（迁入后需 {totalWidthAfterMove:0.##}cm，档口 {slotSpecification.WidthCm:0.##}cm）。");
            }

            return InteractiveTargetValidationResult.Ok(newLocation, slotPurposeText, slotSpaceText);
        }

        private async Task<InteractiveTargetValidationResult> ValidateInteractiveElectronicTargetAsync(
            InteractiveItemPhysicalMoveRequest request,
            YearlyElectronicArchiveUnit source)
        {
            string newLocation = string.Empty;
            string slotPurposeText = string.Empty;
            string slotSpaceText = string.Empty;

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
            string sourcePhysicalLocation = ResolveElectronicUnitPhysicalStorageLocation(source);
            if (ArchiveSlotLocationSupport.TryParseSlotLocation(sourcePhysicalLocation, out string sourceCabinetName, out string sourceFace, out int sourceRow, out int sourceColumn))
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
                return InteractiveTargetValidationResult.Fail($"目标档口专用类别须与源一致（源：{ResolveMagneticSlotCategoryDisplay(normalizedSourceCategory)}，目标：{ResolveMagneticSlotCategoryDisplay(normalizedTargetCategory)}）。");
            }

            slotPurposeText = ResolveMagneticSlotCategoryDisplay(normalizedTargetCategory);

            int sequence = await ResolveInteractiveElectronicTargetSequenceAsync(source, request);
            newLocation = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn,
                sequence);

            int occupiedCount = await CountInteractiveElectronicOccupancyInSlotAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn,
                source.Id);
            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(normalizedTargetCategory);
            int countAfterMove = occupiedCount + 1;
            slotSpaceText = $"迁入后 {countAfterMove} 袋 / 档口容量 {slotCapacity} 袋";
            if (countAfterMove > slotCapacity)
            {
                return InteractiveTargetValidationResult.Fail($"目标档口盘位不足（迁入后需 {countAfterMove} 袋，档口容量 {slotCapacity} 袋）。");
            }

            return InteractiveTargetValidationResult.Ok(newLocation, slotPurposeText, slotSpaceText);
        }

        private async Task<int> ResolveInteractiveSimulatedTargetSequenceAsync(
            YearlyArchiveBox source,
            InteractiveItemPhysicalMoveRequest request)
        {
            var boxesInTarget = await _filingRepository.GetInUseYearlyArchiveBoxesInSlotAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);

            var occupiedIndexes = boxesInTarget
                .Where(box => box.Id != source.Id)
                .Select(box => box.BoxIndex)
                .Where(index => index > 0)
                .ToList();

            return ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(occupiedIndexes);
        }

        private async Task<int> ResolveInteractiveElectronicTargetSequenceAsync(
            YearlyElectronicArchiveUnit source,
            InteractiveItemPhysicalMoveRequest request)
        {
            string slotCode = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            string slotPrefix = slotCode + "-";
            var occupiedIndexes = await _filingRepository.GetElectronicUnitSequenceIndexesInSlotAsync(
                slotCode,
                slotPrefix,
                source.Id);
            return ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(occupiedIndexes);
        }

        private async Task<int> CountInteractiveElectronicOccupancyInSlotAsync(
            string cabinetName,
            string face,
            int row,
            int column,
            int excludeUnitId)
        {
            var units = await _relocationRepository.GetInUseElectronicArchiveUnitsInSlotForRelocationAsync(
                cabinetName,
                face,
                row,
                column);
            return units.Count(unit => unit.Id != excludeUnitId);
        }
    }
}
