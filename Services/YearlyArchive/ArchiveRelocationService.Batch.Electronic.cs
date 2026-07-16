using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        public Task<ArchiveRelocationPreview> PreviewBatchElectronicSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            return BuildBatchElectronicSlotPreviewAsync(request);
        }

        public async Task<ArchiveRelocationResult> ExecuteBatchElectronicSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            var preview = await BuildBatchElectronicSlotPreviewAsync(request);
            if (!preview.CanExecute)
            {
                return ArchiveRelocationResult.Fail(preview.BlockReason);
            }

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                var sourceUnits = await LoadValidatedSourceUnitsForBatchElectronicMoveAsync(request);
                DateTime operatedAt = DateTime.Now;
                string operatorName = ResolveOperatorName();
                string relocationNo = await GenerateRelocationNoAsync(ArchiveRegisterDomainValues.MediaKindElectronic, operatedAt.Year);
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
                string remark = $"档口批量搬迁：由 [{sourceSlotKey}] 迁至 [{targetSlotKey}]。";

                var context = new ArchiveRelocationExecutionContext
                {
                    SourceMediumDisposition = ArchiveRelocationSourceDisposition.None
                };

                int sequence = 1;
                foreach (var unit in sourceUnits)
                {
                    string newLocation = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                        request.TargetCabinetName,
                        request.TargetFace,
                        request.TargetRow,
                        request.TargetColumn,
                        sequence);

                    unit.StorageLocation = newLocation;
                    unit.UnitLifecycleStatus = ArchiveContainerLifecycleStatus.InUse;

                    await UpdateFilingFactsForPhysicalMoveAsync(
                        ArchiveRegisterDomainValues.MediaKindElectronic,
                        unit.Id,
                        newLocation,
                        operatedAt,
                        remark,
                        context.RelocationItems);

                    SyncLinkedHardDiskLedgerStorageLocation(unit, newLocation, operatedAt, remark, operatorName);
                    SyncLinkedOpticalDiscLedgerStorageLocation(unit, newLocation, operatedAt, remark, operatorName);

                    sequence++;
                }

                var firstUnit = sourceUnits[0];
                string targetContainerCode = string.Join("、", sourceUnits.Select(item => item.ElectronicArchiveNo));
                string targetStorageLocation = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                    request.TargetCabinetName,
                    request.TargetFace,
                    request.TargetRow,
                    request.TargetColumn,
                    1);

                var record = BuildBatchElectronicSlotRelocationRecord(
                    relocationNo,
                    firstUnit,
                    sourceSlotKey,
                    targetSlotKey,
                    sourceUnits.Count,
                    firstUnit.Id,
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
                await transaction.CommitAsync();

                return ArchiveRelocationResult.Ok(
                    relocationNo,
                    $"档口批量搬迁完成，共迁移 {sourceUnits.Count} 个电子介质袋。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<ArchiveRelocationPreview> BuildBatchElectronicSlotPreviewAsync(BatchSimulatedSlotPhysicalMoveRequest request)
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

            List<YearlyElectronicArchiveUnit> sourceUnits;
            try
            {
                sourceUnits = await LoadValidatedSourceUnitsForBatchElectronicMoveAsync(request);
            }
            catch (InvalidOperationException ex)
            {
                return Blocked(ex.Message);
            }

            string? targetIssue = await ValidateTargetMagneticDiskSlotForBatchMoveAsync(request, sourceUnits);
            if (!string.IsNullOrWhiteSpace(targetIssue))
            {
                return Blocked(targetIssue);
            }

            return Ready(
                $"【档口批量物理搬迁】将源档口 [{sourceSlotKey}] 内 {sourceUnits.Count} 个年度电子介质袋整体迁至空档口 [{targetSlotKey}]，按原顺序重排为 -01 至 -{sourceUnits.Count:D2}；关联硬盘/光盘台账存放位置将同步更新。",
                sourceUnits.Sum(unit => unit.MediaItemLinks.Count));
        }

        private async Task<List<YearlyElectronicArchiveUnit>> LoadValidatedSourceUnitsForBatchElectronicMoveAsync(
            BatchSimulatedSlotPhysicalMoveRequest request)
        {
            var sourceCabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(request.SourceCabinetName)
                ?? throw new InvalidOperationException($"未找到源防磁磁盘柜 [{request.SourceCabinetName}]。");

            string sourceSlotCode = $"{request.SourceRow}-{request.SourceColumn}";
            string? sourceCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                sourceCabinet.Id,
                request.SourceFace.Trim(),
                sourceSlotCode);
            string normalizedSourceCategory = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(sourceCategory);
            if (!CabinetHardDiskSlotCategoryAssignment.IsRelocatableDedicatedSlotCategory(normalizedSourceCategory))
            {
                throw new InvalidOperationException("源档口须为已设置专用类别的档口。");
            }

            var units = await _relocationRepository.GetInUseElectronicArchiveUnitsInSlotForRelocationAsync(
                request.SourceCabinetName,
                request.SourceFace,
                request.SourceRow,
                request.SourceColumn);

            if (units.Count == 0)
            {
                throw new InvalidOperationException("源档口内没有在用的电子介质袋。");
            }

            if (units.Any(unit => unit.MediaItemLinks.Count == 0))
            {
                throw new InvalidOperationException("源档口存在无资料子项的电子介质袋，请先整理后再批量搬迁。");
            }

            return OrderElectronicUnitsBySequence(units);
        }

        private async Task<string?> ValidateTargetMagneticDiskSlotForBatchMoveAsync(
            BatchSimulatedSlotPhysicalMoveRequest request,
            IReadOnlyList<YearlyElectronicArchiveUnit> sourceUnits)
        {
            var targetCabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(request.TargetCabinetName);
            if (targetCabinet == null)
            {
                return $"未找到目标防磁磁盘柜 [{request.TargetCabinetName}]。";
            }

            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            string targetSlotPrefix = targetSlotKey + "-";
            if (!await _filingRepository.IsMagneticDiskSlotFullyEmptyAsync(targetSlotKey, targetSlotPrefix))
            {
                return "目标档口已有介质占用，请选择全空档口。";
            }

            var sourceCabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(request.SourceCabinetName);
            if (sourceCabinet == null)
            {
                return $"未找到源防磁磁盘柜 [{request.SourceCabinetName}]。";
            }

            string sourceSlotCode = $"{request.SourceRow}-{request.SourceColumn}";
            string targetSlotCode = $"{request.TargetRow}-{request.TargetColumn}";
            string? sourceCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                sourceCabinet.Id,
                request.SourceFace.Trim(),
                sourceSlotCode);
            string? targetCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                targetCabinet.Id,
                request.TargetFace.Trim(),
                targetSlotCode);

            string normalizedSourceCategory = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(sourceCategory);
            string normalizedTargetCategory = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(targetCategory);
            if (!CabinetHardDiskSlotCategoryAssignment.MatchesCategory(normalizedTargetCategory, normalizedSourceCategory))
            {
                return $"目标档口专用类别须与源档口一致（源：{ResolveMagneticSlotCategoryDisplay(normalizedSourceCategory)}）。";
            }

            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(normalizedTargetCategory);
            if (sourceUnits.Count > slotCapacity)
            {
                return $"目标档口容量不足（需要 {sourceUnits.Count} 个盘位，档口容量 {slotCapacity} 个）。";
            }

            return null;
        }

        private static List<YearlyElectronicArchiveUnit> OrderElectronicUnitsBySequence(IReadOnlyList<YearlyElectronicArchiveUnit> units)
        {
            return units
                .OrderBy(unit => ResolveElectronicUnitSequenceIndex(ResolveElectronicUnitPhysicalStorageLocation(unit)))
                .ThenBy(unit => unit.ElectronicArchiveNo, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int ResolveElectronicUnitSequenceIndex(string? storageLocation)
        {
            return ArchiveSlotLocationSupport.TryParseSequenceIndex(storageLocation, out int sequenceIndex)
                ? sequenceIndex
                : int.MaxValue;
        }

        private static string ResolveMagneticSlotCategoryDisplay(string categoryName)
        {
            return string.IsNullOrWhiteSpace(categoryName) ? "未设置" : categoryName;
        }

        private static YearlyArchiveRelocationRecord BuildBatchElectronicSlotRelocationRecord(
            string relocationNo,
            YearlyElectronicArchiveUnit firstUnit,
            string sourceSlotKey,
            string targetSlotKey,
            int movedUnitCount,
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
                MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
                RelocationMode = ArchiveRelocationMode.BatchPhysicalMove,
                SourceContainerId = firstUnit.Id,
                SourceContainerCode = $"批量({movedUnitCount}袋)",
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
    }
}
