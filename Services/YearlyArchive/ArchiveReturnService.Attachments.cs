using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还附件与登记/灭失校验。
    /// </summary>
    public sealed partial class ArchiveReturnService
    {
        public Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(int recordId) =>
            _returnRepository.GetAttachmentsByBusinessIdAsync(recordId).ContinueWith(
                task => (IReadOnlyList<SystemAttachment>)task.Result,
                TaskScheduler.Default);

        public async Task<ArchiveReturnAttachmentFlowResult> UploadSignedHandoverAttachmentFlowAsync(
            int recordId,
            SystemAttachment attachment,
            User user)
        {
            ArgumentNullException.ThrowIfNull(attachment);
            ArgumentNullException.ThrowIfNull(user);

            string? formatError = SystemAttachmentUploadSupport.ValidateUploadFormat(
                attachment.FileName,
                attachment.Extension,
                attachment.FileContent);
            if (!string.IsNullOrWhiteSpace(formatError))
            {
                return ArchiveReturnAttachmentFlowResult.Fail(formatError);
            }

            if (!IsArchiveAdminUser(user))
            {
                return ArchiveReturnAttachmentFlowResult.Fail("仅资料室管理员可上传签批交接单。");
            }

            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveReturnAttachmentFlowResult.Fail("未找到指定的归还单。");
            }

            if (record.Status is YearlyArchiveReturnRecord.Completed
                or YearlyArchiveReturnRecord.WithdrawnVoid
                or YearlyArchiveReturnRecord.ForceVoided)
            {
                return ArchiveReturnAttachmentFlowResult.Fail("当前归还单已完成或已作废，不允许上传签批交接单。");
            }

            if (record.Status != YearlyArchiveReturnRecord.SignedUploaded)
            {
                return ArchiveReturnAttachmentFlowResult.Fail("请先确认实物交接后再上传签批交接单。");
            }

            attachment.BusinessType = ArchiveReturnDomainValues.BusinessTypeAttachment;
            attachment.BusinessNo = record.ReturnNo;
            attachment.BusinessId = record.Id;
            attachment.FileCategory = ArchiveReturnDomainValues.AttachmentKindSignedHandover;
            attachment.UploaderName = ResolveUserName(user);
            attachment.UploadTime = DateTime.Now;

            _returnRepository.AddAttachment(attachment);

            record.SignedAttachmentUploaded = true;
            record.SignedAttachmentUploadedTime = attachment.UploadTime;
            record.SignedAttachmentUploader = attachment.UploaderName;
            record.UpdatedAt = attachment.UploadTime;
            await _returnRepository.SaveOrUpdateRecordGraphAsync(record);

            return ArchiveReturnAttachmentFlowResult.Ok("签批交接单上传成功。", attachment);
        }

        public async Task<ArchiveReturnAttachmentFlowResult> DeleteSignedHandoverAttachmentFlowAsync(
            int recordId,
            SystemAttachment attachment,
            User user)
        {
            ArgumentNullException.ThrowIfNull(attachment);
            ArgumentNullException.ThrowIfNull(user);

            if (!IsArchiveAdminUser(user))
            {
                return ArchiveReturnAttachmentFlowResult.Fail("仅资料室管理员可删除签批交接单。");
            }

            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return ArchiveReturnAttachmentFlowResult.Fail("未找到指定的归还单。");
            }

            if (record.Status is YearlyArchiveReturnRecord.Completed
                or YearlyArchiveReturnRecord.WithdrawnVoid
                or YearlyArchiveReturnRecord.ForceVoided)
            {
                return ArchiveReturnAttachmentFlowResult.Fail("当前归还单已完成或已作废，不允许删除签批交接单。");
            }

            var existing = await _returnRepository.GetAttachmentByIdAsync(attachment.Id);
            if (existing == null || existing.BusinessId != recordId)
            {
                return ArchiveReturnAttachmentFlowResult.Fail("附件不存在或不属于当前归还单。");
            }

            if (!string.Equals(
                    existing.FileCategory,
                    ArchiveReturnDomainValues.AttachmentKindSignedHandover,
                    StringComparison.Ordinal))
            {
                return ArchiveReturnAttachmentFlowResult.Fail("该附件类型不允许按签批交接单删除。");
            }

            _returnRepository.RemoveAttachment(existing);

            var remaining = await _returnRepository.GetAttachmentsByBusinessIdAsync(recordId);
            bool stillHasSigned = remaining.Any(item =>
                item.Id != existing.Id
                && string.Equals(
                    item.FileCategory,
                    ArchiveReturnDomainValues.AttachmentKindSignedHandover,
                    StringComparison.Ordinal));
            if (!stillHasSigned)
            {
                record.SignedAttachmentUploaded = false;
                record.SignedAttachmentUploader = string.Empty;
                record.SignedAttachmentUploadedTime = null;
            }

            record.UpdatedAt = DateTime.Now;
            await _returnRepository.SaveOrUpdateRecordGraphAsync(record);
            return ArchiveReturnAttachmentFlowResult.Ok("签批交接单已删除。");
        }

        public async Task<ArchiveReturnAttachmentFlowResult> PrepareAttachmentViewFlowAsync(SystemAttachment attachment)
        {
            ArgumentNullException.ThrowIfNull(attachment);

            var full = await _returnRepository.GetAttachmentByIdAsync(attachment.Id);
            if (full?.FileContent == null || full.FileContent.Length == 0)
            {
                return ArchiveReturnAttachmentFlowResult.Fail("附件内容为空，无法查看。");
            }

            return ArchiveReturnAttachmentFlowResult.Ok("附件已就绪", full);
        }

        /// <summary>
        /// 登记前校验：内容完整性、份数逻辑一致性、灭失说明。
        /// </summary>
        private async Task<string?> ValidateForRegistrationAsync(
            YearlyArchiveReturnRecord record,
            IReadOnlyCollection<YearlyArchiveReturnItem> items)
        {
            if (items.Count == 0)
            {
                return "请至少保留一条归还明细。";
            }

            if (string.IsNullOrWhiteSpace(record.BorrowerName))
            {
                return "归还单缺少借出人信息，请重新从出库单发起归还。";
            }

            if (record.SourceOutboundRecordId <= 0 || string.IsNullOrWhiteSpace(record.SourceOutboundNo))
            {
                return "归还单缺少源出库单信息，请重新从出库单发起归还。";
            }

            var outbound = await _outboundRepository.GetByIdWithDetailsAsync(record.SourceOutboundRecordId);
            if (outbound == null)
            {
                return "未找到对应的源出库单，无法登记。";
            }

            if (outbound.Status != YearlyArchiveOutboundRecord.Completed)
            {
                return "源出库单不是“已办结出库”状态，无法登记归还。";
            }

            var outboundItemById = outbound.Items.ToDictionary(item => item.Id);
            var seenOutboundItemIds = new HashSet<int>();

            foreach (var item in items)
            {
                ArchiveReturnDomainValues.NormalizeReturnCopyCounts(item);
                string label = BuildItemLabel(item);

                if (!string.Equals(
                        item.MediaKind?.Trim(),
                        ArchiveRegisterDomainValues.MediaKindSimulated,
                        StringComparison.Ordinal))
                {
                    return $"明细「{label}」不是模拟介质，资料归还仅支持模拟介质。";
                }

                if (item.SourceOutboundItemId <= 0)
                {
                    return $"明细「{label}」缺少源出库明细关联，请重新发起归还。";
                }

                if (!seenOutboundItemIds.Add(item.SourceOutboundItemId))
                {
                    return $"明细「{label}」重复关联同一出库明细，请检查后重试。";
                }

                if (!outboundItemById.TryGetValue(item.SourceOutboundItemId, out var outboundItem))
                {
                    return $"明细「{label}」对应的出库明细已不存在，请重新发起归还。";
                }

                if (string.Equals(
                        outboundItem.ReservationStatus,
                        ArchiveOutboundDomainValues.SyncEntryPhaseReturned,
                        StringComparison.Ordinal))
                {
                    return $"明细「{label}」已归还办结，不可重复登记。";
                }

                if (!ArchiveReturnItemDisplaySupport.IsReturnableOutboundItem(outboundItem))
                {
                    return $"明细「{label}」不是可归还的模拟介质提档明细，请重新发起归还。";
                }

                if (item.FilingFactId <= 0)
                {
                    return $"明细「{label}」缺少立档事实关联，请重新发起归还。";
                }

                if (item.FilingFactId != outboundItem.FilingFactId)
                {
                    return $"明细「{label}」与源出库明细的立档事实不一致，请重新发起归还。";
                }

                int borrowed = ArchiveReturnDomainValues.ResolveBorrowedCopyCount(item);
                int intact = ArchiveReturnDomainValues.ResolveIntactReturnCopyCount(item);
                int loss = ArchiveReturnDomainValues.ResolveLossCopyCount(item);
                int expectedBorrowed = Math.Max(1, outboundItem.CopyCount ?? 1);
                if (borrowed != expectedBorrowed)
                {
                    return $"明细「{label}」的借出份数（{borrowed}）与出库借出份数（{expectedBorrowed}）不一致。";
                }

                if (intact < 0 || loss < 0 || intact > borrowed || loss > borrowed)
                {
                    return $"明细「{label}」的完好归还份数或灭失份数无效。";
                }

                if (intact + loss != borrowed)
                {
                    return $"明细「{label}」的完好归还份数与灭失份数之和必须等于借出份数。";
                }
            }

            // 容器状态按活数据再评估（明细为同一引用，结果写回展示字段）
            await EnrichContainerAssessmentsAsync(new YearlyArchiveReturnRecord
            {
                Items = items is List<YearlyArchiveReturnItem> list
                    ? list
                    : items.ToList()
            });
            string? containerError = await ValidateContainerStatusForRegistrationAsync(items);
            if (!string.IsNullOrWhiteSpace(containerError))
            {
                return containerError;
            }

            if (ArchiveReturnDomainValues.HasAbnormalReturnItems(items)
                && string.IsNullOrWhiteSpace(record.LossDescription))
            {
                return "本单存在灭失份数，请填写资料灭失具体情况。";
            }

            // 灭失说明写入签批交接单；签批交接单扫描件由资料室在审批入库侧上传。
            return null;
        }

        private Task<ArchiveReturnFlowResult> ValidateAbnormalReturnGateAsync(YearlyArchiveReturnRecord record)
        {
            if (record.Status == YearlyArchiveReturnRecord.Completed)
            {
                return Task.FromResult(ArchiveReturnFlowResult.Ok(string.Empty, record.Id));
            }

            if (!ArchiveReturnDomainValues.HasAbnormalReturnItems(record.Items))
            {
                return Task.FromResult(ArchiveReturnFlowResult.Ok(string.Empty, record.Id));
            }

            if (string.IsNullOrWhiteSpace(record.LossDescription))
            {
                return Task.FromResult(ArchiveReturnFlowResult.Fail("本单存在灭失份数，请填写资料灭失具体情况。"));
            }

            return Task.FromResult(ArchiveReturnFlowResult.Ok(string.Empty, record.Id));
        }

        private static string BuildItemLabel(YearlyArchiveReturnItem item) =>
            string.IsNullOrWhiteSpace(item.ItemName)
                ? item.MaterialName
                : $"{item.MaterialName}/{item.ItemName}";
    }
}
