using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Cabinets;
using DocMgr.Models.Projects;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    public class ArchiveRegisterSimulationService : IArchiveRegisterSimulationService
    {
        private const string SimulatedApplicantLoginName = "mxc";
        private const int DefaultSimulationCount = 5;
        private const int DefaultComplexElectronicSimulationCount = 20;
        private const int DefaultBorrowBusinessCount = 5;
        private const string SimulationMarker = "[模拟登记]";
        private const string ComplexElectronicMarker = "[模拟登记][复杂电子]";
        private const string InternalBorrowedHardDiskMarker = "[模拟登记][复杂电子][资料室借出硬盘]";
        private const string LegacySimulationMaterialPrefix = "模拟单-";

        private readonly IArchiveRegisterSimulationRepository _archiveRegisterSimulationRepository;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IArchiveFilingService _archiveFilingService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly ArchiveFilingElectronicSubmissionRequestBuilder _electronicSubmissionRequestBuilder;
        private readonly ArchiveRegisterComplexElectronicApplicationOrchestrator _complexElectronicApplicationOrchestrator;
        private readonly HashSet<string> _usedBlankHardDiskCodes = new(StringComparer.OrdinalIgnoreCase);

        public ArchiveRegisterSimulationService(
            IArchiveRegisterSimulationRepository archiveRegisterSimulationRepository,
            IArchiveRegisterService archiveRegisterService,
            IArchiveFilingService archiveFilingService,
            IHardDiskMediaService hardDiskMediaService,
            ArchiveFilingElectronicSubmissionRequestBuilder electronicSubmissionRequestBuilder,
            ArchiveRegisterComplexElectronicApplicationOrchestrator complexElectronicApplicationOrchestrator)
        {
            _archiveRegisterSimulationRepository = archiveRegisterSimulationRepository;
            _archiveRegisterService = archiveRegisterService;
            _archiveFilingService = archiveFilingService;
            _hardDiskMediaService = hardDiskMediaService;
            _electronicSubmissionRequestBuilder = electronicSubmissionRequestBuilder;
            _complexElectronicApplicationOrchestrator = complexElectronicApplicationOrchestrator;
        }

        /// <inheritdoc/>
        public async Task<ArchiveRegisterSimulationResult> GenerateFiveHardDiskBorrowBusinessesAsync(User? operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);

            EnsureArchiveAdminUser(operatorUser);

            var applicant = await GetSimulationApplicantAsync();
            var borrowedDiskCodes = await _hardDiskMediaService.GetCurrentUserBorrowedHardDiskCodesAsync(applicant);
            int requiredCount = Math.Max(0, DefaultBorrowBusinessCount - borrowedDiskCodes.Count);
            if (requiredCount == 0)
            {
                return new ArchiveRegisterSimulationResult(0, Array.Empty<string>(), Array.Empty<string>());
            }

            var selectedMedia = await GetAvailableStockBorrowMediaAsync(requiredCount, borrowedDiskCodes);
            if (selectedMedia.Count < requiredCount)
            {
                throw new InvalidOperationException($"资料室库存可借出的真实硬盘不足。当前还需 {requiredCount} 块，请先补充在库空盘后再执行。");
            }

            string applicantName = string.IsNullOrWhiteSpace(applicant.RealName)
                ? applicant.LoginName.Trim()
                : applicant.RealName.Trim();
            string applicantDept = applicant.Department?.Trim() ?? string.Empty;
            string operatorName = string.IsNullOrWhiteSpace(operatorUser.RealName)
                ? operatorUser.LoginName?.Trim() ?? string.Empty
                : operatorUser.RealName.Trim();

            var generatedApplicationNos = new List<string>(requiredCount);
            DateTime now = DateTime.Now;

            for (int index = 0; index < requiredCount; index++)
            {
                var medium = selectedMedia[index];
                DateTime applyTime = now.AddMinutes(index);
                bool isLongTerm = index % 2 == 1;
                string targetLocation = string.IsNullOrWhiteSpace(applicantDept)
                    ? "申请人借用中"
                    : $"{applicantDept}-借用中";

                var application = new HardDiskMediaApplication
                {
                    MediumId = medium.Id,
                    ApplicationType = isLongTerm
                        ? HardDiskMediaApplication.TypeOutboundLongTerm
                        : HardDiskMediaApplication.TypeOutboundTemporary,
                    ApplicantName = applicantName,
                    ApplicantDept = applicantDept,
                    ApplyTime = applyTime,
                    Reason = $"模拟登记生成真实硬盘借出业务，供 mxc 后续复杂电子申请与自动立档测试使用。",
                    TargetPersonOrUnit = applicantName,
                    CurrentLocation = medium.Ledger?.StorageLocation?.Trim() ?? string.Empty,
                    TargetLocation = targetLocation,
                    ExpectedReturnDate = isLongTerm ? applyTime.AddDays(30) : applyTime.AddDays(7),
                    RelatedBatch = $"模拟登记真实借出-{applyTime:yyyyMMdd}",
                    RelatedArchiveTitle = "模拟登记真实硬盘借出",
                    ApprovalOpinion = "同意",
                    Remark = "模拟登记页面生成5个硬盘借出业务"
                };

                await _hardDiskMediaService.SaveApplicationAsync(application, operatorUser);
                await _hardDiskMediaService.SubmitApplicationAsync(application.Id, operatorUser);

                var approveResult = await _hardDiskMediaService.ApproveApplicationAsync(
                    application,
                    operatorUser,
                    new HardDiskMediaApprovalInput
                    {
                        ReviewerName = operatorName,
                        ApproverName = operatorName,
                        ApprovalOpinion = "同意"
                    });

                if (!approveResult.Success)
                {
                    throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 借出审批失败：{approveResult.Message}");
                }

                var handoverResult = await _hardDiskMediaService.ConfirmPhysicalHandoverAsync(
                    application,
                    operatorUser,
                    new HardDiskMediaApprovalInput
                    {
                        HandoverAdmin = operatorName,
                        HandoverDate = applyTime.AddMinutes(2)
                    });

                if (!handoverResult.Success)
                {
                    throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 借出实物交接确认失败：{handoverResult.Message}");
                }

                byte[] signedAttachmentContent = "%PDF-1.0\n%%EOF\n"u8.ToArray();
                var uploadResult = await _hardDiskMediaService.UploadSignedAttachmentAsync(
                    application,
                    operatorUser,
                    $"{application.ApplicationNo}_模拟签批交接单.pdf",
                    ".pdf",
                    signedAttachmentContent.Length,
                    signedAttachmentContent);

                if (!uploadResult.Success)
                {
                    throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 借出签批交接单上传失败：{uploadResult.Message}");
                }

                var completeResult = await _hardDiskMediaService.CompleteApplicationAsync(application, operatorUser);
                if (!completeResult.Success)
                {
                    throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 借出办结失败：{completeResult.Message}");
                }

                generatedApplicationNos.Add(application.ApplicationNo);
            }

            return new ArchiveRegisterSimulationResult(generatedApplicationNos.Count, generatedApplicationNos, Array.Empty<string>());
        }

        /// <inheritdoc/>
        public async Task<ArchiveRegisterSimulationResult> GenerateApprovedReceivedSamplesAsync(User? operatorUser)
            => await GenerateSamplesAsync(operatorUser, BuildTemplates, DefaultSimulationCount);

        /// <inheritdoc/>
        public async Task<ArchiveRegisterSimulationResult> GenerateComplexElectronicSamplesAsync(User? operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);

            EnsureLoggedInUser(operatorUser);

            var applicant = await ResolveSimulationApplicantAsync(operatorUser);
            var projects = RequireDatabaseProjectsForComplexElectronic(
                await _archiveRegisterSimulationRepository.GetProjectsAsync());
            var domainOptions = await _archiveRegisterService.GetPageDomainOptionsAsync();
            var templates = BuildComplexElectronicTemplates(domainOptions, projects, applicant)
                .Take(DefaultComplexElectronicSimulationCount)
                .ToList();
            int requiredBorrowedCount = templates
                .Select((_, index) => index)
                .Count(IsBorrowedHardDiskScenarioIndex);
            var borrowedHardDiskCodes = await EnsureApplicantBorrowedHardDiskCodesAsync(applicant, operatorUser, requiredBorrowedCount);
            int borrowedDiskIndex = 0;

            var generatedFormNos = new List<string>(templates.Count);
            var checklistLines = new List<string>
            {
                "【复杂电子介质申请单生成】",
                ComplexElectronicMarker,
                $"执行时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                "路径说明：按资料登记申请操作台申请人流程（保存草稿 → 提交；校验在提交流程内），非管理员直写库。",
                string.Empty
            };
            var now = DateTime.Now;
            int succeeded = 0;
            int failed = 0;

            for (int index = 0; index < templates.Count; index++)
            {
                var template = templates[index];
                DateTime createdAt = now.AddMinutes(-(index + 1) * 7);
                checklistLines.Add($"- 第 {index + 1}/{templates.Count} 单");

                var mediaEntries = await BuildComplexScenarioMediaEntriesAsync(
                    template,
                    index,
                    domainOptions,
                    borrowedHardDiskCode: IsBorrowedHardDiskScenarioIndex(index)
                        ? borrowedHardDiskCodes[borrowedDiskIndex++]
                        : null);

                var submit = await _complexElectronicApplicationOrchestrator.SubmitLikeApplicantConsoleAsync(
                    new ComplexElectronicApplicationSubmitRequest
                    {
                        Applicant = applicant,
                        Template = new ComplexElectronicSimulationTemplate(
                            template.ProjectId!.Value,
                            template.ProjectName!,
                            template.SourceType,
                            template.ProvideUnit,
                            template.MaterialName,
                            template.ArchivePurpose,
                            template.OtherRequests,
                            template.MediaEntries),
                        DomainOptions = domainOptions,
                        MediaEntries = mediaEntries,
                        CreatedAt = createdAt,
                        OtherRequestsMarker = $"{SimulationMarker} {ComplexElectronicMarker} {template.OtherRequests}",
                        ExpectsBorrowedHardDiskLock = IsBorrowedHardDiskScenarioIndex(index)
                    });

                checklistLines.AddRange(submit.ChecklistLines);

                if (submit.Success)
                {
                    succeeded++;
                    generatedFormNos.Add(submit.FormNo);
                    checklistLines.Add($"  ✓ 已生成并提交 [{submit.FormNo}]");
                }
                else
                {
                    failed++;
                    checklistLines.Add("  ✗ 本单失败，已中止后续生成。");
                    checklistLines.Add(string.Empty);
                    checklistLines.Add($"汇总：成功 {succeeded} 单，失败 {failed} 单（后续未继续）。");
                    return new ArchiveRegisterSimulationResult(succeeded, generatedFormNos, checklistLines);
                }

                checklistLines.Add(string.Empty);
            }

            checklistLines.Add($"汇总：成功 {succeeded} 单，失败 {failed} 单。");
            return new ArchiveRegisterSimulationResult(succeeded, generatedFormNos, checklistLines);
        }

        private async Task<ArchiveRegisterSimulationResult> GenerateSamplesAsync(
            User? operatorUser,
            Func<ArchiveRegisterPageDomainOptions, IReadOnlyList<ProjectInfo>, User, List<SimulationTemplate>> templateBuilder,
            int targetCount)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);

            EnsureArchiveAdminUser(operatorUser);

            var applicant = await GetSimulationApplicantAsync();

            var projects = await _archiveRegisterSimulationRepository.GetProjectsAsync();

            var domainOptions = await _archiveRegisterService.GetPageDomainOptionsAsync();
            var now = DateTime.Now;
            var generatedFormNos = new List<string>(targetCount);
            var templates = templateBuilder(domainOptions, projects, applicant);

            foreach (var (template, index) in templates.Take(targetCount).Select((item, index) => (item, index)))
            {
                var record = await _archiveRegisterService.CreateDraftRecordWithNextFormNoAsync(applicant);
                ApplyTemplate(record, template, now.AddMinutes(-(index + 1) * 9));
                await _archiveRegisterService.ApplyDefaultApprovalInfoAsync(record, operatorUser);
                record.MarkAsApprovedReceived();
                record.MediaEntries = template.MediaEntries.Select(CloneMediaEntry).ToList();
                await _archiveRegisterService.SaveOrUpdateAsync(record);
                generatedFormNos.Add(record.FormNo);
            }
            return new ArchiveRegisterSimulationResult(generatedFormNos.Count, generatedFormNos, Array.Empty<string>());
        }

        /// <inheritdoc/>
        public async Task<ArchiveRegisterSimulationResult> ClearGeneratedSamplesAsync(User? operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);

            EnsureArchiveAdminUser(operatorUser);

            var records = await _archiveRegisterSimulationRepository.GetSimulatedRegisterRecordsAsync(
                SimulationMarker,
                SimulatedApplicantLoginName,
                LegacySimulationMaterialPrefix);

            var simulatedMedia = await _archiveRegisterSimulationRepository.GetSimulatedHardDiskMediaAsync(InternalBorrowedHardDiskMarker);
            var simulatedApplications = await _archiveRegisterSimulationRepository.GetSimulatedHardDiskApplicationsAsync(InternalBorrowedHardDiskMarker);
            var simulatedTransactions = await _archiveRegisterSimulationRepository.GetSimulatedHardDiskTransactionsAsync(InternalBorrowedHardDiskMarker);

            if (records.Count == 0 && simulatedMedia.Count == 0)
            {
                return new ArchiveRegisterSimulationResult(0, Array.Empty<string>(), Array.Empty<string>());
            }

            var formNos = records.Select(record => record.FormNo).ToList();
            foreach (var record in records)
            {
                await _archiveRegisterService.RemoveRegisterRecordAsync(record.Id);
            }

            if (simulatedMedia.Count > 0)
            {
                if (simulatedTransactions.Count > 0)
                {
                    _archiveRegisterSimulationRepository.RemoveHardDiskMediaTransactionsRange(simulatedTransactions);
                }

                if (simulatedApplications.Count > 0)
                {
                    _archiveRegisterSimulationRepository.RemoveHardDiskMediaApplicationsRange(simulatedApplications);
                }

                _archiveRegisterSimulationRepository.RemoveHardDiskMediaRange(simulatedMedia);
                await _archiveRegisterSimulationRepository.SaveChangesAsync();
            }

            return new ArchiveRegisterSimulationResult(Math.Max(formNos.Count, simulatedMedia.Count), formNos, Array.Empty<string>());
        }

        /// <inheritdoc/>
        public async Task<ArchiveFilingAutomationResult> RunAutomatedFilingTestAsync(User? operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);
            EnsureArchiveAdminUser(operatorUser);
            _usedBlankHardDiskCodes.Clear();

            var checklistLines = new List<string>
            {
                "【自动化立档测试清单】",
                $"执行时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                "处理范围：模拟登记中“已办结”且含电子介质、尚未完成电子立档的记录。"
            };

            var pendingRecords = await _archiveFilingService.GetPendingElectronicRecordsAsync();
            var simulationRecords = pendingRecords
                .Where(IsSimulationRecord)
                .Where(record => record.Status == YearlyArchiveRegisterRecord.Completed)
                .OrderBy(record => record.FormNo, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (simulationRecords.Count == 0)
            {
                checklistLines.Add("结果：未找到可执行自动化立档测试的模拟登记单。");
                return new ArchiveFilingAutomationResult(0, 0, 0, checklistLines);
            }

            var applicant = await GetSimulationApplicantAsync();

            int succeeded = 0;
            int failed = 0;

            foreach (var record in simulationRecords)
            {
                try
                {
                    var (successCount, failureCount) = await TryAutoFileRecordAsync(record, applicant, operatorUser, checklistLines);
                    if (successCount > 0)
                    {
                        succeeded++;
                    }
                    else if (failureCount > 0)
                    {
                        failed++;
                    }
                    else
                    {
                        checklistLines.Add($"- [{record.FormNo}] 跳过：未识别到可自动提交的电子介质明细。");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    checklistLines.Add($"- [{record.FormNo}] 失败：{ex.Message}");
                }
            }

            checklistLines.Add($"汇总：共处理 {simulationRecords.Count} 单，成功 {succeeded} 单，失败 {failed} 单。");
            return new ArchiveFilingAutomationResult(simulationRecords.Count, succeeded, failed, checklistLines);
        }

        /// <inheritdoc/>
        public async Task<ArchiveRegisterSimulationResult> AutoApproveSubmittedApplicationsAsync(User? operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);
            EnsureArchiveAdminUser(operatorUser);

            var submittedRecords = await _archiveRegisterSimulationRepository.GetSubmittedRegisterRecordsAsync();
            if (submittedRecords.Count == 0)
            {
                return new ArchiveRegisterSimulationResult(0, Array.Empty<string>(), Array.Empty<string>());
            }

            var processedFormNos = new List<string>(submittedRecords.Count);
            DateTime now = DateTime.Now;

            foreach (var stub in submittedRecords)
            {
                var record = await _archiveRegisterService.GetByIdAsync(stub.Id);
                if (record == null)
                {
                    continue;
                }

                await _archiveRegisterService.SyncBorrowedHardDiskRegisterLocksAsync(record);

                record.Status = YearlyArchiveRegisterRecord.Completed;
                record.ArchivedDate = now;
                processedFormNos.Add(record.FormNo);
            }

            await _archiveRegisterSimulationRepository.SaveChangesAsync();
            return new ArchiveRegisterSimulationResult(processedFormNos.Count, processedFormNos, Array.Empty<string>());
        }

        private static bool IsSimulationRecord(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            bool hasSimulationMarker = !string.IsNullOrWhiteSpace(record.OtherRequests)
                && record.OtherRequests.Contains(SimulationMarker, StringComparison.Ordinal);
            bool hasLegacyPrefix = !string.IsNullOrWhiteSpace(record.MaterialName)
                && record.MaterialName.StartsWith(LegacySimulationMaterialPrefix, StringComparison.Ordinal);

            return hasSimulationMarker || hasLegacyPrefix;
        }

        private async Task<(int SuccessCount, int FailureCount)> TryAutoFileRecordAsync(
            YearlyArchiveRegisterRecord record,
            User applicant,
            User operatorUser,
            ICollection<string> checklistLines)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(applicant);
            ArgumentNullException.ThrowIfNull(operatorUser);
            ArgumentNullException.ThrowIfNull(checklistLines);

            var electronicMediaEntries = record.MediaEntries
                .Where(entry => string.Equals(entry.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase))
                .Where(entry => entry.Items.Any(item => item.Id > 0 && !item.ElectronicArchiveUnitMediaItemLinks.Any()))
                .ToList();

            if (electronicMediaEntries.Count == 0)
            {
                return (0, 0);
            }

            int successForRecord = 0;
            int failureForRecord = 0;
            checklistLines.Add($"- [{record.FormNo}] 开始：电子介质条目 {electronicMediaEntries.Count} 条。");

            foreach (var mediaEntry in electronicMediaEntries)
            {
                var mediaItems = mediaEntry.Items
                    .Where(item => item.Id > 0)
                    .Where(item => !item.ElectronicArchiveUnitMediaItemLinks.Any())
                    .ToList();

                if (mediaItems.Count == 0)
                {
                    continue;
                }

                ElectronicArchiveSubmissionRequest request;
                try
                {
                    request = await BuildElectronicSubmissionRequestAsync(record, mediaEntry, mediaItems, applicant, operatorUser);
                }
                catch (Exception ex)
                {
                    failureForRecord++;
                    checklistLines.Add(
                        $"  * 失败：介质[{mediaEntry.MediaType}] / 处置[{mediaEntry.Disposition}] / 模式[未生成]，原因：{ex.Message}");
                    continue;
                }

                try
                {
                    ElectronicArchiveSubmissionResult result = await _archiveFilingService.SubmitNewElectronicArchiveUnitAsync(request, operatorUser);
                    successForRecord++;
                    checklistLines.Add(
                        $"  * 成功：介质[{mediaEntry.MediaType}] / 处置[{mediaEntry.Disposition}] / 模式[{request.SubmissionMode}] -> 电子袋[{result.ElectronicArchiveNo}]，入袋明细 {result.MediaEntryCount} 条。");
                }
                catch (Exception ex)
                {
                    failureForRecord++;
                    checklistLines.Add(
                        $"  * 失败：介质[{mediaEntry.MediaType}] / 处置[{mediaEntry.Disposition}] / 模式[{request.SubmissionMode}]，原因：{ex.Message}");
                }
            }

            checklistLines.Add($"  记录汇总：成功 {successForRecord}，失败 {failureForRecord}。");
            return (successForRecord, failureForRecord);
        }

        private Task<ElectronicArchiveSubmissionRequest> BuildElectronicSubmissionRequestAsync(
            YearlyArchiveRegisterRecord record,
            YearlyArchiveRegisterMedia mediaEntry,
            IReadOnlyList<YearlyArchiveRegisterMediaItem> mediaItems,
            User applicant,
            User operatorUser)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(mediaEntry);
            ArgumentNullException.ThrowIfNull(mediaItems);
            ArgumentNullException.ThrowIfNull(applicant);
            ArgumentNullException.ThrowIfNull(operatorUser);

            return _electronicSubmissionRequestBuilder.BuildForNewBagAsync(
                new ArchiveFilingElectronicSubmissionBuildOptions
                {
                    Record = record,
                    MediaEntry = mediaEntry,
                    MediaItems = mediaItems,
                    OperatorUser = operatorUser,
                    Remarks = "自动化立档测试生成",
                    StoragePathPrefix = "/auto-filing",
                    ExternalDiskCodePrefix = "AUTO"
                },
                _usedBlankHardDiskCodes);
        }

        private static string BuildContentSummary(IEnumerable<YearlyArchiveRegisterMediaItem> mediaItems)
        {
            ArgumentNullException.ThrowIfNull(mediaItems);

            string summary = string.Join("；", mediaItems
                .Select(item => item.ContentDesc?.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Take(3));

            return string.IsNullOrWhiteSpace(summary) ? "自动化立档测试内容" : summary;
        }

        private async Task<List<YearlyArchiveRegisterMedia>> BuildComplexScenarioMediaEntriesAsync(
            SimulationTemplate template,
            int scenarioIndex,
            ArchiveRegisterPageDomainOptions domainOptions,
            string? borrowedHardDiskCode)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(domainOptions);

            var mediaEntries = template.MediaEntries.Select(CloneMediaEntry).ToList();

            bool shouldAttachBorrowedHardDisk = IsBorrowedHardDiskScenarioIndex(scenarioIndex);
            if (!shouldAttachBorrowedHardDisk)
            {
                return mediaEntries;
            }

            if (string.IsNullOrWhiteSpace(borrowedHardDiskCode))
            {
                throw new InvalidOperationException("复杂电子场景需要真实借出硬盘，但未分配到可用硬盘编号。");
            }

            var retainedHardDiskMedia = mediaEntries.FirstOrDefault(IsRetainedHardDiskMedia);
            if (retainedHardDiskMedia == null)
            {
                string hardDiskType = PickByKeywordOrFallback(domainOptions.DataElectronicMediaTypes, new[] { "硬盘" }, ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk);
                string retainedDisposition = PickByKeywordOrFallback(domainOptions.DataElectronicDispositions, new[] { "留存" }, ArchiveRegisterDomainValues.ElectronicDispositionRetain);

                retainedHardDiskMedia = CreateElectronicMedia(
                    hardDiskType,
                    retainedDisposition,
                    CreateElectronicItem(
                        $"资料室借出硬盘成果包 {scenarioIndex + 1:D2}",
                        1,
                        $"/archive/2026/complex-e/{scenarioIndex + 1:D2}/borrowed-harddisk/payload",
                        "借出硬盘留存场景",
                        domainOptions));

                mediaEntries.Add(retainedHardDiskMedia);
            }

            retainedHardDiskMedia.IsBorrowedHardDisk = true;
            retainedHardDiskMedia.BorrowedHardDiskCode = borrowedHardDiskCode.Trim();

            if (!retainedHardDiskMedia.Items.Any())
            {
                retainedHardDiskMedia.Items.Add(CreateElectronicItem(
                    $"借出硬盘补充内容 {scenarioIndex + 1:D2}",
                    1,
                    $"/archive/2026/complex-e/{scenarioIndex + 1:D2}/borrowed-harddisk/extra",
                    $"关联借出硬盘：{borrowedHardDiskCode}",
                    domainOptions));
            }
            else
            {
                retainedHardDiskMedia.Items[0].Note = $"{retainedHardDiskMedia.Items[0].Note}；关联借出硬盘：{borrowedHardDiskCode}".Trim('；');
            }

            return mediaEntries;
        }

        private async Task<IReadOnlyList<string>> EnsureApplicantBorrowedHardDiskCodesAsync(User applicant, User operatorUser, int requiredCount)
        {
            ArgumentNullException.ThrowIfNull(applicant);
            ArgumentNullException.ThrowIfNull(operatorUser);

            if (requiredCount <= 0)
            {
                return Array.Empty<string>();
            }

            var existingBorrowedCodes = (await _hardDiskMediaService.GetCurrentUserBorrowedHardDiskCodesAsync(applicant)).ToList();
            int missingCount = requiredCount - existingBorrowedCodes.Count;
            if (missingCount > 0)
            {
                if (!_archiveRegisterService.IsArchiveAdminUser(operatorUser))
                {
                    throw new InvalidOperationException(
                        $"生成含「借出留存硬盘」场景的模拟申请单前，请先在硬盘介质管理中借出至少 {requiredCount} 块硬盘（当前已借 {existingBorrowedCodes.Count} 块，还需 {missingCount} 块）。资料室管理员可在本页「生成5个硬盘借出业务」自动准备。");
                }

                await GenerateRealBorrowBusinessesAsync(applicant, operatorUser, missingCount);
                existingBorrowedCodes = (await _hardDiskMediaService.GetCurrentUserBorrowedHardDiskCodesAsync(applicant)).ToList();
            }

            if (existingBorrowedCodes.Count < requiredCount)
            {
                throw new InvalidOperationException($"申请人当前可用真实借出硬盘不足。需要 {requiredCount} 块，实际仅 {existingBorrowedCodes.Count} 块。");
            }

            return existingBorrowedCodes.Take(requiredCount).ToList();
        }

        private async Task<List<HardDiskMedium>> GetAvailableStockBorrowMediaAsync(int requiredCount, IReadOnlyCollection<string>? excludedDiskCodes)
        {
            if (requiredCount <= 0)
            {
                return [];
            }

            var excluded = excludedDiskCodes == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(excludedDiskCodes.Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()), StringComparer.OrdinalIgnoreCase);

            var media = await _hardDiskMediaService.GetSelectableMediaAsync();
            return media
                .Where(item => item.Id > 0)
                .Where(item => !excluded.Contains(item.DiskCode))
                .Where(item => item.RegisterLock == null)
                .Where(item => item.Ledger != null)
                .Where(item => string.Equals(item.Ledger!.MediaStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
                .Where(item => string.Equals(item.Ledger!.HolderOrOrganization, "资料室", StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.DiskCode, StringComparer.OrdinalIgnoreCase)
                .Take(requiredCount)
                .ToList();
        }

        private async Task<List<string>> GenerateRealBorrowBusinessesAsync(User applicant, User operatorUser, int requiredCount)
        {
            ArgumentNullException.ThrowIfNull(applicant);
            ArgumentNullException.ThrowIfNull(operatorUser);

            if (requiredCount <= 0)
            {
                return [];
            }

            var existingBorrowedCodes = await _hardDiskMediaService.GetCurrentUserBorrowedHardDiskCodesAsync(applicant);
            var selectedMedia = await GetAvailableStockBorrowMediaAsync(requiredCount, existingBorrowedCodes);
            if (selectedMedia.Count < requiredCount)
            {
                throw new InvalidOperationException($"资料室库存可借出的真实硬盘不足。当前还需 {requiredCount} 块，请先补充在库空盘后再执行。");
            }

            string applicantName = string.IsNullOrWhiteSpace(applicant.RealName)
                ? applicant.LoginName.Trim()
                : applicant.RealName.Trim();
            string applicantDept = applicant.Department?.Trim() ?? string.Empty;
            string operatorName = string.IsNullOrWhiteSpace(operatorUser.RealName)
                ? operatorUser.LoginName?.Trim() ?? string.Empty
                : operatorUser.RealName.Trim();
            DateTime now = DateTime.Now;
            var generatedApplicationNos = new List<string>(requiredCount);

            for (int index = 0; index < requiredCount; index++)
            {
                var medium = selectedMedia[index];
                DateTime applyTime = now.AddMinutes(index);
                bool isLongTerm = index % 2 == 1;
                string targetLocation = string.IsNullOrWhiteSpace(applicantDept)
                    ? "申请人借用中"
                    : $"{applicantDept}-借用中";

                var application = new HardDiskMediaApplication
                {
                    MediumId = medium.Id,
                    ApplicationType = isLongTerm
                        ? HardDiskMediaApplication.TypeOutboundLongTerm
                        : HardDiskMediaApplication.TypeOutboundTemporary,
                    ApplicantName = applicantName,
                    ApplicantDept = applicantDept,
                    ApplyTime = applyTime,
                    Reason = "模拟登记复用真实硬盘借出业务，供复杂电子立档测试。",
                    TargetPersonOrUnit = applicantName,
                    CurrentLocation = medium.Ledger?.StorageLocation?.Trim() ?? string.Empty,
                    TargetLocation = targetLocation,
                    ExpectedReturnDate = isLongTerm ? applyTime.AddDays(30) : applyTime.AddDays(7),
                    RelatedBatch = $"模拟登记真实借出-{applyTime:yyyyMMdd}",
                    RelatedArchiveTitle = "复杂电子介质申请单测试",
                    ApprovalOpinion = "同意",
                    Remark = "模拟登记复用真实硬盘"
                };

                await _hardDiskMediaService.SaveApplicationAsync(application, operatorUser);
                await _hardDiskMediaService.SubmitApplicationAsync(application.Id, operatorUser);

                var approveResult = await _hardDiskMediaService.ApproveApplicationAsync(
                    application,
                    operatorUser,
                    new HardDiskMediaApprovalInput
                    {
                        ReviewerName = operatorName,
                        ApproverName = operatorName,
                        ApprovalOpinion = "同意"
                    });

                if (!approveResult.Success)
                {
                    throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 借出审批失败：{approveResult.Message}");
                }

                var handoverResult = await _hardDiskMediaService.ConfirmPhysicalHandoverAsync(
                    application,
                    operatorUser,
                    new HardDiskMediaApprovalInput
                    {
                        HandoverAdmin = operatorName,
                        HandoverDate = applyTime.AddMinutes(2)
                    });

                if (!handoverResult.Success)
                {
                    throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 借出实物交接确认失败：{handoverResult.Message}");
                }

                byte[] signedAttachmentContent = "%PDF-1.0\n%%EOF\n"u8.ToArray();
                var uploadResult = await _hardDiskMediaService.UploadSignedAttachmentAsync(
                    application,
                    operatorUser,
                    $"{application.ApplicationNo}_模拟签批交接单.pdf",
                    ".pdf",
                    signedAttachmentContent.Length,
                    signedAttachmentContent);

                if (!uploadResult.Success)
                {
                    throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 借出签批交接单上传失败：{uploadResult.Message}");
                }

                var completeResult = await _hardDiskMediaService.CompleteApplicationAsync(application, operatorUser);
                if (!completeResult.Success)
                {
                    throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 借出办结失败：{completeResult.Message}");
                }

                generatedApplicationNos.Add(application.ApplicationNo);
            }

            return generatedApplicationNos;
        }

        private static bool IsBorrowedHardDiskScenarioIndex(int scenarioIndex)
            => scenarioIndex is 3 or 7 or 11 or 15 or 19;

        private static bool IsRetainedHardDiskMedia(YearlyArchiveRegisterMedia media)
        {
            ArgumentNullException.ThrowIfNull(media);

            return string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase)
                && media.MediaType.Contains("硬盘", StringComparison.OrdinalIgnoreCase)
                && media.Disposition.Contains("留存", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<ProjectInfo> RequireDatabaseProjectsForComplexElectronic(IReadOnlyList<ProjectInfo> projects)
        {
            ArgumentNullException.ThrowIfNull(projects);

            if (projects.Count == 0)
            {
                throw new InvalidOperationException(
                    "数据库中尚无项目记录，无法生成复杂电子介质申请单。请先在「项目管理」中维护至少一个项目。");
            }

            var validProjects = projects
                .Where(project => project.Id > 0 && !string.IsNullOrWhiteSpace(project.ProjectName))
                .ToList();

            if (validProjects.Count == 0)
            {
                throw new InvalidOperationException(
                    "数据库中的项目记录缺少有效 Id 或项目名称，无法生成复杂电子介质申请单。");
            }

            return validProjects;
        }

        private static void EnsureComplexElectronicRecordProject(
            YearlyArchiveRegisterRecord record,
            SimulationTemplate template,
            int templateIndex)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(template);

            if (!template.ProjectId.HasValue || template.ProjectId.Value <= 0)
            {
                throw new InvalidOperationException(
                    $"复杂电子模拟模板第 {templateIndex + 1} 项未绑定数据库项目（所属项目为必填项）。");
            }

            if (string.IsNullOrWhiteSpace(template.ProjectName))
            {
                throw new InvalidOperationException(
                    $"复杂电子模拟模板第 {templateIndex + 1} 项的项目名称为空（所属项目为必填项）。");
            }

            record.ProjectId = template.ProjectId;
            record.ProjectName = template.ProjectName.Trim();
            record.SourceType = template.SourceType;
            record.ProvideUnit = template.ProvideUnit;
        }

        private static void ApplyTemplate(YearlyArchiveRegisterRecord record, SimulationTemplate template, DateTime createdAt)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(template);

            record.CreatedDate = createdAt;
            record.ApplicantDate = createdAt;
            record.ApplicantName = SimulatedApplicantLoginName;
            record.ProjectId = template.ProjectId;
            record.ProjectName = template.ProjectName;
            record.MaterialName = template.MaterialName;
            record.SourceType = template.SourceType;
            record.ProvideUnit = template.ProvideUnit;
            record.ArchivePurpose = template.ArchivePurpose;
            record.ProofMaterialNote = ArchiveRegisterDomainValues.NormalizeProofMaterialNote(template.ProofMaterialNote);
            record.OtherRequests = $"{SimulationMarker} {template.OtherRequests}";
        }

        private static void EnsureLoggedInUser(User? operatorUser)
        {
            if (operatorUser == null)
            {
                throw new InvalidOperationException("请先登录后再使用模拟登记。");
            }
        }

        private void EnsureArchiveAdminUser(User operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);

            if (!_archiveRegisterService.IsArchiveAdminUser(operatorUser))
            {
                throw new InvalidOperationException("仅资料室资料管理员可以执行该模拟登记操作。");
            }
        }

        /// <summary>
        /// 资料室管理员批量测试仍使用固定账号 mxc；普通申请人使用当前登录用户本人。
        /// </summary>
        private async Task<User> ResolveSimulationApplicantAsync(User operatorUser)
        {
            ArgumentNullException.ThrowIfNull(operatorUser);

            if (_archiveRegisterService.IsArchiveAdminUser(operatorUser))
            {
                return await GetSimulationApplicantAsync();
            }

            return operatorUser;
        }

        private async Task<User> GetSimulationApplicantAsync()
        {
            var applicant = await _archiveRegisterSimulationRepository.GetUserByLoginAsync(SimulatedApplicantLoginName);

            if (applicant == null)
            {
                throw new InvalidOperationException($"未找到登录名为 [{SimulatedApplicantLoginName}] 的申请人用户。");
            }

            return applicant;
        }


        private static YearlyArchiveRegisterMedia CloneMediaEntry(YearlyArchiveRegisterMedia source)
        {
            ArgumentNullException.ThrowIfNull(source);

            return new YearlyArchiveRegisterMedia
            {
                MediaKind = source.MediaKind,
                MediaType = source.MediaType,
                MediaCount = source.MediaCount,
                Disposition = source.Disposition,
                IsBorrowedHardDisk = source.IsBorrowedHardDisk,
                BorrowedHardDiskCode = source.BorrowedHardDiskCode,
                Items = source.Items.Select(CloneMediaItem).ToList()
            };
        }

        private static YearlyArchiveRegisterMediaItem CloneMediaItem(YearlyArchiveRegisterMediaItem source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var clone = new YearlyArchiveRegisterMediaItem
            {
                ItemType = source.ItemType,
                ContentDesc = source.ContentDesc,
                ContentCount = source.ContentCount,
                StoragePath = source.StoragePath,
                Note = source.Note,
                ConfidentialLevel = source.ConfidentialLevel
            };

            if (source.ElectronicDetail != null)
            {
                clone.ElectronicDetail = new YearlyArchiveRegisterElectronicMediaItemDetail
                {
                    MaterialCategory = source.ElectronicDetail.MaterialCategory,
                    SubCategory = source.ElectronicDetail.SubCategory,
                    DataOrganizationForm = source.ElectronicDetail.DataOrganizationForm,
                    DataSizeMb = source.ElectronicDetail.DataSizeMb,
                    Entries = source.ElectronicDetail.Entries
                        .Select(entry => new YearlyArchiveRegisterElectronicMediaItemEntry
                        {
                            EntryKind = entry.EntryKind,
                            EntryName = entry.EntryName,
                            RelativePath = entry.RelativePath,
                            SizeMb = entry.SizeMb,
                            SortOrder = entry.SortOrder
                        })
                        .ToList()
                };
            }

            return clone;
        }

        private static List<SimulationTemplate> BuildTemplates(
            ArchiveRegisterPageDomainOptions domainOptions,
            IReadOnlyList<ProjectInfo> projects,
            User applicant)
        {
            ArgumentNullException.ThrowIfNull(domainOptions);
            ArgumentNullException.ThrowIfNull(projects);
            ArgumentNullException.ThrowIfNull(applicant);

            string sourceTypeInternal = PickOrFallback(domainOptions.SourceTypes, ArchiveRegisterDomainValues.SourceTypeInternal, ArchiveRegisterDomainValues.SourceTypeExternal);
            string sourceTypeExternal = PickOrFallback(domainOptions.SourceTypes, ArchiveRegisterDomainValues.SourceTypeExternal, ArchiveRegisterDomainValues.SourceTypeInternal);
            string archivePurposePrimary = PickOrFallback(domainOptions.ArchivePurposes, "归档", "长期保存", "备查");
            string archivePurposeSecondary = PickOrFallback(domainOptions.ArchivePurposes, "备查", archivePurposePrimary);
            string confidentialLevel = PickOrFallback(domainOptions.ConfidentialLevels, ArchiveRegisterDomainValues.ConfidentialLevelNone, ArchiveRegisterDomainValues.LegacyConfidentialLevelNone);
            string electronicDisposition = PickOrFallback(domainOptions.DataElectronicDispositions, "归档保存", "长期保存", string.Empty);
            string simulatedDisposition = PickOrFallback(domainOptions.DataSimulatedDispositions, "入库", "归档保存", string.Empty);
            string hardDiskType = PickByKeywordOrFallback(domainOptions.DataElectronicMediaTypes, new[] { "硬盘" }, "硬盘");
            string opticalDiskType = PickByKeywordOrFallback(domainOptions.DataElectronicMediaTypes, new[] { "光盘", "DVD", "蓝光" }, "光盘");
            string usbDiskType = PickByKeywordOrFallback(domainOptions.DataElectronicMediaTypes, new[] { "U盘", "移动" }, "U盘");
            string simulatedDataType = PickFirstNonEmpty(domainOptions.DataSimulatedMediaTypes, "档案盒");

            var internalProject = projects.FirstOrDefault();
            string internalProvideUnit = !string.IsNullOrWhiteSpace(internalProject?.CapitalMgrDept)
                ? internalProject.CapitalMgrDept
                : applicant.Department;

            var templates = new List<SimulationTemplate>
            {
                new(
                    internalProject?.Id,
                    internalProject?.ProjectName,
                    internalProject == null ? sourceTypeExternal : sourceTypeInternal,
                    internalProject == null ? "省自然资源厅资料交换中心" : internalProvideUnit,
                    "模拟单-01：地形图纸质资料",
                    archivePurposePrimary,
                    "模拟生成：单一模拟介质，适合测试模拟立档。",
                    [
                        CreateSimulatedMedia(simulatedDataType, 2, simulatedDisposition,
                            CreateItem(ArchiveRegisterDomainValues.ItemTypeData, "1:10000 地形图 12 幅", 12, note: "标准纸质成果"))
                    ]),
                new(
                    internalProject?.Id,
                    internalProject?.ProjectName,
                    internalProject == null ? sourceTypeExternal : sourceTypeInternal,
                    internalProject == null ? "外业协作单位" : internalProvideUnit,
                    "模拟单-02：光盘电子成果",
                    archivePurposePrimary,
                    "模拟生成：单一电子介质，适合测试电子立档。",
                    [
                        CreateElectronicMedia(opticalDiskType, electronicDisposition,
                            CreateElectronicItem("项目数据库成果包", 1, "/archive/2026/optical/01/db", "含元数据与说明书", domainOptions))
                    ]),
                new(
                    internalProject?.Id,
                    internalProject?.ProjectName,
                    internalProject == null ? sourceTypeExternal : sourceTypeInternal,
                    internalProject == null ? "外部测绘合作单位" : internalProvideUnit,
                    "模拟单-03：硬盘+证明材料混合介质",
                    archivePurposeSecondary,
                    "模拟生成：硬盘电子资料和纸质证明并存，适合测试混合立档。",
                    [
                        CreateElectronicMedia(hardDiskType, electronicDisposition,
                            CreateElectronicItem("DOM/DEM/矢量一体化成果", 3, "/archive/2026/harddisk/hd-03/data", "包含成果、质检与说明", domainOptions))
                    ],
                    "项目批复及移交证明"),
                new(
                    null,
                    null,
                    sourceTypeExternal,
                    "省级资料交换平台",
                    "模拟单-04：外来U盘资料",
                    archivePurposeSecondary,
                    "模拟生成：外来电子资料，适合测试外来资料立档。",
                    [
                        CreateElectronicMedia(usbDiskType, electronicDisposition,
                            CreateElectronicItem("外来控制点成果及说明", 2, "/archive/2026/external/usb-04/root", "含扫描件与清单", domainOptions))
                    ]),
                new(
                    internalProject?.Id,
                    internalProject?.ProjectName,
                    internalProject == null ? sourceTypeExternal : sourceTypeInternal,
                    internalProject == null ? "联合生产单位" : internalProvideUnit,
                    "模拟单-05：多介质综合成果",
                    archivePurposePrimary,
                    "模拟生成：单一电子介质类型、多模拟介质及多内容项，适合测试复杂并入与立档。",
                    [
                        CreateElectronicMedia(hardDiskType, electronicDisposition,
                            CreateElectronicItem("航摄影像原始数据", 6, "/archive/2026/complex/harddisk-05/raw", "原始航摄分区", domainOptions),
                            CreateElectronicItem("空三加密成果", 2, "/archive/2026/complex/harddisk-05/at", "空三报告与质量检查", domainOptions),
                            CreateElectronicItem("数据库发布包", 1, "/archive/2026/complex/harddisk-05/release", "含发布脚本", domainOptions)),
                        CreateSimulatedMedia(simulatedDataType, 3, simulatedDisposition,
                            CreateItem(ArchiveRegisterDomainValues.ItemTypeData, "纸质成图及索引", 18, note: "含分幅索引与装订图册"))
                    ],
                    "验收会签材料")
            };

            ApplyConfidentialLevelToMediaEntries(templates.SelectMany(template => template.MediaEntries), confidentialLevel);
            return templates;
        }

        private static void ApplyConfidentialLevelToMediaEntries(
            IEnumerable<YearlyArchiveRegisterMedia> mediaEntries,
            string confidentialLevel)
        {
            string normalized = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(confidentialLevel);
            foreach (var media in mediaEntries)
            {
                foreach (var item in media.Items)
                {
                    item.ConfidentialLevel = normalized;
                }
            }
        }

        private static List<SimulationTemplate> BuildComplexElectronicTemplates(
            ArchiveRegisterPageDomainOptions domainOptions,
            IReadOnlyList<ProjectInfo> projects,
            User applicant)
        {
            ArgumentNullException.ThrowIfNull(domainOptions);
            ArgumentNullException.ThrowIfNull(projects);
            ArgumentNullException.ThrowIfNull(applicant);

            var databaseProjects = RequireDatabaseProjectsForComplexElectronic(projects);

            string sourceTypeInternal = PickOrFallback(domainOptions.SourceTypes, ArchiveRegisterDomainValues.SourceTypeInternal, ArchiveRegisterDomainValues.SourceTypeExternal);
            string archivePurposePrimary = PickOrFallback(domainOptions.ArchivePurposes, "归档", "长期保存", "备查");
            string archivePurposeSecondary = PickOrFallback(domainOptions.ArchivePurposes, "备查", archivePurposePrimary);
            string confidentialLevel = PickOrFallback(domainOptions.ConfidentialLevels, ArchiveRegisterDomainValues.ConfidentialLevelNone, ArchiveRegisterDomainValues.LegacyConfidentialLevelNone);
            string hardDiskType = PickByKeywordOrFallback(domainOptions.DataElectronicMediaTypes, new[] { "硬盘" }, "硬盘");
            string opticalDiskType = PickByKeywordOrFallback(domainOptions.DataElectronicMediaTypes, new[] { "光盘", "DVD", "蓝光" }, "光盘");
            string usbDiskType = PickByKeywordOrFallback(domainOptions.DataElectronicMediaTypes, new[] { "U盘", "移动" }, "U盘");
            string intranetType = PickByKeywordOrFallback(domainOptions.DataElectronicMediaTypes, new[] { "内网" }, "内网");
            string retainedDisposition = PickByKeywordOrFallback(domainOptions.DataElectronicDispositions, new[] { "留存" }, "介质留存");
            string returnDisposition = PickByKeywordOrFallback(domainOptions.DataElectronicDispositions, new[] { "带回" }, "介质带回");
            string noneDisposition = PickByKeywordOrFallback(domainOptions.DataElectronicDispositions, new[] { "无需", "不处置", "免处置" }, ArchiveRegisterDomainValues.ElectronicDispositionNone);
            string simulatedDisposition = ArchiveRegisterDomainValues.SimulatedDispositionRetain;
            string simulatedDataType = PickFirstNonEmpty(domainOptions.DataSimulatedMediaTypes, "档案盒");

            var templates = new List<SimulationTemplate>(DefaultComplexElectronicSimulationCount);

            (int ProjectId, string ProjectName, string SourceType, string ProvideUnit) ResolveSource(int sequence)
            {
                var targetProject = databaseProjects[(sequence - 1) % databaseProjects.Count];
                string provideUnit = !string.IsNullOrWhiteSpace(targetProject.CapitalMgrDept)
                    ? targetProject.CapitalMgrDept.Trim()
                    : applicant.Department?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(provideUnit))
                {
                    throw new InvalidOperationException(
                        $"项目 [{targetProject.ProjectName}] 未配置提供部门（厅资金管理部门），且申请人无部门信息，无法生成复杂电子申请单。");
                }

                return (targetProject.Id, targetProject.ProjectName.Trim(), sourceTypeInternal, provideUnit);
            }

            for (int sequence = 1; sequence <= DefaultComplexElectronicSimulationCount; sequence++)
            {
                var source = ResolveSource(sequence);
                string archivePurpose = sequence % 2 == 0 ? archivePurposeSecondary : archivePurposePrimary;
                string complexityLabel = sequence switch
                {
                    <= 5 => "基础",
                    <= 10 => "进阶",
                    <= 15 => "复杂",
                    _ => "综合"
                };

                var mediaEntries = new List<YearlyArchiveRegisterMedia>();
                string detail;
                string proofMaterialNote = ArchiveRegisterDomainValues.ProofMaterialNoneText;

                switch (sequence)
                {
                    case 1:
                        detail = "单光盘留存、单内容项";
                        mediaEntries.Add(CreateElectronicMedia(opticalDiskType, retainedDisposition,
                            CreateElectronicItem("地形图发布成果", 1, BuildComplexElectronicItemStoragePath("optical-basic", sequence, 1), "单介质单条目", domainOptions)));
                        break;
                    case 2:
                        detail = "单U盘带回、单内容项";
                        mediaEntries.Add(CreateElectronicMedia(usbDiskType, returnDisposition,
                            CreateElectronicItem("控制点复测成果", 1, BuildComplexElectronicItemStoragePath("usb-basic", sequence, 1), "外来介质带回", domainOptions)));
                        break;
                    case 3:
                        detail = "单内网无需处置、双内容项";
                        mediaEntries.Add(CreateElectronicMedia(intranetType, noneDisposition,
                            CreateElectronicItem("内网共享目录成果A", 2, BuildComplexElectronicItemStoragePath("intranet-basic", sequence, 1), "内网无需处置", domainOptions),
                            CreateElectronicItem("内网共享目录成果B", 1, BuildComplexElectronicItemStoragePath("intranet-basic", sequence, 2), "补充目录", domainOptions)));
                        break;
                    case 4:
                        detail = "单硬盘留存、多内容项";
                        mediaEntries.Add(CreateElectronicMedia(hardDiskType, retainedDisposition,
                            CreateElectronicItem("DOM原始数据", 4, BuildComplexElectronicItemStoragePath("disk-basic", sequence, 1), "目录型", domainOptions),
                            CreateElectronicItem("DEM成果", 2, BuildComplexElectronicItemStoragePath("disk-basic", sequence, 2), "目录型", domainOptions)));
                        break;
                    case 5:
                        detail = "硬盘带回+证明材料";
                        mediaEntries.Add(CreateElectronicMedia(hardDiskType, returnDisposition,
                            CreateElectronicItem("外协提交成果", 2, BuildComplexElectronicItemStoragePath("disk-return", sequence, 1), "介质带回", domainOptions)));
                        proofMaterialNote = "签收与移交证明";
                        break;
                    case 8:
                    case 12:
                    case 16:
                    case 20:
                        detail = "借出硬盘留存、多内容项（供立档归还登记联调）";
                        mediaEntries.Add(CreateElectronicMedia(hardDiskType, retainedDisposition,
                            CreateElectronicItem("DOM原始数据", 3, BuildComplexElectronicItemStoragePath("disk-borrowed", sequence, 1), "借出留存主包", domainOptions),
                            CreateElectronicItem("DEM与索引", 2, BuildComplexElectronicItemStoragePath("disk-borrowed", sequence, 2), "借出留存补充", domainOptions),
                            CreateElectronicItem("元数据清单", 1, BuildComplexElectronicItemStoragePath("disk-borrowed", sequence, 3), "目录型", domainOptions)));
                        break;
                    default:
                        // 正式提交校验要求：同一申请单内电子介质只能有一种类型、一种处置方式。
                        int electronicCount = sequence <= 10 ? 2 : sequence <= 15 ? 3 : 4;
                        string[] mediaTypes = [hardDiskType, opticalDiskType, usbDiskType, intranetType];
                        string[] dispositions = [retainedDisposition, returnDisposition, noneDisposition];
                        string primaryMediaType = mediaTypes[(sequence - 1) % mediaTypes.Length];
                        string primaryDisposition = dispositions[(sequence - 1) % dispositions.Length];
                        for (int i = 0; i < electronicCount; i++)
                        {
                            int itemCount = sequence <= 10 ? 1 : (i % 2 == 0 ? 2 : 1);
                            var items = new List<YearlyArchiveRegisterMediaItem>();
                            for (int itemIndex = 1; itemIndex <= itemCount; itemIndex++)
                            {
                                items.Add(CreateElectronicItem(
                                    BuildComplexElectronicContentDesc(primaryMediaType, sequence, itemIndex),
                                    Math.Max(1, (sequence + itemIndex) % 5),
                                    BuildComplexElectronicItemStoragePath($"combo-{i + 1}", sequence, itemIndex),
                                    $"{primaryMediaType}第{i + 1}介质-条目{itemIndex}",
                                    domainOptions));
                            }

                            mediaEntries.Add(CreateElectronicMedia(primaryMediaType, primaryDisposition, items.ToArray()));
                        }

                        if (sequence % 3 == 0)
                        {
                            mediaEntries.Add(CreateSimulatedMedia(simulatedDataType, 1 + sequence % 2, simulatedDisposition,
                                CreateItem(ArchiveRegisterDomainValues.ItemTypeData, $"配套纸质成果清单 {sequence:D2}", 6 + sequence % 4, "电子-纸质联动立档")));
                        }

                        if (sequence % 4 == 0)
                        {
                            proofMaterialNote = $"验收证明材料 {sequence:D2}";
                        }

                        detail = sequence <= 10
                            ? "双介质组合场景"
                            : sequence <= 15
                                ? "多介质混合场景"
                                : "多介质+模拟介质综合场景";
                        break;
                }

                templates.Add(new SimulationTemplate(
                    source.ProjectId,
                    source.ProjectName,
                    source.SourceType,
                    source.ProvideUnit,
                    $"复杂电子单-{sequence:D2}：{complexityLabel}场景",
                    archivePurpose,
                    $"复杂场景{sequence:D2}：{detail}，覆盖介质处置与立档组合测试。",
                    mediaEntries,
                    proofMaterialNote));
            }

            ApplyConfidentialLevelToMediaEntries(templates.SelectMany(template => template.MediaEntries), confidentialLevel);
            return templates;
        }

        private static string BuildComplexElectronicItemStoragePath(string pathSegment, int sequence, int itemIndex)
            => pathSegment == "intranet"
                ? $@"\\intranet\archive\2026\complex-e\{sequence:D2}\share-{itemIndex:D2}\payload"
                : $"/archive/2026/complex-e/{sequence:D2}/{pathSegment}-{itemIndex:D2}/payload";

        private static string BuildComplexElectronicContentDesc(string mediaLabel, int sequence, int itemIndex)
            => $"{mediaLabel}成果包 {sequence:D2}-{itemIndex:D2}";

        private static YearlyArchiveRegisterMedia CreateElectronicMedia(string mediaType, string disposition, params YearlyArchiveRegisterMediaItem[] items)
            => new()
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
                MediaType = mediaType,
                MediaCount = 1,
                Disposition = disposition,
                Items = items.ToList()
            };

        private static YearlyArchiveRegisterMedia CreateSimulatedMedia(string mediaType, int mediaCount, string disposition, params YearlyArchiveRegisterMediaItem[] items)
            => new()
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindSimulated,
                MediaType = mediaType,
                MediaCount = mediaCount,
                Disposition = disposition,
                Items = items.ToList()
            };

        private static YearlyArchiveRegisterMediaItem CreateItem(
            string itemType,
            string contentDesc,
            int contentCount,
            string? note = null,
            string? confidentialLevel = null)
            => new()
            {
                ItemType = itemType,
                ContentDesc = contentDesc,
                ContentCount = contentCount,
                StoragePath = string.Empty,
                Note = note ?? string.Empty,
                ConfidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(
                    confidentialLevel ?? ArchiveRegisterDomainValues.ConfidentialLevelNone)
            };

        private static YearlyArchiveRegisterMediaItem CreateElectronicItem(
            string contentDesc,
            int contentCount,
            string storagePath,
            string? note,
            ArchiveRegisterPageDomainOptions domainOptions,
            string? confidentialLevel = null)
        {
            string materialCategory = domainOptions.ElectronicMaterialCategories.FirstOrDefault()
                ?? ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument;
            string subCategory = string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData, StringComparison.Ordinal)
                ? domainOptions.ElectronicDataSubCategories.FirstOrDefault() ?? "原始观测数据"
                : domainOptions.ElectronicDocumentSubCategories.FirstOrDefault() ?? "外来资料类";
            string organizationForm = domainOptions.ElectronicDataOrganizationForms.FirstOrDefault()
                ?? ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory;
            string entryKind = ElectronicMediaItemSupport.ResolveEntryKind(organizationForm);

            return new YearlyArchiveRegisterMediaItem
            {
                ItemType = ArchiveRegisterDomainValues.ItemTypeData,
                ContentDesc = contentDesc,
                ContentCount = contentCount,
                StoragePath = ElectronicMediaItemSupport.FormatStoragePathForRegistration(storagePath),
                Note = note ?? string.Empty,
                ConfidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(
                    confidentialLevel ?? ArchiveRegisterDomainValues.ConfidentialLevelNone),
                ElectronicDetail = new YearlyArchiveRegisterElectronicMediaItemDetail
                {
                    MaterialCategory = materialCategory,
                    SubCategory = subCategory,
                    DataOrganizationForm = organizationForm,
                    DataSizeMb = 128.5m,
                    Entries =
                    [
                        new YearlyArchiveRegisterElectronicMediaItemEntry
                        {
                            EntryKind = entryKind,
                            EntryName = "payload",
                            RelativePath = "payload",
                            SizeMb = 64.25m,
                            SortOrder = 10
                        }
                    ]
                }
            };
        }

        private static string PickOrFallback(IReadOnlyList<string> options, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (options.Any(option => string.Equals(option, candidate, StringComparison.Ordinal)))
                {
                    return candidate;
                }
            }

            return PickFirstNonEmpty(options, candidates.FirstOrDefault() ?? string.Empty);
        }

        private static string PickByKeywordOrFallback(IReadOnlyList<string> options, IEnumerable<string> keywords, string fallback)
        {
            foreach (var option in options)
            {
                if (keywords.Any(keyword => option.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    return option;
                }
            }

            return PickFirstNonEmpty(options, fallback);
        }

        private static string PickFirstNonEmpty(IReadOnlyList<string> options, string fallback)
            => options.FirstOrDefault(option => !string.IsNullOrWhiteSpace(option)) ?? fallback;

        private sealed record SimulationTemplate(
            int? ProjectId,
            string? ProjectName,
            string SourceType,
            string ProvideUnit,
            string MaterialName,
            string ArchivePurpose,
            string OtherRequests,
            IReadOnlyList<YearlyArchiveRegisterMedia> MediaEntries,
            string ProofMaterialNote = ArchiveRegisterDomainValues.ProofMaterialNoneText);
    }
}
