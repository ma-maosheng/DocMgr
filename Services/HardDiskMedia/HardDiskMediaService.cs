using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using DocMgr.Models.Cabinets;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质模块服务。
    /// </summary>
    public partial class HardDiskMediaService : IHardDiskMediaService
    {
        private const string ApplicationAttachmentBusinessType = "HardDiskMediaApplication";
        private const string SignedAttachmentCategory = "签批交接单";
        private static readonly Regex DiskCodeSequenceRegex = new(@"^(?<prefix>[A-Za-z]+)(?<sequence>\d+)$", RegexOptions.Compiled);

        private readonly IHardDiskMediaRepository _hardDiskMediaRepository;
        private readonly IArchiveFilingRepository _archiveFilingRepository;
        private readonly IBusinessRuleService _businessRuleService;
        private readonly IBusinessLogicSettingsService _businessLogicSettingsService;

        public HardDiskMediaService(
            IHardDiskMediaRepository hardDiskMediaRepository,
            IArchiveFilingRepository archiveFilingRepository,
            IBusinessRuleService businessRuleService,
            IBusinessLogicSettingsService businessLogicSettingsService)
        {
            _hardDiskMediaRepository = hardDiskMediaRepository;
            _archiveFilingRepository = archiveFilingRepository;
            _businessRuleService = businessRuleService;
            _businessLogicSettingsService = businessLogicSettingsService;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<HardDiskMedium>> SearchMediaAsync(string? keyword, string? status, string? nature)
        {
            return await _hardDiskMediaRepository.SearchMediaAsync(keyword, status, nature);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<OpticalDiscMedium>> SearchOpticalDiscMediaAsync(string? keyword, string? status)
        {
            return await _hardDiskMediaRepository.SearchOpticalDiscMediaAsync(keyword, status);
        }

        /// <inheritdoc/>
        public async Task ExportOpticalDiscMediaLedgerAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("导出文件路径不能为空。", nameof(filePath));
            }

            string? directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("导出文件目录无效。", nameof(filePath));
            }

            Directory.CreateDirectory(directoryPath);

            var mediaItems = await _hardDiskMediaRepository.GetOpticalDiscMediaForExportAsync();
            var transactionItems = await SearchOpticalDiscTransactionsAsync(null, null);

            await Task.Run(() =>
            {
                using var workbook = new XSSFWorkbook();
                var sheet = workbook.CreateSheet("光盘台账");
                string[] headers =
                [
                    "光盘编号",
                    "光盘类型",
                    "容量",
                    "当前状态",
                    "存放位置",
                    "当前保管",
                    "来源类型",
                    "来源记录",
                    "创建时间",
                    "更新时间",
                    "备注"
                ];

                var headerRow = sheet.CreateRow(0);
                for (int i = 0; i < headers.Length; i++)
                {
                    headerRow.CreateCell(i).SetCellValue(headers[i]);
                    sheet.SetColumnWidth(i, 20 * 256);
                }

                for (int rowIndex = 0; rowIndex < mediaItems.Count; rowIndex++)
                {
                    var item = mediaItems[rowIndex];
                    var row = sheet.CreateRow(rowIndex + 1);
                    row.CreateCell(0).SetCellValue(item.DiscCode);
                    row.CreateCell(1).SetCellValue(item.DiscType);
                    row.CreateCell(2).SetCellValue(item.Capacity);
                    row.CreateCell(3).SetCellValue(item.Ledger?.MediaStatus ?? OpticalDiscMedium.StatusInStock);
                    row.CreateCell(4).SetCellValue(item.Ledger?.StorageLocation ?? string.Empty);
                    row.CreateCell(5).SetCellValue(item.Ledger?.HolderOrOrganization ?? string.Empty);
                    row.CreateCell(6).SetCellValue(item.SourceType);
                    row.CreateCell(7).SetCellValue(item.SourceRecordKey);
                    row.CreateCell(8).SetCellValue(item.CreatedTime == default ? string.Empty : item.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    row.CreateCell(9).SetCellValue(item.UpdatedTime == default ? string.Empty : item.UpdatedTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    row.CreateCell(10).SetCellValue(item.Remarks);
                }

                var transactionSheet = workbook.CreateSheet("流转记录");
                string[] transactionHeaders =
                [
                    "办理时间",
                    "流转类型",
                    "光盘编号",
                    "业务单号",
                    "前位置",
                    "后位置",
                    "办理人",
                    "备注"
                ];

                var transactionHeaderRow = transactionSheet.CreateRow(0);
                for (int i = 0; i < transactionHeaders.Length; i++)
                {
                    transactionHeaderRow.CreateCell(i).SetCellValue(transactionHeaders[i]);
                    transactionSheet.SetColumnWidth(i, 20 * 256);
                }

                for (int rowIndex = 0; rowIndex < transactionItems.Count; rowIndex++)
                {
                    var item = transactionItems[rowIndex];
                    var row = transactionSheet.CreateRow(rowIndex + 1);
                    row.CreateCell(0).SetCellValue(item.OperateTime == default ? string.Empty : item.OperateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    row.CreateCell(1).SetCellValue(item.TransactionType);
                    row.CreateCell(2).SetCellValue(item.DiscCode);
                    row.CreateCell(3).SetCellValue(item.BusinessNo);
                    row.CreateCell(4).SetCellValue(item.BeforeLocation);
                    row.CreateCell(5).SetCellValue(item.AfterLocation);
                    row.CreateCell(6).SetCellValue(item.OperatorName);
                    row.CreateCell(7).SetCellValue(item.Remark);
                }

                using var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                workbook.Write(outputStream, true);
            });
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<OpticalDiscMediumTransactionRecord>> SearchOpticalDiscTransactionsAsync(string? keyword)
        {
            return await SearchOpticalDiscTransactionsAsync(keyword, keyword);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<OpticalDiscMediumTransactionRecord>> SearchOpticalDiscTransactionsAsync(string? discCodeKeyword, string? businessNoKeyword)
        {
            return await _hardDiskMediaRepository.SearchOpticalDiscTransactionsAsync(discCodeKeyword, businessNoKeyword);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<HardDiskMedium>> GetSelectableMediaAsync()
        {
            return await _hardDiskMediaRepository.GetSelectableMediaAsync();
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<HardDiskMedium>> GetArchiveFilingCandidateBlankHardDisksAsync(string? keyword)
        {
            return await _hardDiskMediaRepository.GetArchiveFilingCandidateBlankHardDisksAsync(keyword);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<HardDiskMediaReturnCandidate>> GetReturnRegistrationCandidatesAsync()
        {
            var sourceApplications = await _hardDiskMediaRepository.GetCompletedOutboundApplicationsForReturnCandidatesAsync();
            var archiveOutboundSources = await _hardDiskMediaRepository.GetArchiveOutboundRequisitionReturnSourcesAsync();

            // 归还登记办结前介质仍处于借出状态，应继续出现在待归还列表；
            // 重复登记由 GetActiveReturnRegistrationByMediumIdAsync 在发起归还时拦截。
            var applicationCandidates = sourceApplications
                .Where(item => item.Medium != null)
                .Select(CreateReturnRegistrationCandidateFromOutboundApplication);

            var archiveOutboundCandidates = archiveOutboundSources
                .Select(CreateReturnRegistrationCandidateFromArchiveOutbound);

            return applicationCandidates
                .Concat(archiveOutboundCandidates)
                .GroupBy(item => item.MediumId)
                .Select(group => group.First())
                .OrderBy(item => item.ApplicantName)
                .ThenBy(item => item.DiskCode)
                .ToList();
        }

        /// <inheritdoc/>
        public Task<HardDiskMediaApplication?> GetActiveReturnRegistrationByMediumIdAsync(int mediumId) =>
            _hardDiskMediaRepository.GetActiveReturnRegistrationByMediumIdAsync(mediumId);

        public async Task<HardDiskMediaReturnCandidate?> GetReturnRegistrationCandidateByDiskCodeAsync(string diskCode)
        {
            if (string.IsNullOrWhiteSpace(diskCode))
            {
                return null;
            }

            var candidates = await GetReturnRegistrationCandidatesAsync();
            var matchedCandidate = candidates.FirstOrDefault(item =>
                string.Equals(item.DiskCode, diskCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matchedCandidate != null)
            {
                return matchedCandidate;
            }

            var sourceApplication = await _hardDiskMediaRepository.GetLatestCompletedOutboundApplicationByDiskCodeAsync(diskCode);
            if (sourceApplication?.Medium != null)
            {
                return CreateReturnRegistrationCandidateFromOutboundApplication(sourceApplication);
            }

            var archiveOutboundSource = (await _hardDiskMediaRepository.GetArchiveOutboundRequisitionReturnSourcesAsync())
                .FirstOrDefault(item => string.Equals(item.DiskCode, diskCode.Trim(), StringComparison.OrdinalIgnoreCase));

            return archiveOutboundSource == null
                ? null
                : CreateReturnRegistrationCandidateFromArchiveOutbound(archiveOutboundSource);
        }

        private static HardDiskMediaReturnCandidate CreateReturnRegistrationCandidateFromOutboundApplication(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(application.Medium);
            ArgumentNullException.ThrowIfNull(application.Medium.Ledger);

            return new HardDiskMediaReturnCandidate
            {
                MediumId = application.MediumId,
                SourceApplicationId = application.Id,
                SourceOutboundRecordId = null,
                SourceApplicationNo = application.ApplicationNo,
                ApplicantName = application.ApplicantName,
                ApplicantDept = application.ApplicantDept,
                DiskCode = application.Medium.DiskCode,
                SerialNumber = application.Medium.SerialNumber,
                Capacity = application.Medium.Capacity,
                InterfaceType = application.Medium.InterfaceType,
                BorrowedLocation = application.Medium.Ledger.StorageLocation,
                OriginalLocation = application.CurrentLocation,
                CurrentStatus = application.Medium.Ledger.MediaStatus,
                ExpectedReturnDate = application.ExpectedReturnDate
            };
        }

        private static HardDiskMediaReturnCandidate CreateReturnRegistrationCandidateFromArchiveOutbound(
            HardDiskMediaArchiveOutboundRequisitionReturnSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            return new HardDiskMediaReturnCandidate
            {
                MediumId = source.MediumId,
                SourceApplicationId = null,
                SourceOutboundRecordId = source.OutboundRecordId,
                SourceApplicationNo = source.OutboundNo,
                ApplicantName = source.ApplicantName,
                ApplicantDept = source.ApplicantDept,
                DiskCode = source.DiskCode,
                SerialNumber = source.SerialNumber,
                Capacity = source.Capacity,
                InterfaceType = source.InterfaceType,
                BorrowedLocation = source.BorrowedLocation,
                OriginalLocation = source.OriginalLocation,
                CurrentStatus = source.CurrentStatus,
                ExpectedReturnDate = source.ExpectedReturnDate
            };
        }

        private static HardDiskMediaReturnCandidate CreateReturnRegistrationCandidate(HardDiskMediaApplication application) =>
            CreateReturnRegistrationCandidateFromOutboundApplication(application);

        /// <inheritdoc/>
        public async Task<string> ResolveReturnSourceApplicationNoAsync(int? sourceApplicationId, int? sourceOutboundRecordId)
        {
            if (sourceApplicationId is > 0)
            {
                return await _hardDiskMediaRepository.GetApplicationNoByIdAsync(sourceApplicationId.Value) ?? string.Empty;
            }

            if (sourceOutboundRecordId is > 0)
            {
                return await _hardDiskMediaRepository.GetOutboundNoByRecordIdAsync(sourceOutboundRecordId.Value) ?? string.Empty;
            }

            return string.Empty;
        }

        /// <inheritdoc/>
        /// <remarks>用于年度资料登记页「硬盘·介质留存」时选择借出硬盘编号：仅返回当前用户名下仍处于借出（临时/长期）且未被占用锁占用的硬盘。</remarks>
        public async Task<IReadOnlyList<string>> GetCurrentUserBorrowedHardDiskCodesAsync(User? user)
        {
            if (user == null)
            {
                return Array.Empty<string>();
            }

            var identityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(user.RealName))
            {
                identityKeys.Add(user.RealName.Trim());
            }

            if (!string.IsNullOrWhiteSpace(user.LoginName))
            {
                identityKeys.Add(user.LoginName.Trim());
            }

            if (identityKeys.Count == 0)
            {
                return Array.Empty<string>();
            }

            var candidates = await GetReturnRegistrationCandidatesAsync();
            var matchedCandidates = candidates
                .Where(c => identityKeys.Contains((c.ApplicantName ?? string.Empty).Trim()))
                .Where(c => c.CurrentStatus == HardDiskMedium.StatusOutTemporary || c.CurrentStatus == HardDiskMedium.StatusOutLongTerm)
                .ToList();

            if (matchedCandidates.Count == 0)
            {
                return Array.Empty<string>();
            }

            var lockedMediumIds = await _hardDiskMediaRepository.GetMediumIdsWithRegisterLockAsync(
                matchedCandidates.Select(c => c.MediumId).ToList());

            return matchedCandidates
                .Where(c => !lockedMediumIds.Contains(c.MediumId))
                .Select(c => c.DiskCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetReturnTargetLocationOptionsAsync(
            string applicationType,
            int mediumId,
            int? sourceApplicationId,
            int? sourceOutboundRecordId = null)
        {
            if (mediumId <= 0 || string.IsNullOrWhiteSpace(applicationType))
            {
                return Array.Empty<HardDiskMediaReturnTargetLocationOption>();
            }

            var candidate = await GetActiveReturnCandidateAsync(mediumId, sourceApplicationId, sourceOutboundRecordId);
            if (candidate == null)
            {
                return Array.Empty<HardDiskMediaReturnTargetLocationOption>();
            }

            return applicationType switch
            {
                HardDiskMediaApplication.TypeReturnBlankRegistration => await GetBlankDedicatedReturnTargetLocationOptionsAsync(),
                HardDiskMediaApplication.TypeReturnDataRegistration => await GetDedicatedReturnTargetLocationOptionsAsync(CabinetHardDiskSlotCategoryAssignment.CategoryData),
                HardDiskMediaApplication.TypeReturnDamagedRegistration => await GetDedicatedReturnTargetLocationOptionsAsync(CabinetHardDiskSlotCategoryAssignment.CategoryDamaged),
                HardDiskMediaApplication.TypeLossRegistration =>
                [
                    new HardDiskMediaReturnTargetLocationOption
                    {
                        Location = "挂失（不归位）",
                        ExistingMediumCount = 0
                    }
                ],
                _ =>
                [
                    new HardDiskMediaReturnTargetLocationOption
                    {
                        Location = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(
                            EmptyAsFallback(candidate.OriginalLocation, candidate.BorrowedLocation)),
                        ExistingMediumCount = 0
                    }
                ]
            };
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetDedicatedTargetLocationOptionsAsync(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return Array.Empty<HardDiskMediaReturnTargetLocationOption>();
            }

            if (CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    categoryName.Trim(),
                    CabinetHardDiskSlotCategoryAssignment.CategoryBlank))
            {
                return await GetOrderedBlankDedicatedSlotLocationOptionsAsync();
            }

            return await GetDedicatedReturnTargetLocationOptionsAsync(categoryName.Trim());
        }

        /// <inheritdoc/>
        public Task<int> GetInStockMediumCountAsync(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return Task.FromResult(0);
            }

            return GetCurrentInStockMediumCountAsync(location.Trim());
        }

        /// <inheritdoc/>
        public async Task SaveMediumAsync(HardDiskMedium medium, User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(medium);

            if (string.IsNullOrWhiteSpace(medium.DiskCode))
            {
                throw new ArgumentException("硬盘编号不能为空。", nameof(medium));
            }

            if (string.IsNullOrWhiteSpace(medium.SerialNumber))
            {
                throw new ArgumentException("序列号不能为空。", nameof(medium));
            }

            if (string.IsNullOrWhiteSpace(medium.DiskType))
            {
                throw new ArgumentException("硬盘类型不能为空。", nameof(medium));
            }

            if (string.IsNullOrWhiteSpace(medium.Brand))
            {
                throw new ArgumentException("品牌不能为空。", nameof(medium));
            }

            DateTime now = DateTime.Now;
            string diskCode = medium.DiskCode.Trim();
            string serialNumber = medium.SerialNumber.Trim();

            bool duplicateDiskCode = await _hardDiskMediaRepository.HasDuplicateDiskCodeAsync(medium.Id, diskCode);
            if (duplicateDiskCode)
            {
                throw new InvalidOperationException($"硬盘编号 [{diskCode}] 已存在。");
            }

            bool duplicateSerialNumber = await _hardDiskMediaRepository.HasDuplicateSerialNumberAsync(medium.Id, serialNumber);
            if (duplicateSerialNumber)
            {
                throw new InvalidOperationException($"序列号 [{serialNumber}] 已存在。");
            }

            if (medium.Id == 0)
            {
                medium.DiskCode = diskCode;
                medium.SerialNumber = serialNumber;
                medium.DiskType = medium.DiskType.Trim();
                medium.Brand = medium.Brand.Trim();
                medium.Capacity = medium.Capacity.Trim();
                medium.InterfaceType = medium.InterfaceType.Trim();
                medium.RegisterPerson = string.IsNullOrWhiteSpace(medium.RegisterPerson) ? currentUser?.RealName?.Trim() ?? string.Empty : medium.RegisterPerson.Trim();
                medium.RegistrationMethod = string.IsNullOrWhiteSpace(medium.RegistrationMethod)
                    ? HardDiskMedium.RegistrationMethodManual
                    : medium.RegistrationMethod.Trim();
                medium.Remark = medium.Remark.Trim();
                medium.RegisterDate = medium.RegisterDate == default ? now : medium.RegisterDate;
                medium.FactoryDate = medium.FactoryDate?.Date;
                medium.CreatedTime = now;
                medium.UpdatedTime = now;

                medium.Ledger ??= new HardDiskLedger
                {
                    MediaStatus = HardDiskMedium.StatusInStockBlank,
                    MediaNature = HardDiskMedium.NatureBlank,
                    StorageLocation = string.Empty,
                    HolderOrOrganization = "资料室",
                    NeedReturn = false
                };
                medium.Ledger.DiskCode = medium.DiskCode;
                medium.Ledger.MediaStatus = string.IsNullOrWhiteSpace(medium.Ledger.MediaStatus) ? HardDiskMedium.StatusInStockBlank : medium.Ledger.MediaStatus.Trim();
                medium.Ledger.MediaNature = string.IsNullOrWhiteSpace(medium.Ledger.MediaNature) ? HardDiskMedium.NatureBlank : medium.Ledger.MediaNature.Trim();
                string storageLocation = medium.Ledger.StorageLocation?.Trim() ?? string.Empty;
                medium.Ledger.StorageLocation =
                    medium.Ledger.MediaStatus == HardDiskMedium.StatusInStockBlank
                    || medium.Ledger.MediaNature == HardDiskMedium.NatureBlank
                        ? await ResolveBlankInStockSlotLocationAsync(storageLocation)
                    : medium.Ledger.MediaStatus == HardDiskMedium.StatusInStockData
                        || medium.Ledger.MediaStatus == HardDiskMedium.StatusInStockDamaged
                        ? await ResolveDataInStockFullLocationAsync(storageLocation)
                        : storageLocation;
                medium.Ledger.HolderOrOrganization = string.IsNullOrWhiteSpace(medium.Ledger.HolderOrOrganization) ? "资料室" : medium.Ledger.HolderOrOrganization.Trim();
                medium.Ledger.NeedReturn = medium.Ledger.NeedReturn;
                medium.Ledger.RegisterPerson = medium.RegisterPerson;
                medium.Ledger.RegisterDate = medium.RegisterDate;
                medium.Ledger.Remark = medium.Remark;
                medium.Ledger.CreatedTime = now;
                medium.Ledger.UpdatedTime = now;

                _hardDiskMediaRepository.AddMedium(medium);
            }
            else
            {
                var existing = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(medium.Id);
                if (existing == null)
                {
                    throw new InvalidOperationException("未找到要保存的硬盘介质记录。");
                }

                existing.DiskCode = diskCode;
                existing.SerialNumber = serialNumber;
                existing.DiskType = medium.DiskType.Trim();
                existing.Brand = medium.Brand.Trim();
                existing.Capacity = medium.Capacity.Trim();
                existing.InterfaceType = medium.InterfaceType.Trim();
                existing.RegisterPerson = medium.RegisterPerson.Trim();
                existing.RegisterDate = medium.RegisterDate;
                existing.FactoryDate = medium.FactoryDate?.Date;
                existing.RegistrationMethod = string.IsNullOrWhiteSpace(medium.RegistrationMethod)
                    ? existing.RegistrationMethod
                    : medium.RegistrationMethod.Trim();
                existing.Remark = medium.Remark.Trim();
                existing.UpdatedTime = now;

                if (existing.Ledger != null)
                {
                    existing.Ledger.DiskCode = existing.DiskCode;
                    existing.Ledger.RegisterPerson = existing.RegisterPerson;
                    existing.Ledger.RegisterDate = existing.RegisterDate;
                    existing.Ledger.Remark = existing.Remark;
                    existing.Ledger.UpdatedTime = now;
                }
            }

            await _hardDiskMediaRepository.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<string> GenerateNextDiskCodeAsync()
        {
            var diskCodes = await _hardDiskMediaRepository.GetActiveDiskCodesAsync();

            var matchedCodes = diskCodes
                .Select(code => DiskCodeSequenceRegex.Match(code.Trim()))
                .Where(match => match.Success)
                .Select(match => new
                {
                    Prefix = match.Groups["prefix"].Value.ToUpperInvariant(),
                    SequenceText = match.Groups["sequence"].Value,
                    Sequence = int.Parse(match.Groups["sequence"].Value, CultureInfo.InvariantCulture)
                })
                .ToList();

            if (matchedCodes.Count == 0)
            {
                return "K001";
            }

            string targetPrefix = matchedCodes
                .GroupBy(item => item.Prefix)
                .OrderByDescending(group => group.Count())
                .ThenByDescending(group => group.Max(item => item.Sequence))
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key)
                .First();

            var prefixCodes = matchedCodes.Where(item => item.Prefix == targetPrefix).ToList();
            int nextSequence = prefixCodes.Max(item => item.Sequence) + 1;
            int sequenceLength = Math.Max(prefixCodes.Max(item => item.SequenceText.Length), 3);
            return $"{targetPrefix}{nextSequence.ToString($"D{sequenceLength}", CultureInfo.InvariantCulture)}";
        }

        /// <inheritdoc/>
        public async Task DeleteMediumAsync(int mediumId)
        {
            var existing = await _hardDiskMediaRepository.GetActiveMediumByIdAsync(mediumId);
            if (existing == null)
            {
                throw new InvalidOperationException("未找到要删除的硬盘介质记录。");
            }

            existing.IsDeleted = true;
            existing.UpdatedTime = DateTime.Now;
            await _hardDiskMediaRepository.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<HardDiskMediaTransaction>> SearchTransactionsAsync(string? keyword, string? transactionType)
        {
            return await _hardDiskMediaRepository.SearchTransactionsAsync(keyword, transactionType);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<HardDiskMediaApplication>> SearchApplicationsAsync(string? keyword, int? status, string? applicationType)
        {
            return await _hardDiskMediaRepository.SearchApplicationsAsync(keyword, status, applicationType);
        }

        /// <inheritdoc/>
        public async Task SaveApplicationAsync(HardDiskMediaApplication application, User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(application);

            if (application.MediumId <= 0)
            {
                throw new ArgumentException("请选择关联介质。", nameof(application));
            }

            if (string.IsNullOrWhiteSpace(application.ApplicationType))
            {
                throw new ArgumentException("申请类型不能为空。", nameof(application));
            }

            ValidateApplicationReason(application);

            DateTime applyTimeForReturnDate = application.ApplyTime == default ? DateTime.Now : application.ApplyTime;
            if (HardDiskMediaOutboundReturnSupport.IsSelectableOutboundApplicationType(application.ApplicationType))
            {
                HardDiskMediaOutboundReturnSupport.ValidateExpectedReturnDate(
                    application.ApplicationType,
                    applyTimeForReturnDate,
                    application.ExpectedReturnDate);
            }

            await ValidateAbnormalReturnRegistrationSubmitAsync(application);

            var medium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdAsync(application.MediumId);
            if (medium == null)
            {
                throw new System.InvalidOperationException("未找到申请关联的硬盘介质。");
            }

            var ledger = medium.Ledger;

            ValidateApplicationRules(application.ApplicationType, medium, currentUser);

            var returnCandidate = await GetActiveReturnCandidateAsync(application.MediumId, application.SourceApplicationId, application.SourceOutboundRecordId);
            if (IsReturnOrLossRegistrationType(application.ApplicationType) && returnCandidate == null)
            {
                throw new InvalidOperationException("未找到该介质当前有效的借出记录，无法办理归还/挂失登记。");
            }

            DateTime now = DateTime.Now;
            string applicationNo = string.IsNullOrWhiteSpace(application.ApplicationNo)
                ? await GenerateBusinessNoByApplicationTypeAsync(application.ApplicationType)
                : application.ApplicationNo.Trim();

            string applicantName = application.ApplicantName.Trim();
            string applicantDept = application.ApplicantDept.Trim();
            string currentLocation = string.IsNullOrWhiteSpace(application.CurrentLocation) ? ledger?.StorageLocation ?? string.Empty : application.CurrentLocation.Trim();
            string targetLocation = application.TargetLocation.Trim();
            string targetPersonOrUnit = application.TargetPersonOrUnit.Trim();
            DateTime? expectedReturnDate = application.ExpectedReturnDate;
            if (returnCandidate == null && HardDiskMediaOutboundReturnSupport.IsSelectableOutboundApplicationType(application.ApplicationType))
            {
                expectedReturnDate = HardDiskMediaOutboundReturnSupport.ResolveExpectedReturnDateForSave(
                    application.ApplicationType,
                    applyTimeForReturnDate,
                    expectedReturnDate);
                application.ExpectedReturnDate = expectedReturnDate;
            }

            int? sourceApplicationId = application.SourceApplicationId;
            int? sourceOutboundRecordId = application.SourceOutboundRecordId;

            if (returnCandidate != null)
            {
                applicantName = returnCandidate.ApplicantName;
                applicantDept = returnCandidate.ApplicantDept;
                currentLocation = EmptyAsFallback(returnCandidate.BorrowedLocation, ledger?.StorageLocation ?? string.Empty);
                targetLocation = await ResolveReturnTargetLocationAsync(application.ApplicationType, returnCandidate, application.TargetLocation);
                targetPersonOrUnit = returnCandidate.ApplicantName;
                expectedReturnDate = returnCandidate.ExpectedReturnDate;
                sourceApplicationId = returnCandidate.SourceApplicationId;
                sourceOutboundRecordId = returnCandidate.SourceOutboundRecordId;
            }

            bool duplicateApplicationNo = await _hardDiskMediaRepository.HasDuplicateApplicationNoAsync(application.Id, applicationNo);
            if (duplicateApplicationNo)
            {
                throw new InvalidOperationException($"申请单编号 [{applicationNo}] 已存在。");
            }

            if (application.Id == 0)
            {
                if (IsReturnOrLossRegistrationType(application.ApplicationType))
                {
                    var activeReturnRegistration = await _hardDiskMediaRepository.GetActiveReturnRegistrationByMediumIdAsync(application.MediumId);
                    if (activeReturnRegistration != null)
                    {
                        throw new InvalidOperationException(
                            $"该硬盘已有未办结归还登记单 [{activeReturnRegistration.ApplicationNo}]，请打开该单续办。");
                    }
                }

                application.ApplicationNo = applicationNo;
                application.SourceApplicationId = sourceApplicationId;
                application.SourceOutboundRecordId = sourceOutboundRecordId;
                application.ApplicationType = application.ApplicationType.Trim();
                application.ApplicationStatus = ApplicationWorkflowStatus.IsDefined(application.ApplicationStatus)
                    ? application.ApplicationStatus
                    : HardDiskMediaApplication.StatusDraft;
                application.ApplicantName = string.IsNullOrWhiteSpace(applicantName)
                    ? currentUser?.RealName?.Trim() ?? string.Empty
                    : applicantName;
                application.ApplicantDept = string.IsNullOrWhiteSpace(applicantDept)
                    ? currentUser?.Department?.Trim() ?? string.Empty
                    : applicantDept;
                application.ApplyTime = application.ApplyTime == default ? now : application.ApplyTime;
                application.TargetPersonOrUnit = targetPersonOrUnit;
                application.CurrentLocation = currentLocation;
                application.TargetLocation = targetLocation;
                application.Reason = application.Reason.Trim();
                application.RelatedBatch = application.RelatedBatch.Trim();
                application.RelatedArchiveTitle = application.RelatedArchiveTitle.Trim();
                application.ApprovalOpinion = application.ApprovalOpinion.Trim();
                application.InspectionResult = application.InspectionResult.Trim();
                application.FormatConfirmation = application.FormatConfirmation.Trim();
                application.ApprovedBy = application.ApprovedBy.Trim();
                application.ExecutedBy = application.ExecutedBy.Trim();
                application.Remark = application.Remark.Trim();
                application.CreatedTime = now;
                application.UpdatedTime = now;

                _hardDiskMediaRepository.AddApplication(application);

                await _hardDiskMediaRepository.SaveChangesAsync();

                if (ShouldKeepOutboundLock(application.ApplicationType, application.ApplicationStatus))
                {
                    var lockMedium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(application.MediumId)
                        ?? throw new InvalidOperationException("未找到申请关联的硬盘介质。");

                    await EnsureCanLockOutboundMediumAsync(application, lockMedium);
                    LockOutboundMedium(application, lockMedium);
                    await _hardDiskMediaRepository.SaveChangesAsync();
                }

                return;
            }
            else
            {
                var existing = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
                if (existing == null)
                {
                    throw new InvalidOperationException("未找到要保存的业务申请记录。");
                }

                if (existing.ApplicationStatus == HardDiskMediaApplication.StatusCompleted ||
                    existing.ApplicationStatus == HardDiskMediaApplication.StatusCancelled ||
                    existing.ApplicationStatus == HardDiskMediaApplication.StatusForceWithdrawn ||
                    existing.ApplicationStatus == HardDiskMediaApplication.StatusWithdrawn ||
                    existing.ApplicationStatus == HardDiskMediaApplication.StatusPendingProcess ||
                    existing.SignedAttachmentUploaded)
                {
                    throw new InvalidOperationException("当前申请已进入交接办理阶段，不允许变更。请重新发起业务单据。");
                }

                int originalMediumId = existing.MediumId;
                string originalApplicationType = existing.ApplicationType;
                int originalApplicationStatus = existing.ApplicationStatus;

                existing.ApplicationNo = applicationNo;
                existing.MediumId = application.MediumId;
                existing.SourceApplicationId = sourceApplicationId;
                existing.SourceOutboundRecordId = sourceOutboundRecordId;
                existing.ApplicationType = application.ApplicationType.Trim();
                existing.ApplicationStatus = ApplicationWorkflowStatus.IsDefined(application.ApplicationStatus)
                    ? application.ApplicationStatus
                    : existing.ApplicationStatus;
                existing.ApplicantName = applicantName;
                existing.ApplicantDept = applicantDept;
                existing.ApplyTime = application.ApplyTime;
                existing.Reason = application.Reason.Trim();
                existing.TargetPersonOrUnit = targetPersonOrUnit;
                existing.CurrentLocation = currentLocation;
                existing.TargetLocation = targetLocation;
                existing.ExpectedReturnDate = expectedReturnDate;
                existing.RelatedBatch = application.RelatedBatch.Trim();
                existing.RelatedArchiveTitle = application.RelatedArchiveTitle.Trim();
                existing.PrintCount = application.PrintCount;
                existing.PrintedTime = application.PrintedTime;
                existing.SignedAttachmentUploaded = application.SignedAttachmentUploaded;
                existing.SignedAttachmentUploadedTime = application.SignedAttachmentUploadedTime;
                existing.SignedAttachmentUploader = application.SignedAttachmentUploader.Trim();
                existing.ApprovedBy = application.ApprovedBy.Trim();
                existing.ApprovedTime = application.ApprovedTime;
                existing.ApprovalOpinion = application.ApprovalOpinion.Trim();
                existing.InspectionResult = application.InspectionResult.Trim();
                existing.FormatConfirmation = application.FormatConfirmation.Trim();
                existing.ExecutedBy = application.ExecutedBy.Trim();
                existing.ExecutedTime = application.ExecutedTime;
                existing.Remark = application.Remark.Trim();
                existing.UpdatedTime = now;

                await _hardDiskMediaRepository.SaveChangesAsync();

                if (ShouldKeepOutboundLock(originalApplicationType, originalApplicationStatus) &&
                    (originalMediumId != existing.MediumId || !ShouldKeepOutboundLock(existing.ApplicationType, existing.ApplicationStatus)))
                {
                    var originalMedium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(originalMediumId);
                    if (originalMedium != null)
                    {
                        UnlockOutboundMedium(existing.Id, originalMedium);
                    }
                }

                if (ShouldKeepOutboundLock(existing.ApplicationType, existing.ApplicationStatus))
                {
                    var lockMedium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(existing.MediumId)
                        ?? throw new InvalidOperationException("未找到申请关联的硬盘介质。");

                    await EnsureCanLockOutboundMediumAsync(existing, lockMedium);
                    LockOutboundMedium(existing, lockMedium);
                }

                await _hardDiskMediaRepository.SaveChangesAsync();
                return;
            }
        }

        /// <inheritdoc/>
        public async Task DeleteApplicationAsync(int applicationId)
        {
            var existing = await _hardDiskMediaRepository.GetApplicationByIdAsync(applicationId);
            if (existing == null)
            {
                throw new InvalidOperationException("未找到要删除的业务申请记录。");
            }

            if (existing.ApplicationStatus == HardDiskMediaApplication.StatusCompleted ||
                existing.ApplicationStatus == HardDiskMediaApplication.StatusPendingProcess ||
                existing.ApplicationStatus == HardDiskMediaApplication.StatusForceWithdrawn ||
                existing.ApplicationStatus == HardDiskMediaApplication.StatusWithdrawn ||
                existing.SignedAttachmentUploaded)
            {
                throw new InvalidOperationException("当前申请已进入交接办理阶段，不允许撤销。请联系资料室管理员处理。");
            }

            if (IsOutboundLockableType(existing.ApplicationType))
            {
                var medium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(existing.MediumId);
                if (medium != null)
                {
                    UnlockOutboundMedium(existing.Id, medium);
                }
            }

            _hardDiskMediaRepository.RemoveApplication(existing);
            await _hardDiskMediaRepository.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task SubmitApplicationAsync(int applicationId, User? currentUser)
        {
            var existing = await _hardDiskMediaRepository.GetApplicationWithMediumLedgerByIdAsync(applicationId);
            if (existing == null)
            {
                throw new InvalidOperationException("未找到要提交的业务申请记录。");
            }

            if (existing.ApplicationStatus != HardDiskMediaApplication.StatusDraft)
            {
                throw new InvalidOperationException("只有“未提交”状态的申请单才能提交。");
            }

            if (existing.Medium == null || existing.Medium.IsDeleted)
            {
                throw new InvalidOperationException("未找到申请关联的硬盘介质。");
            }

            await EnsureCanLockOutboundMediumAsync(existing, existing.Medium);

            ValidateApplicationRules(existing.ApplicationType, existing.Medium, currentUser);

            if (ShouldKeepOutboundLock(existing.ApplicationType, HardDiskMediaApplication.StatusSubmitted))
            {
                LockOutboundMedium(existing, existing.Medium);
            }

            existing.ApplicationStatus = HardDiskMediaApplication.StatusSubmitted;
            existing.UpdatedTime = DateTime.Now;

            await _hardDiskMediaRepository.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<string> GenerateNextApplicationNoAsync()
        {
            return await GenerateBusinessNoByApplicationTypeAsync(HardDiskMediaApplication.TypeOutboundTemporary);
        }

        /// <inheritdoc/>
        public async Task<string> GenerateNextReturnRegistrationNoAsync()
        {
            return await GenerateBusinessNoByApplicationTypeAsync(HardDiskMediaApplication.TypeReturnBlankRegistration);
        }

        private Task<string> GenerateBusinessNoByApplicationTypeAsync(string applicationType)
        {
            if (string.IsNullOrWhiteSpace(applicationType))
            {
                throw new ArgumentException("申请类型不能为空。", nameof(applicationType));
            }

            BusinessNoCategory category = ResolveBusinessNoCategory(applicationType);
            return _businessRuleService.GenerateBusinessNoAsync(category);
        }

        private static BusinessNoCategory ResolveBusinessNoCategory(string applicationType)
        {
            if (string.Equals(applicationType, HardDiskMediaApplication.TypeOutboundDestroy, StringComparison.Ordinal))
            {
                return BusinessNoCategory.DiskDestroyApply;
            }

            if (IsReturnOrLossRegistrationType(applicationType))
            {
                return BusinessNoCategory.DiskInboundRegister;
            }

            return BusinessNoCategory.DiskOutboundApply;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> GetDomainOptionLabelsAsync(string entityName, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new ArgumentException("实体名称不能为空。", nameof(entityName));
            }

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                throw new ArgumentException("字段名称不能为空。", nameof(fieldName));
            }

            return await _hardDiskMediaRepository.GetDomainOptionLabelsAsync(entityName, fieldName);
        }

    }
}
