using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        private async Task<ArchiveRelocationPreview> BuildElectronicPreviewAsync(ElectronicRelocationRequest request)
        {
            var source = await _relocationRepository.GetElectronicUnitForRelocationAsync(request.SourceUnitId);
            if (source == null)
            {
                return Blocked("未找到源电子介质袋。");
            }

            if (string.Equals(source.UnitLifecycleStatus, ArchiveContainerLifecycleStatus.Disposed, StringComparison.Ordinal)
                && source.MediaItemLinks.Count == 0)
            {
                return Blocked("源电子介质袋已处置，无法迁档。");
            }

            if (source.MediaItemLinks.Count == 0
                && request.RelocationMode != ArchiveRelocationMode.PhysicalMove)
            {
                return Blocked("源电子介质袋内无立档资料，无法执行容器迁档。");
            }

            if (request.RelocationMode == ArchiveRelocationMode.PhysicalMove)
            {
                if (string.IsNullOrWhiteSpace(request.NewStorageLocation))
                {
                    return Blocked("请完整选择新的存放档口。");
                }

                string sourceLocation = ResolveElectronicUnitPhysicalStorageLocation(source);
                if (HardDiskLedgerSyncSupport.IsSameFullLocation(sourceLocation, request.NewStorageLocation))
                {
                    return Blocked("新位置与当前位置相同，无需迁移。");
                }

                string hardDiskSyncHint = IsHardDiskCarrier(source.StorageCarrierType)
                    ? "，关联硬盘台账存放位置将同步更新"
                    : string.Empty;
                string opticalDiscSyncHint = IsOpticalDiscCarrier(source.StorageCarrierType)
                    ? "，关联光盘台账存放位置将同步更新"
                    : string.Empty;

                return Ready(
                    $"【物理位置迁移】电子介质袋 [{source.ElectronicArchiveNo}] 将从 [{sourceLocation}] 迁至 [{request.NewStorageLocation.Trim()}]，资料子项 {source.MediaItemLinks.Count} 条保持不变{hardDiskSyncHint}{opticalDiscSyncHint}。",
                    source.MediaItemLinks.Count);
            }

            if (request.RelocationMode == ArchiveRelocationMode.MoveToEmpty)
            {
                if (!request.TargetBlankHardDiskMediumId.HasValue || request.TargetBlankHardDiskMediumId.Value <= 0)
                {
                    return Blocked("请选择拟迁入的空白硬盘。");
                }

                var targetMedium = await _filingRepository.GetHardDiskMediumByIdWithLedgerAsync(request.TargetBlankHardDiskMediumId.Value);
                if (targetMedium == null)
                {
                    return Blocked("未找到所选空白硬盘。");
                }

                string targetDiskCode = string.IsNullOrWhiteSpace(request.TargetBlankHardDiskCode)
                    ? targetMedium.DiskCode
                    : request.TargetBlankHardDiskCode.Trim();

                if (!string.Equals(targetMedium.DiskCode, targetDiskCode, StringComparison.OrdinalIgnoreCase))
                {
                    return Blocked("所选空白硬盘编号与介质记录不一致，请重新选择。");
                }

                if (!string.Equals(targetMedium.Ledger?.MediaStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
                {
                    return Blocked($"所选硬盘 [{targetDiskCode}] 当前不是在库空盘状态，不能作为迁入目标。");
                }

                string? targetUnitBlockReason = await ValidateBlankHardDiskTargetUnitAsync(source, targetMedium);
                if (!string.IsNullOrWhiteSpace(targetUnitBlockReason))
                {
                    return Blocked(targetUnitBlockReason);
                }

                bool sourceIsHardDisk = IsHardDiskCarrier(source.StorageCarrierType);
                bool sourceIsDisc = IsOpticalDiscCarrier(source.StorageCarrierType);

                if (request.ExecuteBackupMechanism)
                {
                    if (sourceIsHardDisk && source.MediumLinks.Any(link => link.HardDiskMediumId == targetMedium.Id))
                    {
                        return Blocked("拟迁入空白硬盘不能与源袋当前关联硬盘相同。");
                    }

                    string finalLocationBackup = await ResolveMoveToEmptyFinalStorageLocationAsync(
                        source,
                        string.IsNullOrWhiteSpace(request.NewStorageLocation)
                            ? source.StorageLocation
                            : request.NewStorageLocation);
                    request.NewStorageLocation = finalLocationBackup;

                    return Ready(
                        $"【保留原件·备份至空白硬盘】原件袋 [{source.ElectronicArchiveNo}] 与介质保留于 [{source.StorageLocation}] 不变；"
                        + $"将在 [{finalLocationBackup}] 新建备份袋并由空白硬盘 [{targetDiskCode}] 承载 {source.MediaItemLinks.Count} 条资料子项（逻辑备份，物理拷贝由人工完成）。",
                        source.MediaItemLinks.Count);
                }

                if (sourceIsHardDisk)
                {
                    if (string.IsNullOrWhiteSpace(request.SourceHardDiskReturnLocation))
                    {
                        return Blocked("请选择原硬盘放回位置。");
                    }

                    if (!request.ConfirmHardDiskFormatted)
                    {
                        return Blocked("请确认原硬盘已格式化并将按空盘管理。");
                    }

                    if (source.MediumLinks.Any(link => link.HardDiskMediumId == targetMedium.Id))
                    {
                        return Blocked("拟迁入空白硬盘不能与源袋当前关联硬盘相同。");
                    }
                }

                if (sourceIsDisc && !request.ConfirmOpticalDiscDestroyed)
                {
                    return Blocked("请确认原光盘已物理销毁。");
                }

                string finalLocation = await ResolveMoveToEmptyFinalStorageLocationAsync(
                    source,
                    string.IsNullOrWhiteSpace(request.NewStorageLocation)
                        ? source.StorageLocation
                        : request.NewStorageLocation);
                request.NewStorageLocation = finalLocation;

                string dispositionHint = sourceIsHardDisk
                    ? $"原硬盘将格式化后归位至 [{request.SourceHardDiskReturnLocation.Trim()}]。"
                    : sourceIsDisc
                        ? "原光盘将标记为已销毁。"
                        : string.Empty;

                bool keepOriginalSlot = string.Equals(finalLocation, source.StorageLocation?.Trim(), StringComparison.OrdinalIgnoreCase);
                string locationHint = keepOriginalSlot
                    ? $"电子介质袋编号 [{source.ElectronicArchiveNo}] 与档口 [{finalLocation}] 保持不变"
                    : $"电子介质袋编号 [{source.ElectronicArchiveNo}] 保持不变，档口调整为 [{finalLocation}]";

                return Ready(
                    $"【迁入空盘/空袋·换盘】{locationHint}，{source.MediaItemLinks.Count} 条资料子项将改由空白硬盘 [{targetDiskCode}] 承载。{dispositionHint}",
                    source.MediaItemLinks.Count);
            }

            if (request.RelocationMode != ArchiveRelocationMode.MergeToExisting)
            {
                return Blocked($"不支持的迁档模式：{request.RelocationMode}");
            }

            if (!request.TargetUnitId.HasValue || request.TargetUnitId.Value <= 0)
            {
                return Blocked("请选择目标电子介质袋。");
            }

            if (request.TargetUnitId.Value == source.Id)
            {
                return Blocked("目标电子介质袋不能与源袋相同。");
            }

            var target = await _relocationRepository.GetElectronicUnitForRelocationAsync(request.TargetUnitId.Value);
            if (target == null)
            {
                return Blocked("未找到目标电子介质袋。");
            }

            if (!string.Equals(target.ProjectName, source.ProjectName, StringComparison.Ordinal)
                || !string.Equals(target.Year, source.Year, StringComparison.Ordinal))
            {
                return Blocked("目标电子介质袋必须与源袋属于同一项目、同一年度。");
            }

            bool targetIsEmpty = target.MediaItemLinks.Count == 0;
            if (targetIsEmpty)
            {
                return Blocked("并入同项目硬盘模式下，目标应为本项目已用硬盘袋。");
            }

            string? hardDiskTargetBlockReason = ValidateHardDiskMergeTargetUnit(target);
            if (!string.IsNullOrWhiteSpace(hardDiskTargetBlockReason))
            {
                return Blocked(hardDiskTargetBlockReason);
            }

            bool sourceIsHardDiskMerge = IsHardDiskCarrier(source.StorageCarrierType);
            bool sourceIsDiscMerge = IsOpticalDiscCarrier(source.StorageCarrierType);

            if (request.ExecuteBackupMechanism)
            {
                string targetMediumCodeBackup = ResolveTargetMediumCode(target);
                return Ready(
                    $"【保留原件·备份并入同项目硬盘】原件袋 [{source.ElectronicArchiveNo}] 保留于 [{source.StorageLocation}]；"
                    + $"将 {source.MediaItemLinks.Count} 条资料子项备份副本并入 [{target.ElectronicArchiveNo}]（{target.StorageLocation}"
                    + $"{(string.IsNullOrWhiteSpace(targetMediumCodeBackup) ? "" : $" / 硬盘 {targetMediumCodeBackup}")}）。",
                    source.MediaItemLinks.Count);
            }

            if (sourceIsHardDiskMerge)
            {
                if (string.IsNullOrWhiteSpace(request.SourceHardDiskReturnLocation))
                {
                    return Blocked("请选择原硬盘放回位置。");
                }

                if (!request.ConfirmHardDiskFormatted)
                {
                    return Blocked("请确认原硬盘已格式化并将按空盘管理。");
                }
            }

            if (sourceIsDiscMerge && !request.ConfirmOpticalDiscDestroyed)
            {
                return Blocked("请确认原光盘已物理销毁。");
            }

            string dispositionHintMerge = sourceIsHardDiskMerge
                ? $"原硬盘将格式化后归位至 [{request.SourceHardDiskReturnLocation.Trim()}]。"
                : sourceIsDiscMerge
                    ? "原光盘将标记为已销毁。"
                    : "原介质袋将置空。";

            return Ready(
                $"【容器内容迁移·并入同项目硬盘】将电子介质袋 [{source.ElectronicArchiveNo}] 内 {source.MediaItemLinks.Count} 条资料子项迁至 [{target.ElectronicArchiveNo}]（{target.StorageLocation}）。{dispositionHintMerge}",
                source.MediaItemLinks.Count);
        }

        private async Task<ArchiveRelocationExecutionContext> ExecuteElectronicPhysicalMoveAsync(
            YearlyElectronicArchiveUnit source,
            ElectronicRelocationRequest request,
            DateTime operatedAt)
        {
            string newLocation = request.NewStorageLocation.Trim();
            string sourceLocation = ResolveElectronicUnitPhysicalStorageLocation(source);
            if (HardDiskLedgerSyncSupport.IsSameFullLocation(sourceLocation, newLocation))
            {
                throw new InvalidOperationException("新位置与当前位置相同，无需迁移。");
            }

            string remark = $"资料迁档：物理位置由 [{sourceLocation}] 迁至 [{newLocation}]。";
            source.StorageLocation = newLocation;
            source.UnitLifecycleStatus = ArchiveContainerLifecycleStatus.InUse;

            var context = new ArchiveRelocationExecutionContext
            {
                TargetContainerId = source.Id,
                TargetContainerCode = source.ElectronicArchiveNo,
                TargetStorageLocation = newLocation,
                SourceMediumDisposition = ArchiveRelocationSourceDisposition.None
            };

            await UpdateFilingFactsForPhysicalMoveAsync(
                ArchiveRegisterDomainValues.MediaKindElectronic,
                source.Id,
                newLocation,
                operatedAt,
                remark,
                context.RelocationItems);

            string operatorName = ResolveOperatorName();
            SyncLinkedHardDiskLedgerStorageLocation(
                source,
                newLocation,
                operatedAt,
                remark,
                operatorName);
            SyncLinkedOpticalDiscLedgerStorageLocation(
                source,
                newLocation,
                operatedAt,
                remark,
                operatorName);

            await _relocationRepository.SaveChangesAsync();
            return context;
        }

        private async Task<ArchiveRelocationExecutionContext> ExecuteElectronicContainerMoveAsync(
            YearlyElectronicArchiveUnit source,
            ElectronicRelocationRequest request,
            DateTime operatedAt,
            bool requireEmptyTarget)
        {
            int targetUnitId = request.TargetUnitId ?? throw new InvalidOperationException("未指定目标电子介质袋。");
            var target = await _relocationRepository.GetElectronicUnitForRelocationAsync(targetUnitId)
                ?? throw new InvalidOperationException("未找到目标电子介质袋。");

            bool targetIsEmpty = target.MediaItemLinks.Count == 0;
            if (requireEmptyTarget && !targetIsEmpty)
            {
                throw new InvalidOperationException("目标电子介质袋不为空。");
            }

            if (!requireEmptyTarget && targetIsEmpty)
            {
                throw new InvalidOperationException("并档目标电子介质袋不能为空。");
            }

            EnsureHardDiskMergeTargetUnit(target);

            if (requireEmptyTarget && !string.IsNullOrWhiteSpace(request.NewStorageLocation))
            {
                target.StorageLocation = request.NewStorageLocation.Trim();
            }

            string remark = requireEmptyTarget
                ? $"资料迁档：由电子介质袋 [{source.ElectronicArchiveNo}] 迁入空袋 [{target.ElectronicArchiveNo}]，存放于 [{target.StorageLocation}]。"
                : $"资料迁档：由电子介质袋 [{source.ElectronicArchiveNo}] 迁至 [{target.ElectronicArchiveNo}]。";
            var itemLinkIds = source.MediaItemLinks.Select(link => link.Id).ToList();
            string targetMediumCode = ResolveTargetMediumCode(target);

            foreach (var link in source.MediaItemLinks.ToList())
            {
                link.YearlyElectronicArchiveUnitId = target.Id;
                if (!string.IsNullOrWhiteSpace(targetMediumCode))
                {
                    link.MediumCode = targetMediumCode;
                }
            }

            foreach (var entryLink in source.MediaEntryLinks.ToList())
            {
                entryLink.YearlyElectronicArchiveUnitId = target.Id;
            }

            foreach (var record in source.RegisterRecords.ToList())
            {
                source.RegisterRecords.Remove(record);
                if (!target.RegisterRecords.Any(item => item.Id == record.Id))
                {
                    target.RegisterRecords.Add(record);
                }
            }

            target.UnitLifecycleStatus = ArchiveContainerLifecycleStatus.InUse;
            target.MediaCount = target.MediaItemLinks.Count;
            if (string.IsNullOrWhiteSpace(target.ContentSummary) && !string.IsNullOrWhiteSpace(source.ContentSummary))
            {
                target.ContentSummary = source.ContentSummary;
            }

            MergeLinkedMediumCodes(target, source);

            string disposition = await DisposeSourceElectronicMediumAsync(source, target, request, operatedAt, remark);

            source.UnitLifecycleStatus = ArchiveContainerLifecycleStatus.Relocated;
            source.MediaCount = 0;
            source.RegisterRecords.Clear();

            var context = new ArchiveRelocationExecutionContext
            {
                TargetContainerId = target.Id,
                TargetContainerCode = target.ElectronicArchiveNo,
                TargetStorageLocation = target.StorageLocation,
                SourceMediumDisposition = disposition
            };

            await UpdateFilingFactsForLinksAsync(
                FilingFactSourceLinkType.ElectronicMediaItemLink,
                itemLinkIds,
                target.ElectronicArchiveNo,
                target.StorageLocation,
                target.Id,
                operatedAt,
                remark,
                context.RelocationItems);

            foreach (var fact in await _relocationRepository.GetFilingFactsBySourceLinksAsync(
                         FilingFactSourceLinkType.ElectronicMediaItemLink,
                         itemLinkIds))
            {
                if (!string.IsNullOrWhiteSpace(targetMediumCode))
                {
                    fact.MediumCode = targetMediumCode;
                }

                fact.StorageCarrierType = target.StorageCarrierType;
            }

            await _relocationRepository.SaveChangesAsync();
            return context;
        }

        private static string ResolveTargetMediumCode(YearlyElectronicArchiveUnit target)
        {
            if (!string.IsNullOrWhiteSpace(target.LinkedMediumCodes))
            {
                return target.LinkedMediumCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
                    ?? target.LinkedMediumCodes.Trim();
            }

            return string.Empty;
        }

        private static void MergeLinkedMediumCodes(YearlyElectronicArchiveUnit target, YearlyElectronicArchiveUnit source)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string code in (target.LinkedMediumCodes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                codes.Add(code);
            }

            foreach (string code in (source.LinkedMediumCodes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                codes.Add(code);
            }

            target.LinkedMediumCodes = string.Join(",", codes.OrderBy(code => code, StringComparer.OrdinalIgnoreCase));
        }

        private async Task<string> DisposeSourceElectronicMediumAsync(
            YearlyElectronicArchiveUnit source,
            YearlyElectronicArchiveUnit target,
            ElectronicRelocationRequest request,
            DateTime operatedAt,
            string remark)
        {
            if (IsHardDiskCarrier(source.StorageCarrierType))
            {
                if (!request.ConfirmHardDiskFormatted)
                {
                    throw new InvalidOperationException("请先确认原硬盘已格式化。");
                }

                string formattedBlankLocation = !string.IsNullOrWhiteSpace(request.SourceHardDiskReturnLocation)
                    ? HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(request.SourceHardDiskReturnLocation.Trim())
                    : HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(source.StorageLocation);
                string operatorName = ResolveOperatorName();
                string relatedBatch = source.ElectronicArchiveNo.Trim();
                string relatedArchiveTitle = string.IsNullOrWhiteSpace(source.ContentSummary)
                    ? relatedBatch
                    : source.ContentSummary.Trim();

                foreach (var mediumLink in source.MediumLinks.ToList())
                {
                    var medium = mediumLink.HardDiskMedium;
                    if (medium == null)
                    {
                        continue;
                    }

                    FormatHardDiskMediumToBlank(
                        medium,
                        formattedBlankLocation,
                        operatedAt,
                        $"{remark} 原硬盘 [{medium.DiskCode}] 已格式化并归位至 [{formattedBlankLocation}]。",
                        operatorName,
                        relatedBatch,
                        relatedArchiveTitle);
                    source.MediumLinks.Remove(mediumLink);
                }

                return ArchiveRelocationSourceDisposition.HardDiskFormattedBlank;
            }

            if (IsOpticalDiscCarrier(source.StorageCarrierType))
            {
                if (!request.ConfirmOpticalDiscDestroyed)
                {
                    throw new InvalidOperationException("请先确认原光盘已物理销毁。");
                }

                foreach (var discLink in source.DiscLinks.ToList())
                {
                    var disc = discLink.OpticalDiscMedium;
                    if (disc == null)
                    {
                        continue;
                    }

                    MarkOpticalDiscDestroyed(disc, operatedAt, $"{remark} 原光盘 [{disc.DiscCode}] 已物理销毁。", ResolveOperatorName());
                    source.DiscLinks.Remove(discLink);
                }

                return ArchiveRelocationSourceDisposition.OpticalDiscDestroyed;
            }

            source.UnitLifecycleStatus = ArchiveContainerLifecycleStatus.Disposed;
            return ArchiveRelocationSourceDisposition.UnitRelocated;
        }

        private async Task<ArchiveRelocationExecutionContext> ExecuteElectronicMoveToEmptyAsync(
            YearlyElectronicArchiveUnit source,
            ElectronicRelocationRequest request,
            DateTime operatedAt)
        {
            int mediumId = request.TargetBlankHardDiskMediumId
                ?? throw new InvalidOperationException("未指定拟迁入空白硬盘。");

            var targetMedium = await _filingRepository.GetHardDiskMediumByIdWithLedgerAsync(mediumId)
                ?? throw new InvalidOperationException("未找到所选空白硬盘。");

            if (!string.Equals(targetMedium.Ledger?.MediaStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"硬盘 [{targetMedium.DiskCode}] 当前不是在库空盘状态。");
            }

            string finalLocation = await ResolveMoveToEmptyFinalStorageLocationAsync(
                source,
                string.IsNullOrWhiteSpace(request.NewStorageLocation)
                    ? source.StorageLocation
                    : request.NewStorageLocation);
            string newDiskCode = targetMedium.DiskCode.Trim();
            string remark = $"资料迁档：电子介质袋 [{source.ElectronicArchiveNo}] 换盘至空白硬盘 [{newDiskCode}]，存放于 [{finalLocation}]。";

            await DetachMediumFromOtherElectronicUnitsAsync(source.Id, targetMedium);

            string disposition = await ReplaceSourceElectronicMediumForMoveToEmptyAsync(
                source,
                targetMedium,
                request,
                finalLocation,
                operatedAt,
                remark);

            source.StorageLocation = finalLocation;
            source.LinkedMediumCodes = newDiskCode;
            source.UnitLifecycleStatus = ArchiveContainerLifecycleStatus.InUse;
            source.MediaCount = source.MediaItemLinks.Count;

            foreach (var link in source.MediaItemLinks)
            {
                link.MediumCode = newDiskCode;
            }

            if (!source.MediumLinks.Any(link => link.HardDiskMediumId == targetMedium.Id))
            {
                source.MediumLinks.Add(new YearlyElectronicArchiveUnitMediumLink
                {
                    YearlyElectronicArchiveUnitId = source.Id,
                    HardDiskMediumId = targetMedium.Id,
                    ElectronicArchiveUnit = source,
                    HardDiskMedium = targetMedium
                });
            }

            var blankDiskLedgerBefore = HardDiskLedgerSyncSupport.CaptureSnapshot(targetMedium);
            ConvertBlankHardDiskToDataCarrier(targetMedium, finalLocation, operatedAt);
            RecordBlankHardDiskToDataCarrierSync(
                targetMedium,
                blankDiskLedgerBefore,
                finalLocation,
                operatedAt,
                ResolveOperatorName(),
                remark,
                source.ElectronicArchiveNo.Trim(),
                string.IsNullOrWhiteSpace(source.ContentSummary)
                    ? source.ElectronicArchiveNo.Trim()
                    : source.ContentSummary.Trim());

            var context = new ArchiveRelocationExecutionContext
            {
                TargetContainerId = source.Id,
                TargetContainerCode = source.ElectronicArchiveNo,
                TargetStorageLocation = finalLocation,
                SourceMediumDisposition = disposition
            };

            await UpdateFilingFactsForDiskSwapAsync(
                source.Id,
                finalLocation,
                newDiskCode,
                operatedAt,
                remark,
                context.RelocationItems);

            request.TargetBlankHardDiskCode = newDiskCode;
            request.NewStorageLocation = finalLocation;
            await _relocationRepository.SaveChangesAsync();
            return context;
        }

        private async Task DetachMediumFromOtherElectronicUnitsAsync(int keepUnitId, HardDiskMedium medium)
        {
            var links = await _relocationRepository.GetElectronicMediumLinksByMediumIdAsync(medium.Id);
            foreach (var link in links.Where(item => item.YearlyElectronicArchiveUnitId != keepUnitId))
            {
                var unit = link.ElectronicArchiveUnit
                    ?? await _relocationRepository.GetElectronicUnitForRelocationAsync(link.YearlyElectronicArchiveUnitId);
                if (unit == null)
                {
                    continue;
                }

                unit.MediumLinks.RemoveAll(item => item.HardDiskMediumId == medium.Id);
                if (unit.MediaItemLinks.Count == 0)
                {
                    unit.LinkedMediumCodes = string.Empty;
                    unit.MediaCount = 0;
                    unit.UnitLifecycleStatus = ArchiveContainerLifecycleStatus.Disposed;
                }
            }
        }

        private Task<string> ReplaceSourceElectronicMediumForMoveToEmptyAsync(
            YearlyElectronicArchiveUnit source,
            HardDiskMedium newMedium,
            ElectronicRelocationRequest request,
            string finalStorageLocation,
            DateTime operatedAt,
            string remark)
        {
            if (IsHardDiskCarrier(source.StorageCarrierType))
            {
                if (!request.ConfirmHardDiskFormatted)
                {
                    throw new InvalidOperationException("请先确认原硬盘已格式化。");
                }

                string formattedBlankLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(
                    string.IsNullOrWhiteSpace(request.SourceHardDiskReturnLocation)
                        ? source.StorageLocation
                        : request.SourceHardDiskReturnLocation);
                string operatorName = ResolveOperatorName();
                string relatedBatch = source.ElectronicArchiveNo.Trim();
                string relatedArchiveTitle = string.IsNullOrWhiteSpace(source.ContentSummary)
                    ? relatedBatch
                    : source.ContentSummary.Trim();

                foreach (var mediumLink in source.MediumLinks.ToList())
                {
                    if (mediumLink.HardDiskMediumId == newMedium.Id)
                    {
                        continue;
                    }

                    var medium = mediumLink.HardDiskMedium;
                    if (medium == null)
                    {
                        continue;
                    }

                    FormatHardDiskMediumToBlank(
                        medium,
                        formattedBlankLocation,
                        operatedAt,
                        $"{remark} 原硬盘 [{medium.DiskCode}] 已格式化并归位至 [{formattedBlankLocation}]。",
                        operatorName,
                        relatedBatch,
                        relatedArchiveTitle);
                    source.MediumLinks.Remove(mediumLink);
                }

                return Task.FromResult(ArchiveRelocationSourceDisposition.HardDiskFormattedBlank);
            }

            if (IsOpticalDiscCarrier(source.StorageCarrierType))
            {
                if (!request.ConfirmOpticalDiscDestroyed)
                {
                    throw new InvalidOperationException("请先确认原光盘已物理销毁。");
                }

                foreach (var discLink in source.DiscLinks.ToList())
                {
                    var disc = discLink.OpticalDiscMedium;
                    if (disc == null)
                    {
                        continue;
                    }

                    MarkOpticalDiscDestroyed(disc, operatedAt, $"{remark} 原光盘 [{disc.DiscCode}] 已物理销毁。", ResolveOperatorName());
                    source.DiscLinks.Remove(discLink);
                }

                return Task.FromResult(ArchiveRelocationSourceDisposition.OpticalDiscDestroyed);
            }

            return Task.FromResult(ArchiveRelocationSourceDisposition.None);
        }

        private async Task<string?> ValidateBlankHardDiskTargetUnitAsync(
            YearlyElectronicArchiveUnit source,
            HardDiskMedium targetMedium)
        {
            var linkInfos = await _filingRepository.GetElectronicArchiveLinkInfosAsync([targetMedium.Id]);
            if (linkInfos.Count == 0)
            {
                return null;
            }

            foreach (var linkInfo in linkInfos)
            {
                if (linkInfo.ElectronicArchiveUnitId == source.Id)
                {
                    continue;
                }

                var linkedUnit = await _relocationRepository.GetElectronicUnitForRelocationAsync(linkInfo.ElectronicArchiveUnitId);
                if (linkedUnit == null)
                {
                    continue;
                }

                if (linkedUnit.MediaItemLinks.Count > 0)
                {
                    return $"空白硬盘 [{targetMedium.DiskCode}] 已关联电子袋 [{linkedUnit.ElectronicArchiveNo}]，且该袋内已有资料。";
                }

                if (!string.Equals(linkedUnit.ProjectName, source.ProjectName, StringComparison.Ordinal)
                    || !string.Equals(linkedUnit.Year, source.Year, StringComparison.Ordinal))
                {
                    return $"空白硬盘 [{targetMedium.DiskCode}] 关联的空袋与源袋不属于同一项目、同一年度。";
                }
            }

            return null;
        }

        private static void ConvertBlankHardDiskToDataCarrier(
            HardDiskMedium medium,
            string storageLocation,
            DateTime operatedAt)
        {
            var ledger = medium.Ledger ?? new HardDiskLedger
            {
                MediumId = medium.Id,
                DiskCode = medium.DiskCode,
                CreatedTime = operatedAt
            };

            if (medium.Ledger == null)
            {
                medium.Ledger = ledger;
            }

            ledger.MediaNature = HardDiskMedium.NatureDataCarrier;
            ledger.MediaStatus = HardDiskMedium.StatusInStockData;
            ledger.HolderOrOrganization = "资料室";
            ledger.NeedReturn = false;
            ledger.StorageLocation = storageLocation;
            ledger.UpdatedTime = operatedAt;
            medium.UpdatedTime = operatedAt;
            medium.RegisterLock = null;
        }

        private void RecordBlankHardDiskToDataCarrierSync(
            HardDiskMedium medium,
            HardDiskLedgerSyncSupport.LedgerSnapshot before,
            string storageLocation,
            DateTime operatedAt,
            string operatorName,
            string remark,
            string relatedBatch,
            string relatedArchiveTitle)
        {
            if (medium.Ledger == null
                || !HardDiskLedgerSyncSupport.HasLedgerMaterialChange(before, medium.Ledger))
            {
                return;
            }

            _filingRepository.AddHardDiskMediaTransaction(
                HardDiskLedgerSyncSupport.BuildSyncTransaction(
                    medium,
                    before,
                    operatorName,
                    operatedAt,
                    remark,
                    "资料迁档：空白硬盘转为数据盘并同步存放位置",
                    relatedBatch,
                    relatedArchiveTitle));
        }
    }
}
