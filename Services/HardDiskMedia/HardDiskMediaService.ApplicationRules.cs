using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 硬盘申请流转的状态映射、锁定规则与校验逻辑。
    /// </summary>
    public partial class HardDiskMediaService
    {
        private static string MapTransactionType(string applicationType)
        {
            return applicationType switch
            {
                HardDiskMediaApplication.TypeOutboundTemporary => HardDiskMediaTransaction.TypeOutboundTemporary,
                HardDiskMediaApplication.TypeOutboundLongTerm => HardDiskMediaTransaction.TypeOutboundLongTerm,
                HardDiskMediaApplication.TypeOutboundPermanent => HardDiskMediaTransaction.TypeOutboundPermanent,
                HardDiskMediaApplication.TypeReturnBlankRegistration => HardDiskMediaTransaction.TypeReturnRegistration,
                HardDiskMediaApplication.TypeReturnDataRegistration => HardDiskMediaTransaction.TypeReturnRegistration,
                HardDiskMediaApplication.TypeReturnDamagedRegistration => HardDiskMediaTransaction.TypeReturnRegistration,
                HardDiskMediaApplication.TypeLossRegistration => HardDiskMediaTransaction.TypeLossRegistration,
                HardDiskMediaApplication.TypeRelocate => HardDiskMediaTransaction.TypeRelocate,
                _ => HardDiskMediaTransaction.TypeRegister
            };
        }

        private static void ApplyApplicationToMedium(HardDiskMediaApplication application, HardDiskMedium medium, HardDiskLedger ledger, DateTime now)
        {
            medium.UpdatedTime = now;
            ledger.UpdatedTime = now;
            ledger.RegisterPerson = medium.RegisterPerson;
            ledger.RegisterDate = medium.RegisterDate;
            ledger.Remark = medium.Remark;
            ledger.DiskCode = medium.DiskCode;

            switch (application.ApplicationType)
            {
                case HardDiskMediaApplication.TypeOutboundTemporary:
                    ledger.MediaStatus = HardDiskMedium.StatusOutTemporary;
                    ledger.HolderOrOrganization = string.IsNullOrWhiteSpace(application.TargetPersonOrUnit) ? ledger.HolderOrOrganization : application.TargetPersonOrUnit.Trim();
                    ledger.StorageLocation = string.IsNullOrWhiteSpace(application.TargetLocation) ? ledger.StorageLocation : application.TargetLocation.Trim();
                    ledger.NeedReturn = true;
                    break;

                case HardDiskMediaApplication.TypeOutboundLongTerm:
                    ledger.MediaStatus = HardDiskMedium.StatusOutLongTerm;
                    ledger.HolderOrOrganization = string.IsNullOrWhiteSpace(application.TargetPersonOrUnit) ? ledger.HolderOrOrganization : application.TargetPersonOrUnit.Trim();
                    ledger.StorageLocation = string.IsNullOrWhiteSpace(application.TargetLocation) ? ledger.StorageLocation : application.TargetLocation.Trim();
                    ledger.NeedReturn = true;
                    break;

                case HardDiskMediaApplication.TypeOutboundPermanent:
                    ledger.MediaStatus = HardDiskMedium.StatusOutPermanent;
                    ledger.HolderOrOrganization = string.IsNullOrWhiteSpace(application.TargetPersonOrUnit) ? ledger.HolderOrOrganization : application.TargetPersonOrUnit.Trim();
                    ledger.StorageLocation = string.IsNullOrWhiteSpace(application.TargetLocation) ? ledger.StorageLocation : application.TargetLocation.Trim();
                    ledger.NeedReturn = false;
                    break;

                case HardDiskMediaApplication.TypeReturnBlankRegistration:
                    ledger.MediaNature = HardDiskMedium.NatureBlank;
                    ledger.MediaStatus = HardDiskMedium.StatusInStockBlank;
                    ledger.HolderOrOrganization = "资料室";
                    ledger.StorageLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(
                        string.IsNullOrWhiteSpace(application.TargetLocation)
                            ? application.CurrentLocation
                            : application.TargetLocation);
                    ledger.NeedReturn = false;
                    break;

                case HardDiskMediaApplication.TypeReturnDataRegistration:
                    ledger.MediaNature = HardDiskMedium.NatureDataCarrier;
                    ledger.MediaStatus = HardDiskMedium.StatusInStockData;
                    ledger.HolderOrOrganization = "资料室";
                    ledger.StorageLocation = string.IsNullOrWhiteSpace(application.TargetLocation) ? application.CurrentLocation.Trim() : application.TargetLocation.Trim();
                    ledger.NeedReturn = false;
                    break;

                case HardDiskMediaApplication.TypeReturnDamagedRegistration:
                    ledger.MediaStatus = HardDiskMedium.StatusInStockDamaged;
                    ledger.HolderOrOrganization = "资料室";
                    ledger.StorageLocation = string.IsNullOrWhiteSpace(application.TargetLocation) ? application.CurrentLocation.Trim() : application.TargetLocation.Trim();
                    ledger.NeedReturn = false;
                    break;

                case HardDiskMediaApplication.TypeLossRegistration:
                    ledger.MediaStatus = HardDiskMedium.StatusOutLost;
                    ledger.NeedReturn = false;
                    break;

                case HardDiskMediaApplication.TypeRelocate:
                    ledger.StorageLocation = string.IsNullOrWhiteSpace(application.TargetLocation) ? ledger.StorageLocation : application.TargetLocation.Trim();
                    break;
            }
        }

        private static bool IsInStockStatus(string status)
        {
            return status == HardDiskMedium.StatusInStockBlank ||
                   status == HardDiskMedium.StatusInStockData ||
                   status == HardDiskMedium.StatusInStockDamaged;
        }

        private static bool IsOutTemporaryOrLongTerm(string status)
        {
            return status == HardDiskMedium.StatusOutTemporary ||
                   status == HardDiskMedium.StatusOutLongTerm;
        }

        private static bool IsOutboundBorrowType(string applicationType)
        {
            return applicationType == HardDiskMediaApplication.TypeOutboundTemporary ||
                   applicationType == HardDiskMediaApplication.TypeOutboundLongTerm;
        }

        /// <summary>
        /// 出库申请流程中需占用库内空盘征用锁的类型（临时/长期/永久）。
        /// </summary>
        private static bool IsOutboundLockableType(string applicationType)
        {
            return HardDiskMediaOutboundReturnSupport.IsSelectableOutboundApplicationType(applicationType);
        }

        private static bool ShouldKeepOutboundLock(string applicationType, int applicationStatus)
        {
            if (!IsOutboundLockableType(applicationType))
            {
                return false;
            }

            return applicationStatus == HardDiskMediaApplication.StatusDraft ||
                   applicationStatus == HardDiskMediaApplication.StatusSubmitted ||
                   applicationStatus == HardDiskMediaApplication.StatusApproved ||
                   applicationStatus == HardDiskMediaApplication.StatusSignedUploaded;
        }

        private static bool IsLockedByOtherApplication(HardDiskMedium medium, int currentApplicationId)
        {
            ArgumentNullException.ThrowIfNull(medium);

            var lockItem = medium.RegisterLock;
            if (lockItem == null)
            {
                return false;
            }

            if (!string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeOutboundApplication, StringComparison.Ordinal))
            {
                return true;
            }

            return lockItem.BusinessRecordId != currentApplicationId;
        }

        private static string ResolveOutboundLockOwner(HardDiskMedium medium)
        {
            ArgumentNullException.ThrowIfNull(medium);

            var lockItem = medium.RegisterLock;
            if (lockItem == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(lockItem.BusinessNo))
            {
                return lockItem.BusinessNo.Trim();
            }

            return lockItem.BusinessRecordId?.ToString() ?? string.Empty;
        }

        private static void LockOutboundMedium(HardDiskMediaApplication application, HardDiskMedium medium)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(medium);

            if (application.Id <= 0)
            {
                throw new InvalidOperationException("申请单无效，无法锁定关联介质。");
            }

            if (medium.RegisterLock != null &&
                (!string.Equals(medium.RegisterLock.BusinessType, HardDiskRegisterLock.BusinessTypeOutboundApplication, StringComparison.Ordinal)
                 || medium.RegisterLock.BusinessRecordId != application.Id))
            {
                throw new InvalidOperationException("该硬盘已被其他业务占用，无法锁定。", innerException: null);
            }

            medium.RegisterLock = new HardDiskRegisterLock
            {
                MediumId = medium.Id,
                BusinessType = HardDiskRegisterLock.BusinessTypeOutboundApplication,
                BusinessRecordId = application.Id,
                BusinessNo = application.ApplicationNo?.Trim() ?? string.Empty,
                PreviousStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty,
                LockedTime = DateTime.Now
            };

            medium.UpdatedTime = DateTime.Now;
        }

        private static void UnlockOutboundMedium(int applicationId, HardDiskMedium medium)
        {
            ArgumentNullException.ThrowIfNull(medium);

            var lockItem = medium.RegisterLock;
            if (lockItem == null)
            {
                return;
            }

            if (!string.Equals(lockItem.BusinessType, HardDiskRegisterLock.BusinessTypeOutboundApplication, StringComparison.Ordinal)
                || lockItem.BusinessRecordId != applicationId)
            {
                return;
            }

            medium.RegisterLock = null;
            medium.UpdatedTime = DateTime.Now;
        }

        private async Task EnsureCanLockOutboundMediumAsync(HardDiskMediaApplication application, HardDiskMedium medium)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(medium);

            if (!IsOutboundLockableType(application.ApplicationType))
            {
                return;
            }

            if (IsLockedByOtherApplication(medium, application.Id))
            {
                string owner = ResolveOutboundLockOwner(medium);
                throw new InvalidOperationException($"该硬盘已被申请单【{owner}】占用，暂不可重复借出申请。");
            }

            bool hasOtherActiveOutbound = await _hardDiskMediaRepository.ExistsOtherActiveOutboundApplicationAsync(medium.Id, application.Id == 0 ? null : application.Id);
            if (hasOtherActiveOutbound)
            {
                throw new InvalidOperationException("该硬盘已存在其他在途借出申请，暂不可重复借出申请。");
            }

            string currentStatus = medium.Ledger?.MediaStatus ?? string.Empty;
            if (!string.Equals(currentStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"该硬盘当前状态为“{currentStatus}”，不属于可借出介质（仅允许“{HardDiskMedium.StatusInStockBlank}”）。");
            }
        }

        /// <summary>
        /// 归还/挂失登记现已统一走「申请→审批→实物交接→上传签批交接单→办结」7态流程，
        /// 不再存在跳过审批的登记类型，故恒为 false。
        /// </summary>
        private static bool IsRegistrationWithoutApprovalType(string applicationType)
        {
            return false;
        }

        /// <summary>
        /// 归还/挂失登记域类型判断（含挂失），用于业务编号分类、候选匹配、附件归类等归还域业务规则；
        /// 不代表跳过审批，审批流程判断请使用 <see cref="IsRegistrationWithoutApprovalType"/>。
        /// </summary>
        private static bool IsReturnOrLossRegistrationType(string applicationType)
        {
            return applicationType == HardDiskMediaApplication.TypeReturnBlankRegistration ||
                   applicationType == HardDiskMediaApplication.TypeReturnDataRegistration ||
                   applicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration ||
                   applicationType == HardDiskMediaApplication.TypeLossRegistration;
        }

        private static void ValidateApplicationReason(HardDiskMediaApplication application)
        {
            ArgumentNullException.ThrowIfNull(application);

            string inspectionResult = application.InspectionResult?.Trim() ?? string.Empty;
            bool requiresReason = application.ApplicationType == HardDiskMediaApplication.TypeOutboundTemporary ||
                                  application.ApplicationType == HardDiskMediaApplication.TypeOutboundLongTerm ||
                                  application.ApplicationType == HardDiskMediaApplication.TypeOutboundPermanent ||
                                  application.ApplicationType == HardDiskMediaApplication.TypeRelocate ||
                                  application.ApplicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration ||
                                  application.ApplicationType == HardDiskMediaApplication.TypeLossRegistration ||
                                  string.Equals(inspectionResult, "损坏登记", StringComparison.Ordinal) ||
                                  string.Equals(inspectionResult, "挂失登记", StringComparison.Ordinal);

            if (!requiresReason)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(application.Reason))
            {
                if (IsReturnOrLossRegistrationType(application.ApplicationType) ||
                    string.Equals(inspectionResult, "损坏登记", StringComparison.Ordinal) ||
                    string.Equals(inspectionResult, "挂失登记", StringComparison.Ordinal))
                {
                    string message = inspectionResult switch
                    {
                        "损坏登记" => "损坏登记时，请填写特殊情况说明。",
                        "挂失登记" => "挂失登记时，请填写特殊情况说明。",
                        _ when application.ApplicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration
                            => "损坏登记时，请填写特殊情况说明。",
                        _ when application.ApplicationType == HardDiskMediaApplication.TypeLossRegistration
                            => "挂失登记时，请填写特殊情况说明。",
                        _ => "特殊情况说明不能为空。"
                    };
                    throw new ArgumentException(message, nameof(application));
                }

                throw new ArgumentException("申请原因不能为空。", nameof(application));
            }
        }

        private static bool IsReturnRegistrationType(string applicationType)
        {
            return applicationType == HardDiskMediaApplication.TypeReturnBlankRegistration ||
                   applicationType == HardDiskMediaApplication.TypeReturnDataRegistration ||
                   applicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration;
        }

        private static bool IsArchiveRoomMediaAdmin(User? currentUser) =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser);

        private static bool CanSubmitHardDiskApplication(User? currentUser) =>
            ArchiveRegisterBusinessRules.CanSubmitApplication(currentUser);

        private static void ValidateApplicationRules(string applicationType, HardDiskMedium medium, User? currentUser)
        {
            var ledger = medium.Ledger;
            string mediaStatus = ledger?.MediaStatus ?? string.Empty;

            bool isOutboundApply =
                applicationType == HardDiskMediaApplication.TypeOutboundTemporary ||
                applicationType == HardDiskMediaApplication.TypeOutboundLongTerm ||
                applicationType == HardDiskMediaApplication.TypeOutboundPermanent ||
                applicationType == HardDiskMediaApplication.TypeRelocate;

            bool isReturnApply = IsReturnOrLossRegistrationType(applicationType);

            if ((isOutboundApply || isReturnApply) && !CanSubmitHardDiskApplication(currentUser))
            {
                throw new InvalidOperationException("仅部门资料管理员可发起硬盘借出/归还申请。");
            }

            if (applicationType == HardDiskMediaApplication.TypeOutboundTemporary ||
                applicationType == HardDiskMediaApplication.TypeOutboundLongTerm ||
                applicationType == HardDiskMediaApplication.TypeOutboundPermanent ||
                applicationType == HardDiskMediaApplication.TypeRelocate)
            {
                if (!IsInStockStatus(mediaStatus))
                {
                    throw new InvalidOperationException("仅在库介质可发起出库/调整申请。");
                }

                return;
            }

            if (isReturnApply && !IsOutTemporaryOrLongTerm(mediaStatus))
            {
                throw new InvalidOperationException("仅临时或长期出库的介质可办理归还/挂失登记。");
            }
        }

        private static HardDiskLedger EnsureLedger(HardDiskMedium medium, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(medium);

            medium.Ledger ??= new HardDiskLedger
            {
                MediumId = medium.Id,
                DiskCode = medium.DiskCode,
                MediaStatus = HardDiskMedium.StatusInStockBlank,
                MediaNature = HardDiskMedium.NatureBlank,
                StorageLocation = string.Empty,
                HolderOrOrganization = "资料室",
                NeedReturn = false,
                RegisterPerson = medium.RegisterPerson,
                RegisterDate = medium.RegisterDate,
                Remark = medium.Remark,
                CreatedTime = medium.CreatedTime == default ? now : medium.CreatedTime,
                UpdatedTime = now
            };

            return medium.Ledger;
        }

        private static IReadOnlyList<string> ResolveExpectedBeforeStatuses(string applicationType)
        {
            return applicationType switch
            {
                HardDiskMediaApplication.TypeOutboundTemporary or
                HardDiskMediaApplication.TypeOutboundLongTerm or
                HardDiskMediaApplication.TypeOutboundPermanent or
                HardDiskMediaApplication.TypeRelocate
                    => new[]
                    {
                        HardDiskMedium.StatusInStockBlank,
                        HardDiskMedium.StatusInStockData,
                        HardDiskMedium.StatusInStockDamaged
                    },

                HardDiskMediaApplication.TypeReturnBlankRegistration or
                HardDiskMediaApplication.TypeReturnDataRegistration or
                HardDiskMediaApplication.TypeReturnDamagedRegistration or
                HardDiskMediaApplication.TypeLossRegistration
                    => new[]
                    {
                        HardDiskMedium.StatusOutTemporary,
                        HardDiskMedium.StatusOutLongTerm
                    },

                _ => Array.Empty<string>()
            };
        }
    }
}
