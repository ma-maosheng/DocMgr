using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.Projects;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 存量光盘直办立档服务。编目路径按年度/项目/资料/子项生成，盘上目录原样记入存储目录。
    /// </summary>
    public sealed class StockOpticalDiscDirectFilingService : IStockOpticalDiscDirectFilingService
    {
        private readonly IProjectService _projectService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IArchiveRegisterRepository _archiveRegisterRepository;
        private readonly IArchiveFilingService _archiveFilingService;
        private readonly IArchiveFilingRepository _archiveFilingRepository;
        private readonly IStockDirectFilingYearProjectCatalog _yearProjectCatalog;

        public StockOpticalDiscDirectFilingService(
            IProjectService projectService,
            IHardDiskMediaService hardDiskMediaService,
            IArchiveRegisterService archiveRegisterService,
            IArchiveRegisterRepository archiveRegisterRepository,
            IArchiveFilingService archiveFilingService,
            IArchiveFilingRepository archiveFilingRepository,
            IStockDirectFilingYearProjectCatalog yearProjectCatalog)
        {
            _projectService = projectService;
            _hardDiskMediaService = hardDiskMediaService;
            _archiveRegisterService = archiveRegisterService;
            _archiveRegisterRepository = archiveRegisterRepository;
            _archiveFilingService = archiveFilingService;
            _archiveFilingRepository = archiveFilingRepository;
            _yearProjectCatalog = yearProjectCatalog;
        }

        /// <inheritdoc/>
        public StockOpticalDiscDirectoryScanResult ScanDirectory(string? rootPath)
            => StockOpticalDiscDirectFilingDirectorySupport.ScanRoot(rootPath);

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
        public async Task<int> CountExistingOpticalDiscBagsAsync(string projectName, string year)
        {
            var units = await _archiveFilingService.GetExistingElectronicUnitsForProjectAsync(projectName, year);
            return units.Count(unit => ArchiveFilingBusinessRules.IsOpticalDiscArchiveCarrierType(unit.StorageCarrierType));
        }

        /// <inheritdoc/>
        public Task<OpticalDiscMedium?> FindDiscByCodeAsync(string? discCode)
        {
            if (string.IsNullOrWhiteSpace(discCode))
            {
                return Task.FromResult<OpticalDiscMedium?>(null);
            }

            return _archiveFilingRepository.GetOpticalDiscMediumByCodeAsync(discCode.Trim());
        }

        /// <inheritdoc/>
        public Task<string?> RecommendDataSlotLocationAsync()
            => _hardDiskMediaService.AllocateNextDedicatedFullLocationAsync(CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc);

        /// <inheritdoc/>
        public Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetDataSlotOptionsAsync()
            => _hardDiskMediaService.GetDedicatedTargetLocationOptionsAsync(CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc);

        /// <inheritdoc/>
        public Task<string> ResolveDataFullLocationAsync(string? requestedLocation)
            => _hardDiskMediaService.ResolveDataInStockFullLocationAsync(
                requestedLocation,
                CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc);

        /// <inheritdoc/>
        public Task<string> PeekNextElectronicArchiveNoAsync(string year)
            => _archiveFilingService.GenerateNextElectronicArchiveNoAsync(year);

        /// <inheritdoc/>
        public async Task<StockOpticalDiscDirectFilingResult> CommitAsync(
            StockOpticalDiscDirectFilingRequest request,
            User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
            {
                return StockOpticalDiscDirectFilingResult.Fail("仅资料管理员可执行存量光盘直办立档。");
            }

            var errors = await CollectCommitErrorsAsync(request, currentUser);
            if (errors.Count > 0)
            {
                return StockOpticalDiscDirectFilingResult.Fail(string.Join(Environment.NewLine, errors));
            }

            try
            {
                int? autoNumberYear = TryParseProjectNumberYear(request.Year);
                string electronicNo = await _archiveFilingService.GenerateNextElectronicArchiveNoAsync(request.Year);
                string discCode = electronicNo;
                var existingDisc = await FindDiscByCodeAsync(discCode);
                if (existingDisc != null)
                {
                    return StockOpticalDiscDirectFilingResult.Fail($"电子袋号 [{discCode}] 已作为光盘编号登记，请稍后重试。");
                }

                ProjectInfo project = EnsureProject(request);
                string fullLocation = await _hardDiskMediaService.ResolveDataInStockFullLocationAsync(
                    request.StorageLocation,
                    CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc);
                int existingBagCount = await CountExistingOpticalDiscBagsAsync(project.ProjectName, request.Year.Trim());
                var savedRecords = new List<YearlyArchiveRegisterRecord>();
                var pathByMediaItemId = new Dictionary<int, string>();

                foreach (var material in request.Materials)
                {
                    var record = await CreateRegisterRecordAsync(request, project, currentUser, material, autoNumberYear);
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
                        throw new InvalidOperationException($"建档记录 [{persisted.FormNo}] 的子项数量与指派结果不一致。");
                    }

                    for (int index = 0; index < items.Count; index++)
                    {
                        pathByMediaItemId[items[index].Id] = StockOpticalDiscDirectFilingDirectorySupport.BuildCatalogPath(
                            request.Year,
                            project.ProjectName,
                            material.MaterialName,
                            drafts[index].ItemName);
                    }
                }

                string operatorName = currentUser?.RealName?.Trim() ?? "资料管理员";
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
                        StorageCarrierType = ArchiveFilingBusinessRules.DefaultOpticalDiscBagCarrierType,
                        StoragePath = string.Join("\\", new[]
                        {
                            string.Empty,
                            ElectronicFilingStoragePathSupport.SanitizePathSegment(request.Year, "未知年度"),
                            ElectronicFilingStoragePathSupport.SanitizePathSegment(project.ProjectName, "未知项目"),
                            string.Empty
                        }),
                        StorageLocation = fullLocation,
                        LinkedMediumCodes = string.Empty,
                        Disposition = ArchiveRegisterDomainValues.ElectronicDispositionRetain,
                        MediaCount = 1,
                        ContentSummary = contentSummary,
                        ArchivedBy = operatorName,
                        SourceType = ArchiveRegisterDomainValues.SourceTypeStockDirect,
                        SourceRecordKey = discCode,
                        Remarks = bagRemark
                    },
                    MediaItemIds = mediaItemIds,
                    FilingStoragePathByMediaItemId = pathByMediaItemId,
                    SubmissionMode = ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew,
                    IsRetainedHardDiskScenario = false,
                    RequiresFormatRetainedHardDisk = false,
                    IsOpticalDiscArchiveScenario = true,
                    IsStockOpticalDiscDirectFiling = true,
                    PreferredOpticalDiscCode = discCode,
                    FilingMode = "存量光盘直办",
                    AutoNumberYear = autoNumberYear
                };

                var filingResult = await _archiveFilingService.SubmitNewElectronicArchiveUnitAsync(submission, currentUser);
                return new StockOpticalDiscDirectFilingResult
                {
                    Succeeded = true,
                    Message = existingBagCount > 0
                        ? $"已完成存量光盘直办立档（第 {existingBagCount + 1} 袋）。电子袋 [{filingResult.ElectronicArchiveNo}]，光盘编号与电子袋号相同，物理位置编号 [{fullLocation}]。"
                        : $"已完成存量光盘直办立档。电子袋 [{filingResult.ElectronicArchiveNo}]，光盘编号与电子袋号相同，物理位置编号 [{fullLocation}]。",
                    ElectronicArchiveNo = filingResult.ElectronicArchiveNo,
                    DiscCode = discCode,
                    StorageLocation = fullLocation,
                    FormNos = savedRecords.Select(item => item.FormNo).ToList()
                };
            }
            catch (Exception ex)
            {
                return StockOpticalDiscDirectFilingResult.Fail(ex.Message);
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> CollectCommitErrorsAsync(
            StockOpticalDiscDirectFilingRequest request,
            User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(request);

            var errors = new List<string>();
            if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
            {
                errors.Add("仅资料管理员可执行存量光盘直办立档。");
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

            if (string.IsNullOrWhiteSpace(request.StorageLocation))
            {
                errors.Add("请推荐或选择年度数据光盘专用档口。");
            }
            else if (!ArchiveSlotLocationSupport.TryParseSlotLocation(request.StorageLocation, out _, out _, out _, out _)
                     || !ArchiveSlotLocationSupport.TryParseSequenceIndex(request.StorageLocation, out _))
            {
                errors.Add("数据档口必须是带档内序号的完整位置，请使用「推荐空位」或从列表选择后确认。");
            }

            var pageOptions = await _archiveRegisterService.GetPageDomainOptionsAsync();
            var allowedArchivePurposes = ArchiveRegisterDomainValues.FilterDirectFilingArchivePurposes(pageOptions.ArchivePurposes);
            if (string.IsNullOrWhiteSpace(request.ArchivePurpose))
            {
                errors.Add("库管模式不能为空。");
            }
            else if (ArchiveRegisterDomainValues.IsExternalEntrustedArchivePurpose(request.ArchivePurpose))
            {
                errors.Add(
                    $"直办立档不支持库管模式「{ArchiveRegisterDomainValues.ArchivePurposeExternalEntrusted}」。");
            }
            else if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(request.ArchivePurpose, allowedArchivePurposes))
            {
                errors.Add("请选择有效的库管模式。");
            }

            if (request.AssignByFolder
                && (request.ExcludedFolderCount > 0 || request.LooseFileCount > 0)
                && !request.IgnoredContentAcknowledged)
            {
                errors.Add("还有未纳入的文件夹或资料所属目录散文件。请纳入，或勾选确认忽略后再立档。");
            }

            if (request.AssignByExtension
                && (request.ExcludedExtensionCount > 0 || request.ExtensionlessFileCount > 0)
                && !request.IgnoredContentAcknowledged)
            {
                errors.Add("还有未纳入的后缀或无后缀文件。请纳入，或勾选确认忽略后再立档。");
            }

            if (request.Materials == null || request.Materials.Count == 0)
            {
                errors.Add(request.AssignByExtension
                    ? "请至少纳入一个后缀作为资料子项。"
                    : request.AssignByFolder
                        ? "请至少纳入一个文件夹作为资料子项。"
                        : "请先扫描光盘目录。");
            }
            else if (request.Materials.Any(material => string.IsNullOrWhiteSpace(material.MaterialName)))
            {
                errors.Add("资料名称不能为空。");
            }
            else if (request.Materials.Any(material => material.Items == null || material.Items.Count == 0))
            {
                errors.Add("存在没有子项的资料名称。");
            }
            else
            {
                var catalogPaths = new List<string>();
                foreach (var material in request.Materials)
                {
                    var itemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in material.Items)
                    {
                        string prefix = $"资料「{material.MaterialName}」子项「{item.ItemName}」";
                        if (string.IsNullOrWhiteSpace(item.ItemName))
                        {
                            errors.Add($"{prefix}：子项名称不能为空。");
                        }
                        else if (!itemNames.Add(item.ItemName.Trim()))
                        {
                            errors.Add($"资料「{material.MaterialName.Trim()}」下子项名称「{item.ItemName.Trim()}」重复。");
                        }

                        catalogPaths.Add(StockOpticalDiscDirectFilingDirectorySupport.BuildCatalogPath(
                            request.Year,
                            request.ProjectName,
                            material.MaterialName,
                            item.ItemName));

                        string confidential = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(item.ConfidentialLevel);
                        if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(confidential, pageOptions.ConfidentialLevels))
                        {
                            errors.Add($"{prefix}：请选择有效的密级。");
                        }

                        if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(item.MaterialCategory, pageOptions.ElectronicMaterialCategories))
                        {
                            errors.Add($"{prefix}：请选择有效的资料类型。");
                        }
                        else
                        {
                            IReadOnlyList<string> subCategoryOptions = ResolveElectronicSubCategoryOptions(
                                pageOptions,
                                item.MaterialCategory);
                            if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(item.SubCategory, subCategoryOptions))
                            {
                                errors.Add($"{prefix}：请选择与资料类型匹配的所属子类。");
                            }
                        }

                        string itemSourceType = ResolveItemSourceType(item, request);
                        if (!string.Equals(itemSourceType, ArchiveRegisterDomainValues.SourceTypeInternal, StringComparison.Ordinal)
                            && !ArchiveRegisterBusinessRules.IsExternalSourceType(itemSourceType))
                        {
                            errors.Add($"{prefix}：资料来源须为「内部」或「外来」。");
                        }

                        if (string.IsNullOrWhiteSpace(ResolveItemProvideUnit(item, request)))
                        {
                            errors.Add($"{prefix}：提供单位不能为空。");
                        }

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

                if (catalogPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != catalogPaths.Count)
                {
                    errors.Add("编目路径重复，请调整资料名称或子项名称。");
                }
            }

            return errors;
        }

        private ProjectInfo EnsureProject(StockOpticalDiscDirectFilingRequest request)
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

        private async Task<YearlyArchiveRegisterRecord> CreateRegisterRecordAsync(
            StockOpticalDiscDirectFilingRequest request,
            ProjectInfo project,
            User? currentUser,
            StockOpticalDiscMaterialDraft material,
            int? numberingYear)
        {
            DateTime archiveYearDate = ResolveArchiveYearDate(request.Year);
            string formNo = await _archiveRegisterService.GenerateNextFormNoAsync(numberingYear);
            string operatorName = currentUser?.RealName?.Trim() ?? string.Empty;

            var media = new YearlyArchiveRegisterMedia
            {
                MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
                MediaType = ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc,
                MediaCount = 1,
                Disposition = ArchiveRegisterDomainValues.ElectronicDispositionRetain,
                IsBorrowedHardDisk = false
            };

            foreach (var item in material.Items)
            {
                string confidential = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(item.ConfidentialLevel);
                if (string.IsNullOrWhiteSpace(confidential))
                {
                    confidential = "秘密";
                }

                media.Items.Add(new YearlyArchiveRegisterMediaItem
                {
                    ContentDesc = item.ItemName.Trim(),
                    ContentCount = 1,
                    StoragePath = item.StoragePath?.Trim() ?? item.FullPath,
                    ConfidentialLevel = confidential,
                    SourceType = ResolveItemSourceType(item, request),
                    ProvideUnit = ResolveItemProvideUnit(item, request),
                    ElectronicDetail = new YearlyArchiveRegisterElectronicMediaItemDetail
                    {
                        MaterialCategory = string.IsNullOrWhiteSpace(item.MaterialCategory)
                            ? ArchiveRegisterDomainValues.ElectronicMaterialCategoryData
                            : item.MaterialCategory.Trim(),
                        SubCategory = string.IsNullOrWhiteSpace(item.SubCategory)
                            ? ArchiveRegisterDomainValues.DefaultStockDirectSubCategory
                            : item.SubCategory.Trim(),
                        DataOrganizationForm = item.DataOrganizationForm,
                        DataSizeMb = item.DataSizeMb,
                        Entries = item.Entries.Select((entry, index) => new YearlyArchiveRegisterElectronicMediaItemEntry
                        {
                            EntryKind = entry.EntryKind,
                            EntryName = entry.EntryName,
                            SizeMb = entry.SizeMb,
                            CreatedAt = entry.CreatedAt,
                            ModifiedAt = entry.ModifiedAt,
                            SortOrder = index + 1
                        }).ToList()
                    }
                });
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

        private static string ResolveItemSourceType(
            StockOpticalDiscItemDraft item,
            StockOpticalDiscDirectFilingRequest request)
        {
            string sourceType = string.IsNullOrWhiteSpace(item.SourceType)
                ? request.SourceType?.Trim() ?? string.Empty
                : item.SourceType.Trim();
            return string.IsNullOrWhiteSpace(sourceType)
                ? ArchiveRegisterDomainValues.SourceTypeInternal
                : sourceType;
        }

        private static string ResolveItemProvideUnit(
            StockOpticalDiscItemDraft item,
            StockOpticalDiscDirectFilingRequest request)
        {
            string provideUnit = string.IsNullOrWhiteSpace(item.ProvideUnit)
                ? request.ProvideUnit?.Trim() ?? string.Empty
                : item.ProvideUnit.Trim();

            string sourceType = ResolveItemSourceType(item, request);
            if (!ArchiveRegisterBusinessRules.IsExternalSourceType(sourceType))
            {
                return string.IsNullOrWhiteSpace(provideUnit)
                    ? ArchiveRegisterDomainValues.ProvideUnitArchiveRoom
                    : provideUnit;
            }

            return provideUnit;
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

        private static IReadOnlyList<string> ResolveElectronicSubCategoryOptions(
            ArchiveRegisterPageDomainOptions pageOptions,
            string? materialCategory)
        {
            if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument, StringComparison.Ordinal))
            {
                return pageOptions.ElectronicDocumentSubCategories;
            }

            if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData, StringComparison.Ordinal))
            {
                return pageOptions.ElectronicDataSubCategories;
            }

            if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategorySoftware, StringComparison.Ordinal))
            {
                return pageOptions.ElectronicSoftwareSubCategories;
            }

            return Array.Empty<string>();
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
