using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 硬盘业务申请处理、办结与打印相关流程。
    /// </summary>
    public partial class HardDiskMediaService
    {
        /// <inheritdoc/>
        public async Task<HardDiskMediaFlowResult> ApproveApplicationAsync(HardDiskMediaApplication? application, User? currentUser, HardDiskMediaApprovalInput? approvalInput)
        {
            if (application == null || application.Id == 0)
            {
                return HardDiskMediaFlowResult.Fail("当前申请单无效，无法审批。");
            }

            if (!IsArchiveRoomMediaAdmin(currentUser))
            {
                return HardDiskMediaFlowResult.Fail("仅资料室资料管理员可执行审批通过。");
            }

            var existing = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
            if (existing == null)
            {
                return HardDiskMediaFlowResult.Fail("未找到当前申请单。");
            }

            if (existing.ApplicationStatus != HardDiskMediaApplication.StatusSubmitted)
            {
                return HardDiskMediaFlowResult.Fail("只有“已提交-待审批”的申请单才能执行审批通过。");
            }

            var now = DateTime.Now;
            var input = approvalInput ?? new HardDiskMediaApprovalInput();

            if (IsReturnRegistrationType(existing.ApplicationType) &&
                existing.ApplicationType != HardDiskMediaApplication.TypeLossRegistration)
            {
                string requestedTargetLocation = !string.IsNullOrWhiteSpace(input.TargetLocation)
                    ? input.TargetLocation.Trim()
                    : existing.TargetLocation?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(requestedTargetLocation))
                {
                    return HardDiskMediaFlowResult.Fail("请先由资料室管理员指定归还位置后再审批通过。");
                }

                var returnCandidate = await GetActiveReturnCandidateAsync(
                    existing.MediumId,
                    existing.SourceApplicationId,
                    existing.SourceOutboundRecordId);
                if (returnCandidate == null)
                {
                    return HardDiskMediaFlowResult.Fail("未找到当前有效的借出记录，无法确定归还位置。");
                }

                try
                {
                    existing.TargetLocation = await ResolveReturnTargetLocationAsync(
                        existing.ApplicationType,
                        returnCandidate,
                        requestedTargetLocation);
                }
                catch (InvalidOperationException ex)
                {
                    return HardDiskMediaFlowResult.Fail(ex.Message);
                }
            }
            else if (existing.ApplicationType == HardDiskMediaApplication.TypeLossRegistration)
            {
                existing.TargetLocation = string.Empty;
            }

            existing.ApplicationStatus = HardDiskMediaApplication.StatusApproved;
            existing.ReviewerName = string.IsNullOrWhiteSpace(input.ReviewerName)
                ? currentUser?.RealName?.Trim() ?? string.Empty
                : input.ReviewerName.Trim();
            existing.ReviewerDate = input.ReviewerDate ?? now;
            existing.ApprovedBy = string.IsNullOrWhiteSpace(input.ApproverName)
                ? currentUser?.RealName?.Trim() ?? string.Empty
                : input.ApproverName.Trim();
            existing.ApprovedTime = input.ApproverDate ?? now;
            existing.ApprovalOpinion = string.IsNullOrWhiteSpace(input.ApprovalOpinion) ? "同意" : input.ApprovalOpinion.Trim();
            existing.UpdatedTime = now;

            await _hardDiskMediaRepository.SaveChangesAsync();
            return HardDiskMediaFlowResult.Ok("审批信息录入成功。请办理实物交接。");
        }

        /// <inheritdoc/>
        public async Task<HardDiskMediaFlowResult> ConfirmPhysicalHandoverAsync(
            HardDiskMediaApplication? application,
            User? currentUser,
            HardDiskMediaApprovalInput? handoverInput)
        {
            if (application == null || application.Id == 0)
            {
                return HardDiskMediaFlowResult.Fail("当前申请单无效，无法确认实物交接。");
            }

            if (!IsArchiveRoomMediaAdmin(currentUser))
            {
                return HardDiskMediaFlowResult.Fail("仅资料室资料管理员可确认实物交接。");
            }

            var existing = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
            if (existing == null)
            {
                return HardDiskMediaFlowResult.Fail("未找到当前申请单。");
            }

            if (existing.ApplicationStatus != HardDiskMediaApplication.StatusApproved)
            {
                return HardDiskMediaFlowResult.Fail("只有“已审批-待实物交接”的申请单才能确认实物交接。");
            }

            var input = handoverInput ?? new HardDiskMediaApprovalInput();
            string handoverAdmin = input.HandoverAdmin?.Trim() ?? string.Empty;
            string handoverName = input.HandoverName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(handoverAdmin) && string.IsNullOrWhiteSpace(handoverName))
            {
                return HardDiskMediaFlowResult.Fail("请填写办理交接人（资料管理员）。");
            }

            if (!input.HandoverDate.HasValue)
            {
                return HardDiskMediaFlowResult.Fail("请填写办理交接日期。");
            }

            if (IsReturnRegistrationType(existing.ApplicationType) &&
                existing.ApplicationType != HardDiskMediaApplication.TypeLossRegistration)
            {
                string requestedTargetLocation = !string.IsNullOrWhiteSpace(input.TargetLocation)
                    ? input.TargetLocation.Trim()
                    : existing.TargetLocation?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(requestedTargetLocation))
                {
                    return HardDiskMediaFlowResult.Fail("请先由资料室管理员指定归还位置后再确认实物交接。");
                }

                var returnCandidate = await GetActiveReturnCandidateAsync(
                    existing.MediumId,
                    existing.SourceApplicationId,
                    existing.SourceOutboundRecordId);
                if (returnCandidate == null)
                {
                    return HardDiskMediaFlowResult.Fail("未找到当前有效的借出记录，无法确定归还位置。");
                }

                try
                {
                    existing.TargetLocation = await ResolveReturnTargetLocationAsync(
                        existing.ApplicationType,
                        returnCandidate,
                        requestedTargetLocation);
                }
                catch (InvalidOperationException ex)
                {
                    return HardDiskMediaFlowResult.Fail(ex.Message);
                }
            }

            var now = DateTime.Now;
            existing.ApplicationStatus = HardDiskMediaApplication.StatusSignedUploaded;
            existing.ExecutedBy = !string.IsNullOrWhiteSpace(handoverAdmin)
                ? handoverAdmin
                : handoverName;
            existing.ExecutedTime = input.HandoverDate.Value;
            existing.UpdatedTime = now;

            await _hardDiskMediaRepository.SaveChangesAsync();
            return HardDiskMediaFlowResult.Ok("实物交接确认成功。请上传签批交接单。");
        }

        /// <inheritdoc/>
        public async Task<HardDiskMediaFlowResult> WithdrawApplicationAsync(HardDiskMediaApplication? application, User? currentUser, string? opinion)
        {
            if (application == null || application.Id == 0)
            {
                return HardDiskMediaFlowResult.Fail("当前申请单无效，无法撤回作废。");
            }

            if (currentUser == null)
            {
                return HardDiskMediaFlowResult.Fail("未识别当前用户，无法撤回作废。");
            }

            var existing = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
            if (existing == null)
            {
                return HardDiskMediaFlowResult.Fail("未找到当前申请单。");
            }

            if (existing.ApplicationStatus == HardDiskMediaApplication.StatusApproved ||
                existing.ApplicationStatus == HardDiskMediaApplication.StatusPendingProcess ||
                existing.ApplicationStatus == HardDiskMediaApplication.StatusCompleted ||
                existing.ApplicationStatus == HardDiskMediaApplication.StatusWithdrawn ||
                existing.ApplicationStatus == HardDiskMediaApplication.StatusForceWithdrawn)
            {
                return HardDiskMediaFlowResult.Fail("当前申请单已进入或完成审批信息阶段，不允许申请人撤回作废。");
            }

            if (IsOutboundLockableType(existing.ApplicationType))
            {
                var medium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(existing.MediumId);
                if (medium != null)
                {
                    UnlockOutboundMedium(existing.Id, medium);
                }
            }

            existing.ApplicationStatus = HardDiskMediaApplication.StatusWithdrawn;
            existing.ApprovedBy = currentUser.RealName?.Trim() ?? string.Empty;
            existing.ApprovedTime = DateTime.Now;
            existing.ApprovalOpinion = string.IsNullOrWhiteSpace(opinion) ? "申请人撤回作废" : opinion.Trim();
            existing.UpdatedTime = existing.ApprovedTime.Value;

            await _hardDiskMediaRepository.SaveChangesAsync();
            return HardDiskMediaFlowResult.Ok("申请单已撤回作废。");
        }

        /// <inheritdoc/>
        public async Task<HardDiskMediaFlowResult> ForceWithdrawApplicationAsync(HardDiskMediaApplication? application, User? currentUser, string? opinion)
        {
            if (application == null || application.Id == 0)
            {
                return HardDiskMediaFlowResult.Fail("当前申请单无效，无法强制撤回作废。");
            }

            if (!IsArchiveRoomMediaAdmin(currentUser))
            {
                return HardDiskMediaFlowResult.Fail("仅资料室资料管理员可执行强制撤回作废。");
            }

            var existing = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
            if (existing == null)
            {
                return HardDiskMediaFlowResult.Fail("未找到当前申请单。");
            }

            if (existing.ApplicationStatus == HardDiskMediaApplication.StatusCompleted ||
                existing.ApplicationStatus == HardDiskMediaApplication.StatusWithdrawn ||
                existing.ApplicationStatus == HardDiskMediaApplication.StatusForceWithdrawn)
            {
                return HardDiskMediaFlowResult.Fail("当前申请单状态不允许强制撤回作废。");
            }

            bool isOverdue = await IsEligibleForAdminForceVoidAsync(existing.ApplyTime);
            if (!isOverdue)
            {
                string settingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();
                return HardDiskMediaFlowResult.Fail(_businessLogicSettingsService.BuildNotEligibleMessage(settingCode));
            }

            if (existing.ApplicationStatus == HardDiskMediaApplication.StatusApproved ||
                existing.ApplicationStatus == HardDiskMediaApplication.StatusPendingProcess)
            {
                return HardDiskMediaFlowResult.Fail("当前申请单已录入审批信息或已上传附件，不允许强制撤回作废。");
            }

            if (IsOutboundLockableType(existing.ApplicationType))
            {
                var medium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(existing.MediumId);
                if (medium != null)
                {
                    UnlockOutboundMedium(existing.Id, medium);
                }
            }

            existing.ApplicationStatus = HardDiskMediaApplication.StatusForceWithdrawn;
            existing.ApprovedBy = currentUser?.RealName?.Trim() ?? string.Empty;
            existing.ApprovedTime = DateTime.Now;
            existing.ApprovalOpinion = string.IsNullOrWhiteSpace(opinion) ? "资料室资料管理员强制撤回作废" : opinion.Trim();
            existing.UpdatedTime = existing.ApprovedTime.Value;

            await _hardDiskMediaRepository.SaveChangesAsync();
            return HardDiskMediaFlowResult.Ok("申请单已强制作废。");
        }

        private async Task<bool> IsEligibleForAdminForceVoidAsync(DateTime applyTime)
        {
            string settingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();
            return _businessLogicSettingsService.IsEligibleForAdminForceVoid(applyTime, settingCode);
        }

        /// <inheritdoc/>
        public async Task<HardDiskMediaFlowResult> CompleteApplicationAsync(HardDiskMediaApplication? application, User? currentUser)
        {
            if (application == null || application.Id == 0)
            {
                return HardDiskMediaFlowResult.Fail("当前申请单无效，无法办结。");
            }

            if (!IsArchiveRoomMediaAdmin(currentUser))
            {
                return HardDiskMediaFlowResult.Fail("仅资料室资料管理员可执行办理完成。");
            }

            var existingApplication = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
            if (existingApplication == null)
            {
                return HardDiskMediaFlowResult.Fail("未找到当前申请单。");
            }

            if (existingApplication.ApplicationStatus != HardDiskMediaApplication.StatusSignedUploaded)
            {
                return HardDiskMediaFlowResult.Fail("请先完成实物交接并上传签批交接单后再确认办结。");
            }

            if (!existingApplication.SignedAttachmentUploaded)
            {
                return HardDiskMediaFlowResult.Fail("请先上传签批交接单后再办理。");
            }

            var medium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(existingApplication.MediumId);
            if (medium == null)
            {
                return HardDiskMediaFlowResult.Fail("未找到关联的硬盘介质。");
            }

            var ledger = EnsureLedger(medium, now: DateTime.Now);

            var returnCandidate = await GetActiveReturnCandidateAsync(
                existingApplication.MediumId,
                existingApplication.SourceApplicationId,
                existingApplication.SourceOutboundRecordId);
            if (IsReturnOrLossRegistrationType(existingApplication.ApplicationType) && returnCandidate == null)
            {
                return HardDiskMediaFlowResult.Fail("未找到当前有效的借出记录，无法完成登记。\n请检查介质状态与借出记录是否一致。");
            }

            if (returnCandidate != null)
            {
                try
                {
                    existingApplication.SourceApplicationId = returnCandidate.SourceApplicationId;
                    existingApplication.SourceOutboundRecordId = returnCandidate.SourceOutboundRecordId;
                    existingApplication.ApplicantName = returnCandidate.ApplicantName;
                    existingApplication.ApplicantDept = returnCandidate.ApplicantDept;
                    existingApplication.CurrentLocation = EmptyAsFallback(returnCandidate.BorrowedLocation, ledger.StorageLocation);
                    existingApplication.TargetPersonOrUnit = returnCandidate.ApplicantName;
                    existingApplication.ExpectedReturnDate = returnCandidate.ExpectedReturnDate;

                    if (existingApplication.ApplicationType == HardDiskMediaApplication.TypeLossRegistration)
                    {
                        existingApplication.TargetLocation = string.Empty;
                    }
                    else if (IsReturnRegistrationType(existingApplication.ApplicationType))
                    {
                        if (string.IsNullOrWhiteSpace(existingApplication.TargetLocation))
                        {
                            return HardDiskMediaFlowResult.Fail("请先由资料室管理员指定归还位置后再办结。");
                        }

                        existingApplication.TargetLocation = await ResolveReturnTargetLocationAsync(
                            existingApplication.ApplicationType,
                            returnCandidate,
                            existingApplication.TargetLocation);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    return HardDiskMediaFlowResult.Fail(ex.Message);
                }
            }

            string beforeStatus = ledger.MediaStatus;
            string beforeLocation = ledger.StorageLocation;
            DateTime now = DateTime.Now;

            // 办结保护校验：BeforeStatus 与台账当前状态（按业务预期）不一致时告警并阻止办结
            var expectedBeforeStatuses = ResolveExpectedBeforeStatuses(existingApplication.ApplicationType);
            if (expectedBeforeStatuses.Count > 0 &&
                !expectedBeforeStatuses.Contains(beforeStatus, StringComparer.Ordinal))
            {
                string expectedText = string.Join("、", expectedBeforeStatuses);
                return HardDiskMediaFlowResult.Fail(
                    $"告警：办结前状态校验失败。当前台账状态为“{beforeStatus}”，" +
                    $"但“{existingApplication.ApplicationType}”办结前预期状态应为“{expectedText}”。请先刷新并核对台账状态后重试。");
            }

            ApplyApplicationToMedium(existingApplication, medium, ledger, now);

            var transaction = new HardDiskMediaTransaction
            {
                MediumId = medium.Id,
                ApplicationId = existingApplication.Id,
                TransactionType = MapTransactionType(existingApplication.ApplicationType),
                BeforeStatus = beforeStatus,
                AfterStatus = ledger.MediaStatus,
                BeforeLocation = beforeLocation,
                AfterLocation = ledger.StorageLocation,
                OperatorName = currentUser?.RealName?.Trim() ?? string.Empty,
                OperateTime = now,
                RelatedPerson = existingApplication.TargetPersonOrUnit,
                TargetOrganization = existingApplication.TargetPersonOrUnit,
                NeedReturn = ledger.NeedReturn,
                ExpectedReturnDate = existingApplication.ExpectedReturnDate,
                ActualReturnDate = IsReturnRegistrationType(existingApplication.ApplicationType) ? now : null,
                RelatedBatch = existingApplication.RelatedBatch,
                RelatedArchiveTitle = existingApplication.RelatedArchiveTitle,
                Description = existingApplication.Reason,
                Remark = existingApplication.Remark
            };

            _hardDiskMediaRepository.AddTransaction(transaction);

            existingApplication.ApplicationStatus = HardDiskMediaApplication.StatusCompleted;
            existingApplication.ExecutedBy = string.IsNullOrWhiteSpace(existingApplication.ExecutedBy)
                ? currentUser?.RealName?.Trim() ?? string.Empty
                : existingApplication.ExecutedBy.Trim();
            existingApplication.ExecutedTime ??= now;
            existingApplication.UpdatedTime = now;

            if (IsOutboundLockableType(existingApplication.ApplicationType))
            {
                UnlockOutboundMedium(existingApplication.Id, medium);
            }

            await _hardDiskMediaRepository.SaveChangesAsync();
            return HardDiskMediaFlowResult.Ok("业务办理完成。");
        }

        /// <inheritdoc/>
        public async Task<HardDiskMediaPrintData> BuildPrintDataAsync(HardDiskMediaApplication? application)
        {
            if (application == null || application.Id == 0)
            {
                throw new System.InvalidOperationException("当前申请单无效，无法打印。");
            }

            var existingApplication = await _hardDiskMediaRepository.GetApplicationWithMediumLedgerByIdAsNoTrackingAsync(application.Id);
            if (existingApplication?.Medium == null)
            {
                throw new System.InvalidOperationException("未找到申请单或关联介质，无法打印。");
            }

            string sourceApplicationNo = string.Empty;
            if (existingApplication.SourceApplicationId.HasValue)
            {
                sourceApplicationNo = await _hardDiskMediaRepository.GetApplicationNoByIdAsync(existingApplication.SourceApplicationId.Value) ?? string.Empty;
            }
            else if (existingApplication.SourceOutboundRecordId.HasValue)
            {
                sourceApplicationNo = await _hardDiskMediaRepository.GetOutboundNoByRecordIdAsync(existingApplication.SourceOutboundRecordId.Value) ?? string.Empty;
            }

            return new HardDiskMediaPrintData
            {
                ApplicationNo = existingApplication.ApplicationNo,
                SourceApplicationNo = sourceApplicationNo,
                ApplicationType = existingApplication.ApplicationType,
                ApplicationStatus = existingApplication.StatusStr,
                IsCompleted = existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusCompleted,
                DiskCode = existingApplication.Medium.DiskCode,
                SerialNumber = existingApplication.Medium.SerialNumber,
                DiskType = existingApplication.Medium.DiskType,
                DeviceSummary = $"{existingApplication.Medium.Brand} / {existingApplication.Medium.Capacity} / {existingApplication.Medium.InterfaceType}",
                CurrentStatus = existingApplication.Medium.Ledger?.MediaStatus ?? string.Empty,
                MediaNature = existingApplication.Medium.Ledger?.MediaNature ?? string.Empty,
                RegistrationMethod = existingApplication.Medium.RegistrationMethod,
                ApplicantName = existingApplication.ApplicantName,
                ApplicantDept = existingApplication.ApplicantDept,
                ApplyDateText = existingApplication.ApplyTime.ToString("yyyy-MM-dd"),
                CurrentLocation = existingApplication.CurrentLocation,
                TargetLocation = ResolvePrintTargetLocation(existingApplication),
                TargetPersonOrUnit = existingApplication.TargetPersonOrUnit,
                ExpectedReturnDateText = HardDiskMediaOutboundReturnSupport.FormatExpectedReturnDateText(
                    existingApplication.ApplicationType,
                    existingApplication.ExpectedReturnDate),
                RelatedBatch = existingApplication.RelatedBatch,
                RelatedArchiveTitle = existingApplication.RelatedArchiveTitle,
                Reason = existingApplication.Reason,
                Remark = existingApplication.Remark,
                ReviewerName = existingApplication.ReviewerName,
                ReviewerDateText = existingApplication.ReviewerDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                ApproverName = existingApplication.ApprovedBy,
                ApproverDateText = existingApplication.ApprovedTime?.ToString("yyyy-MM-dd") ?? string.Empty,
                HandoverApplicant = existingApplication.ApplicantName,
                HandoverAdmin = existingApplication.ExecutedBy,
                HandoverDateText = existingApplication.ExecutedTime?.ToString("yyyy-MM-dd") ?? string.Empty,
                InspectionResultText = existingApplication.InspectionResult,
                FormatConfirmationText = ResolveFormatConfirmationText(existingApplication),
                ApprovalOpinion = existingApplication.ApprovalOpinion,
                ApprovalSignatureText = BuildApprovalSignatureText(existingApplication.ApprovedBy, existingApplication.ApprovedTime),
                PrintCount = existingApplication.PrintCount
            };
        }

        private static string BuildApprovalSignatureText(string? approverName, DateTime? approvedTime)
        {
            string normalizedName = approverName?.Trim() ?? string.Empty;
            string dateText = approvedTime?.ToString("yyyy-MM-dd") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedName) && string.IsNullOrWhiteSpace(dateText))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return $"签字：    日期:{dateText}";
            }

            if (string.IsNullOrWhiteSpace(dateText))
            {
                return $"签字：{normalizedName}";
            }

            return $"签字：{normalizedName}    日期:{dateText}";
        }

        private static string ResolveFormatConfirmationText(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);

            string persistedValue = application.FormatConfirmation?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(persistedValue))
            {
                return persistedValue;
            }

            return application.ApplicationType switch
            {
                HardDiskMediaApplication.TypeLossRegistration => "格式化确认：□已格式化  ■不适用",
                HardDiskMediaApplication.TypeReturnDamagedRegistration => "格式化确认：□已格式化  ■不适用",
                HardDiskMediaApplication.TypeReturnDataRegistration => "格式化确认：□已格式化  ■不适用",
                _ => "格式化确认：■已格式化  □不适用"
            };
        }

        private static string ResolvePrintTargetLocation(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);

            if (!string.IsNullOrWhiteSpace(application.TargetLocation))
            {
                return application.TargetLocation.Trim();
            }

            if (IsReturnRegistrationType(application.ApplicationType))
            {
                return "待资料室指定档口";
            }

            return string.Empty;
        }

        private async Task<HardDiskMediaReturnCandidate?> GetActiveReturnCandidateAsync(
            int mediumId,
            int? sourceApplicationId,
            int? sourceOutboundRecordId = null)
        {
            var candidates = await GetReturnRegistrationCandidatesAsync();
            var exactCandidate = candidates.FirstOrDefault(item =>
                HardDiskMediaReturnCandidateSupport.MatchesCandidateSource(item, sourceApplicationId, sourceOutboundRecordId));
            if (exactCandidate != null)
            {
                return exactCandidate;
            }

            var mediumCandidate = candidates.FirstOrDefault(item => item.MediumId == mediumId);
            if (mediumCandidate != null)
            {
                return mediumCandidate;
            }

            return await ResolveReturnCandidateFromSourceAsync(mediumId, sourceApplicationId, sourceOutboundRecordId);
        }

        /// <summary>
        /// 按登记单已保存的借出来源回溯候选项，用于归还登记已提交但尚未办结时的办结/保存校验。
        /// </summary>
        private async Task<HardDiskMediaReturnCandidate?> ResolveReturnCandidateFromSourceAsync(
            int mediumId,
            int? sourceApplicationId,
            int? sourceOutboundRecordId)
        {
            if (sourceApplicationId is > 0)
            {
                var sourceApplication = await _hardDiskMediaRepository.GetApplicationWithMediumLedgerByIdAsNoTrackingAsync(sourceApplicationId.Value);
                if (sourceApplication?.Medium != null
                    && sourceApplication.MediumId == mediumId
                    && sourceApplication.ApplicationStatus == HardDiskMediaApplication.StatusCompleted
                    && (sourceApplication.ApplicationType == HardDiskMediaApplication.TypeOutboundTemporary
                        || sourceApplication.ApplicationType == HardDiskMediaApplication.TypeOutboundLongTerm)
                    && sourceApplication.Medium.Ledger != null
                    && (sourceApplication.Medium.Ledger.MediaStatus == HardDiskMedium.StatusOutTemporary
                        || sourceApplication.Medium.Ledger.MediaStatus == HardDiskMedium.StatusOutLongTerm))
                {
                    return CreateReturnRegistrationCandidateFromOutboundApplication(sourceApplication);
                }
            }

            if (sourceOutboundRecordId is > 0)
            {
                var archiveOutboundSource = (await _hardDiskMediaRepository.GetArchiveOutboundRequisitionReturnSourcesAsync())
                    .FirstOrDefault(item => item.MediumId == mediumId && item.OutboundRecordId == sourceOutboundRecordId.Value);
                if (archiveOutboundSource != null)
                {
                    return CreateReturnRegistrationCandidateFromArchiveOutbound(archiveOutboundSource);
                }
            }

            return null;
        }

        private async Task<string> ResolveReturnTargetLocationAsync(string applicationType, HardDiskMediaReturnCandidate candidate, string? requestedTargetLocation)
        {
            if (applicationType == HardDiskMediaApplication.TypeReturnBlankRegistration)
            {
                string? recommended = await RecommendBlankDedicatedSlotLocationAsync();
                if (string.IsNullOrWhiteSpace(recommended))
                {
                    throw new InvalidOperationException("请先在磁盘柜开柜界面设置“空白硬盘专用档口”。");
                }

                string normalizedRequestedLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(requestedTargetLocation);
                if (!string.IsNullOrWhiteSpace(normalizedRequestedLocation))
                {
                    var blankOptions = await GetBlankDedicatedReturnTargetLocationOptionsAsync();
                    if (blankOptions.Any(item => string.Equals(item.Location, normalizedRequestedLocation, StringComparison.OrdinalIgnoreCase)))
                    {
                        return normalizedRequestedLocation;
                    }
                }

                return recommended;
            }

            var options = await GetReturnTargetLocationOptionsAsync(
                applicationType,
                candidate.MediumId,
                candidate.SourceApplicationId,
                candidate.SourceOutboundRecordId);
            if (options.Count == 0)
            {
                if (applicationType == HardDiskMediaApplication.TypeReturnDataRegistration)
                {
                    throw new InvalidOperationException("请先在磁盘柜开柜界面设置“年度数据硬盘专用档口”。");
                }

                if (applicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration)
                {
                    throw new InvalidOperationException("请先在磁盘柜开柜界面设置“损坏硬盘专用档口”。");
                }

                return EmptyAsFallback(candidate.OriginalLocation, candidate.BorrowedLocation);
            }

            string trimmedRequestedLocation = requestedTargetLocation?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(trimmedRequestedLocation))
            {
                var matchedOption = options.FirstOrDefault(item => string.Equals(item.Location, trimmedRequestedLocation, StringComparison.OrdinalIgnoreCase));
                if (matchedOption != null)
                {
                    return matchedOption.Location;
                }
            }

            return options[0].Location;
        }

        private async Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetDedicatedReturnTargetLocationOptionsAsync(string categoryName)
        {
            var dedicatedSlots = await _hardDiskMediaRepository.GetDedicatedMagneticSlotsByCategoryAsync(categoryName);

            var locations = dedicatedSlots
                .Where(item => item.Cabinet != null)
                .Select(item => $"{item.Cabinet!.Name}{item.FaceCode}-{item.SlotCode}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (locations.Count == 0)
            {
                return Array.Empty<HardDiskMediaReturnTargetLocationOption>();
            }

            var results = new List<HardDiskMediaReturnTargetLocationOption>(locations.Count);
            foreach (string location in locations)
            {
                string slotCode = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(location);
                var occupiedIndexes = await GetOccupiedDedicatedSlotSequenceIndexesAsync(slotCode);
                results.Add(new HardDiskMediaReturnTargetLocationOption
                {
                    Location = location,
                    ExistingMediumCount = occupiedIndexes.Count
                });
            }

            return results;
        }

        private async Task<int> GetCurrentInStockMediumCountAsync(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return 0;
            }

            string trimmedLocation = location.Trim();
            return await _hardDiskMediaRepository.GetCurrentInStockMediumCountAsync(trimmedLocation);
        }

        private static string EmptyAsFallback(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback.Trim() : value.Trim();
        }

        /// <inheritdoc/>
        public async Task MarkApplicationPrintedAsync(HardDiskMediaApplication? application)
        {
            if (application == null || application.Id == 0)
            {
                throw new System.InvalidOperationException("当前申请单无效，无法记录打印信息。");
            }

            var existingApplication = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
            if (existingApplication == null)
            {
                throw new System.InvalidOperationException("未找到申请单，无法记录打印信息。");
            }

            if (existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusWithdrawn ||
                existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusForceWithdrawn ||
                existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusCancelled)
            {
                throw new System.InvalidOperationException("当前申请单已作废，不允许记录打印信息。");
            }

            existingApplication.PrintCount += 1;
            existingApplication.PrintedTime = DateTime.Now;
            existingApplication.UpdatedTime = existingApplication.PrintedTime.Value;

            await _hardDiskMediaRepository.SaveChangesAsync();
        }
    }
}
