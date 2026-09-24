using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Shared;

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
        public Task<HardDiskMediaAttachmentFlowResult> UploadSignedAttachmentAsync(
            HardDiskMediaApplication? application,
            User? currentUser,
            string fileName,
            string extension,
            long fileSize,
            byte[] fileContent)
        {
            return UploadApplicationAttachmentAsync(
                application,
                currentUser,
                HardDiskOutboundDomainValues.AttachmentCategorySignedHandover,
                fileName,
                extension,
                fileSize,
                fileContent);
        }

        /// <inheritdoc/>
        public async Task<HardDiskMediaAttachmentFlowResult> UploadApplicationAttachmentAsync(
            HardDiskMediaApplication? application,
            User? currentUser,
            string fileCategory,
            string fileName,
            string extension,
            long fileSize,
            byte[] fileContent)
        {
            if (application == null || application.Id == 0)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("请先保存业务申请后再上传附件。");
            }

            string category = fileCategory?.Trim() ?? string.Empty;
            if (!HardDiskOutboundDomainValues.IsKnownAttachmentCategory(category))
            {
                return HardDiskMediaAttachmentFlowResult.Fail("附件分类无效。");
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
                return HardDiskMediaAttachmentFlowResult.Fail("未找到业务申请记录，无法上传附件。");
            }

            if (string.IsNullOrWhiteSpace(existingApplication.ApplicationNo))
            {
                return HardDiskMediaAttachmentFlowResult.Fail("申请单编号为空，无法上传附件。");
            }

            if (existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusCancelled ||
                existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusWithdrawn ||
                existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusForceWithdrawn)
            {
                return HardDiskMediaAttachmentFlowResult.Fail("当前申请已作废，不允许上传附件。");
            }

            bool isOther = string.Equals(category, HardDiskOutboundDomainValues.AttachmentCategoryOther, StringComparison.Ordinal);
            var attachGate = OfflineApprovalLifecycleSupport.EvaluateAttachmentUpload(
                new OfflineApprovalLifecycleSupport.GateContext(
                    existingApplication.ApplicationStatus,
                    existingApplication.SignedAttachmentUploaded,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
                isOtherCategory: isOther,
                isArchiveAdmin: IsArchiveRoomMediaAdmin(currentUser));
            if (!attachGate.Allowed)
            {
                return HardDiskMediaAttachmentFlowResult.Fail(attachGate.DenyMessage ?? "当前状态不允许上传附件。");
            }

            if (existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusSignedUploaded)
            {
                if (string.Equals(category, HardDiskOutboundDomainValues.AttachmentCategoryPhysicalPhoto, StringComparison.Ordinal)
                    && !HardDiskOutboundDomainValues.RequiresPhysicalPhotoAttachment(existingApplication.ApplicationType))
                {
                    return HardDiskMediaAttachmentFlowResult.Fail("本单无实物流转，无需上传实物照片。");
                }

                if (string.Equals(category, HardDiskOutboundDomainValues.AttachmentCategoryProofMaterial, StringComparison.Ordinal)
                    && !HardDiskOutboundDomainValues.RequiresProofMaterialAttachment(existingApplication.ProofMaterialNote))
                {
                    return HardDiskMediaAttachmentFlowResult.Fail("申请时未声明附有证明材料，无需上传证明材料。");
                }
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
                FileCategory = category,
                UploadTime = DateTime.Now,
                UploaderName = currentUser?.RealName?.Trim() ?? string.Empty
            };

            _hardDiskMediaRepository.AddSystemAttachment(attachment);

            if (HardDiskOutboundDomainValues.IsSignedHandoverCategory(category))
            {
                existingApplication.SignedAttachmentUploaded = true;
                existingApplication.SignedAttachmentUploadedTime = attachment.UploadTime;
                existingApplication.SignedAttachmentUploader = attachment.UploaderName;
            }

            existingApplication.UpdatedTime = attachment.UploadTime;

            await _hardDiskMediaRepository.SaveChangesAsync();
            return HardDiskMediaAttachmentFlowResult.Ok($"{category}上传成功。", attachment);
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
            if (relatedApplication != null)
            {
                var deleteGate = OfflineApprovalLifecycleSupport.EvaluateAttachmentDelete(relatedApplication.ApplicationStatus);
                if (!deleteGate.Allowed)
                {
                    return HardDiskMediaAttachmentFlowResult.Fail(deleteGate.DenyMessage ?? "当前状态不允许删除附件。");
                }
            }

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

            string deletedCategory = existingAttachment.FileCategory?.Trim() ?? string.Empty;
            _hardDiskMediaRepository.RemoveSystemAttachment(existingAttachment);

            if (relatedApplication != null)
            {
                relatedApplication.UpdatedTime = DateTime.Now;

                if (HardDiskOutboundDomainValues.IsSignedHandoverCategory(deletedCategory))
                {
                    bool hasAnyAttachment = await _hardDiskMediaRepository.HasOtherSignedAttachmentsAsync(
                        ApplicationAttachmentBusinessType,
                        relatedApplication.Id,
                        existingAttachment.Id,
                        HardDiskOutboundDomainValues.AttachmentCategorySignedHandover);

                    if (!hasAnyAttachment)
                    {
                        relatedApplication.SignedAttachmentUploaded = false;
                        relatedApplication.SignedAttachmentUploadedTime = null;
                        relatedApplication.SignedAttachmentUploader = string.Empty;
                    }
                }
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
    }
}
