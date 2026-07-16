using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 硬盘归还非正常归还附件、校验与打印数据。
    /// </summary>
    public partial class HardDiskMediaService
    {
        /// <inheritdoc/>
        public async Task<HardDiskMediaAbnormalReturnReportPrintData> BuildAbnormalReturnReportPrintDataAsync(
            HardDiskMediaApplication? application,
            bool blankReturnerSignature)
        {
            if (application == null || application.Id == 0)
            {
                throw new InvalidOperationException("请先保存登记单后再打印非正常归还情况表。");
            }

            var existingApplication = await _hardDiskMediaRepository.GetApplicationWithMediumLedgerByIdAsNoTrackingAsync(application.Id)
                ?? throw new InvalidOperationException("未找到指定的归还登记单。");

            if (existingApplication.ApplicationStatus is HardDiskMediaApplication.StatusCompleted
                or HardDiskMediaApplication.StatusWithdrawn
                or HardDiskMediaApplication.StatusForceWithdrawn)
            {
                throw new InvalidOperationException("已办结或已作废的登记单不可打印非正常归还情况表。");
            }

            if (!HardDiskMediaReturnDomainValues.IsAbnormalReturn(existingApplication))
            {
                throw new InvalidOperationException("当前登记单为正常归还，无需打印非正常归还情况表。");
            }

            string sourceApplicationNo = await ResolveReturnSourceApplicationNoAsync(
                existingApplication.SourceApplicationId,
                existingApplication.SourceOutboundRecordId);

            var borrowApproval = await ResolveBorrowApprovalSignatureSnapshotAsync(
                existingApplication.SourceApplicationId,
                existingApplication.SourceOutboundRecordId,
                blankReturnerSignature);

            return new HardDiskMediaAbnormalReturnReportPrintData
            {
                ApplicationNo = existingApplication.ApplicationNo,
                SourceApplicationNo = sourceApplicationNo,
                ReturnDateText = existingApplication.ApplyTime == default
                    ? string.Empty
                    : existingApplication.ApplyTime.ToString("yyyy-MM-dd"),
                ApplicantDept = existingApplication.ApplicantDept,
                ApplicantName = existingApplication.ApplicantName,
                ApplicationType = HardDiskMediaReturnDomainValues.ResolveRegistrationKindDisplay(
                    existingApplication.ApplicationType,
                    existingApplication.InspectionResult),
                DiskCode = existingApplication.Medium?.DiskCode ?? string.Empty,
                SerialNumber = existingApplication.Medium?.SerialNumber ?? string.Empty,
                CurrentLocation = existingApplication.CurrentLocation,
                InspectionResult = HardDiskMediaReturnDomainValues.ResolveInspectionResultDisplay(
                    existingApplication.ApplicationType,
                    existingApplication.InspectionResult),
                Reason = existingApplication.Reason,
                ApplicantDeptHeadSignerSlot = borrowApproval.DeptHeadSignerSlot,
                ApplicantDeptHeadSignatureDateText = borrowApproval.DeptHeadSignatureDateText,
                ArchiveRoomHeadSignerSlot = borrowApproval.ArchiveRoomHeadSignerSlot,
                ArchiveRoomHeadSignatureDateText = borrowApproval.ArchiveRoomHeadSignatureDateText,
                BlankReturnerSignature = blankReturnerSignature,
                BlankBorrowApprovalSignatures = borrowApproval.BlankSignatures
            };
        }

        private async Task<(string DeptHeadSignerSlot, string DeptHeadSignatureDateText, string ArchiveRoomHeadSignerSlot, string ArchiveRoomHeadSignatureDateText, bool BlankSignatures)> ResolveBorrowApprovalSignatureSnapshotAsync(
            int? sourceApplicationId,
            int? sourceOutboundRecordId,
            bool blankSignatures)
        {
            if (blankSignatures)
            {
                return (string.Empty, string.Empty, string.Empty, string.Empty, true);
            }

            if (sourceOutboundRecordId is > 0)
            {
                var outbound = await _hardDiskMediaRepository.GetOutboundApprovalSnapshotAsync(sourceOutboundRecordId.Value);
                if (outbound != null)
                {
                    return (
                        outbound.DeptAuditor,
                        FormatApprovalDate(outbound.DeptAuditDate),
                        outbound.ArchiveRoomHead,
                        FormatApprovalDate(outbound.ArchiveRoomHeadDate),
                        false);
                }
            }

            if (sourceApplicationId is > 0)
            {
                var sourceApplication = await _hardDiskMediaRepository.GetApplicationByIdAsync(sourceApplicationId.Value);
                if (sourceApplication != null)
                {
                    return (
                        sourceApplication.ReviewerName,
                        FormatApprovalDate(sourceApplication.ReviewerDate),
                        sourceApplication.ApprovedBy,
                        FormatApprovalDate(sourceApplication.ApprovedTime),
                        false);
                }
            }

            return (string.Empty, string.Empty, string.Empty, string.Empty, true);
        }

        private static string FormatApprovalDate(DateTime? value) =>
            value.HasValue ? value.Value.ToString("yyyy-MM-dd") : string.Empty;

        /// <inheritdoc/>
        public async Task<HardDiskMediaAttachmentFlowResult> UploadAbnormalReturnReportAsync(
            HardDiskMediaApplication? application,
            User? currentUser,
            string fileName,
            string extension,
            long fileSize,
            byte[] fileContent)
        {
            if (application == null || application.Id == 0)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("请先保存登记草稿后再上传非正常归还情况表扫描件。");
            }

            if (!IsArchiveRoomMediaAdmin(currentUser))
            {
                return HardDiskMediaAttachmentFlowResult.Fail("仅资料室资料管理员可上传非正常归还情况表扫描件。");
            }

            if (string.IsNullOrWhiteSpace(fileName) || fileContent == null || fileContent.Length == 0)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("附件内容为空，无法上传。");
            }

            string? formatError = SystemAttachmentUploadSupport.ValidateUploadFormat(fileName, extension, fileContent);
            if (!string.IsNullOrWhiteSpace(formatError))
            {
                return HardDiskMediaAttachmentFlowResult.Fail(formatError);
            }

            var existingApplication = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
            if (existingApplication == null)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("未找到归还登记单，无法上传扫描件。");
            }

            if (!HardDiskMediaReturnDomainValues.IsAbnormalReturn(existingApplication))
            {
                return HardDiskMediaAttachmentFlowResult.Fail("当前登记单为正常归还，无需上传非正常归还情况表扫描件。");
            }

            if (existingApplication.ApplicationStatus is HardDiskMediaApplication.StatusCompleted
                or HardDiskMediaApplication.StatusWithdrawn
                or HardDiskMediaApplication.StatusForceWithdrawn
                or HardDiskMediaApplication.StatusSignedUploaded)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("当前登记单已进入交接办理阶段，不允许上传扫描件。");
            }

            if (string.IsNullOrWhiteSpace(existingApplication.ApplicationNo))
            {
                return HardDiskMediaAttachmentFlowResult.Fail("登记单编号为空，无法上传扫描件。");
            }

            var attachment = new SystemAttachment
            {
                BusinessType = ApplicationAttachmentBusinessType,
                BusinessNo = existingApplication.ApplicationNo,
                BusinessId = existingApplication.Id,
                FileName = fileName,
                Extension = extension ?? string.Empty,
                FileSize = fileSize,
                FileContent = fileContent,
                FileCategory = HardDiskMediaReturnDomainValues.AttachmentKindSignedAbnormalReturnReport,
                UploadTime = DateTime.Now,
                UploaderName = currentUser?.RealName?.Trim() ?? string.Empty
            };

            _hardDiskMediaRepository.AddSystemAttachment(attachment);
            existingApplication.UpdatedTime = attachment.UploadTime;
            await _hardDiskMediaRepository.SaveChangesAsync();

            return HardDiskMediaAttachmentFlowResult.Ok("非正常归还情况表扫描件上传成功。", attachment);
        }

        /// <inheritdoc/>
        public async Task<HardDiskMediaAttachmentFlowResult> DeleteAbnormalReturnReportAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("附件不存在，无法删除。");
            }

            var existingAttachment = await _hardDiskMediaRepository.GetSystemAttachmentByIdAsync(attachment.Id);
            if (existingAttachment == null)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("附件不存在，无法删除。");
            }

            if (!string.Equals(
                    existingAttachment.FileCategory,
                    HardDiskMediaReturnDomainValues.AttachmentKindSignedAbnormalReturnReport,
                    StringComparison.Ordinal))
            {
                return HardDiskMediaAttachmentFlowResult.Fail("该附件类型不允许通过此操作删除。");
            }

            var relatedApplication = await _hardDiskMediaRepository.GetApplicationByIdAsync(existingAttachment.BusinessId);
            if (relatedApplication == null)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("未找到关联的归还登记单。");
            }

            if (relatedApplication.ApplicationStatus is HardDiskMediaApplication.StatusCompleted
                or HardDiskMediaApplication.StatusWithdrawn
                or HardDiskMediaApplication.StatusForceWithdrawn
                or HardDiskMediaApplication.StatusSignedUploaded)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("当前登记单已进入交接办理阶段，不允许删除扫描件。");
            }

            _hardDiskMediaRepository.RemoveSystemAttachment(existingAttachment);
            relatedApplication.UpdatedTime = DateTime.Now;
            await _hardDiskMediaRepository.SaveChangesAsync();
            return HardDiskMediaAttachmentFlowResult.Ok("扫描件已删除。");
        }

        /// <inheritdoc/>
        public async Task<bool> HasUploadedAbnormalReturnReportAsync(int applicationId, string? applicationNo)
        {
            string resolvedNo = applicationNo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resolvedNo) && applicationId > 0)
            {
                var application = await _hardDiskMediaRepository.GetApplicationByIdAsync(applicationId);
                resolvedNo = application?.ApplicationNo?.Trim() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(resolvedNo))
            {
                return false;
            }

            var attachments = await _hardDiskMediaRepository.GetApplicationAttachmentsAsync(
                ApplicationAttachmentBusinessType,
                resolvedNo);
            return attachments.Any(item => string.Equals(
                item.FileCategory,
                HardDiskMediaReturnDomainValues.AttachmentKindSignedAbnormalReturnReport,
                StringComparison.Ordinal));
        }

        private async Task ValidateAbnormalReturnRegistrationSubmitAsync(HardDiskMediaApplication application)
        {
            if (!IsReturnOrLossRegistrationType(application.ApplicationType) ||
                application.ApplicationStatus != HardDiskMediaApplication.StatusSubmitted ||
                !HardDiskMediaReturnDomainValues.IsAbnormalReturn(application))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(application.Reason))
            {
                throw new InvalidOperationException("非正常归还需填写具体情况说明。");
            }
        }
    }
}
