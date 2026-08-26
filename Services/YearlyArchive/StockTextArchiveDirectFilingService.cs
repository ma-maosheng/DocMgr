using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.Cabinets;
using DocMgr.Models.Projects;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 存档文本资料直办立档服务。
    /// </summary>
    public sealed class StockTextArchiveDirectFilingService : IStockTextArchiveDirectFilingService
    {
        private readonly IProjectService _projectService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IArchiveRegisterRepository _archiveRegisterRepository;
        private readonly IArchiveFilingService _archiveFilingService;
        private readonly IArchiveFilingRepository _archiveFilingRepository;
        private readonly ICabinetService _cabinetService;
        private readonly IStockDirectFilingYearProjectCatalog _yearProjectCatalog;

        public StockTextArchiveDirectFilingService(
            IProjectService projectService,
            IArchiveRegisterService archiveRegisterService,
            IArchiveRegisterRepository archiveRegisterRepository,
            IArchiveFilingService archiveFilingService,
            IArchiveFilingRepository archiveFilingRepository,
            ICabinetService cabinetService,
            IStockDirectFilingYearProjectCatalog yearProjectCatalog)
        {
            _projectService = projectService;
            _archiveRegisterService = archiveRegisterService;
            _archiveRegisterRepository = archiveRegisterRepository;
            _archiveFilingService = archiveFilingService;
            _archiveFilingRepository = archiveFilingRepository;
            _cabinetService = cabinetService;
            _yearProjectCatalog = yearProjectCatalog;
        }

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
        public Task<IReadOnlyList<ArchiveBoxTargetLocationOption>> GetBoxSlotOptionsAsync(
            string projectName,
            string year,
            string boxSpecification)
            => _archiveFilingService.GetArchiveBoxTargetLocationOptionsAsync(projectName, year, boxSpecification);

        /// <inheritdoc/>
        public Task<ArchiveBoxLocationSuggestion?> SuggestBoxSlotAsync(
            string projectName,
            string year,
            string boxSpecification)
            => _archiveFilingService.SuggestArchiveBoxLocationAsync(projectName, year, boxSpecification);

        /// <inheritdoc/>
        public Task<string> PeekNextArchiveSequenceNoAsync(string year)
            => _archiveFilingService.GenerateNextArchiveSequenceNoAsync(year);

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> CollectCommitErrorsAsync(
            StockTextArchiveDirectFilingRequest request,
            User? currentUser)
        {
            var errors = new List<string>();
            if (request == null)
            {
                errors.Add("提交请求不能为空。");
                return errors;
            }

            if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
            {
                errors.Add("仅资料室资料管理员可执行存档文本直办立档。");
            }

            string year = request.Year?.Trim() ?? string.Empty;
            if (year.Length != 4 || !year.All(char.IsDigit))
            {
                errors.Add("实施年度必须是四位数字年份。");
            }

            if (string.IsNullOrWhiteSpace(request.ProjectName))
            {
                errors.Add("项目名称不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.MaterialName))
            {
                errors.Add("资料名称不能为空。");
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

            if (string.IsNullOrWhiteSpace(request.ArchivePurpose))
            {
                errors.Add("库管模式不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.BoxSpecification))
            {
                errors.Add("档案盒规格不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.CabinetName)
                || string.IsNullOrWhiteSpace(request.Side)
                || request.Row <= 0
                || request.Column <= 0)
            {
                errors.Add("请选择有效的年度资料专用档口（柜体/面/层/列）。");
            }

            if (request.SpecifiedBoxIndex.HasValue && request.SpecifiedBoxIndex.Value < 1)
            {
                errors.Add("指定的档案盒序号必须大于 0。");
            }

            var groups = request.MediaGroups ?? Array.Empty<StockTextArchiveMediaGroupDraft>();
            if (groups.Count == 0)
            {
                errors.Add("请至少添加一组资料介质。");
            }
            else
            {
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    var group = groups[groupIndex];
                    string mediaType = group.MediaType?.Trim() ?? string.Empty;
                    if (!ArchiveRegisterDomainValues.IsSimulatedDataMediaType(mediaType))
                    {
                        errors.Add($"第 {groupIndex + 1} 组介质类型不在允许的载体类型中。");
                    }

                    if (groupIndex > 0
                        && !string.Equals(mediaType, groups[0].MediaType?.Trim(), StringComparison.Ordinal))
                    {
                        errors.Add("一份直办单只能使用同一种模拟载体类型。");
                    }

                    var items = group.Items ?? Array.Empty<StockTextArchiveMediaItemDraft>();
                    if (items.Count == 0)
                    {
                        errors.Add($"第 {groupIndex + 1} 组介质至少需要一条资料子项。");
                        continue;
                    }

                    for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                    {
                        var item = items[itemIndex];
                        string prefix = $"第 {groupIndex + 1} 组第 {itemIndex + 1} 条子项";
                        if (string.IsNullOrWhiteSpace(item.ContentDesc))
                        {
                            errors.Add($"{prefix}：子项名称不能为空。");
                        }

                        if (item.ContentCount < 1)
                        {
                            errors.Add($"{prefix}：份数须不少于 1。");
                        }

                        if (string.IsNullOrWhiteSpace(item.ConfidentialLevel))
                        {
                            errors.Add($"{prefix}：密级不能为空。");
                        }

                        if (!ArchiveRegisterDomainValues.SimulatedMaterialCategories.Contains(item.MaterialCategory?.Trim() ?? string.Empty))
                        {
                            errors.Add($"{prefix}：资料类型须为文本或图件。");
                        }

                        var subOptions = ArchiveRegisterDomainValues.GetSimulatedSubCategories(item.MaterialCategory);
                        if (!subOptions.Contains(item.SubCategory?.Trim() ?? string.Empty))
                        {
                            errors.Add($"{prefix}：所属子类与资料类型不匹配。");
                        }

                        if (!ArchiveRegisterDomainValues.SimulatedOrganizationForms.Contains(item.OrganizationForm?.Trim() ?? string.Empty))
                        {
                            errors.Add($"{prefix}：组织形式须为散页或装订。");
                        }
                    }
                }
            }

            return errors;
        }

        /// <inheritdoc/>
        public async Task<StockTextArchiveDirectFilingResult> CommitAsync(
            StockTextArchiveDirectFilingRequest request,
            User? currentUser)
        {
            if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
            {
                return StockTextArchiveDirectFilingResult.Fail("仅资料室资料管理员可执行存档文本直办立档。");
            }

            var errors = await CollectCommitErrorsAsync(request, currentUser);
            if (errors.Count > 0)
            {
                return StockTextArchiveDirectFilingResult.Fail(string.Join(Environment.NewLine, errors));
            }

            try
            {
                if (request.SyncUnsetSlotCategoryOnCommit)
                {
                    await SyncUnsetSlotToYearlyMaterialsIfNeededAsync(request);
                }

                var project = EnsureProject(request);
                int? numberingYear = TryParseProjectNumberYear(request.Year);
                string year = request.Year.Trim();
                string operatorName = currentUser?.RealName?.Trim() ?? string.Empty;

                var record = await CreateRegisterRecordAsync(request, project, currentUser, numberingYear);
                await _archiveRegisterRepository.SaveOrUpdateRecordGraphAsync(record);
                var persisted = await _archiveRegisterRepository.GetByFormNoWithDetailsAsync(record.FormNo)
                    ?? throw new InvalidOperationException($"保存建档记录 [{record.FormNo}] 后未能重新加载。");

                var mediaItemIds = persisted.MediaEntries
                    .SelectMany(entry => entry.Items)
                    .OrderBy(item => item.Id)
                    .Select(item => item.Id)
                    .ToList();
                if (mediaItemIds.Count == 0)
                {
                    throw new InvalidOperationException($"建档记录 [{persisted.FormNo}] 未包含可入盒的资料子项。");
                }

                string archiveSequenceNo = await _archiveFilingService.GenerateNextArchiveSequenceNoAsync(year);
                int boxSequence = request.SpecifiedBoxIndex.HasValue
                    ? request.SpecifiedBoxIndex.Value
                    : await _archiveFilingService.GetMinimumAvailableBoxSequenceInCellAsync(
                        request.CabinetName.Trim(),
                        request.Side.Trim(),
                        request.Row,
                        request.Column);
                string boxLocationCode = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                    request.CabinetName.Trim(),
                    request.Side.Trim(),
                    request.Row,
                    request.Column,
                    boxSequence);

                var newBox = new YearlyArchiveBox
                {
                    ArchiveSequenceNo = archiveSequenceNo,
                    BoxLocationCode = boxLocationCode,
                    CabinetName = request.CabinetName.Trim(),
                    Side = request.Side.Trim(),
                    Row = request.Row,
                    Column = request.Column,
                    BoxIndex = boxSequence,
                    ProjectName = project.ProjectName.Trim(),
                    Year = year,
                    Specs = request.BoxSpecification.Trim(),
                    ArchivedBy = operatorName,
                    ArchivedDate = DateTime.Now,
                    Remarks = request.Remarks?.Trim() ?? string.Empty
                };

                await _archiveFilingService.CreateArchiveBoxAsync(newBox, mediaItemIds, numberingYear);

                return new StockTextArchiveDirectFilingResult
                {
                    Succeeded = true,
                    FormNo = persisted.FormNo,
                    ArchiveSequenceNo = newBox.ArchiveSequenceNo,
                    BoxLocationCode = newBox.BoxLocationCode,
                    ItemCount = mediaItemIds.Count,
                    Message =
                        $"存档文本直办立档成功。\n"
                        + $"建档单号：{persisted.FormNo}\n"
                        + $"档案盒编号：{newBox.ArchiveSequenceNo}\n"
                        + $"物理位置：{newBox.BoxLocationCode}\n"
                        + $"本次已入盒 {mediaItemIds.Count} 个资料子项。"
                };
            }
            catch (Exception ex)
            {
                return StockTextArchiveDirectFilingResult.Fail(ex.Message);
            }
        }

        private ProjectInfo EnsureProject(StockTextArchiveDirectFilingRequest request)
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
            StockTextArchiveDirectFilingRequest request,
            ProjectInfo project,
            User? currentUser,
            int? numberingYear)
        {
            DateTime archiveYearDate = ResolveArchiveYearDate(request.Year);
            string formNo = await _archiveRegisterService.GenerateNextFormNoAsync(numberingYear);
            string operatorName = currentUser?.RealName?.Trim() ?? string.Empty;

            var mediaEntries = new List<YearlyArchiveRegisterMedia>();
            foreach (var group in request.MediaGroups)
            {
                var media = new YearlyArchiveRegisterMedia
                {
                    MediaKind = ArchiveRegisterDomainValues.MediaKindSimulated,
                    MediaType = group.MediaType.Trim(),
                    MediaCount = group.Items.Count,
                    Disposition = ArchiveRegisterDomainValues.SimulatedDispositionRetain
                };

                foreach (var item in group.Items)
                {
                    string confidential = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(item.ConfidentialLevel);
                    if (string.IsNullOrWhiteSpace(confidential))
                    {
                        confidential = "秘密";
                    }

                    media.Items.Add(new YearlyArchiveRegisterMediaItem
                    {
                        ItemType = ArchiveRegisterDomainValues.ItemTypeData,
                        ContentDesc = item.ContentDesc.Trim(),
                        ContentCount = item.ContentCount < 1 ? 1 : item.ContentCount,
                        Note = item.Note?.Trim() ?? string.Empty,
                        ConfidentialLevel = confidential,
                        SimulatedDetail = SimulatedMediaItemClassificationSupport.CreateDetail(
                            item.MaterialCategory,
                            item.SubCategory,
                            item.OrganizationForm)
                    });
                }

                mediaEntries.Add(media);
            }

            return new YearlyArchiveRegisterRecord
            {
                FormNo = formNo,
                Status = YearlyArchiveRegisterRecord.Completed,
                CreatedDate = archiveYearDate,
                ApplicantDate = archiveYearDate,
                ProjectId = project.Id,
                ProjectName = project.ProjectName,
                MaterialName = request.MaterialName.Trim(),
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
                MediaEntries = mediaEntries
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

        /// <inheritdoc/>
        public IReadOnlyList<string> ListExcelSheetNames(string filePath)
            => StockTextArchiveExcelImportSupport.ListSheetNames(filePath);

        /// <inheritdoc/>
        public StockTextArchiveExcelParseResult ParseExcel(string filePath, string sheetName, bool expandItemsByTextLine = false)
            => StockTextArchiveExcelImportSupport.Parse(filePath, sheetName, expandItemsByTextLine);

        /// <inheritdoc/>
        public async Task<IReadOnlyList<StockTextArchiveExcelBoxValidation>> ValidateExcelImportAsync(
            IReadOnlyList<StockTextArchiveExcelBoxDraft> boxes,
            User? currentUser,
            IProgress<(int Current, int Total, string Status)>? progress = null)
        {
            var source = boxes ?? Array.Empty<StockTextArchiveExcelBoxDraft>();
            var occupied = await LoadOccupiedBoxLocationCodesAsync();
            var cabinets = await _cabinetService.GetAllCabinetsAsync();

            var results = new List<StockTextArchiveExcelBoxValidation>(source.Count);
            int total = source.Count;
            int index = 0;
            foreach (var box in source)
            {
                index++;
                progress?.Report((index, total, $"正在校验第 {index} / {total} 盒…"));
                await Task.Yield();
                var errors = box.ParseErrors.ToList();
                var request = box.ToRequest();
                errors.AddRange(await CollectCommitErrorsAsync(request, currentUser));

                if (!string.IsNullOrWhiteSpace(box.NormalizedBoxLocationCode)
                    && occupied.Contains(box.NormalizedBoxLocationCode))
                {
                    errors.Add($"物理位置 [{box.NormalizedBoxLocationCode}] 已被占用。");
                }

                errors.AddRange(CollectExcelSlotCategoryErrors(box, cabinets));

                results.Add(new StockTextArchiveExcelBoxValidation
                {
                    Box = box,
                    Errors = errors.Distinct(StringComparer.Ordinal).ToList()
                });
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<StockTextArchiveExcelImportCommitResult> CommitExcelImportAsync(
            IReadOnlyList<StockTextArchiveExcelBoxDraft> boxes,
            User? currentUser,
            IProgress<(int Current, int Total, string Status)>? progress = null)
        {
            int expected = boxes?.Count ?? 0;
            if (expected > 0)
            {
                progress?.Report((0, expected, "正在复核可立档盒…"));
                await Task.Yield();
            }

            var validations = await ValidateExcelImportAsync(boxes, currentUser, progress);
            int skipped = 0;
            int succeeded = 0;
            int failed = 0;
            var messages = new List<string>();
            var occupiedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int total = validations.Count;
            int index = 0;

            foreach (var validation in validations)
            {
                index++;
                var box = validation.Box;
                progress?.Report((index, total, $"正在立档第 {index} / {total} 盒（{box.NormalizedBoxLocationCode}）…"));
                await Task.Yield();
                if (!validation.CanImport)
                {
                    skipped++;
                    messages.Add($"跳过 {box.NormalizedBoxLocationCode}（{box.ProjectName}）：{string.Join("；", validation.Errors)}");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(box.NormalizedBoxLocationCode)
                    && !occupiedThisRun.Add(box.NormalizedBoxLocationCode))
                {
                    failed++;
                    messages.Add($"失败 {box.NormalizedBoxLocationCode}：本批重复占用。");
                    continue;
                }

                var result = await CommitAsync(box.ToRequest(), currentUser);
                if (result.Succeeded)
                {
                    succeeded++;
                    messages.Add($"成功 {result.BoxLocationCode} → {result.ArchiveSequenceNo}（{result.ItemCount} 子项）");
                }
                else
                {
                    failed++;
                    occupiedThisRun.Remove(box.NormalizedBoxLocationCode);
                    messages.Add($"失败 {box.NormalizedBoxLocationCode}：{result.Message}");
                }
            }

            return new StockTextArchiveExcelImportCommitResult
            {
                SucceededCount = succeeded,
                FailedCount = failed,
                SkippedCount = skipped,
                Messages = messages
            };
        }

        private async Task<HashSet<string>> LoadOccupiedBoxLocationCodesAsync()
        {
            var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string code in await _archiveFilingRepository.GetYearlyArchiveBoxLocationCodesAsync())
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    occupied.Add(code.Trim());
                }
            }

            var history = (await _archiveFilingRepository.GetTopoMapBoxNumbersAsync())
                .Concat(await _archiveFilingRepository.GetAerialPhotoBoxNumbersAsync())
                .Concat(await _archiveFilingRepository.GetOtherMapBoxNumbersAsync());
            foreach (string source in history)
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                foreach (string code in source.Split(
                    [';', '；', ',', '，', '\r', '\n'],
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    occupied.Add(code);
                }
            }

            return occupied;
        }

        /// <summary>
        /// Excel 导入前：未设置档口同步为年度资料专用；与历史专用冲突则改为混用档口。
        /// </summary>
        private async Task SyncUnsetSlotToYearlyMaterialsIfNeededAsync(StockTextArchiveDirectFilingRequest request)
        {
            string cabinetName = request.CabinetName?.Trim() ?? string.Empty;
            string faceCode = request.Side?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cabinetName)
                || string.IsNullOrWhiteSpace(faceCode)
                || request.Row <= 0
                || request.Column <= 0)
            {
                return;
            }

            var cabinet = (await _cabinetService.GetAllCabinetsAsync())
                .FirstOrDefault(item => string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null || cabinet.Type != CabinetType.Standard)
            {
                return;
            }

            string slotCode = ArchiveStorageSlotCategorySupport.BuildSlotCode(request.Row, request.Column);
            string? storedCategory = await _archiveFilingRepository.GetArchiveSlotCategoryNameAsync(
                cabinet.Id,
                faceCode,
                slotCode);
            string normalized = CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(storedCategory);
            if (ArchiveStorageSlotCategorySupport.MatchesCompatibleLandingCategory(
                    normalized,
                    ArchiveStorageSlotCategorySupport.ExpectedYearlyMaterialsCategory))
            {
                return;
            }

            if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetArchiveSlotCategoryAssignment.CategoryHistoricalMaterials))
            {
                _cabinetService.PromoteArchiveSlotToMixedUse(cabinet.Id, faceCode, slotCode);
                return;
            }

            int historyCount = await _archiveFilingRepository.CountHistoryArchiveOccupanciesInSlotAsync(
                cabinetName,
                faceCode,
                request.Row,
                request.Column);
            if (historyCount > 0)
            {
                _cabinetService.PromoteArchiveSlotToMixedUse(cabinet.Id, faceCode, slotCode);
                return;
            }

            _cabinetService.PromoteUnsetArchiveSlotToYearlyMaterials(cabinet.Id, faceCode, slotCode);
        }

        private static IReadOnlyList<string> CollectExcelSlotCategoryErrors(
            StockTextArchiveExcelBoxDraft box,
            IReadOnlyList<Cabinet> cabinets)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(box.CabinetName)
                || string.IsNullOrWhiteSpace(box.Side)
                || box.Row <= 0
                || box.Column <= 0)
            {
                return errors;
            }

            var cabinet = cabinets.FirstOrDefault(item =>
                string.Equals(item.Name, box.CabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                errors.Add($"未找到资料柜 [{box.CabinetName.Trim()}]。");
                return errors;
            }

            string face = box.Side.Trim();
            bool faceAllowed = cabinet.FaceCount > 1
                ? string.Equals(face, "A", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(face, "B", StringComparison.OrdinalIgnoreCase)
                : string.Equals(face, "A", StringComparison.OrdinalIgnoreCase);
            if (!faceAllowed || box.Row > cabinet.LayerCount || box.Column > cabinet.ColumnCount)
            {
                errors.Add($"档口 [{ArchiveSlotLocationSupport.BuildSlotKey(box.CabinetName, box.Side, box.Row, box.Column)}] 在柜体中不存在。");
                return errors;
            }

            return errors;
        }
    }
}
