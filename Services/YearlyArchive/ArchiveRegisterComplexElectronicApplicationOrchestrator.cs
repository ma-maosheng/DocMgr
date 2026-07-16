using DocMgr.Models.Projects;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 按资料登记申请操作台（申请人）路径：保存草稿（生成单号）→ 提交申请（提交流程内校验），并核对持久化结果。
    /// </summary>
    public sealed class ArchiveRegisterComplexElectronicApplicationOrchestrator
    {
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IArchiveRegisterRepository _archiveRegisterRepository;

        public ArchiveRegisterComplexElectronicApplicationOrchestrator(
            IArchiveRegisterService archiveRegisterService,
            IArchiveRegisterRepository archiveRegisterRepository)
        {
            _archiveRegisterService = archiveRegisterService;
            _archiveRegisterRepository = archiveRegisterRepository;
        }

        public async Task<ComplexElectronicApplicationSubmitResult> SubmitLikeApplicantConsoleAsync(
            ComplexElectronicApplicationSubmitRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Applicant);
            ArgumentNullException.ThrowIfNull(request.Template);
            ArgumentNullException.ThrowIfNull(request.DomainOptions);
            ArgumentNullException.ThrowIfNull(request.MediaEntries);

            var lines = new List<string>
            {
                $"  · 场景：{request.Template.MaterialName}",
                "  · 流程：SaveDraftFlow → SubmitApplicationFlow（与操作台申请人保存草稿后提交一致；校验在提交流程内执行）"
            };

            var record = _archiveRegisterService.CreateDraftRecord(request.Applicant);
            ApplyApplicantRecordHeader(record, request);

            bool isExternalSource = _archiveRegisterService.IsExternalSourceType(record.SourceType);
            var mediaEntries = BuildConsoleMediaEntries(request.MediaEntries, request.DomainOptions);

            var draftResult = await _archiveRegisterService.SaveDraftFlowAsync(record, mediaEntries, request.Applicant);
            if (!draftResult.Success)
            {
                return Fail(lines, $"保存草稿失败：{draftResult.Message}");
            }

            lines.Add($"  · 保存草稿：{draftResult.Message}（单号 {record.FormNo}）");

            record.SourceType = request.Template.SourceType?.Trim() ?? string.Empty;
            record.ArchivePurpose = request.Template.ArchivePurpose?.Trim() ?? string.Empty;

            var submitResult = await _archiveRegisterService.SubmitApplicationFlowAsync(
                record,
                mediaEntries,
                isExternalSource,
                request.Applicant);
            if (!submitResult.Success)
            {
                return Fail(lines, $"提交申请失败：{submitResult.Message}");
            }

            lines.Add($"  · 提交申请：{submitResult.Message}（单号 {record.FormNo}）");

            var verifyIssues = await ArchiveRegisterComplexElectronicPostSubmitVerifier.VerifyAsync(
                _archiveRegisterRepository,
                _archiveRegisterService,
                record.FormNo,
                mediaEntries,
                request.ExpectsBorrowedHardDiskLock);

            if (verifyIssues.Count > 0)
            {
                lines.Add("  · 提交后核对：未通过");
                foreach (string issue in verifyIssues)
                {
                    lines.Add($"      - {issue}");
                }

                return new ComplexElectronicApplicationSubmitResult(record.FormNo, false, lines);
            }

            lines.Add("  · 提交后核对：通过（项目、介质明细、借出硬盘锁与操作台一致）");
            return new ComplexElectronicApplicationSubmitResult(record.FormNo, true, lines);
        }

        private static void ApplyApplicantRecordHeader(
            YearlyArchiveRegisterRecord record,
            ComplexElectronicApplicationSubmitRequest request)
        {
            var template = request.Template;
            var applicant = request.Applicant;

            record.CreatedDate = request.CreatedAt;
            record.ApplicantDate = request.CreatedAt;
            record.ApplicantName = string.IsNullOrWhiteSpace(applicant.RealName)
                ? applicant.LoginName.Trim()
                : applicant.RealName.Trim();
            record.ApplicantDept = applicant.Department?.Trim() ?? string.Empty;
            record.ProjectId = template.ProjectId;
            record.ProjectName = template.ProjectName?.Trim() ?? string.Empty;
            record.MaterialName = template.MaterialName;
            record.SourceType = template.SourceType?.Trim() ?? string.Empty;
            record.ProvideUnit = template.ProvideUnit?.Trim() ?? string.Empty;
            record.ArchivePurpose = template.ArchivePurpose?.Trim() ?? string.Empty;
            record.OtherRequests = request.OtherRequestsMarker;
        }

        /// <summary>
        /// 按 <see cref="ArchiveRegisterViewModel"/> 的 BuildMediaEntries 规则规范化介质明细。
        /// </summary>
        private static List<YearlyArchiveRegisterMedia> BuildConsoleMediaEntries(
            IReadOnlyList<YearlyArchiveRegisterMedia> sourceEntries,
            ArchiveRegisterPageDomainOptions domainOptions)
        {
            ArgumentNullException.ThrowIfNull(domainOptions);

            var result = new List<YearlyArchiveRegisterMedia>(sourceEntries.Count);
            string? sharedElectronicType = sourceEntries
                .FirstOrDefault(entry => string.Equals(entry.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase))
                ?.MediaType?.Trim();
            string? sharedElectronicDisposition = sourceEntries
                .FirstOrDefault(entry => string.Equals(entry.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase))
                ?.Disposition?.Trim();

            foreach (var source in sourceEntries)
            {
                bool isElectronic = string.Equals(
                    source.MediaKind,
                    ArchiveRegisterDomainValues.MediaKindElectronic,
                    StringComparison.OrdinalIgnoreCase);

                var media = new YearlyArchiveRegisterMedia
                {
                    MediaKind = source.MediaKind,
                    MediaType = isElectronic
                        ? sharedElectronicType ?? source.MediaType
                        : source.MediaType,
                    MediaCount = isElectronic ? 1 : source.MediaCount,
                    Disposition = isElectronic
                        ? sharedElectronicDisposition ?? source.Disposition
                        : ArchiveRegisterDomainValues.SimulatedDispositionRetain,
                    IsBorrowedHardDisk = isElectronic && IsRetainedHardDiskMedia(source) && source.IsBorrowedHardDisk,
                    BorrowedHardDiskCode = isElectronic && IsRetainedHardDiskMedia(source) && source.IsBorrowedHardDisk
                        ? source.BorrowedHardDiskCode?.Trim() ?? string.Empty
                        : string.Empty,
                    Items = source.Items.Select(item => MapMediaItemForConsole(item, isElectronic)).ToList()
                };

                result.Add(media);
            }

            return result;
        }

        private static YearlyArchiveRegisterMediaItem MapMediaItemForConsole(
            YearlyArchiveRegisterMediaItem source,
            bool isElectronic)
        {
            var item = new YearlyArchiveRegisterMediaItem
            {
                ItemType = source.ItemType,
                ContentDesc = source.ContentDesc,
                ContentCount = source.ContentCount,
                StoragePath = isElectronic
                    ? ElectronicMediaItemSupport.FormatStoragePathForRegistration(source.StoragePath)
                    : source.StoragePath ?? string.Empty,
                Note = source.Note ?? string.Empty,
                ConfidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(source.ConfidentialLevel)
            };

            if (!isElectronic || source.ElectronicDetail == null)
            {
                return item;
            }

            var detail = source.ElectronicDetail;
            item.ElectronicDetail = new YearlyArchiveRegisterElectronicMediaItemDetail
            {
                MaterialCategory = detail.MaterialCategory,
                SubCategory = detail.SubCategory,
                DataOrganizationForm = detail.DataOrganizationForm,
                DataSizeMb = detail.DataSizeMb,
                Entries = detail.Entries
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

            return item;
        }

        private static bool IsRetainedHardDiskMedia(YearlyArchiveRegisterMedia media)
        {
            return string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(media.MediaType)
                && media.MediaType.Contains("硬盘", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(media.Disposition)
                && media.Disposition.Contains("留存", StringComparison.OrdinalIgnoreCase);
        }

        private static ComplexElectronicApplicationSubmitResult Fail(List<string> lines, string message)
        {
            lines.Add($"  ✗ {message}");
            return new ComplexElectronicApplicationSubmitResult(string.Empty, false, lines);
        }
    }

    public sealed class ComplexElectronicApplicationSubmitRequest
    {
        public required User Applicant { get; init; }

        public required ComplexElectronicSimulationTemplate Template { get; init; }

        public required ArchiveRegisterPageDomainOptions DomainOptions { get; init; }

        public required IReadOnlyList<YearlyArchiveRegisterMedia> MediaEntries { get; init; }

        public required DateTime CreatedAt { get; init; }

        public required string OtherRequestsMarker { get; init; }

        public bool ExpectsBorrowedHardDiskLock { get; init; }
    }

    public sealed record ComplexElectronicApplicationSubmitResult(
        string FormNo,
        bool Success,
        IReadOnlyList<string> ChecklistLines);

    public sealed record ComplexElectronicSimulationTemplate(
        int ProjectId,
        string ProjectName,
        string SourceType,
        string ProvideUnit,
        string MaterialName,
        string ArchivePurpose,
        string OtherRequests,
        IReadOnlyList<YearlyArchiveRegisterMedia> MediaEntries);
}
