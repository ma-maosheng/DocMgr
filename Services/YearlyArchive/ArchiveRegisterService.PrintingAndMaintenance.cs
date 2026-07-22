using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 打印、附件维护和域值装配相关实现。
    /// 将大体量服务按职责拆分，降低主流程文件复杂度。
    /// </summary>
    public partial class ArchiveRegisterService
    {
        public ArchiveRegisterPrintNormalizationResult NormalizePrintFields(
            string? confidentialLevel,
            string? prodOpinion,
            string? rndOpinion,
            string? deputyOpinion)
        {
            var pageDomainOptions = CreatePageDomainOptions(GetPageDomainDefinitions());

            var confidentialLevels = pageDomainOptions.ConfidentialLevels;
            var prodOptions = pageDomainOptions.ProdOpinionOptions;
            var rndOptions = pageDomainOptions.RndOpinionOptions;
            var deputyOptions = pageDomainOptions.DeputyOpinionOptions;

            return ArchiveRegisterBusinessRules.NormalizePrintFields(
                confidentialLevel,
                prodOpinion,
                rndOpinion,
                deputyOpinion,
                confidentialLevels,
                prodOptions,
                rndOptions,
                deputyOptions);
        }

        public bool IsArchiveAdminUser(User? user)
        {
            return ArchiveRegisterBusinessRules.IsArchiveAdminUser(user);
        }

        public bool IsDepartmentArchiveAdmin(User? user)
        {
            return ArchiveRegisterBusinessRules.IsDepartmentArchiveAdmin(user);
        }

        public bool IsApplicantUser(User? user)
        {
            return ArchiveRegisterBusinessRules.IsApplicantUser(user);
        }

        public bool CanSubmitApplication(User? user)
        {
            return ArchiveRegisterBusinessRules.CanSubmitApplication(user);
        }

        public ArchiveRegisterPrintData BuildPrintData(
            YearlyArchiveRegisterRecord record,
            string? selectedSourceType,
            IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries)
        {
            ArgumentNullException.ThrowIfNull(record);

            string dateStr = record.ApplicantDate != DateTime.MinValue
                ? record.ApplicantDate.ToString("yyyy-MM-dd")
                : string.Empty;

            const string blankDt = "      年    月    日";

            var normalizedPrint = NormalizePrintFields(
                null,
                record.ProdDeptOpinion,
                record.RndDeptOpinion,
                record.DeputyOpinion);

            var sourceType = string.IsNullOrWhiteSpace(record.SourceType)
                ? (selectedSourceType ?? string.Empty)
                : record.SourceType;

            var entries = mediaEntries?.ToList() ?? new List<YearlyArchiveRegisterMedia>();
            string retainedHardDiskRegistration = BuildRetainedHardDiskRegistrationText(entries);
            var pageDomainOptions = CreatePageDomainOptions(GetPageDomainDefinitions());

            return new ArchiveRegisterPrintData
            {
                FormNo = record.FormNo,
                MaterialName = record.MaterialName,
                ProjectName = record.ProjectName ?? string.Empty,
                SourceType = sourceType,
                ProvideUnit = record.ProvideUnit ?? string.Empty,
                Purpose = record.ArchivePurpose,
                OtherRequests = record.OtherRequests,
                Dept = record.ApplicantDept,
                Applicant = record.ApplicantName,
                Date = dateStr,
                ProdOpinion = $"{normalizedPrint.ProdOpinion}|{record.ProdDate?.ToString("yyyy-MM-dd") ?? blankDt}",
                RndOpinion = $"{normalizedPrint.RndOpinion}|{record.RndDate?.ToString("yyyy-MM-dd") ?? blankDt}",
                DeptLeaderApproval = $"{record.DeptLeader}|{record.DeptDate?.ToString("yyyy-MM-dd") ?? blankDt}",
                DeputyOpinion = normalizedPrint.DeputyOpinion,
                ProdFull = $"{normalizedPrint.ProdOpinion}|{record.ProdLeader}|{record.ProdDate?.ToString("yyyy-MM-dd") ?? blankDt}",
                RndFull = $"{normalizedPrint.RndOpinion}|{record.RndLeader}|{record.RndDate?.ToString("yyyy-MM-dd") ?? blankDt}",
                DeputyFull = $"{normalizedPrint.DeputyOpinion}|{record.DeputyLeader}|{record.DeputyDate?.ToString("yyyy-MM-dd") ?? blankDt}",
                DeliverFull = $"{record.Deliverer}|{record.DeliverDate?.ToString("yyyy-MM-dd") ?? blankDt}",
                AdminFull = $"{record.Administrator}|{record.AdminDate?.ToString("yyyy-MM-dd") ?? blankDt}",
                RetainedHardDiskRegistration = retainedHardDiskRegistration,
                OpticalDiscLedgerSummary = string.Empty,
                ItemLines = BuildItemLinesForPrint(entries, "资料", pageDomainOptions.ConfidentialLevels),
                ProofLines = BuildProofLinesForPrint(record)
            };
        }

        private static List<string> BuildProofLinesForPrint(YearlyArchiveRegisterRecord record)
        {
            string note = ArchiveRegisterDomainValues.NormalizeProofMaterialNote(record.ProofMaterialNote);
            if (!ArchiveRegisterDomainValues.HasProofMaterial(note))
            {
                return new List<string>();
            }

            return new List<string> { note };
        }

        /// <inheritdoc/>
        public async Task<string> BuildOpticalDiscLedgerSummaryAsync(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var unitIds = await _archiveRegisterRepository.GetElectronicArchiveUnitIdsByRegisterRecordIdAsync(record.Id);

            if (unitIds.Count == 0)
            {
                return string.Empty;
            }

            var records = await _archiveRegisterRepository.GetOpticalDiscLedgerRowsAsync(unitIds);

            if (records.Count == 0)
            {
                return string.Empty;
            }

            var lines = records.Select(item =>
                $"光盘：{item.DiscCode}；位置：{item.Location}；业务单号：{item.BusinessNo}；流转：立档入库（{item.OperateTime:yyyy-MM-dd}）");
            return string.Join("\n", lines);
        }

        private static string BuildRetainedHardDiskRegistrationText(IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries)
        {
            var retainedHardDiskEntries = mediaEntries
                .Where(entry => string.Equals(entry.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.MediaType, ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.Disposition, ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (retainedHardDiskEntries.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("；", retainedHardDiskEntries.Select(entry => entry.IsBorrowedHardDisk
                ? $"资料室借出硬盘：是，介质编号：{entry.BorrowedHardDiskCode}"
                : "资料室借出硬盘：否"));
        }

        private static List<string> BuildItemLinesForPrint(
            IReadOnlyCollection<YearlyArchiveRegisterMedia> mediaEntries,
            string itemType,
            IReadOnlyCollection<string> confidentialLevels)
        {
            var lines = new List<string>();
            int mediaIndex = 1;

            foreach (var media in mediaEntries)
            {
                var matchedItems = media.Items
                    .Where(i => string.Equals(i.ItemType ?? ArchiveRegisterDomainValues.ItemTypeData, itemType, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matchedItems.Count == 0)
                {
                    continue;
                }

                lines.Add($"介质{mediaIndex++}：{FormatMediaLabelForPrint(media)}");

                int itemIndex = 1;
                foreach (var item in matchedItems)
                {
                    var countLabel = item.ContentCount > 0 ? $"{item.ContentCount}份" : string.Empty;
                    var extras = new List<string>();
                    extras.AddRange(ElectronicMediaItemSupport.BuildElectronicItemPrintExtraParts(item));
                    if (!string.IsNullOrWhiteSpace(item.StoragePath)) extras.Add($"目录：{item.StoragePath}");
                    if (!string.IsNullOrWhiteSpace(item.Note)) extras.Add($"备注：{item.Note}");
                    var normalizedLevel = ArchiveRegisterBusinessRules.NormalizeConfidentialLevel(item.ConfidentialLevel);
                    if (ArchiveRegisterBusinessRules.IsAllowedDomainValue(normalizedLevel, confidentialLevels))
                    {
                        extras.Add($"密级：{normalizedLevel}");
                    }

                    var suffix = extras.Count > 0 ? $"（{string.Join("；", extras)}）" : string.Empty;
                    var line = "       " + $"({itemIndex++}). {item.ContentDesc} {countLabel}{suffix}".Trim();
                    lines.Add(line);
                }
            }

            return lines;
        }

        private static string FormatMediaLabelForPrint(YearlyArchiveRegisterMedia media)
        {
            var detail = FormatMediaEntryForPrint(media, string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase));
            var kind = string.IsNullOrWhiteSpace(media.MediaKind) ? "介质" : media.MediaKind;
            return string.IsNullOrWhiteSpace(detail) ? kind : $"{kind}/{detail}";
        }

        private static string FormatMediaEntryForPrint(YearlyArchiveRegisterMedia media, bool includeStorage)
        {
            if (media == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(media.MediaType))
            {
                return string.Empty;
            }

            string detail = media.MediaType;
            string count = media.MediaCount > 0 ? $"×{media.MediaCount}" : string.Empty;
            detail += count;

            if (includeStorage)
            {
                if (!string.IsNullOrWhiteSpace(media.Disposition))
                {
                    detail += $"(介质处置:{media.Disposition})";
                }

                if (string.Equals(media.MediaType, ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(media.Disposition, ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.OrdinalIgnoreCase))
                {
                    detail += media.IsBorrowedHardDisk
                        ? $"(资料室借出硬盘:是；介质编号:{media.BorrowedHardDiskCode})"
                        : "(资料室借出硬盘:否)";
                }
            }

            return detail;
        }

        public async Task UploadAttachmentAsync(SystemAttachment attachment)
        {
            if (attachment == null) throw new ArgumentNullException(nameof(attachment));
            _archiveRegisterRepository.AddAttachment(attachment);
            await _archiveRegisterRepository.SaveChangesAsync();
        }

        public async Task<List<SystemAttachment>> GetAttachmentsByFormNoAsync(string formNo)
        {
            return await _archiveRegisterRepository.GetAttachmentSummariesByFormNoAsync(formNo);
        }

        public async Task DeleteAttachmentAsync(int attachmentId)
        {
            var att = await _archiveRegisterRepository.GetAttachmentByIdAsync(attachmentId);
            if (att != null)
            {
                _archiveRegisterRepository.RemoveAttachment(att);
                await _archiveRegisterRepository.SaveChangesAsync();
            }
        }

        public async Task<List<YearlyArchiveRegisterRecord>> GetMyRecordsAsync(string applicantName)
        {
            if (string.IsNullOrWhiteSpace(applicantName)) return new List<YearlyArchiveRegisterRecord>();
            return await _archiveRegisterRepository.GetRecordsByApplicantAsync(applicantName);
        }

        public async Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId)
        {
            return await _archiveRegisterRepository.GetAttachmentByIdAsync(attachmentId);
        }

        public async Task<List<YearlyArchiveRegisterRecord>> GetAllRecordsByYearAsync(int year)
        {
            if (year == 0) year = DateTime.Now.Year;
            return await _archiveRegisterRepository.GetRecordsByYearAsync(year);
        }

        public async Task<string> GenerateNextFormNoAsync()
        {
            return await GenerateFormNoByPurposeAsync(null);
        }

        public async Task<List<int>> GetExistingYearsAsync()
        {
            var years = await _archiveRegisterRepository.GetDistinctCreatedYearsAsync();

            int currentYear = DateTime.Now.Year;
            if (!years.Contains(currentYear))
            {
                years.Insert(0, currentYear);
                years = years.OrderByDescending(y => y).ToList();
            }

            return years;
        }

        public async Task RemoveRegisterRecordAsync(int id)
        {
            var record = await _archiveRegisterRepository.GetRecordForRemovalAsync(id);

            if (record == null)
            {
                return;
            }

            var attachments = await _archiveRegisterRepository.GetAttachmentsByBusinessIdAsync(id);

            if (attachments.Any())
            {
                _archiveRegisterRepository.RemoveAttachments(attachments);
            }

            _archiveRegisterRepository.RemoveRegisterRecord(record);
            await _archiveRegisterRepository.SaveChangesAsync();
        }

        private static string FindUserByDeptAndRole(List<User> users, string? dept, string roleKeyword)
        {
            if (string.IsNullOrWhiteSpace(dept)) return string.Empty;
            return users.FirstOrDefault(u => string.Equals(u.Department, dept, StringComparison.OrdinalIgnoreCase)
                                             && (u.Role?.Contains(roleKeyword) ?? false))?.RealName ?? string.Empty;
        }

        private static string FindUserByRoleOrDept(List<User> users, string keyword)
        {
            return users.FirstOrDefault(u => (u.Role?.Contains(keyword) ?? false)
                                             || (u.Department?.Contains(keyword) ?? false))?.RealName ?? string.Empty;
        }

        private List<FieldDomainDefinition> GetPageDomainDefinitions()
        {
            return _archiveRegisterRepository.GetPageDomainDefinitions(
                RegisterRecordEntityName,
                RegisterRecordDomainFields,
                RegisterMediaEntityName,
                RegisterMediaDomainFields,
                RegisterMediaItemEntityName,
                RegisterMediaItemDomainFields);
        }

        private static ArchiveRegisterPageDomainOptions CreatePageDomainOptions(IReadOnlyCollection<FieldDomainDefinition> definitions)
        {
            return new ArchiveRegisterPageDomainOptions
            {
                SourceTypes = GetDomainOptionValues(definitions, RegisterRecordEntityName, nameof(YearlyArchiveRegisterRecord.SourceType), EmptyScope),
                ArchivePurposes = GetDomainOptionValues(definitions, RegisterRecordEntityName, nameof(YearlyArchiveRegisterRecord.ArchivePurpose), EmptyScope),
                SimulatedMediaKinds = GetDomainOptionValues(definitions, RegisterMediaEntityName, nameof(YearlyArchiveRegisterMedia.MediaKind), SimulatedMediaKindScope),
                DataItemTypes = GetDomainOptionValues(definitions, RegisterMediaItemEntityName, nameof(YearlyArchiveRegisterMediaItem.ItemType), DataItemTypeScope),
                ProofItemTypes = GetDomainOptionValues(definitions, RegisterMediaItemEntityName, nameof(YearlyArchiveRegisterMediaItem.ItemType), ProofItemTypeScope),
                DataElectronicMediaTypes = GetDomainOptionValues(definitions, RegisterMediaEntityName, nameof(YearlyArchiveRegisterMedia.MediaType), MediaKindElectronicScope),
                DataSimulatedMediaTypes = GetDomainOptionValues(definitions, RegisterMediaEntityName, nameof(YearlyArchiveRegisterMedia.MediaType), MediaKindSimulatedDataScope),
                ProofSimulatedMediaTypes = GetDomainOptionValues(definitions, RegisterMediaEntityName, nameof(YearlyArchiveRegisterMedia.MediaType), MediaKindSimulatedProofScope),
                DataElectronicDispositions = GetDomainOptionValues(definitions, RegisterMediaEntityName, nameof(YearlyArchiveRegisterMedia.Disposition), MediaKindElectronicScope),
                DataSimulatedDispositions = GetDomainOptionValues(definitions, RegisterMediaEntityName, nameof(YearlyArchiveRegisterMedia.Disposition), MediaKindSimulatedScope),
                ElectronicMaterialCategories = GetDomainOptionValues(definitions, RegisterElectronicDetailEntityName, nameof(YearlyArchiveRegisterElectronicMediaItemDetail.MaterialCategory), EmptyScope),
                ElectronicDocumentSubCategories = GetDomainOptionValues(definitions, RegisterElectronicDetailEntityName, nameof(YearlyArchiveRegisterElectronicMediaItemDetail.SubCategory), ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocumentScope),
                ElectronicDataSubCategories = GetDomainOptionValues(definitions, RegisterElectronicDetailEntityName, nameof(YearlyArchiveRegisterElectronicMediaItemDetail.SubCategory), ArchiveRegisterDomainValues.ElectronicMaterialCategoryDataScope),
                ElectronicDataOrganizationForms = GetDomainOptionValues(definitions, RegisterElectronicDetailEntityName, nameof(YearlyArchiveRegisterElectronicMediaItemDetail.DataOrganizationForm), EmptyScope),
                ConfidentialLevels = GetDomainOptionValues(definitions, RegisterMediaItemEntityName, nameof(YearlyArchiveRegisterMediaItem.ConfidentialLevel), EmptyScope),
                ProdOpinionOptions = GetDomainOptionValues(definitions, RegisterRecordEntityName, nameof(YearlyArchiveRegisterRecord.ProdDeptOpinion), EmptyScope),
                RndOpinionOptions = GetDomainOptionValues(definitions, RegisterRecordEntityName, nameof(YearlyArchiveRegisterRecord.RndDeptOpinion), EmptyScope),
                DeputyOpinionOptions = GetDomainOptionValues(definitions, RegisterRecordEntityName, nameof(YearlyArchiveRegisterRecord.DeputyOpinion), EmptyScope)
            };
        }

        private static IReadOnlyList<string> GetDomainOptionValues(
            IReadOnlyCollection<FieldDomainDefinition> definitions,
            string entityName,
            string fieldName,
            string scope)
        {
            return definitions
                .Where(d => d.EntityName == entityName && d.FieldName == fieldName)
                .SelectMany(d => d.Options)
                .Where(o => string.Equals(NormalizeScope(o.Scope), NormalizeScope(scope), StringComparison.OrdinalIgnoreCase))
                .OrderBy(o => o.SortOrder)
                .ThenBy(o => o.Id)
                .Select(o => o.OptionValue)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeScope(string? scope)
        {
            return string.IsNullOrWhiteSpace(scope) ? EmptyScope : scope.Trim();
        }

        private static bool HasRequiredPageDomainOptions(ArchiveRegisterPageDomainOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return options.SourceTypes.Count > 0
                && options.ArchivePurposes.Count > 0
                && options.SimulatedMediaKinds.Count > 0
                && options.DataItemTypes.Count > 0
                && options.ProofItemTypes.Count > 0
                && options.DataElectronicMediaTypes.Count > 0
                && options.DataSimulatedMediaTypes.Count > 0
                && options.ProofSimulatedMediaTypes.Count > 0
                && options.DataElectronicDispositions.Count > 0
                && options.DataSimulatedDispositions.Count > 0
                && options.ElectronicMaterialCategories.Count > 0
                && options.ElectronicDocumentSubCategories.Count > 0
                && options.ElectronicDataSubCategories.Count > 0
                && options.ElectronicDataOrganizationForms.Count > 0
                && options.ConfidentialLevels.Count > 0
                && options.ProdOpinionOptions.Count > 0
                && options.RndOpinionOptions.Count > 0
                && options.DeputyOpinionOptions.Count > 0;
        }

        private static IEnumerable<string> CollectElectronicMediaItemValidationErrors(
            YearlyArchiveRegisterMediaItem item,
            int mediaSequence,
            int itemSequence,
            ArchiveRegisterPageDomainOptions pageDomainOptions)
        {
            var errors = new List<string>();
            var prefix = $"• 第{mediaSequence}条电子介质第{itemSequence}个子项";

            if (string.IsNullOrWhiteSpace(item.ContentDesc))
            {
                errors.Add($"{prefix}【子项资料名称】未填写");
            }

            if (!ElectronicMediaItemSupport.TryValidateRegistrationStoragePath(
                    item.StoragePath,
                    out var normalizedStoragePath,
                    out var storagePathError))
            {
                errors.Add($"{prefix}【存储目录】{storagePathError}");
            }
            else
            {
                item.StoragePath = normalizedStoragePath;
            }

            if (item.ElectronicDetail == null)
            {
                errors.Add($"{prefix}缺少电子扩展信息");
                return errors;
            }

            var detail = item.ElectronicDetail;

            if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(detail.MaterialCategory, pageDomainOptions.ElectronicMaterialCategories))
            {
                errors.Add($"{prefix}【资料类型】不在域值定义中（允许值：{string.Join("、", pageDomainOptions.ElectronicMaterialCategories)}）");
            }

            var subCategoryOptions = string.Equals(detail.MaterialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument, StringComparison.Ordinal)
                ? pageDomainOptions.ElectronicDocumentSubCategories
                : string.Equals(detail.MaterialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData, StringComparison.Ordinal)
                    ? pageDomainOptions.ElectronicDataSubCategories
                    : Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(detail.SubCategory))
            {
                errors.Add($"{prefix}【所属子类】未填写");
            }
            else if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(detail.SubCategory, subCategoryOptions))
            {
                errors.Add($"{prefix}【所属子类】与资料类型不匹配或不在域值定义中");
            }

            if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(detail.DataOrganizationForm, pageDomainOptions.ElectronicDataOrganizationForms))
            {
                errors.Add($"{prefix}【数据组织形式】不在域值定义中（允许值：{string.Join("、", pageDomainOptions.ElectronicDataOrganizationForms)}）");
            }

            if (detail.DataSizeMb <= 0)
            {
                errors.Add($"{prefix}【数据量】必须大于 0 MB");
            }

            var expectedEntryKind = ElectronicMediaItemSupport.ResolveEntryKind(detail.DataOrganizationForm);
            if (string.IsNullOrWhiteSpace(expectedEntryKind))
            {
                errors.Add($"{prefix}【数据组织形式】无效");
                return errors;
            }

            var entries = detail.Entries ?? new List<YearlyArchiveRegisterElectronicMediaItemEntry>();
            if (entries.Count == 0)
            {
                errors.Add($"{prefix}至少需要一条目录/文件明细");
                return errors;
            }

            if (entries.Any(entry => !string.Equals(entry.EntryKind, expectedEntryKind, StringComparison.Ordinal)))
            {
                errors.Add($"{prefix}目录/文件明细类型与【数据组织形式】不一致");
            }

            if (entries.Any(entry => string.IsNullOrWhiteSpace(entry.EntryName)))
            {
                errors.Add($"{prefix}存在未填写名称的目录/文件明细");
            }

            return errors;
        }
    }
}
