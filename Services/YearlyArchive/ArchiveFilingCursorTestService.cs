using DocMgr.Models.Cabinets;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 立档测试（Cursor 版）：按资料立档操作台同一套场景决策组装请求，预览+提交后核对硬盘台账同步。
    /// </summary>
    public sealed class ArchiveFilingCursorTestService : IArchiveFilingCursorTestService
    {
        private const string SimulationMarker = "[模拟登记]";
        private const string LegacySimulationMaterialPrefix = "模拟单-";
        private const string ChecklistMarker = "[立档测试_cursor]";

        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IArchiveFilingService _archiveFilingService;
        private readonly IArchiveFilingRepository _archiveFilingRepository;
        private readonly ArchiveFilingElectronicSubmissionRequestBuilder _electronicSubmissionRequestBuilder;

        public ArchiveFilingCursorTestService(
            IArchiveRegisterService archiveRegisterService,
            IArchiveFilingService archiveFilingService,
            IArchiveFilingRepository archiveFilingRepository,
            ArchiveFilingElectronicSubmissionRequestBuilder electronicSubmissionRequestBuilder)
        {
            _archiveRegisterService = archiveRegisterService;
            _archiveFilingService = archiveFilingService;
            _archiveFilingRepository = archiveFilingRepository;
            _electronicSubmissionRequestBuilder = electronicSubmissionRequestBuilder;
        }

        /// <inheritdoc />
        public async Task<ArchiveFilingAutomationResult> RunCursorFilingTestAsync(User? operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);
            EnsureArchiveAdminUser(operatorUser);

            var usedBlankHardDiskCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reservedDedicatedFullLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var lines = new List<string>
            {
                "【立档测试_cursor】",
                ChecklistMarker,
                $"执行时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                "策略说明：",
                "  · 与资料立档操作台一致：ResolveElectronicArchiveUiDecision + 新建袋提交请求组装。",
                "  · 与界面一致：同一电子介质条目下，全部未立档子项一次入袋。",
                "  · 流程：预览 → 提交（同 SubmitNewElectronicArchiveUnitAsync）→ 核对 DatabaseChanges 与硬盘台账。",
                "  · 拷贝入光盘袋场景：物理硬盘台账可不变更（业务设计如此）；借出留存直接入袋须写入归还登记。",
                $"  · 专用档口容量：硬盘 {CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity} 盘/档口，光盘 {CabinetHardDiskSlotCategoryAssignment.DedicatedOpticalDiscSlotCapacity} 盘/档口；满档自动切换下一专用档口。",
                string.Empty
            };

            var electronicRecords = (await _archiveFilingService.GetPendingElectronicRecordsAsync())
                .Where(IsSimulationRecord)
                .OrderBy(record => record.FormNo, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var simulatedRecords = (await _archiveFilingService.GetPendingSimulatedRecordsAsync())
                .Where(IsSimulationRecord)
                .OrderBy(record => record.FormNo, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (electronicRecords.Count == 0 && simulatedRecords.Count == 0)
            {
                lines.Add("结果：未找到可执行的模拟登记待立档数据（需已办结且含未立档明细）。");
                return new ArchiveFilingAutomationResult(0, 0, 0, lines);
            }

            int electronicEntrySuccess = 0;
            int electronicEntryFailure = 0;
            int electronicRecordSucceeded = 0;
            int electronicRecordFailed = 0;
            int simulatedRecordSucceeded = 0;
            int simulatedRecordFailed = 0;

            if (electronicRecords.Count > 0)
            {
                lines.Add($"—— 电子介质立档（{electronicRecords.Count} 单）——");
                foreach (var record in electronicRecords)
                {
                    var (success, failure, entryLines) = await FileElectronicRecordAsync(
                        record,
                        operatorUser,
                        usedBlankHardDiskCodes,
                        reservedDedicatedFullLocations);
                    electronicEntrySuccess += success;
                    electronicEntryFailure += failure;
                    if (success > 0)
                    {
                        electronicRecordSucceeded++;
                    }
                    else if (failure > 0)
                    {
                        electronicRecordFailed++;
                    }

                    lines.AddRange(entryLines);
                }

                lines.Add($"电子汇总：成功 {electronicEntrySuccess} 条介质，失败 {electronicEntryFailure} 条介质；登记单成功 {electronicRecordSucceeded}，失败 {electronicRecordFailed}。");
                lines.Add(string.Empty);
            }

            if (simulatedRecords.Count > 0)
            {
                lines.Add($"—— 模拟介质立档（{simulatedRecords.Count} 单）——");
                foreach (var record in simulatedRecords)
                {
                    var (success, failure, entryLines) = await FileSimulatedRecordAsync(record, operatorUser);
                    if (success > 0)
                    {
                        simulatedRecordSucceeded++;
                    }
                    else if (failure > 0)
                    {
                        simulatedRecordFailed++;
                    }

                    lines.AddRange(entryLines);
                }

                lines.Add($"模拟汇总：成功 {simulatedRecordSucceeded} 单，失败 {simulatedRecordFailed} 单。");
                lines.Add(string.Empty);
            }

            int processedRecords = electronicRecords.Count + simulatedRecords.Count;
            int succeededRecords = electronicRecordSucceeded + simulatedRecordSucceeded;
            int failedRecords = electronicRecordFailed + simulatedRecordFailed;

            lines.Add($"总汇总：登记单 {processedRecords} 单；至少完成一项立档 {succeededRecords} 单；全部失败 {failedRecords} 单。");
            return new ArchiveFilingAutomationResult(processedRecords, succeededRecords, failedRecords, lines);
        }

        private async Task<(int SuccessCount, int FailureCount, List<string> Lines)> FileElectronicRecordAsync(
            YearlyArchiveRegisterRecord record,
            User operatorUser,
            ISet<string> usedBlankHardDiskCodes,
            ISet<string> reservedDedicatedFullLocations)
        {
            var lines = new List<string> { $"- [{record.FormNo}] 电子立档" };
            int success = 0;
            int failure = 0;

            var pendingEntries = record.MediaEntries
                .Where(entry => string.Equals(entry.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase))
                .Where(entry => entry.Items.Any(item => item.Id > 0 && !item.ElectronicArchiveUnitMediaItemLinks.Any()))
                .ToList();

            if (pendingEntries.Count == 0)
            {
                lines.Add("  · 跳过：无待立档电子介质条目。");
                return (0, 0, lines);
            }

            foreach (var mediaEntry in pendingEntries)
            {
                var mediaItems = mediaEntry.Items
                    .Where(item => item.Id > 0 && !item.ElectronicArchiveUnitMediaItemLinks.Any())
                    .ToList();

                if (mediaItems.Count == 0)
                {
                    continue;
                }

                ElectronicArchiveSubmissionRequest? request = null;
                string plannedStorageLocation = string.Empty;
                try
                {
                    request = await _electronicSubmissionRequestBuilder.BuildForNewBagAsync(
                        new ArchiveFilingElectronicSubmissionBuildOptions
                        {
                            Record = record,
                            MediaEntry = mediaEntry,
                            MediaItems = mediaItems,
                            OperatorUser = operatorUser,
                            Remarks = ChecklistMarker,
                            StoragePathPrefix = "/cursor-filing",
                            ExternalDiskCodePrefix = "CURSOR",
                            ReservedDedicatedFullLocations = reservedDedicatedFullLocations
                        },
                        usedBlankHardDiskCodes);

                    lines.Add(
                        $"  · 计划：{request.FilingMode} / 模式 [{request.SubmissionMode}]（本介质 {mediaItems.Count} 个子项一并入袋）");

                    if (request.MediaItemIds.Count != mediaItems.Count)
                    {
                        throw new InvalidOperationException("立档请求子项数量与介质待立档子项不一致，已中止。");
                    }

                    plannedStorageLocation = request.ArchiveUnit.StorageLocation?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(plannedStorageLocation))
                    {
                        reservedDedicatedFullLocations.Add(plannedStorageLocation);
                    }

                    await _archiveFilingService.PreviewNewElectronicArchiveUnitAsync(request, operatorUser);
                    var submit = await _archiveFilingService.SubmitNewElectronicArchiveUnitAsync(request, operatorUser);

                    var syncIssues = await ArchiveFilingCursorTestHardDiskSyncVerifier.VerifyAsync(
                        _archiveFilingRepository,
                        request,
                        submit,
                        mediaEntry);

                    AppendDatabaseChangeLines(lines, submit);

                    if (syncIssues.Count > 0)
                    {
                        failure++;
                        if (!string.IsNullOrWhiteSpace(plannedStorageLocation))
                        {
                            reservedDedicatedFullLocations.Remove(plannedStorageLocation);
                        }

                        lines.Add($"    ✗ 硬盘同步核对未通过 [{request.SubmissionMode}]：");
                        foreach (string issue in syncIssues)
                        {
                            lines.Add($"      - {issue}");
                        }

                        continue;
                    }

                    success++;
                    lines.Add(
                        $"    ✓ 成功 [{request.SubmissionMode}] -> {submit.ElectronicArchiveNo} @ {plannedStorageLocation}，入袋子项 {request.MediaItemIds.Count} 条；硬盘侧变更已核对。");
                }
                catch (Exception ex)
                {
                    failure++;
                    if (request?.ArchiveUnit?.StorageLocation is string failedLocation
                        && !string.IsNullOrWhiteSpace(failedLocation))
                    {
                        reservedDedicatedFullLocations.Remove(failedLocation.Trim());
                    }

                    lines.Add($"    ✗ 失败：{ex.Message}");
                }
            }

            lines.Add($"  本单：成功 {success}，失败 {failure}");
            return (success, failure, lines);
        }

        private static void AppendDatabaseChangeLines(List<string> lines, ElectronicArchiveSubmissionResult submit)
        {
            if (submit.DatabaseChanges == null || submit.DatabaseChanges.Lines.Count == 0)
            {
                lines.Add("    · 数据库变更明细：（无记录，请检查是否为 Release 构建未写入 ChangeTracker）");
                return;
            }

            lines.Add("    · 数据库变更明细（与操作台提交后对话框同源）：");
            foreach (string changeLine in submit.DatabaseChanges.Lines)
            {
                lines.Add($"      {changeLine}");
            }
        }

        private async Task<(int SuccessCount, int FailureCount, List<string> Lines)> FileSimulatedRecordAsync(
            YearlyArchiveRegisterRecord record,
            User operatorUser)
        {
            var lines = new List<string> { $"- [{record.FormNo}] 模拟立档" };

            var mediaItemIds = record.MediaEntries
                .Where(entry => string.Equals(entry.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.OrdinalIgnoreCase))
                .SelectMany(entry => entry.Items)
                .Where(item => item.Id > 0 && !item.ArchiveBoxLinks.Any())
                .Select(item => item.Id)
                .Distinct()
                .ToList();

            if (mediaItemIds.Count == 0)
            {
                lines.Add("  · 跳过：无待入盒模拟介质子项。");
                return (0, 0, lines);
            }

            string year = record.CreatedDate.Year.ToString();
            string projectName = ArchiveFilingBusinessRules.ResolveElectronicArchiveProjectName(record);
            const string boxSpec = "中";

            try
            {
                string sequenceNo = await _archiveFilingService.GenerateNextArchiveSequenceNoAsync(year);
                var location = await _archiveFilingService.SuggestArchiveBoxLocationAsync(projectName, year, boxSpec)
                    ?? throw new InvalidOperationException("无法推荐档案盒档口，请检查柜体配置。");

                var box = new YearlyArchiveBox
                {
                    ArchiveSequenceNo = sequenceNo,
                    BoxLocationCode = location.SuggestedBoxLocationCode,
                    CabinetName = location.CabinetName,
                    Side = location.Side,
                    Row = location.Row,
                    Column = location.Column,
                    BoxIndex = location.ExistingBoxCount + 1,
                    ProjectName = projectName,
                    Year = year,
                    Specs = boxSpec,
                    ArchivedBy = ResolveOperatorName(operatorUser),
                    ArchivedDate = DateTime.Now,
                    Remarks = ChecklistMarker
                };

                await _archiveFilingService.CreateArchiveBoxAsync(box, mediaItemIds);
                lines.Add($"  ✓ 成功：新建档案盒 {sequenceNo} @ {location.SuggestedBoxLocationCode}，入盒 {mediaItemIds.Count} 项");
                return (1, 0, lines);
            }
            catch (Exception ex)
            {
                lines.Add($"  ✗ 失败：{ex.Message}");
                return (0, 1, lines);
            }
        }

        private static string ResolveOperatorName(User user)
            => string.IsNullOrWhiteSpace(user.RealName) ? user.LoginName ?? "Unknown" : user.RealName;

        private static bool IsSimulationRecord(YearlyArchiveRegisterRecord record)
        {
            bool hasMarker = !string.IsNullOrWhiteSpace(record.OtherRequests)
                && record.OtherRequests.Contains(SimulationMarker, StringComparison.Ordinal);
            bool hasLegacyPrefix = !string.IsNullOrWhiteSpace(record.MaterialName)
                && record.MaterialName.StartsWith(LegacySimulationMaterialPrefix, StringComparison.Ordinal);
            return hasMarker || hasLegacyPrefix;
        }

        private void EnsureArchiveAdminUser(User operatorUser)
        {
            if (!_archiveRegisterService.IsArchiveAdminUser(operatorUser))
            {
                throw new InvalidOperationException("仅资料室资料管理员可以执行立档测试_cursor。");
            }
        }
    }
}
