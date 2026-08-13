using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.HardDiskMedia;

public partial class HardDiskMediaService
{
    /// <inheritdoc/>
    public async Task CompleteBlankReturnFromNetworkInboundAsync(
        HardDiskMediaReturnCandidate candidate,
        string targetBlankSlotLocation,
        string inboundNo,
        string projectName,
        User currentUser,
        DateTime completedAt)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(currentUser);

        if (!IsArchiveRoomMediaAdmin(currentUser))
        {
            throw new InvalidOperationException("仅资料室资料管理员可执行办理完成。");
        }

        string normalizedInboundNo = inboundNo?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedInboundNo))
        {
            throw new ArgumentException("入网单编号不能为空。", nameof(inboundNo));
        }

        if (await _archiveFilingRepository.HasCompletedReturnApplicationAsync(candidate.MediumId, candidate.SourceApplicationId))
        {
            return;
        }

        HardDiskMediaReturnCandidate? returnCandidate = await GetActiveReturnCandidateAsync(
            candidate.MediumId,
            candidate.SourceApplicationId,
            candidate.SourceOutboundRecordId);
        if (returnCandidate == null)
        {
            throw new InvalidOperationException(
                $"未找到借出硬盘 [{candidate.DiskCode}] 当前有效的借出记录，无法办理空盘归还登记。");
        }

        HardDiskMedium? medium = await _hardDiskMediaRepository.GetActiveMediumWithLedgerByIdForUpdateAsync(candidate.MediumId);
        if (medium == null)
        {
            throw new InvalidOperationException($"未找到借出硬盘 [{candidate.DiskCode}] 关联介质。");
        }

        HardDiskLedger ledger = EnsureLedger(medium, completedAt);
        string beforeStatus = ledger.MediaStatus;
        if (!IsOutTemporaryOrLongTerm(beforeStatus))
        {
            throw new InvalidOperationException(
                $"借出硬盘 [{candidate.DiskCode}] 当前状态为“{beforeStatus}”，无法办理空盘归还登记。");
        }

        string beforeLocation = ledger.StorageLocation;
        string operatorName = currentUser.RealName?.Trim() ?? currentUser.LoginName?.Trim() ?? string.Empty;
        string targetLocation = await ResolveReturnTargetLocationAsync(
            HardDiskMediaApplication.TypeReturnBlankRegistration,
            returnCandidate,
            targetBlankSlotLocation);

        string trimmedProjectName = projectName?.Trim() ?? string.Empty;
        var returnApplication = new HardDiskMediaApplication
        {
            ApplicationNo = await GenerateNextReturnRegistrationNoAsync(),
            MediumId = medium.Id,
            SourceApplicationId = returnCandidate.SourceApplicationId,
            SourceOutboundRecordId = returnCandidate.SourceOutboundRecordId,
            ApplicationType = HardDiskMediaApplication.TypeReturnBlankRegistration,
            ApplicationStatus = HardDiskMediaApplication.StatusCompleted,
            ApplicantName = returnCandidate.ApplicantName.Trim(),
            ApplicantDept = returnCandidate.ApplicantDept.Trim(),
            ApplyTime = completedAt,
            Reason = $"资料入网单 [{normalizedInboundNo}] 办结后，借出硬盘 [{returnCandidate.DiskCode}] 随入网资料归还，办理空盘归还登记。",
            TargetPersonOrUnit = "资料室",
            CurrentLocation = EmptyAsFallback(returnCandidate.BorrowedLocation, returnCandidate.OriginalLocation),
            TargetLocation = targetLocation,
            ExpectedReturnDate = returnCandidate.ExpectedReturnDate,
            RelatedBatch = normalizedInboundNo,
            RelatedArchiveTitle = trimmedProjectName,
            FormatConfirmation = "已格式化",
            InspectionResult = HardDiskMediaReturnDomainValues.RegistrationKindNormalReturn,
            SignedAttachmentUploaded = true,
            SignedAttachmentUploadedTime = completedAt,
            SignedAttachmentUploader = operatorName,
            ReviewerName = operatorName,
            ReviewerDate = completedAt.Date,
            ApprovedBy = operatorName,
            ApprovedTime = completedAt,
            ApprovalOpinion = "资料入网办结后代办空盘归还",
            ExecutedBy = operatorName,
            ExecutedTime = completedAt,
            Remark = $"由资料入网单 [{normalizedInboundNo}] 自动办结空盘归还。",
            CreatedTime = completedAt,
            UpdatedTime = completedAt
        };

        _hardDiskMediaRepository.AddApplication(returnApplication);
        ApplyApplicationToMedium(returnApplication, medium, ledger, completedAt);

        if (returnCandidate.SourceApplicationId is > 0)
        {
            UnlockOutboundMedium(returnCandidate.SourceApplicationId.Value, medium);
        }
        else
        {
            medium.RegisterLock = null;
        }

        await _hardDiskMediaRepository.SaveChangesAsync();

        _hardDiskMediaRepository.AddTransaction(new HardDiskMediaTransaction
        {
            MediumId = medium.Id,
            ApplicationId = returnApplication.Id,
            TransactionType = HardDiskMediaTransaction.TypeReturnRegistration,
            BeforeStatus = beforeStatus,
            AfterStatus = ledger.MediaStatus,
            BeforeLocation = beforeLocation,
            AfterLocation = ledger.StorageLocation,
            OperatorName = operatorName,
            OperateTime = completedAt,
            RelatedPerson = returnCandidate.ApplicantName.Trim(),
            TargetOrganization = "资料室",
            NeedReturn = false,
            ExpectedReturnDate = returnCandidate.ExpectedReturnDate,
            ActualReturnDate = completedAt,
            RelatedBatch = normalizedInboundNo,
            RelatedArchiveTitle = trimmedProjectName,
            Description = returnApplication.Reason,
            Remark = returnApplication.Remark
        });

        await _hardDiskMediaRepository.SaveChangesAsync();
    }
}
