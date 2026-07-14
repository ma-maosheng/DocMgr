using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        private async Task<ArchiveRelocationPreview> BuildSimulatedPreviewAsync(SimulatedRelocationRequest request)
        {
            var source = await _relocationRepository.GetArchiveBoxForRelocationAsync(request.SourceBoxId);
            if (source == null)
            {
                return Blocked("未找到源档案盒。");
            }

            if (!ArchiveContainerLifecycleStatus.OccupiesCabinet(source.ContainerLifecycleStatus)
                || source.MediaItemLinks.Count == 0)
            {
                return Blocked("源档案盒不可用或已无资料，无法迁档。");
            }

            if (request.RelocationMode == ArchiveRelocationMode.PhysicalMove)
            {
                if (string.IsNullOrWhiteSpace(request.NewStorageLocation))
                {
                    return Blocked("请完整选择新的存放档口。");
                }

                if (HardDiskLedgerSyncSupport.IsSameFullLocation(source.BoxLocationCode, request.NewStorageLocation))
                {
                    return Blocked("新位置与当前位置相同，无需迁移。");
                }

                if (request.MoveContentsToNewEmptyBox)
                {
                    if (string.IsNullOrWhiteSpace(request.NewBoxSpecification))
                    {
                        return Blocked("迁入空盒模式下，请选择新档案盒规格。");
                    }

                    return Ready(
                        $"【物理位置迁移·迁入空盒】将档案盒 [{source.ArchiveSequenceNo}] 内 {source.MediaItemLinks.Count} 条资料子项迁至新档口 [{request.NewStorageLocation.Trim()}] 的新建空盒（规格：{request.NewBoxSpecification.Trim()}），源档案盒将从柜内销号。",
                        source.MediaItemLinks.Count);
                }

                return Ready(
                    $"【物理位置迁移】档案盒 [{source.ArchiveSequenceNo}] 将从 [{source.BoxLocationCode}] 迁至 [{request.NewStorageLocation.Trim()}]，资料子项 {source.MediaItemLinks.Count} 条保持不变。",
                    source.MediaItemLinks.Count);
            }

            if (!request.TargetBoxId.HasValue || request.TargetBoxId.Value <= 0)
            {
                return Blocked("请选择目标档案盒。");
            }

            if (request.TargetBoxId.Value == source.Id)
            {
                return Blocked("目标档案盒不能与源档案盒相同。");
            }

            var target = await _relocationRepository.GetArchiveBoxForRelocationAsync(request.TargetBoxId.Value);
            if (target == null)
            {
                return Blocked("未找到目标档案盒。");
            }

            if (!ArchiveContainerLifecycleStatus.OccupiesCabinet(target.ContainerLifecycleStatus))
            {
                return Blocked("目标档案盒已销号，不能作为并档目标。");
            }

            if (!string.Equals(target.ProjectName, source.ProjectName, StringComparison.Ordinal)
                || !string.Equals(target.Year, source.Year, StringComparison.Ordinal))
            {
                return Blocked("目标档案盒必须与源档案盒属于同一项目、同一年度。");
            }

            bool targetIsEmpty = target.MediaItemLinks.Count == 0;

            if (request.RelocationMode == ArchiveRelocationMode.MergeToExisting && targetIsEmpty)
            {
                return Blocked("并档模式下，目标档案盒应为本项目已用档案盒。");
            }

            return Ready(
                $"【并入已有档案盒】将档案盒 [{source.ArchiveSequenceNo}] 内 {source.MediaItemLinks.Count} 条资料子项迁至 [{target.ArchiveSequenceNo}]（{target.BoxLocationCode}），源档案盒将从柜内销号。",
                source.MediaItemLinks.Count);
        }

        private async Task<ArchiveRelocationExecutionContext> ExecuteSimulatedPhysicalMoveAsync(
            YearlyArchiveBox source,
            SimulatedRelocationRequest request,
            DateTime operatedAt)
        {
            if (request.MoveContentsToNewEmptyBox)
            {
                return await ExecuteSimulatedPhysicalMoveToNewEmptyBoxAsync(source, request, operatedAt);
            }

            string newLocation = request.NewStorageLocation.Trim();
            if (HardDiskLedgerSyncSupport.IsSameFullLocation(source.BoxLocationCode, newLocation))
            {
                throw new InvalidOperationException("新位置与当前位置相同，无需迁移。");
            }

            string operatorName = ResolveOperatorName();
            string remark = $"资料迁档：物理位置由 [{source.BoxLocationCode}] 迁至 [{newLocation}]。";

            if (!request.NewRow.HasValue || !request.NewColumn.HasValue)
            {
                throw new InvalidOperationException("请选择有效的柜位信息。");
            }

            ApplySimulatedBoxPhysicalLocation(
                source,
                newLocation,
                request.NewCabinetName,
                request.NewSide,
                request.NewRow.Value,
                request.NewColumn.Value,
                request.NewBoxIndex ?? source.BoxIndex,
                operatedAt,
                operatorName);

            var context = new ArchiveRelocationExecutionContext
            {
                TargetContainerId = source.Id,
                TargetContainerCode = source.ArchiveSequenceNo,
                TargetStorageLocation = newLocation,
                SourceMediumDisposition = ArchiveRelocationSourceDisposition.None
            };

            await UpdateFilingFactsForPhysicalMoveAsync(
                ArchiveRegisterDomainValues.MediaKindSimulated,
                source.Id,
                newLocation,
                operatedAt,
                remark,
                context.RelocationItems);

            await _relocationRepository.SaveChangesAsync();
            return context;
        }

        private async Task<ArchiveRelocationExecutionContext> ExecuteSimulatedPhysicalMoveToNewEmptyBoxAsync(
            YearlyArchiveBox source,
            SimulatedRelocationRequest request,
            DateTime operatedAt)
        {
            string newLocation = request.NewStorageLocation.Trim();
            if (string.IsNullOrWhiteSpace(newLocation))
            {
                throw new InvalidOperationException("请完整选择新的存放档口。");
            }

            if (string.IsNullOrWhiteSpace(request.NewBoxSpecification))
            {
                throw new InvalidOperationException("请选择新档案盒规格。");
            }

            if (!request.NewRow.HasValue || !request.NewColumn.HasValue)
            {
                throw new InvalidOperationException("请选择有效的柜位信息。");
            }

            string operatorName = ResolveOperatorName();
            string archiveSequenceNo = await GenerateNextArchiveSequenceNoAsync(source.Year);
            var newBox = new YearlyArchiveBox
            {
                ArchiveSequenceNo = archiveSequenceNo,
                BoxLocationCode = newLocation,
                CabinetName = request.NewCabinetName.Trim(),
                Side = request.NewSide.Trim(),
                Row = request.NewRow.Value,
                Column = request.NewColumn.Value,
                BoxIndex = request.NewBoxIndex ?? 1,
                ProjectName = source.ProjectName,
                Year = source.Year,
                Specs = request.NewBoxSpecification.Trim(),
                PlacementMode = source.PlacementMode,
                ArchivedBy = operatorName,
                ArchivedDate = operatedAt,
                ContainerLifecycleStatus = ArchiveContainerLifecycleStatus.InUse,
                Remarks = $"由迁档自 [{source.ArchiveSequenceNo}] 迁入空盒创建。"
            };

            _filingRepository.AddArchiveBox(newBox);
            await _relocationRepository.SaveChangesAsync();

            string remark = $"资料迁档：由档案盒 [{source.ArchiveSequenceNo}] 迁入新档口空盒 [{archiveSequenceNo}]（{newLocation}）。";
            var linkIds = source.MediaItemLinks.Select(link => link.Id).ToList();

            foreach (var link in source.MediaItemLinks.ToList())
            {
                link.YearlyArchiveBoxId = newBox.Id;
            }

            foreach (var record in source.RegisterRecords.ToList())
            {
                source.RegisterRecords.Remove(record);
                if (!newBox.RegisterRecords.Any(item => item.Id == record.Id))
                {
                    newBox.RegisterRecords.Add(record);
                }
            }

            UpsertArchiveBoxPlacement(newBox, operatedAt, operatorName);
            RetireSimulatedSourceBox(source, operatedAt, operatorName);

            var context = new ArchiveRelocationExecutionContext
            {
                TargetContainerId = newBox.Id,
                TargetContainerCode = newBox.ArchiveSequenceNo,
                TargetStorageLocation = newLocation,
                SourceMediumDisposition = ArchiveRelocationSourceDisposition.BoxRetired
            };

            await UpdateFilingFactsForLinksAsync(
                FilingFactSourceLinkType.BoxMediaItemLink,
                linkIds,
                newBox.ArchiveSequenceNo,
                newLocation,
                newBox.Id,
                operatedAt,
                remark,
                context.RelocationItems);

            await _relocationRepository.SaveChangesAsync();
            return context;
        }

        private async Task<ArchiveRelocationExecutionContext> ExecuteSimulatedContainerMoveAsync(
            YearlyArchiveBox source,
            SimulatedRelocationRequest request,
            DateTime operatedAt,
            bool requireEmptyTarget)
        {
            int targetBoxId = request.TargetBoxId ?? throw new InvalidOperationException("未指定目标档案盒。");
            var target = await _relocationRepository.GetArchiveBoxForRelocationAsync(targetBoxId)
                ?? throw new InvalidOperationException("未找到目标档案盒。");

            if (!ArchiveContainerLifecycleStatus.OccupiesCabinet(target.ContainerLifecycleStatus))
            {
                throw new InvalidOperationException("目标档案盒已销号。");
            }

            bool targetIsEmpty = target.MediaItemLinks.Count == 0;

            if (requireEmptyTarget && !targetIsEmpty)
            {
                throw new InvalidOperationException("目标档案盒不为空。");
            }

            if (!requireEmptyTarget && targetIsEmpty)
            {
                throw new InvalidOperationException("并档目标档案盒不能为空。");
            }

            string operatorName = ResolveOperatorName();
            string remark = $"资料迁档：由档案盒 [{source.ArchiveSequenceNo}] 迁至 [{target.ArchiveSequenceNo}]。";
            var linkIds = source.MediaItemLinks.Select(link => link.Id).ToList();

            foreach (var link in source.MediaItemLinks.ToList())
            {
                link.YearlyArchiveBoxId = target.Id;
            }

            foreach (var record in source.RegisterRecords.ToList())
            {
                source.RegisterRecords.Remove(record);
                if (!target.RegisterRecords.Any(item => item.Id == record.Id))
                {
                    target.RegisterRecords.Add(record);
                }
            }

            target.ContainerLifecycleStatus = ArchiveContainerLifecycleStatus.InUse;
            UpsertArchiveBoxPlacement(target, operatedAt, operatorName);
            RetireSimulatedSourceBox(source, operatedAt, operatorName);

            var context = new ArchiveRelocationExecutionContext
            {
                TargetContainerId = target.Id,
                TargetContainerCode = target.ArchiveSequenceNo,
                TargetStorageLocation = target.BoxLocationCode,
                SourceMediumDisposition = ArchiveRelocationSourceDisposition.BoxRetired
            };

            await UpdateFilingFactsForLinksAsync(
                FilingFactSourceLinkType.BoxMediaItemLink,
                linkIds,
                target.ArchiveSequenceNo,
                target.BoxLocationCode,
                target.Id,
                operatedAt,
                remark,
                context.RelocationItems);

            await _relocationRepository.SaveChangesAsync();
            return context;
        }

        private void RetireSimulatedSourceBox(
            YearlyArchiveBox source,
            DateTime operatedAt,
            string operatorName)
        {
            string lastLocation = source.BoxLocationCode?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(lastLocation))
            {
                _filingRepository.RemoveArchiveBoxPlacementByBoxCode(lastLocation);
            }

            source.LastStorageLocation = lastLocation;
            source.RetiredAt = operatedAt;
            source.RetiredBy = operatorName;
            source.ContainerLifecycleStatus = ArchiveContainerLifecycleStatus.Retired;
            source.RegisterRecords.Clear();
            source.BoxLocationCode = string.Empty;
            source.CabinetName = string.Empty;
            source.Side = string.Empty;
            source.Row = 0;
            source.Column = 0;
            source.BoxIndex = 0;
        }

        private async Task<string> GenerateNextArchiveSequenceNoAsync(string year)
        {
            string normalizedYear = string.IsNullOrWhiteSpace(year) ? DateTime.Now.Year.ToString() : year.Trim();
            string prefix = $"年度模拟-{normalizedYear}-";
            var lastBox = await _filingRepository.GetLastArchiveBoxByPrefixAsync(prefix);
            int nextSeq = 1;
            if (lastBox != null)
            {
                string suffix = lastBox.ArchiveSequenceNo.Substring(prefix.Length);
                if (int.TryParse(suffix, out int current))
                {
                    nextSeq = current + 1;
                }
            }

            return $"{prefix}{nextSeq:D3}";
        }

        private static ArchiveRelocationPreview Blocked(string reason)
            => new() { CanExecute = false, BlockReason = reason, SummaryText = reason };

        private static ArchiveRelocationPreview Ready(string summary, int affectedCount)
            => new() { CanExecute = true, SummaryText = summary, AffectedItemCount = affectedCount };
    }
}
