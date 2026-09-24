using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.Shared;

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
                return HardDiskMediaFlowResult.Fail("仅资料管理员可执行审批通过。");
            }

            var existing = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
            if (existing == null)
            {
                return HardDiskMediaFlowResult.Fail("未找到当前申请单。");
            }

            var gate = OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    existing.ApplicationStatus,
                    existing.SignedAttachmentUploaded,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
                OfflineApprovalLifecycleSupport.Action.ApprovePass);
            if (!gate.Allowed)
            {
                return HardDiskMediaFlowResult.Fail(gate.DenyMessage ?? "当前状态不允许审批通过。");
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
                    return HardDiskMediaFlowResult.Fail("请先由资料管理员指定归还位置后再审批通过。");
                }

                var returnCandidate = await GetActiveReturnCandidateAsync(
                    existing.MediumId,
                    existing.SourceApplicationId,
                    existing.SourceOutboundRecordId,
                    existing.SourceNetworkOutboundRecordId);
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

            existing.ApplicationStatus = gate.NextStatus ?? HardDiskMediaApplication.StatusApproved;
            existing.DeptHead = input.DeptHead?.Trim() ?? string.Empty;
            existing.DeptHeadDate = string.IsNullOrWhiteSpace(existing.DeptHead)
                ? null
                : input.DeptHeadDate ?? now;
            existing.ArchiveRoomHead = input.ArchiveRoomHead?.Trim() ?? string.Empty;
            existing.ArchiveRoomHeadDate = string.IsNullOrWhiteSpace(existing.ArchiveRoomHead)
                ? null
                : input.ArchiveRoomHeadDate ?? now;
            existing.ProductionHead = input.ProductionHead?.Trim() ?? string.Empty;
            existing.ProductionHeadDate = string.IsNullOrWhiteSpace(existing.ProductionHead)
                ? null
                : input.ProductionHeadDate ?? now;
            existing.ArchiveDeputyPresident = input.ArchiveDeputyPresident?.Trim() ?? string.Empty;
            existing.ArchiveDeputyPresidentDate = string.IsNullOrWhiteSpace(existing.ArchiveDeputyPresident)
                ? null
                : input.ArchiveDeputyPresidentDate ?? now;
            existing.ProductionVicePresident = input.ProductionVicePresident?.Trim() ?? string.Empty;
            existing.ProductionVicePresidentDate = string.IsNullOrWhiteSpace(existing.ProductionVicePresident)
                ? null
                : input.ProductionVicePresidentDate ?? now;
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
                return HardDiskMediaFlowResult.Fail("仅资料管理员可确认实物交接。");
            }

            var existing = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
            if (existing == null)
            {
                return HardDiskMediaFlowResult.Fail("未找到当前申请单。");
            }

            var gate = OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    existing.ApplicationStatus,
                    existing.SignedAttachmentUploaded,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
                OfflineApprovalLifecycleSupport.Action.ConfirmMidStep);
            if (!gate.Allowed)
            {
                return HardDiskMediaFlowResult.Fail(gate.DenyMessage ?? "当前状态不允许确认实物交接。");
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
                    return HardDiskMediaFlowResult.Fail("请先由资料管理员指定归还位置后再确认实物交接。");
                }

                var returnCandidate = await GetActiveReturnCandidateAsync(
                    existing.MediumId,
                    existing.SourceApplicationId,
                    existing.SourceOutboundRecordId,
                    existing.SourceNetworkOutboundRecordId);
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
            existing.ApplicationStatus = gate.NextStatus ?? HardDiskMediaApplication.StatusSignedUploaded;
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

            var gate = OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    existing.ApplicationStatus,
                    existing.SignedAttachmentUploaded,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.Applicant),
                OfflineApprovalLifecycleSupport.Action.Withdraw);
            if (!gate.Allowed)
            {
                return HardDiskMediaFlowResult.Fail(gate.DenyMessage ?? "当前申请单不允许撤回作废。");
            }

            if (IsOutboundLockableType(existing.ApplicationType))
            {
                var medium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(existing.MediumId);
                if (medium != null)
                {
                    UnlockOutboundMedium(existing.Id, medium);
                }
            }

            existing.ApplicationStatus = gate.NextStatus ?? HardDiskMediaApplication.StatusWithdrawn;
            existing.ArchiveRoomHead = currentUser.RealName?.Trim() ?? string.Empty;
            existing.ArchiveRoomHeadDate = DateTime.Now;
            existing.ApprovalOpinion = string.IsNullOrWhiteSpace(opinion) ? "申请人撤回作废" : opinion.Trim();
            existing.UpdatedTime = existing.ArchiveRoomHeadDate.Value;

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
                return HardDiskMediaFlowResult.Fail("仅资料管理员可执行强制撤回作废。");
            }

            var existing = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
            if (existing == null)
            {
                return HardDiskMediaFlowResult.Fail("未找到当前申请单。");
            }

            bool isOverdue = await IsEligibleForAdminForceVoidAsync(existing.ApplyTime);
            var gate = OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    existing.ApplicationStatus,
                    existing.SignedAttachmentUploaded,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin,
                    forceVoidEligible: isOverdue),
                OfflineApprovalLifecycleSupport.Action.ForceVoid);
            if (!gate.Allowed)
            {
                if (!isOverdue
                    && existing.ApplicationStatus is not (
                        HardDiskMediaApplication.StatusCompleted
                        or HardDiskMediaApplication.StatusWithdrawn
                        or HardDiskMediaApplication.StatusForceWithdrawn
                        or HardDiskMediaApplication.StatusApproved
                        or HardDiskMediaApplication.StatusSignedUploaded))
                {
                    string settingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();
                    return HardDiskMediaFlowResult.Fail(_businessLogicSettingsService.BuildNotEligibleMessage(settingCode));
                }

                return HardDiskMediaFlowResult.Fail(gate.DenyMessage ?? "当前申请单不允许强制撤回作废。");
            }

            if (IsOutboundLockableType(existing.ApplicationType))
            {
                var medium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(existing.MediumId);
                if (medium != null)
                {
                    UnlockOutboundMedium(existing.Id, medium);
                }
            }

            existing.ApplicationStatus = gate.NextStatus ?? HardDiskMediaApplication.StatusForceWithdrawn;
            existing.ArchiveRoomHead = currentUser?.RealName?.Trim() ?? string.Empty;
            existing.ArchiveRoomHeadDate = DateTime.Now;
            existing.ApprovalOpinion = string.IsNullOrWhiteSpace(opinion) ? "资料管理员强制撤回作废" : opinion.Trim();
            existing.UpdatedTime = existing.ArchiveRoomHeadDate.Value;

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
                return HardDiskMediaFlowResult.Fail("仅资料管理员可执行办理完成。");
            }

            var existingApplication = await _hardDiskMediaRepository.GetApplicationByIdAsync(application.Id);
            if (existingApplication == null)
            {
                return HardDiskMediaFlowResult.Fail("未找到当前申请单。");
            }

            var gate = OfflineApprovalLifecycleSupport.TryTransition(
                new OfflineApprovalLifecycleSupport.GateContext(
                    existingApplication.ApplicationStatus,
                    existingApplication.SignedAttachmentUploaded,
                    OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                    OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
                OfflineApprovalLifecycleSupport.Action.Complete);
            if (!gate.Allowed)
            {
                return HardDiskMediaFlowResult.Fail(gate.DenyMessage ?? "当前状态不允许办结。");
            }

            if (!IsReturnOrLossRegistrationType(existingApplication.ApplicationType))
            {
                var attachments = await _hardDiskMediaRepository.GetApplicationAttachmentsAsync(
                    ApplicationAttachmentBusinessType,
                    existingApplication.ApplicationNo);

                if (HardDiskOutboundDomainValues.RequiresPhysicalPhotoAttachment(existingApplication.ApplicationType))
                {
                    bool hasPhysicalPhoto = attachments.Any(item =>
                        string.Equals(
                            item.FileCategory?.Trim(),
                            HardDiskOutboundDomainValues.AttachmentCategoryPhysicalPhoto,
                            StringComparison.Ordinal));
                    if (!hasPhysicalPhoto)
                    {
                        return HardDiskMediaFlowResult.Fail("请先上传实物照片后再办理。");
                    }
                }

                if (HardDiskOutboundDomainValues.RequiresProofMaterialAttachment(existingApplication.ProofMaterialNote))
                {
                    bool hasProofMaterial = attachments.Any(item =>
                        string.Equals(
                            item.FileCategory?.Trim(),
                            HardDiskOutboundDomainValues.AttachmentCategoryProofMaterial,
                            StringComparison.Ordinal));
                    if (!hasProofMaterial)
                    {
                        return HardDiskMediaFlowResult.Fail("申请已声明附有证明材料，请先上传证明材料后再办理。");
                    }
                }
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
                existingApplication.SourceOutboundRecordId,
                existingApplication.SourceNetworkOutboundRecordId);
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
                    existingApplication.SourceNetworkOutboundRecordId = returnCandidate.SourceNetworkOutboundRecordId;
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
                            return HardDiskMediaFlowResult.Fail("请先由资料管理员指定归还位置后再办结。");
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

            existingApplication.ApplicationStatus = gate.NextStatus ?? HardDiskMediaApplication.StatusCompleted;
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
            else if (existingApplication.SourceNetworkOutboundRecordId.HasValue)
            {
                sourceApplicationNo = await _hardDiskMediaRepository.GetNetworkOutboundNoByRecordIdAsync(existingApplication.SourceNetworkOutboundRecordId.Value)
                    ?? string.Empty;
            }

            var chain = await _approvalWorkflowService.ResolveAsync(
                new ApprovalChainResolveRequest
                {
                    BusinessType = ApprovalChainApplySupport.ResolveHardDiskApplicationBusinessType(existingApplication),
                    ApplicantDept = existingApplication.ApplicantDept,
                    FieldValues = ApprovalChainApplySupport.BuildHardDiskApplicationFieldValues(existingApplication)
                },
                _userService.GetAllUsers());

            bool blankApprovalSignatures = existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusDraft
                || existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusSubmitted;
            bool isCompleted = existingApplication.ApplicationStatus == HardDiskMediaApplication.StatusCompleted;

            return new HardDiskMediaPrintData
            {
                ApplicationNo = existingApplication.ApplicationNo,
                SourceApplicationNo = sourceApplicationNo,
                ApplicationType = existingApplication.ApplicationType,
                ApplicationStatus = existingApplication.StatusStr,
                IsCompleted = isCompleted,
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
                EnableDeptHead = chain.DeptHead.IsEnabled,
                EnableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled,
                EnableProductionHead = chain.ProductionHead.IsEnabled,
                EnableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled,
                EnableProductionVicePresident = chain.ProductionVicePresident.IsEnabled,
                DeptHead = blankApprovalSignatures ? string.Empty : existingApplication.DeptHead,
                DeptHeadDateText = blankApprovalSignatures
                    ? string.Empty
                    : existingApplication.DeptHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                ArchiveRoomHead = blankApprovalSignatures ? string.Empty : existingApplication.ArchiveRoomHead,
                ArchiveRoomHeadDateText = blankApprovalSignatures
                    ? string.Empty
                    : existingApplication.ArchiveRoomHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                ProductionHead = blankApprovalSignatures ? string.Empty : existingApplication.ProductionHead,
                ProductionHeadDateText = blankApprovalSignatures
                    ? string.Empty
                    : existingApplication.ProductionHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                ArchiveDeputyPresident = blankApprovalSignatures
                    ? string.Empty
                    : existingApplication.ArchiveDeputyPresident,
                ArchiveDeputyPresidentDateText = blankApprovalSignatures
                    ? string.Empty
                    : existingApplication.ArchiveDeputyPresidentDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                ProductionVicePresident = blankApprovalSignatures
                    ? string.Empty
                    : existingApplication.ProductionVicePresident,
                ProductionVicePresidentDateText = blankApprovalSignatures
                    ? string.Empty
                    : existingApplication.ProductionVicePresidentDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                HandoverApplicant = existingApplication.ApplicantName,
                HandoverAdmin = existingApplication.ExecutedBy,
                HandoverDateText = existingApplication.ExecutedTime?.ToString("yyyy-MM-dd") ?? string.Empty,
                InspectionResultText = existingApplication.InspectionResult,
                FormatConfirmationText = ResolveFormatConfirmationText(existingApplication),
                ApprovalOpinion = existingApplication.ApprovalOpinion,
                ApprovalSignatureText = BuildApprovalSignatureText(existingApplication.ArchiveRoomHead, existingApplication.ArchiveRoomHeadDate),
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
            int? sourceOutboundRecordId = null,
            int? sourceNetworkOutboundRecordId = null)
        {
            var candidates = await GetReturnRegistrationCandidatesAsync();
            var exactCandidate = candidates.FirstOrDefault(item =>
                HardDiskMediaReturnCandidateSupport.MatchesCandidateSource(
                    item,
                    sourceApplicationId,
                    sourceOutboundRecordId,
                    sourceNetworkOutboundRecordId));
            if (exactCandidate != null)
            {
                return exactCandidate;
            }

            var mediumCandidate = candidates.FirstOrDefault(item => item.MediumId == mediumId);
            if (mediumCandidate != null)
            {
                return mediumCandidate;
            }

            return await ResolveReturnCandidateFromSourceAsync(
                mediumId,
                sourceApplicationId,
                sourceOutboundRecordId,
                sourceNetworkOutboundRecordId);
        }

        /// <summary>
        /// 按登记单已保存的借出来源回溯候选项，用于归还登记已提交但尚未办结时的办结/保存校验。
        /// </summary>
        private async Task<HardDiskMediaReturnCandidate?> ResolveReturnCandidateFromSourceAsync(
            int mediumId,
            int? sourceApplicationId,
            int? sourceOutboundRecordId,
            int? sourceNetworkOutboundRecordId = null)
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

            if (sourceNetworkOutboundRecordId is > 0)
            {
                var networkOutboundSource = (await _hardDiskMediaRepository.GetNetworkOutboundRequisitionReturnSourcesAsync())
                    .FirstOrDefault(item => item.MediumId == mediumId && item.OutboundRecordId == sourceNetworkOutboundRecordId.Value);
                if (networkOutboundSource != null)
                {
                    return CreateReturnRegistrationCandidateFromNetworkOutbound(networkOutboundSource);
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
                candidate.SourceOutboundRecordId,
                candidate.SourceNetworkOutboundRecordId);
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

            var orderedSlots = dedicatedSlots
                .Where(item => item.Cabinet != null)
                .OrderBy(item => item, Comparer<CabinetHardDiskSlotCategoryAssignment>.Create(HardDiskBlankSlotLocationSupport.CompareDedicatedSlots))
                .ToList();

            if (orderedSlots.Count == 0)
            {
                return Array.Empty<HardDiskMediaReturnTargetLocationOption>();
            }

            var results = new List<HardDiskMediaReturnTargetLocationOption>(orderedSlots.Count);
            var seenLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dedicatedSlot in orderedSlots)
            {
                string location = HardDiskBlankSlotLocationSupport.BuildLocationCode(
                    dedicatedSlot.Cabinet!.Name,
                    dedicatedSlot.FaceCode,
                    dedicatedSlot.SlotCode);
                if (!seenLocations.Add(location))
                {
                    continue;
                }

                string slotCode = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(location);
                var occupiedIndexes = await GetOccupiedDedicatedSlotSequenceIndexesAsync(slotCode);
                results.Add(new HardDiskMediaReturnTargetLocationOption
                {
                    Location = location,
                    ExistingMediumCount = occupiedIndexes.Count,
                    SlotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                        categoryName,
                        dedicatedSlot.Cabinet)
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
            var now = DateTime.Now;
            if (!existingApplication.FirstPrintedAt.HasValue)
                existingApplication.FirstPrintedAt = now;
            existingApplication.PrintedTime = now;
            existingApplication.UpdatedTime = existingApplication.PrintedTime.Value;

            await _hardDiskMediaRepository.SaveChangesAsync();
        }
    }
}
