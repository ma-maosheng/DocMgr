using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 按资料立档操作台同一套场景决策（<see cref="IArchiveFilingService.ResolveElectronicArchiveUiDecision"/>）
    /// 组装电子介质新建袋提交请求，供自动化测试与界面提交共用逻辑。
    /// </summary>
    public sealed class ArchiveFilingElectronicSubmissionRequestBuilder
    {
        private readonly IArchiveFilingService _archiveFilingService;
        private readonly IHardDiskMediaService _hardDiskMediaService;

        public ArchiveFilingElectronicSubmissionRequestBuilder(
            IArchiveFilingService archiveFilingService,
            IHardDiskMediaService hardDiskMediaService)
        {
            _archiveFilingService = archiveFilingService;
            _hardDiskMediaService = hardDiskMediaService;
        }

        public async Task<ElectronicArchiveSubmissionRequest> BuildForNewBagAsync(
            ArchiveFilingElectronicSubmissionBuildOptions options,
            ISet<string>? usedBlankHardDiskCodes = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(options.Record);
            ArgumentNullException.ThrowIfNull(options.MediaEntry);
            ArgumentNullException.ThrowIfNull(options.MediaItems);
            ArgumentNullException.ThrowIfNull(options.OperatorUser);

            var record = options.Record;
            var mediaEntry = options.MediaEntry;
            var mediaItems = options.MediaItems;
            var operatorUser = options.OperatorUser;

            string year = record.CreatedDate.Year.ToString();
            string projectName = ArchiveFilingBusinessRules.ResolveElectronicArchiveProjectName(record);
            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new InvalidOperationException($"登记单 [{record.FormNo}] 未确定所属项目，无法立档。");
            }

            string disposition = mediaEntry.Disposition?.Trim() ?? string.Empty;
            string mediaType = mediaEntry.MediaType?.Trim() ?? string.Empty;
            bool isBorrowedHardDisk = mediaEntry.IsBorrowedHardDisk && !string.IsNullOrWhiteSpace(mediaEntry.BorrowedHardDiskCode);
            bool isRetainedHardDiskScenario = string.Equals(mediaType, ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.Ordinal)
                && string.Equals(disposition, ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.Ordinal);
            bool isOpticalDiscArchiveScenario = string.Equals(mediaType, ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc, StringComparison.Ordinal)
                && string.Equals(disposition, ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.Ordinal);

            var existingUnits = await _archiveFilingService.GetExistingElectronicUnitsForProjectAsync(projectName, year);
            var decision = _archiveFilingService.ResolveElectronicArchiveUiDecision(new ElectronicArchiveScenarioInput
            {
                ProjectName = projectName,
                Year = year,
                SelectedMediaTypes = [mediaType],
                Disposition = disposition,
                SelectedMediaEntryIds = mediaEntry.Id > 0 ? [mediaEntry.Id] : [],
                ExistingElectronicUnits = existingUnits,
                SelectedArchiveAction = ElectronicArchiveArchiveAction.New,
                StepOneMediumCode = isBorrowedHardDisk ? mediaEntry.BorrowedHardDiskCode : null,
                SelectedRetainedHardDiskSource = isBorrowedHardDisk
                    ? ArchiveFilingBusinessRules.BorrowedHardDiskSourceOption
                    : ArchiveFilingBusinessRules.ExternalHardDiskSourceOption
            });

            ElectronicArchiveSubmissionMode submissionMode = ResolveSubmissionMode(decision, mediaType, disposition);
            if (submissionMode is ElectronicArchiveSubmissionMode.CopyAppendExistingHardDisk
                or ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk)
            {
                submissionMode = isRetainedHardDiskScenario
                    ? ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew
                    : ElectronicArchiveSubmissionMode.CopyNewOpticalDisc;
            }

            string electronicNo = await _archiveFilingService.GenerateNextElectronicArchiveNoAsync(year);
            string linkedMediumCodes = string.Empty;
            HardDiskMediaReturnCandidate? borrowedHardDiskCandidate = null;
            PendingExternalHardDiskRegistration? pendingExternalHardDisk = null;
            string filingMode = decision.AvailableModes
                .FirstOrDefault(item => item.Mode == submissionMode)?.DisplayName
                ?? submissionMode.ToString();

            switch (submissionMode)
            {
                case ElectronicArchiveSubmissionMode.CopyNewHardDisk:
                    linkedMediumCodes = await ResolveUnusedBlankHardDiskCodeAsync(usedBlankHardDiskCodes);
                    filingMode = ArchiveFilingBusinessRules.BlankHardDiskSourceOption;
                    break;

                case ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew:
                    if (isBorrowedHardDisk)
                    {
                        linkedMediumCodes = mediaEntry.BorrowedHardDiskCode!.Trim();
                        borrowedHardDiskCandidate = await _hardDiskMediaService.GetReturnRegistrationCandidateByDiskCodeAsync(linkedMediumCodes)
                            ?? throw new InvalidOperationException($"未找到借出硬盘 [{linkedMediumCodes}] 的归还登记候选信息。");
                    }
                    else
                    {
                        pendingExternalHardDisk = CreatePendingExternalHardDiskRegistration(
                            record,
                            mediaEntry,
                            operatorUser,
                            options.ExternalDiskCodePrefix);
                        linkedMediumCodes = pendingExternalHardDisk.DiskCode;
                    }

                    break;

                case ElectronicArchiveSubmissionMode.CopyNewOpticalDisc:
                case ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew:
                case ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc:
                    linkedMediumCodes = string.Empty;
                    break;

                default:
                    throw new InvalidOperationException($"当前编排暂不支持提交模式 [{submissionMode}]。");
            }

            bool requiresFormatRetainedHardDisk = submissionMode is ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc
                or ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk
                || (isRetainedHardDiskScenario && submissionMode == ElectronicArchiveSubmissionMode.CopyNewHardDisk);

            bool isOpticalDiscCarrier = submissionMode is ElectronicArchiveSubmissionMode.CopyNewOpticalDisc
                or ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew
                or ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc;

            string defaultLocation = await ResolveStorageLocationByCarrierTypeAsync(
                isOpticalDiscCarrier,
                options.ReservedDedicatedFullLocations);
            string storagePath = $"{options.StoragePathPrefix.TrimEnd('/')}/{year}/{record.FormNo}/{mediaEntry.Id}";
            var filingStoragePath = mediaItems.ToDictionary(item => item.Id, _ => storagePath);
            string operatorName = string.IsNullOrWhiteSpace(operatorUser.RealName)
                ? operatorUser.LoginName ?? "Unknown"
                : operatorUser.RealName;

            var unit = new YearlyElectronicArchiveUnit
            {
                ElectronicArchiveNo = electronicNo,
                ProjectName = projectName,
                Year = year,
                StorageCarrierType = decision.StorageCarrierType,
                StoragePath = storagePath,
                StorageLocation = defaultLocation,
                LinkedMediumCodes = linkedMediumCodes,
                Disposition = disposition,
                MediaCount = isOpticalDiscCarrier || isOpticalDiscArchiveScenario ? Math.Max(1, mediaEntry.MediaCount) : 1,
                ContentSummary = BuildContentSummary(mediaItems),
                ArchivedBy = operatorName,
                SourceType = record.SourceType,
                SourceRecordKey = record.FormNo,
                Remarks = options.Remarks
            };

            return new ElectronicArchiveSubmissionRequest
            {
                ArchiveUnit = unit,
                MediaItemIds = mediaItems.Select(item => item.Id).ToList(),
                FilingStoragePathByMediaItemId = filingStoragePath,
                MediaEntryIds = mediaEntry.Id > 0 ? [mediaEntry.Id] : [],
                SubmissionMode = submissionMode,
                IsRetainedHardDiskScenario = isRetainedHardDiskScenario,
                IsOpticalDiscArchiveScenario = isOpticalDiscArchiveScenario,
                FilingMode = filingMode,
                RequiresFormatRetainedHardDisk = requiresFormatRetainedHardDisk,
                BorrowedHardDiskCandidate = borrowedHardDiskCandidate,
                PendingExternalHardDisk = pendingExternalHardDisk
            };
        }

        private static ElectronicArchiveSubmissionMode ResolveSubmissionMode(
            ElectronicArchiveUiDecision decision,
            string mediaType,
            string disposition)
        {
            if (decision.SelectedMode != null)
            {
                return decision.SelectedMode.Value;
            }

            bool isRetained = string.Equals(disposition, ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.Ordinal);
            if (string.Equals(mediaType, ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc, StringComparison.Ordinal))
            {
                return isRetained
                    ? ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew
                    : ElectronicArchiveSubmissionMode.CopyNewOpticalDisc;
            }

            if (string.Equals(mediaType, ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.Ordinal) && isRetained)
            {
                return ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew;
            }

            return ElectronicArchiveSubmissionMode.CopyNewOpticalDisc;
        }

        private static PendingExternalHardDiskRegistration CreatePendingExternalHardDiskRegistration(
            YearlyArchiveRegisterRecord record,
            YearlyArchiveRegisterMedia mediaEntry,
            User operatorUser,
            string diskCodePrefix)
        {
            string operatorName = string.IsNullOrWhiteSpace(operatorUser.RealName)
                ? operatorUser.LoginName?.Trim() ?? "auto"
                : operatorUser.RealName.Trim();
            string diskCode = $"{diskCodePrefix}-{SanitizeDiskCodePart(record.FormNo)}-{mediaEntry.Id}";

            return new PendingExternalHardDiskRegistration
            {
                DiskCode = diskCode,
                SerialNumber = $"SN-{diskCode}",
                DiskType = "移动硬盘",
                Brand = "电子立档测试",
                Capacity = "1024GB",
                InterfaceType = "USB3.0",
                RegisterPerson = operatorName,
                RegisterDate = DateTime.Today,
                RegistrationMethod = "电子立档测试",
                CurrentLocation = "外来硬盘-待立档",
                CurrentStatus = HardDiskMedium.StatusInStockData,
                MediaNature = "数据盘",
                CurrentHolder = operatorName,
                NeedReturn = false,
                DataDescription = BuildContentSummary(mediaEntry.Items),
                Remark = "电子立档测试登记外来留存硬盘"
            };
        }

        private async Task<string> ResolveUnusedBlankHardDiskCodeAsync(ISet<string>? usedBlankHardDiskCodes)
        {
            var selectableMedia = await _hardDiskMediaService.GetSelectableMediaAsync();
            foreach (var medium in selectableMedia.OrderBy(item => item.DiskCode, StringComparer.OrdinalIgnoreCase))
            {
                if (medium.Id <= 0 || medium.RegisterLock != null || medium.Ledger == null)
                {
                    continue;
                }

                if (!string.Equals(medium.Ledger.HolderOrOrganization, "资料室", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(medium.Ledger.MediaStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
                {
                    continue;
                }

                string code = medium.DiskCode.Trim();
                if (string.IsNullOrWhiteSpace(code) || usedBlankHardDiskCodes?.Contains(code) == true)
                {
                    continue;
                }

                var linkedUnits = await _archiveFilingService.GetElectronicArchiveLinkInfosAsync([medium.Id]);
                if (linkedUnits.Count > 0)
                {
                    continue;
                }

                usedBlankHardDiskCodes?.Add(code);
                return code;
            }

            throw new InvalidOperationException("未找到可用于拷贝型立档的库内空硬盘。请先补充在库空盘，或清理已占用硬盘后再试。");
        }

        private async Task<string> ResolveStorageLocationByCarrierTypeAsync(
            bool isOpticalDiscCarrier,
            ISet<string>? reservedFullLocations = null)
        {
            string categoryName = isOpticalDiscCarrier
                ? CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc
                : CabinetHardDiskSlotCategoryAssignment.CategoryData;

            string? fullLocation = await _hardDiskMediaService.AllocateNextDedicatedFullLocationAsync(
                categoryName,
                reservedFullLocations: reservedFullLocations);
            if (!string.IsNullOrWhiteSpace(fullLocation))
            {
                return fullLocation.Trim();
            }

            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(categoryName);
            throw new InvalidOperationException(
                isOpticalDiscCarrier
                    ? $"未找到仍有容量的年度数据光盘专用档口（每档口最多 {slotCapacity} 盘），请先在磁盘柜开柜界面完成设置或启用新档口。"
                    : $"未找到仍有容量的年度数据硬盘专用档口（每档口最多 {slotCapacity} 盘），请先在磁盘柜开柜界面完成设置或启用新档口。");
        }

        private static string BuildContentSummary(IEnumerable<YearlyArchiveRegisterMediaItem> mediaItems)
        {
            string summary = string.Join("；", mediaItems
                .Select(item => item.ContentDesc?.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Take(3));

            return string.IsNullOrWhiteSpace(summary) ? "电子立档测试" : summary;
        }

        private static string SanitizeDiskCodePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "UNKNOWN";
            }

            return new string(value.Trim().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        }
    }

    public sealed class ArchiveFilingElectronicSubmissionBuildOptions
    {
        public required YearlyArchiveRegisterRecord Record { get; init; }

        public required YearlyArchiveRegisterMedia MediaEntry { get; init; }

        public required IReadOnlyList<YearlyArchiveRegisterMediaItem> MediaItems { get; init; }

        public required User OperatorUser { get; init; }

        public required string Remarks { get; init; }

        public required string StoragePathPrefix { get; init; }

        public string ExternalDiskCodePrefix { get; init; } = "TEST-EXT";

        /// <summary>
        /// 立档测试等同批次内已预占、尚未落库的专用档口完整位置（如 壬A-1-2-03）。
        /// </summary>
        public ISet<string>? ReservedDedicatedFullLocations { get; init; }
    }
}
