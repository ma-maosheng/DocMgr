using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Projects;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 存量硬盘直办立档服务。
    /// </summary>
    public sealed class StockHardDiskDirectFilingService : IStockHardDiskDirectFilingService
    {
        private readonly IProjectService _projectService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IArchiveRegisterRepository _archiveRegisterRepository;
        private readonly IArchiveFilingService _archiveFilingService;
        private readonly IStockDirectFilingYearProjectCatalog _yearProjectCatalog;

        public StockHardDiskDirectFilingService(
            IProjectService projectService,
            IHardDiskMediaService hardDiskMediaService,
            IArchiveRegisterService archiveRegisterService,
            IArchiveRegisterRepository archiveRegisterRepository,
            IArchiveFilingService archiveFilingService,
            IStockDirectFilingYearProjectCatalog yearProjectCatalog)
        {
            _projectService = projectService;
            _hardDiskMediaService = hardDiskMediaService;
            _archiveRegisterService = archiveRegisterService;
            _archiveRegisterRepository = archiveRegisterRepository;
            _archiveFilingService = archiveFilingService;
            _yearProjectCatalog = yearProjectCatalog;
        }

        /// <inheritdoc/>
        public StockHardDiskDirectoryScanResult ScanDirectory(string? rootPath)
            => StockHardDiskDirectFilingDirectorySupport.ScanRoot(rootPath);

        /// <inheritdoc/>
        public ProjectInfo? FindProject(string year, string projectName)
        {
            string normalizedYear = year?.Trim() ?? string.Empty;
            string normalizedName = projectName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedYear) || string.IsNullOrWhiteSpace(normalizedName))
            {
                return null;
            }

            return _projectService.SearchProjects(normalizedYear, normalizedName)
                .FirstOrDefault(item =>
                    string.Equals(item.ImplementYear?.Trim(), normalizedYear, StringComparison.Ordinal)
                    && string.Equals(item.ProjectName?.Trim(), normalizedName, StringComparison.Ordinal));
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> ListRegisteredYears()
            => _yearProjectCatalog.ListRegisteredYears();

        /// <inheritdoc/>
        public IReadOnlyList<string> ListRegisteredProjectNames(string year)
            => _yearProjectCatalog.ListRegisteredProjectNames(year);

        /// <inheritdoc/>
        public IReadOnlyList<ProjectInfo> ListProjectsByYear(string year)
            => _yearProjectCatalog.ListRegisteredProjects(year);

        /// <inheritdoc/>
        public async Task<int> CountExistingHardDiskBagsAsync(string projectName, string year)
        {
            var units = await _archiveFilingService.GetExistingElectronicUnitsForProjectAsync(projectName, year);
            return units.Count(unit => ArchiveFilingBusinessRules.IsHardDiskArchiveCarrierType(unit.StorageCarrierType));
        }

        /// <inheritdoc/>
        public Task<HardDiskMedium?> FindMediumBySerialNumberAsync(string? serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                return Task.FromResult<HardDiskMedium?>(null);
            }

            return _hardDiskMediaService.FindRegisteredMediumBySerialNumberAsync(0, serialNumber.Trim());
        }

        /// <inheritdoc/>
        public Task<string?> RecommendDataSlotLocationAsync()
            => _hardDiskMediaService.AllocateNextDedicatedFullLocationAsync(CabinetHardDiskSlotCategoryAssignment.CategoryData);

        /// <inheritdoc/>
        public Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetDataSlotOptionsAsync()
            => _hardDiskMediaService.GetDedicatedTargetLocationOptionsAsync(CabinetHardDiskSlotCategoryAssignment.CategoryData);

        /// <inheritdoc/>
        public Task<string> ResolveDataFullLocationAsync(string? requestedLocation)
            => _hardDiskMediaService.ResolveDataInStockFullLocationAsync(requestedLocation);

        /// <inheritdoc/>
        public Task<string> PeekNextElectronicArchiveNoAsync(string year)
            => _archiveFilingService.GenerateNextElectronicArchiveNoAsync(year);

        /// <inheritdoc/>
        public async Task<StockHardDiskDirectFilingResult> CommitAsync(StockHardDiskDirectFilingRequest request, User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
            {
                return StockHardDiskDirectFilingResult.Fail("仅资料室资料管理员可执行存量硬盘直办立档。");
            }

            var errors = await CollectCommitErrorsAsync(request, currentUser);
            if (errors.Count > 0)
            {
                return StockHardDiskDirectFilingResult.Fail(string.Join(Environment.NewLine, errors));
            }

            try
            {
                ProjectInfo project = EnsureProject(request);
                HardDiskMedium medium = await EnsureHardDiskMediumAsync(request, currentUser);
                int? autoNumberYear = TryParseProjectNumberYear(request.Year);
                if (medium.Id > 0)
                {
                    var links = await _archiveFilingService.GetElectronicArchiveLinkInfosAsync([medium.Id]);
                    if (links.Count > 0)
                    {
                        return StockHardDiskDirectFilingResult.Fail(BuildAlreadyLinkedElectronicBagMessage(medium));
                    }
                }

                string fullLocation = ArchiveSlotLocationSupport.TryParseSequenceIndex(medium.Ledger?.StorageLocation, out _)
                    ? medium.Ledger!.StorageLocation.Trim()
                    : await _hardDiskMediaService.ResolveDataInStockFullLocationAsync(
                        string.IsNullOrWhiteSpace(medium.Ledger?.StorageLocation)
                            ? request.StorageLocation
                            : medium.Ledger.StorageLocation);
                int existingBagCount = await CountExistingHardDiskBagsAsync(project.ProjectName, request.Year.Trim());
                var savedRecords = new List<YearlyArchiveRegisterRecord>();
                var pathByMediaItemId = new Dictionary<int, string>();

                foreach (var material in request.Materials)
                {
                    var record = await CreateRegisterRecordAsync(request, project, currentUser, medium.DiskCode, material, autoNumberYear);
                    await _archiveRegisterRepository.SaveOrUpdateRecordGraphAsync(record);
                    var persisted = await _archiveRegisterRepository.GetByFormNoWithDetailsAsync(record.FormNo)
                        ?? throw new InvalidOperationException($"保存建档记录 [{record.FormNo}] 后未能重新加载。");
                    savedRecords.Add(persisted);

                    var items = persisted.MediaEntries
                        .SelectMany(entry => entry.Items)
                        .OrderBy(item => item.Id)
                        .ToList();
                    var drafts = material.Items.ToList();
                    if (items.Count != drafts.Count)
                    {
                        throw new InvalidOperationException($"建档记录 [{persisted.FormNo}] 的子项数量与扫描结果不一致。");
                    }

                    for (int index = 0; index < items.Count; index++)
                    {
                        pathByMediaItemId[items[index].Id] = drafts[index].FilingStoragePath?.Trim()
                            ?? items[index].StoragePath;
                    }
                }

                string electronicNo = await _archiveFilingService.GenerateNextElectronicArchiveNoAsync(request.Year);
                string operatorName = currentUser?.RealName?.Trim() ?? "资料室管理员";
                string bagRemark = existingBagCount > 0
                    ? $"{ArchiveRegisterDomainValues.SourceTypeStockDirect}；同项目第 {existingBagCount + 1} 袋。"
                    : ArchiveRegisterDomainValues.SourceTypeStockDirect;
                string contentSummary = string.Join("；", request.Materials.Select(item => item.MaterialName.Trim()));
                var mediaItemIds = pathByMediaItemId.Keys.ToList();

                var submission = new ElectronicArchiveSubmissionRequest
                {
                    ArchiveUnit = new YearlyElectronicArchiveUnit
                    {
                        ElectronicArchiveNo = electronicNo,
                        ProjectName = project.ProjectName.Trim(),
                        Year = request.Year.Trim(),
                        StorageCarrierType = ArchiveFilingBusinessRules.DefaultElectronicBagCarrierType,
                        StoragePath = string.Join("\\", new[]
                        {
                            string.Empty,
                            ElectronicFilingStoragePathSupport.SanitizePathSegment(request.Year, "未知年度"),
                            ElectronicFilingStoragePathSupport.SanitizePathSegment(project.ProjectName, "未知项目"),
                            string.Empty
                        }),
                        StorageLocation = fullLocation,
                        LinkedMediumCodes = medium.DiskCode.Trim(),
                        Disposition = ArchiveRegisterDomainValues.ElectronicDispositionRetain,
                        MediaCount = 1,
                        ContentSummary = contentSummary,
                        ArchivedBy = operatorName,
                        SourceType = ArchiveRegisterDomainValues.SourceTypeStockDirect,
                        SourceRecordKey = medium.DiskCode.Trim(),
                        Remarks = bagRemark
                    },
                    MediaItemIds = mediaItemIds,
                    FilingStoragePathByMediaItemId = pathByMediaItemId,
                    SubmissionMode = ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew,
                    IsRetainedHardDiskScenario = false,
                    RequiresFormatRetainedHardDisk = false,
                    FilingMode = ArchiveFilingBusinessRules.DirectRetainedHardDiskSourceOption,
                    AutoNumberYear = autoNumberYear
                };

                var filingResult = await _archiveFilingService.SubmitNewElectronicArchiveUnitAsync(submission, currentUser);

                return new StockHardDiskDirectFilingResult
                {
                    Succeeded = true,
                    Message = existingBagCount > 0
                        ? $"已完成存量直办立档（第 {existingBagCount + 1} 袋）。电子袋 [{filingResult.ElectronicArchiveNo}]，硬盘 [{medium.DiskCode}]，档口 [{fullLocation}]。"
                        : $"已完成存量直办立档。电子袋 [{filingResult.ElectronicArchiveNo}]，硬盘 [{medium.DiskCode}]，档口 [{fullLocation}]。",
                    ElectronicArchiveNo = filingResult.ElectronicArchiveNo,
                    DiskCode = medium.DiskCode,
                    StorageLocation = fullLocation,
                    FormNos = savedRecords.Select(item => item.FormNo).ToList()
                };
            }
            catch (Exception ex)
            {
                return StockHardDiskDirectFilingResult.Fail(ex.Message);
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> CollectCommitErrorsAsync(
            StockHardDiskDirectFilingRequest request,
            User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(request);

            var errors = new List<string>();
            if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
            {
                errors.Add("仅资料室资料管理员可执行存量硬盘直办立档。");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(request.Year) || request.Year.Trim().Length != 4
                || !request.Year.Trim().All(char.IsDigit))
            {
                errors.Add("年度必须是四位数字。");
            }

            if (string.IsNullOrWhiteSpace(request.ProjectName))
            {
                errors.Add("项目名称不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.DiskCode))
            {
                errors.Add("请填写或生成硬盘编号。");
            }

            if (string.IsNullOrWhiteSpace(request.SerialNumber))
            {
                errors.Add("请读取或填写硬盘序列号。");
            }

            if (string.IsNullOrWhiteSpace(request.DiskType))
            {
                errors.Add("请选择或填写硬盘类型。");
            }

            if (string.IsNullOrWhiteSpace(request.Brand))
            {
                errors.Add("请选择或填写硬盘品牌。");
            }

            if (string.IsNullOrWhiteSpace(request.InterfaceType))
            {
                errors.Add("请选择或填写接口类型。");
            }

            if (string.IsNullOrWhiteSpace(request.Capacity)
                || ElectronicMediaCapacitySupport.ParseCapacityTextToMb(request.Capacity) <= 0)
            {
                errors.Add("请填写有效的硬盘容量。");
            }

            if (!request.FactoryDate.HasValue)
            {
                errors.Add("请选择或确认出厂日期（读取本机硬盘后若未能解析，请手工补全）。");
            }

            if (string.IsNullOrWhiteSpace(request.StorageLocation))
            {
                errors.Add("请推荐或选择年度数据硬盘专用档口。");
            }
            else if (!ArchiveSlotLocationSupport.TryParseSlotLocation(request.StorageLocation, out _, out _, out _, out _)
                     || !ArchiveSlotLocationSupport.TryParseSequenceIndex(request.StorageLocation, out _))
            {
                errors.Add("数据档口必须是带档内序号的完整位置，请使用「推荐空位」或从列表选择后确认。");
            }

            if (!string.Equals(
                    string.IsNullOrWhiteSpace(request.SourceType)
                        ? ArchiveRegisterDomainValues.SourceTypeStockDirect
                        : request.SourceType.Trim(),
                    ArchiveRegisterDomainValues.SourceTypeStockDirect,
                    StringComparison.Ordinal))
            {
                errors.Add("来源必须为「存量直办」。");
            }

            if (!string.Equals(
                    string.IsNullOrWhiteSpace(request.ProvideUnit)
                        ? ArchiveRegisterDomainValues.ProvideUnitArchiveRoom
                        : request.ProvideUnit.Trim(),
                    ArchiveRegisterDomainValues.ProvideUnitArchiveRoom,
                    StringComparison.Ordinal))
            {
                errors.Add("提供单位必须为「资料室」。");
            }

            var pageOptions = await _archiveRegisterService.GetPageDomainOptionsAsync();
            if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(request.ArchivePurpose, pageOptions.ArchivePurposes))
            {
                errors.Add("请选择有效的库管模式。");
            }

            string confidential = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(request.ConfidentialLevel);
            if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(confidential, pageOptions.ConfidentialLevels))
            {
                errors.Add("请选择有效的密级。");
            }

            if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(request.MaterialCategory, pageOptions.ElectronicMaterialCategories))
            {
                errors.Add("请选择有效的资料类型。");
            }
            else
            {
                IReadOnlyList<string> subCategoryOptions = string.Equals(
                        request.MaterialCategory,
                        ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument,
                        StringComparison.Ordinal)
                    ? pageOptions.ElectronicDocumentSubCategories
                    : string.Equals(
                        request.MaterialCategory,
                        ArchiveRegisterDomainValues.ElectronicMaterialCategoryData,
                        StringComparison.Ordinal)
                        ? pageOptions.ElectronicDataSubCategories
                        : string.Equals(
                            request.MaterialCategory,
                            ArchiveRegisterDomainValues.ElectronicMaterialCategorySoftware,
                            StringComparison.Ordinal)
                            ? pageOptions.ElectronicSoftwareSubCategories
                            : Array.Empty<string>();
                if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(request.SubCategory, subCategoryOptions))
                {
                    errors.Add("请选择与资料类型匹配的所属子类。");
                }
            }

            if (request.Materials == null || request.Materials.Count == 0)
            {
                errors.Add("请先扫描硬盘目录并确认资料明细。");
            }
            else if (request.Materials.Any(material => material.Items == null || material.Items.Count == 0))
            {
                errors.Add("存在没有子项的资料名称，请检查目录或从预览中删除。");
            }
            else
            {
                foreach (var material in request.Materials)
                {
                    foreach (var item in material.Items)
                    {
                        string prefix = $"资料「{material.MaterialName}」子项「{item.ItemName}」";
                        var mappedEntries = (item.Entries ?? Array.Empty<ElectronicMediaContentScanEntry>())
                            .Select(entry => new YearlyArchiveRegisterElectronicMediaItemEntry
                            {
                                EntryKind = entry.EntryKind,
                                EntryName = entry.EntryName
                            })
                            .ToList();
                        errors.AddRange(ElectronicMediaItemSupport.CollectDataOrganizationEntryErrors(
                            item.DataOrganizationForm,
                            mappedEntries,
                            prefix,
                            requireEntries: true));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(request.SerialNumber))
            {
                var existing = await FindMediumBySerialNumberAsync(request.SerialNumber);
                if (existing != null)
                {
                    if (HardDiskMedium.IsTerminalUnavailableStatus(existing.Ledger?.MediaStatus))
                    {
                        errors.Add($"硬盘 [{existing.DiskCode}] 当前状态为 {existing.Ledger?.MediaStatus}，不能直办立档。");
                    }
                    else
                    {
                        bool isBlank = string.Equals(existing.Ledger?.MediaStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal)
                            || string.Equals(existing.Ledger?.MediaNature, HardDiskMedium.NatureBlank, StringComparison.Ordinal);
                        bool isUnlinkedData = string.Equals(existing.Ledger?.MediaStatus, HardDiskMedium.StatusInStockData, StringComparison.Ordinal);
                        if (!isBlank && !isUnlinkedData)
                        {
                            errors.Add($"序列号已登记为硬盘 [{existing.DiskCode}]，当前状态 [{existing.Ledger?.MediaStatus}]，不能用于存量直办。");
                        }
                        else if (!string.IsNullOrWhiteSpace(request.DiskCode)
                                 && !string.Equals(existing.DiskCode.Trim(), request.DiskCode.Trim(), StringComparison.Ordinal))
                        {
                            errors.Add($"序列号已对应库内硬盘 [{existing.DiskCode}]，请使用该编号。");
                        }
                        else if (existing.Id > 0)
                        {
                            var links = await _archiveFilingService.GetElectronicArchiveLinkInfosAsync([existing.Id]);
                            if (links.Count > 0)
                            {
                                errors.Add(BuildAlreadyLinkedElectronicBagMessage(existing));
                            }
                        }
                    }
                }
            }

            return errors;
        }

        private static string BuildAlreadyLinkedElectronicBagMessage(HardDiskMedium medium)
        {
            string diskCode = medium.DiskCode?.Trim() ?? string.Empty;
            string serial = string.IsNullOrWhiteSpace(medium.SerialNumber)
                ? "（序列号未登记）"
                : $"（序列号 [{medium.SerialNumber.Trim()}]）";
            return $"硬盘 [{diskCode}]{serial} 已关联电子立档袋，不能再次直办立档。";
        }

        private ProjectInfo EnsureProject(StockHardDiskDirectFilingRequest request)
        {
            var existing = FindProject(request.Year, request.ProjectName);
            if (existing != null)
            {
                return existing;
            }

            var project = new ProjectInfo
            {
                ProjectName = request.ProjectName.Trim(),
                ProjectCode = request.ProjectCode?.Trim() ?? string.Empty,
                ImplementYear = request.Year.Trim(),
                Remark = ArchiveRegisterDomainValues.SourceTypeStockDirect
            };
            _projectService.AddProject(project);
            return FindProject(request.Year, request.ProjectName)
                ?? throw new InvalidOperationException("新建项目后未能读取到项目信息。");
        }

        private async Task<HardDiskMedium> EnsureHardDiskMediumAsync(StockHardDiskDirectFilingRequest request, User? currentUser)
        {
            var existing = await FindMediumBySerialNumberAsync(request.SerialNumber);
            if (existing != null)
            {
                if (HardDiskMedium.IsTerminalUnavailableStatus(existing.Ledger?.MediaStatus))
                {
                    throw new InvalidOperationException($"硬盘 [{existing.DiskCode}] 当前状态为 {existing.Ledger?.MediaStatus}，不能直办立档。");
                }

                bool isBlank = string.Equals(existing.Ledger?.MediaStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal)
                    || string.Equals(existing.Ledger?.MediaNature, HardDiskMedium.NatureBlank, StringComparison.Ordinal);
                bool isUnlinkedData = string.Equals(existing.Ledger?.MediaStatus, HardDiskMedium.StatusInStockData, StringComparison.Ordinal);
                if (!isBlank && !isUnlinkedData)
                {
                    throw new InvalidOperationException(
                        $"序列号 [{request.SerialNumber.Trim()}] 已登记为硬盘 [{existing.DiskCode}]，当前状态 [{existing.Ledger?.MediaStatus}]，不能用于存量直办。");
                }

                if (!string.Equals(existing.DiskCode.Trim(), request.DiskCode.Trim(), StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(request.DiskCode))
                {
                    throw new InvalidOperationException(
                        $"序列号已对应库内硬盘 [{existing.DiskCode}]，请使用该编号，不要另编 [{request.DiskCode.Trim()}]。");
                }

                return existing;
            }

            var medium = new HardDiskMedium
            {
                DiskCode = request.DiskCode.Trim(),
                SerialNumber = request.SerialNumber.Trim(),
                DiskType = request.DiskType.Trim(),
                Brand = request.Brand.Trim(),
                Capacity = request.Capacity?.Trim() ?? string.Empty,
                InterfaceType = request.InterfaceType?.Trim() ?? string.Empty,
                FactoryDate = request.FactoryDate,
                RegistrationMethod = HardDiskMedium.RegistrationMethodArchive,
                RegisterPerson = currentUser?.RealName?.Trim() ?? string.Empty,
                RegisterDate = DateTime.Now,
                Remark = ArchiveRegisterDomainValues.SourceTypeStockDirect,
                Ledger = new HardDiskLedger
                {
                    DiskCode = request.DiskCode.Trim(),
                    MediaStatus = HardDiskMedium.StatusInStockData,
                    MediaNature = HardDiskMedium.NatureDataCarrier,
                    StorageLocation = request.StorageLocation.Trim(),
                    HolderOrOrganization = "资料室",
                    NeedReturn = false,
                    RegisterPerson = currentUser?.RealName?.Trim() ?? string.Empty,
                    RegisterDate = DateTime.Now,
                    Remark = ArchiveRegisterDomainValues.SourceTypeStockDirect
                }
            };

            await _hardDiskMediaService.SaveMediumAsync(medium, currentUser);
            return await FindMediumBySerialNumberAsync(request.SerialNumber)
                ?? throw new InvalidOperationException("硬盘登记后未能读取到介质记录。");
        }

        private async Task<YearlyArchiveRegisterRecord> CreateRegisterRecordAsync(
            StockHardDiskDirectFilingRequest request,
            ProjectInfo project,
            User? currentUser,
            string diskCode,
            StockHardDiskMaterialDraft material,
            int? numberingYear)
        {
            DateTime archiveYearDate = ResolveArchiveYearDate(request.Year);
            string formNo = await _archiveRegisterService.GenerateNextFormNoAsync(numberingYear);
            string operatorName = currentUser?.RealName?.Trim() ?? string.Empty;
            string confidential = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(request.ConfidentialLevel);
            if (string.IsNullOrWhiteSpace(confidential))
            {
                confidential = "秘密";
            }

            var media = new YearlyArchiveRegisterMedia
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
                MediaType = ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk,
                MediaCount = 1,
                Disposition = ArchiveRegisterDomainValues.ElectronicDispositionRetain,
                IsBorrowedHardDisk = false,
                BorrowedHardDiskCode = diskCode
            };

            foreach (var item in material.Items)
            {
                var mediaItem = new YearlyArchiveRegisterMediaItem
                {
                    ItemType = ArchiveRegisterDomainValues.ItemTypeData,
                    ContentDesc = item.ItemName.Trim(),
                    ContentCount = 1,
                    StoragePath = item.StoragePath?.Trim() ?? item.FullPath,
                    ConfidentialLevel = confidential,
                    ElectronicDetail = new YearlyArchiveRegisterElectronicMediaItemDetail
                    {
                        MaterialCategory = string.IsNullOrWhiteSpace(request.MaterialCategory)
                            ? ArchiveRegisterDomainValues.ElectronicMaterialCategoryData
                            : request.MaterialCategory.Trim(),
                        SubCategory = string.IsNullOrWhiteSpace(request.SubCategory)
                            ? ArchiveRegisterDomainValues.DefaultStockDirectSubCategory
                            : request.SubCategory.Trim(),
                        DataOrganizationForm = item.DataOrganizationForm,
                        DataSizeMb = item.DataSizeMb,
                        Entries = item.Entries.Select((entry, index) => new YearlyArchiveRegisterElectronicMediaItemEntry
                        {
                            EntryKind = entry.EntryKind,
                            EntryName = entry.EntryName,
                            RelativePath = entry.RelativePath,
                            SizeMb = entry.SizeMb,
                            CreatedAt = entry.CreatedAt,
                            ModifiedAt = entry.ModifiedAt,
                            SortOrder = index + 1
                        }).ToList()
                    }
                };
                media.Items.Add(mediaItem);
            }

            return new YearlyArchiveRegisterRecord
            {
                FormNo = formNo,
                Status = YearlyArchiveRegisterRecord.Completed,
                CreatedDate = archiveYearDate,
                ApplicantDate = archiveYearDate,
                ProjectId = project.Id,
                ProjectName = project.ProjectName,
                MaterialName = material.MaterialName.Trim(),
                SourceType = ArchiveRegisterDomainValues.SourceTypeStockDirect,
                ProvideUnit = ArchiveRegisterDomainValues.ProvideUnitArchiveRoom,
                ArchivePurpose = string.IsNullOrWhiteSpace(request.ArchivePurpose)
                    ? ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage
                    : request.ArchivePurpose.Trim(),
                ProofMaterialNote = ArchiveRegisterDomainValues.ProofMaterialNoneText,
                ApplicantName = operatorName,
                ApplicantDept = currentUser?.Department?.Trim() ?? "资料室",
                Administrator = operatorName,
                AdminDate = DateTime.Now.Date,
                MediaEntries = [media]
            };
        }

        private static DateTime ResolveArchiveYearDate(string year)
        {
            if (!int.TryParse(year.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
                || parsed < 1900
                || parsed > 2100)
            {
                return DateTime.Now;
            }

            return new DateTime(parsed, 12, 31);
        }

        private static int? TryParseProjectNumberYear(string? year)
        {
            if (string.IsNullOrWhiteSpace(year) || year.Trim().Length != 4 || !year.Trim().All(char.IsDigit))
            {
                return null;
            }

            return int.Parse(year.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);
        }
    }
}
