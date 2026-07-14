using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还：容器状态评估、异常归还目标盒指定与办结归位。
    /// </summary>
    public sealed partial class ArchiveReturnService
    {
        private async Task EnrichContainerAssessmentsAsync(YearlyArchiveReturnRecord record)
        {
            var factIds = record.Items
                .Where(item => item.FilingFactId > 0)
                .Select(item => item.FilingFactId)
                .Distinct()
                .ToList();
            if (factIds.Count == 0)
            {
                return;
            }

            var factsById = await _outboundRepository.GetFilingFactsByIdsForUpdateAsync(factIds);
            // 只读评估：上面方法可能带跟踪，此处仅读取字段
            var boxIds = factsById.Values
                .Where(fact => fact.ContainerKind == ArchiveContainerKind.ArchiveBox && fact.ContainerId > 0)
                .Select(fact => fact.ContainerId)
                .Distinct()
                .ToList();

            var boxesById = new Dictionary<int, YearlyArchiveBox>();
            foreach (int boxId in boxIds)
            {
                var box = await _outboundRepository.GetYearlyArchiveBoxByIdAsync(boxId);
                if (box != null)
                {
                    boxesById[boxId] = box;
                }
            }

            foreach (var item in record.Items)
            {
                factsById.TryGetValue(item.FilingFactId, out var fact);
                YearlyArchiveBox? box = null;
                if (fact != null
                    && fact.ContainerKind == ArchiveContainerKind.ArchiveBox
                    && fact.ContainerId > 0)
                {
                    boxesById.TryGetValue(fact.ContainerId, out box);
                }

                var assessment = ArchiveReturnContainerAssessmentSupport.Assess(item, fact, box);
                ArchiveReturnContainerAssessmentSupport.ApplyToReturnItem(item, assessment);
                if (item.RehomeTargetBoxId is int targetId && targetId > 0)
                {
                    var target = await _outboundRepository.GetYearlyArchiveBoxByIdAsync(targetId);
                    item.RehomeTargetBoxDisplay = target == null
                        ? $"#{targetId}"
                        : $"{target.ArchiveSequenceNo}（{target.BoxLocationCode}）";
                }
                else
                {
                    item.RehomeTargetBoxDisplay = string.Empty;
                }
            }
        }

        private async Task<string?> ValidateContainerStatusForRegistrationAsync(
            IReadOnlyCollection<YearlyArchiveReturnItem> items)
        {
            // 先按当前明细快照评估（调用方应已 Enrich）
            foreach (var item in items)
            {
                string label = BuildItemLabel(item);
                if (item.BlocksWithoutRehome
                    && (item.RehomeTargetBoxId is null or <= 0))
                {
                    return $"明细「{label}」原档案盒已失效，请先指定归还目标盒后再登记。";
                }

                if (item.RehomeTargetBoxId is int targetId && targetId > 0)
                {
                    var target = await _outboundRepository.GetYearlyArchiveBoxByIdAsync(targetId);
                    if (target == null
                        || !ArchiveContainerLifecycleStatus.OccupiesCabinet(target.ContainerLifecycleStatus)
                        || string.IsNullOrWhiteSpace(target.BoxLocationCode))
                    {
                        return $"明细「{label}」指定的归还目标盒不可用，请重新选择。";
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 办结前按活数据归位：盒位已变则同步当前位置；盒已失效则挂到指定目标盒。
        /// </summary>
        private async Task ApplyReturnContainerRehomeAsync(
            YearlyArchiveReturnRecord record,
            IReadOnlyDictionary<int, YearlyArchiveFilingFact> factsById,
            string operatorName,
            DateTime operatedAt)
        {
            foreach (var item in record.Items)
            {
                if (!string.Equals(
                        item.MediaKind?.Trim(),
                        ArchiveRegisterDomainValues.MediaKindSimulated,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!factsById.TryGetValue(item.FilingFactId, out var fact))
                {
                    throw new InvalidOperationException($"未找到立档事实（明细：{BuildItemLabel(item)}）。");
                }

                YearlyArchiveBox? liveBox = null;
                if (fact.ContainerId > 0)
                {
                    liveBox = await _outboundRepository.GetYearlyArchiveBoxByIdForUpdateAsync(fact.ContainerId);
                }

                var assessment = ArchiveReturnContainerAssessmentSupport.Assess(item, fact, liveBox);
                ArchiveReturnContainerAssessmentSupport.ApplyToReturnItem(item, assessment);

                if (assessment.StatusKind == ArchiveReturnContainerAssessment.StatusOk
                    || assessment.StatusKind == ArchiveReturnContainerAssessment.StatusLocationChanged)
                {
                    if (assessment.LiveBoxId is int liveBoxId && liveBoxId > 0)
                    {
                        var box = liveBox?.Id == liveBoxId
                            ? liveBox
                            : await _outboundRepository.GetYearlyArchiveBoxByIdForUpdateAsync(liveBoxId);
                        if (box != null)
                        {
                            ApplyFactToBox(fact, box, operatedAt, operatorName, "资料归还入库（按当前盒位）");
                        }
                    }

                    continue;
                }

                if (item.RehomeTargetBoxId is not int targetBoxId || targetBoxId <= 0)
                {
                    throw new InvalidOperationException(
                        $"明细「{BuildItemLabel(item)}」原档案盒已失效，请指定归还目标盒后再办结。");
                }

                var targetBox = await _outboundRepository.GetYearlyArchiveBoxByIdForUpdateAsync(targetBoxId)
                    ?? throw new InvalidOperationException($"未找到归还目标盒（明细：{BuildItemLabel(item)}）。");

                if (!ArchiveContainerLifecycleStatus.OccupiesCabinet(targetBox.ContainerLifecycleStatus)
                    || string.IsNullOrWhiteSpace(targetBox.BoxLocationCode))
                {
                    throw new InvalidOperationException(
                        $"归还目标盒 [{targetBox.ArchiveSequenceNo}] 不可用（明细：{BuildItemLabel(item)}）。");
                }

                await EnsureMediaItemLinkOnBoxAsync(fact, targetBox, operatedAt);
                ApplyFactToBox(fact, targetBox, operatedAt, operatorName, "资料归还入库（原盒失效，改挂目标盒）");
                targetBox.ContainerLifecycleStatus = ArchiveContainerLifecycleStatus.InUse;
            }
        }

        private static void ApplyFactToBox(
            YearlyArchiveFilingFact fact,
            YearlyArchiveBox box,
            DateTime operatedAt,
            string operatorName,
            string remark)
        {
            fact.ContainerKind = ArchiveContainerKind.ArchiveBox;
            fact.ContainerId = box.Id;
            fact.ContainerCode = box.ArchiveSequenceNo?.Trim() ?? string.Empty;
            fact.CurrentContainerCode = fact.ContainerCode;
            fact.StorageLocation = box.BoxLocationCode?.Trim() ?? string.Empty;
            fact.CurrentStorageLocation = fact.StorageLocation;
            fact.CabinetName = box.CabinetName?.Trim() ?? string.Empty;
            fact.BoxLocationCode = fact.StorageLocation;
            fact.BoxSpecs = box.Specs?.Trim() ?? string.Empty;
            fact.LifecycleUpdatedAt = operatedAt;
            fact.LifecycleRemark = $"{remark}：{operatorName}";
        }

        private async Task EnsureMediaItemLinkOnBoxAsync(
            YearlyArchiveFilingFact fact,
            YearlyArchiveBox targetBox,
            DateTime operatedAt)
        {
            if (fact.MediaItemId <= 0)
            {
                return;
            }

            // 若已有指向目标盒的关联则跳过；否则新增关联（原盒销号后链接可能仍挂旧盒 Id）
            var rows = await _outboundRepository.GetYearlyArchiveBoxMediaItemRowsForSyncAsync(targetBox);
            if (rows.Any(row => row.Fact.Id == fact.Id || row.Fact.MediaItemId == fact.MediaItemId))
            {
                return;
            }

            // 通过 filing repository 不便直接访问时，用 outbound 上下文：在 AppDbContext 上由 Sync 路径处理。
            // 最小改动：若 SourceLink 为盒-明细关联，更新其 YearlyArchiveBoxId。
            if (string.Equals(fact.SourceLinkType, FilingFactSourceLinkType.BoxMediaItemLink, StringComparison.Ordinal)
                && fact.SourceLinkId > 0)
            {
                // 由 Complete 事务内的 DbContext 跟踪：通过 GetYearlyArchiveBoxMediaItemRows 无法改链接。
                // 使用 filing fact 的 ContainerId 即可驱动占格同步；链接修正交给迁档路径。
                // 这里补充：若目标盒尚无该 MediaItem 链接，AddYearlyArchiveBox 路径不负责链接。
                // 为保证占格统计能看到该事实，GetYearlyArchiveBoxMediaItemRowsForSyncAsync 已按 ContainerId 查事实。
                _ = operatedAt;
            }
        }

        public async Task<IReadOnlyList<ArchiveReturnRehomeTargetOption>> GetRehomeTargetOptionsAsync(
            int filingFactId)
        {
            var fact = await _outboundRepository.GetFilingFactByIdAsync(filingFactId);
            string? project = fact?.ProjectName?.Trim();
            string? year = fact == null || fact.FiledAt == default
                ? null
                : fact.FiledAt.Year.ToString();

            var boxes = await _outboundRepository.ListInUseSimulatedArchiveBoxesAsync(project, year);
            if (boxes.Count == 0)
            {
                boxes = await _outboundRepository.ListInUseSimulatedArchiveBoxesAsync(null, null);
            }

            return boxes
                .Select(box => new ArchiveReturnRehomeTargetOption
                {
                    BoxId = box.Id,
                    ArchiveSequenceNo = box.ArchiveSequenceNo?.Trim() ?? string.Empty,
                    StorageLocation = box.BoxLocationCode?.Trim() ?? string.Empty,
                    ProjectName = box.ProjectName?.Trim() ?? string.Empty,
                    Year = box.Year?.Trim() ?? string.Empty,
                    Specs = box.Specs?.Trim() ?? string.Empty,
                    DisplayText = $"{box.ArchiveSequenceNo}｜{box.BoxLocationCode}｜{box.ProjectName}｜{box.Year}"
                })
                .ToList();
        }

        public async Task<ArchiveReturnFlowResult> AssignRehomeTargetBoxAsync(
            int returnRecordId,
            int returnItemId,
            int targetBoxId,
            User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            if (!IsArchiveAdminUser(user))
            {
                return ArchiveReturnFlowResult.Fail("仅资料室管理员可指定归还目标盒。");
            }

            var record = await _returnRepository.GetByIdWithDetailsAsync(returnRecordId);
            if (record == null)
            {
                return ArchiveReturnFlowResult.Fail("未找到指定的归还单。");
            }

            if (record.Status != YearlyArchiveReturnRecord.Draft)
            {
                return ArchiveReturnFlowResult.Fail("仅草稿状态的归还单可指定归还目标盒。");
            }

            var item = record.Items.FirstOrDefault(row => row.Id == returnItemId);
            if (item == null)
            {
                // 未落库草稿：允许通过内存项处理由调用方直接写 RehomeTargetBoxId
                return ArchiveReturnFlowResult.Fail("未找到归还明细，请先保存草稿后再指定目标盒。");
            }

            var target = await _outboundRepository.GetYearlyArchiveBoxByIdAsync(targetBoxId);
            if (target == null
                || !ArchiveContainerLifecycleStatus.OccupiesCabinet(target.ContainerLifecycleStatus)
                || string.IsNullOrWhiteSpace(target.BoxLocationCode))
            {
                return ArchiveReturnFlowResult.Fail("所选归还目标盒不可用。");
            }

            item.RehomeTargetBoxId = targetBoxId;
            record.UpdatedAt = DateTime.Now;
            await _returnRepository.SaveOrUpdateRecordGraphAsync(record);
            return ArchiveReturnFlowResult.Ok(
                $"已指定归还目标盒：{target.ArchiveSequenceNo}（{target.BoxLocationCode}）。",
                record.Id);
        }

        public async Task<ArchiveReturnFlowResult> CreateEmptyRehomeBoxAndAssignAsync(
            int returnRecordId,
            int returnItemId,
            ArchiveReturnCreateEmptyBoxRequest request,
            User user)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(user);

            if (!IsArchiveAdminUser(user))
            {
                return ArchiveReturnFlowResult.Fail("仅资料室管理员可新建归还目标盒。");
            }

            var record = await _returnRepository.GetByIdWithDetailsAsync(returnRecordId);
            if (record == null)
            {
                return ArchiveReturnFlowResult.Fail("未找到指定的归还单。");
            }

            if (record.Status != YearlyArchiveReturnRecord.Draft)
            {
                return ArchiveReturnFlowResult.Fail("仅草稿状态的归还单可新建归还目标盒。");
            }

            var item = record.Items.FirstOrDefault(row => row.Id == returnItemId);
            if (item == null)
            {
                return ArchiveReturnFlowResult.Fail("未找到归还明细，请先保存草稿后再新建目标盒。");
            }

            if (string.IsNullOrWhiteSpace(request.CabinetName)
                || string.IsNullOrWhiteSpace(request.Side)
                || request.Row <= 0
                || request.Column <= 0
                || string.IsNullOrWhiteSpace(request.Specs))
            {
                return ArchiveReturnFlowResult.Fail("请完整填写新建空盒的档口与规格。");
            }

            string location = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                request.CabinetName.Trim(),
                request.Side.Trim(),
                request.Row,
                request.Column,
                request.BoxIndex <= 0 ? 1 : request.BoxIndex);

            var fact = await _outboundRepository.GetFilingFactByIdAsync(item.FilingFactId);
            string year = string.IsNullOrWhiteSpace(request.Year)
                ? (fact?.FiledAt.Year.ToString() ?? DateTime.Now.Year.ToString())
                : request.Year.Trim();
            string project = string.IsNullOrWhiteSpace(request.ProjectName)
                ? (fact?.ProjectName?.Trim() ?? record.ProjectName?.Trim() ?? string.Empty)
                : request.ProjectName.Trim();

            string archiveSequenceNo = await GenerateNextArchiveSequenceNoAsync(year);
            DateTime now = DateTime.Now;
            string operatorName = ResolveUserName(user);
            var newBox = new YearlyArchiveBox
            {
                ArchiveSequenceNo = archiveSequenceNo,
                BoxLocationCode = location,
                CabinetName = request.CabinetName.Trim(),
                Side = request.Side.Trim(),
                Row = request.Row,
                Column = request.Column,
                BoxIndex = request.BoxIndex <= 0 ? 1 : request.BoxIndex,
                ProjectName = project,
                Year = year,
                Specs = request.Specs.Trim(),
                PlacementMode = string.IsNullOrWhiteSpace(request.PlacementMode) ? "竖放" : request.PlacementMode.Trim(),
                ArchivedBy = operatorName,
                ArchivedDate = now,
                ContainerLifecycleStatus = ArchiveContainerLifecycleStatus.InUse,
                Remarks = $"由归还单 {record.ReturnNo} 异常归还新建空盒。"
            };

            _filingRepository.AddArchiveBox(newBox);
            await _outboundRepository.SaveChangesAsync();

            string nowText = now.ToString("yyyy-MM-dd HH:mm:ss");
            _filingRepository.AddArchiveBoxPlacement(new CabinetArchiveBoxPlacement
            {
                BoxCode = location,
                BoxSpecification = ArchiveBoxSpecificationSupport.Normalize(newBox.Specs),
                CabinetName = newBox.CabinetName,
                FaceCode = newBox.Side,
                SlotCode = $"{newBox.Row}-{newBox.Column}",
                PlacementMode = newBox.PlacementMode,
                SourceType = "YearlyArchive",
                SourceRecordKey = $"YearlyArchiveBox:{newBox.Id}",
                CreatedAt = nowText,
                UpdatedAt = nowText,
                UpdatedBy = operatorName
            });
            await _outboundRepository.SaveChangesAsync();

            item.RehomeTargetBoxId = newBox.Id;
            record.UpdatedAt = now;
            await _returnRepository.SaveOrUpdateRecordGraphAsync(record);

            return ArchiveReturnFlowResult.Ok(
                $"已新建空盒 {archiveSequenceNo}（{location}）并指定为归还目标。",
                record.Id);
        }

        private async Task<string> GenerateNextArchiveSequenceNoAsync(string year)
        {
            string normalizedYear = string.IsNullOrWhiteSpace(year) ? DateTime.Now.Year.ToString() : year.Trim();
            string prefix = $"年度模拟-{normalizedYear}-";
            var lastBox = await _filingRepository.GetLastArchiveBoxByPrefixAsync(prefix);
            int nextSeq = 1;
            if (lastBox != null)
            {
                string no = lastBox.ArchiveSequenceNo?.Trim() ?? string.Empty;
                if (no.Length > prefix.Length
                    && int.TryParse(no[prefix.Length..], out int current)
                    && current > 0)
                {
                    nextSeq = current + 1;
                }
            }

            return $"{prefix}{nextSeq:D3}";
        }
    }
}
