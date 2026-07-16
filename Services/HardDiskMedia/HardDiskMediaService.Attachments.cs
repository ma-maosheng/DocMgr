using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 硬盘业务申请附件上传/删除/预览准备逻辑。
    /// </summary>
    public partial class HardDiskMediaService
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<SystemAttachment>> GetApplicationAttachmentsAsync(string applicationNo)
        {
            if (string.IsNullOrWhiteSpace(applicationNo))
            {
                return Array.Empty<SystemAttachment>();
            }

            return await _hardDiskMediaRepository.GetApplicationAttachmentsAsync(ApplicationAttachmentBusinessType, applicationNo.Trim());
        }

        /// <inheritdoc/>
        public async Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId)
        {
            return await _hardDiskMediaRepository.GetAttachmentByIdAsync(attachmentId);
        }

        /// <inheritdoc/>
        public async Task<HardDiskMediaAttachmentFlowResult> UploadSignedAttachmentAsync(HardDiskMediaApplication? application, User? currentUser, string fileName, string extension, long fileSize, byte[] fileContent)
        {
            if (application == null || application.Id == 0)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("请先保存业务申请后再上传签字件。");
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
                return HardDiskMediaAttachmentFlowResult.Fail("未找到业务申请记录，无法上传签字件。");
            }

            if (string.IsNullOrWhiteSpace(existingApplication.ApplicationNo))
            {
                return HardDiskMediaAttachmentFlowResult.Fail("申请单编号为空，无法上传签字件。");
            }

            if (existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusCompleted ||
                existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusCancelled ||
                existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusWithdrawn ||
                existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusForceWithdrawn)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("当前申请已完成或已作废，不允许上传签批交接单。");
            }

            if (existingApplication.ApplicationStatus != HardDiskMediaApplication.StatusSignedUploaded)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("请先确认实物交接后再上传签批交接单。");
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
                FileCategory = GetSignedAttachmentCategory(existingApplication.ApplicationType),
                UploadTime = DateTime.Now,
                UploaderName = currentUser?.RealName?.Trim() ?? string.Empty
            };

            _hardDiskMediaRepository.AddSystemAttachment(attachment);

            existingApplication.SignedAttachmentUploaded = true;
            existingApplication.SignedAttachmentUploadedTime = attachment.UploadTime;
            existingApplication.SignedAttachmentUploader = attachment.UploaderName;
            existingApplication.UpdatedTime = attachment.UploadTime;

            await _hardDiskMediaRepository.SaveChangesAsync();
            return HardDiskMediaAttachmentFlowResult.Ok("签批交接单上传成功。", attachment);
        }

        /// <inheritdoc/>
        public async Task<HardDiskMediaAttachmentFlowResult> DeleteApplicationAttachmentAsync(SystemAttachment? attachment)
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

            var relatedApplication = await _hardDiskMediaRepository.GetApplicationByIdAsync(existingAttachment.BusinessId);
            if (relatedApplication != null &&
                string.Equals(
                    existingAttachment.FileCategory,
                    HardDiskMediaReturnDomainValues.AttachmentKindSignedAbnormalReturnReport,
                    StringComparison.Ordinal))
            {
                _hardDiskMediaRepository.RemoveSystemAttachment(existingAttachment);
                relatedApplication.UpdatedTime = DateTime.Now;
                await _hardDiskMediaRepository.SaveChangesAsync();
                return HardDiskMediaAttachmentFlowResult.Ok("附件删除成功。");
            }

            _hardDiskMediaRepository.RemoveSystemAttachment(existingAttachment);

            if (relatedApplication != null)
            {
                // 排除当前待删除附件后再判断，避免未提交删除时误判“仍有附件”。
                bool hasAnyAttachment = await _hardDiskMediaRepository.HasOtherSignedAttachmentsAsync(
                    ApplicationAttachmentBusinessType,
                    relatedApplication.Id,
                    existingAttachment.Id,
                    GetSignedAttachmentCategory(relatedApplication.ApplicationType));

                if (!hasAnyAttachment)
                {
                    relatedApplication.SignedAttachmentUploaded = false;
                    relatedApplication.SignedAttachmentUploadedTime = null;
                    relatedApplication.SignedAttachmentUploader = string.Empty;
                }

                relatedApplication.UpdatedTime = DateTime.Now;
            }

            await _hardDiskMediaRepository.SaveChangesAsync();
            return HardDiskMediaAttachmentFlowResult.Ok("附件删除成功。");
        }

        /// <inheritdoc/>
        public async Task<HardDiskMediaAttachmentFlowResult> PrepareApplicationAttachmentViewAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("附件不存在，无法查看。");
            }

            var fullAttachment = await _hardDiskMediaRepository.GetAttachmentByIdAsync(attachment.Id);
            if (fullAttachment?.FileContent == null || fullAttachment.FileContent.Length == 0)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("附件内容为空，无法查看。");
            }

            return HardDiskMediaAttachmentFlowResult.Ok("附件已就绪。", fullAttachment);
        }

        private static string GetSignedAttachmentCategory(string applicationType)
        {
            return SignedAttachmentCategory;
        }
    }
}
