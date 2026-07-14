using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    internal static class ArchiveRegisterBusinessRules
    {
        private static readonly Dictionary<string, string[]> ElectronicDispositionRules = new(StringComparer.Ordinal)
        {
            ["U盘"] = ["介质带回"],
            ["光盘"] = ["介质留存"],
            ["内网"] = ["无需处置"],
            ["硬盘"] = ["介质留存", "介质带回"]
        };

        /// <summary>
        /// 获取指定电子介质类型允许的处置方式列表。
        /// </summary>
        public static IReadOnlyList<string> GetAllowedElectronicDispositions(string? mediaType, IReadOnlyCollection<string> allDispositionOptions)
        {
            if (ElectronicDispositionRules.TryGetValue(mediaType?.Trim() ?? string.Empty, out var rules))
            {
                return rules;
            }

            return allDispositionOptions?.Count > 0
                ? allDispositionOptions.ToList()
                : Array.Empty<string>();
        }

        public static bool IsExternalSourceType(string? sourceType)
        {
            return string.Equals(sourceType?.Trim(), ArchiveRegisterDomainValues.SourceTypeExternal, StringComparison.Ordinal);
        }

        public static bool IsAllowedDomainValue(string? value, IReadOnlyCollection<string> options)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            return options.Any(o => string.Equals(o, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizeConfidentialLevel(string? value)
            => ArchiveRegisterDomainValues.NormalizeConfidentialLevel(value);

        public static bool IsArchiveAdminUser(User? user)
        {
            if (user == null)
            {
                return false;
            }

            return user.Department == "资料室"
                   && (user.Role == "部门资料管理员"
                       || user.Role == "资料室资料管理员"
                       || user.Role == "资料室管理员"
                       || user.Role == "Administrator"
                       || user.Role == "管理员");
        }

        public static bool IsApplicantUser(User? user)
        {
            if (user == null)
            {
                return false;
            }

            var role = user.Role ?? string.Empty;
            var isArchiveAdmin = IsArchiveAdminUser(user);
            var isDeptDocAdmin = role == "部门资料管理员" && !isArchiveAdmin;

            return role == "普通用户" || isDeptDocAdmin;
        }

        public static ArchiveRegisterUiPermissionState ResolveUiPermissionState(User? user, YearlyArchiveRegisterRecord? currentRecord)
        {
            if (user == null)
            {
                return new ArchiveRegisterUiPermissionState(false, false, false, false, false, false);
            }

            var role = user.Role ?? string.Empty;
            bool isDraft = currentRecord == null || currentRecord.IsDraft;
            bool isSubmitted = currentRecord?.IsSubmitted == true;
            bool isApproved = currentRecord?.IsApprovedReceived == true;
            bool isSignedUploaded = currentRecord?.IsSignedUploaded == true;
            bool isCompleted = currentRecord?.IsArchived == true;

            bool isSysAdmin = role == "Administrator";
            bool isArchiveAdmin = IsArchiveAdminUser(user);
            bool isDeptDocAdmin = role.Contains("部门资料管理员", StringComparison.Ordinal) && !isArchiveAdmin;
            bool isApplicant = role == "普通用户" || isDeptDocAdmin;

            bool canEditForm = (isApplicant && (isDraft || isSubmitted)) || isSysAdmin;
            bool canApprove = (isArchiveAdmin || isSysAdmin) && isSubmitted;
            bool canUpload = (isArchiveAdmin || isSysAdmin) && (isApproved || isSignedUploaded);
            bool canEditItemConfidentialLevel = !isCompleted
                && (canEditForm
                    || ((isArchiveAdmin || isSysAdmin) && isSubmitted));

            return new ArchiveRegisterUiPermissionState(
                isArchiveAdmin,
                isApplicant,
                canEditForm,
                canApprove,
                canUpload,
                canEditItemConfidentialLevel);
        }

        public static ArchiveRegisterPrintNormalizationResult NormalizePrintFields(
            string? confidentialLevel,
            string? prodOpinion,
            string? rndOpinion,
            string? deputyOpinion,
            IReadOnlyCollection<string> confidentialLevels,
            IReadOnlyCollection<string> prodOptions,
            IReadOnlyCollection<string> rndOptions,
            IReadOnlyCollection<string> deputyOptions)
        {
            var normalizedConfidentialLevel = NormalizeConfidentialLevelForPrint(confidentialLevel, confidentialLevels);
            var normalizedProdOpinion = NormalizeOpinionForPrint(prodOpinion, prodOptions);
            var normalizedRndOpinion = NormalizeOpinionForPrint(rndOpinion, rndOptions);
            var normalizedDeputyOpinion = NormalizeOpinionForPrint(deputyOpinion, deputyOptions);

            return new ArchiveRegisterPrintNormalizationResult(
                normalizedConfidentialLevel,
                normalizedProdOpinion,
                normalizedRndOpinion,
                normalizedDeputyOpinion,
                confidentialLevels.ToList());
        }

        private static string NormalizeConfidentialLevelForPrint(string? value, IReadOnlyCollection<string> options)
        {
            var normalized = NormalizeConfidentialLevel(value);
            return IsAllowedDomainValue(normalized, options)
                ? normalized
                : string.Empty;
        }

        private static string NormalizeOpinionForPrint(string? value, IReadOnlyCollection<string> options)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim();
            return IsAllowedDomainValue(normalized, options) ? normalized : string.Empty;
        }

        /// <summary>
        /// 将 UI 录入的审批签字信息复制到持久化实体，不触及申请主表字段。
        /// </summary>
        public static void CopyRegisterApprovalFields(YearlyArchiveRegisterRecord target, YearlyArchiveRegisterRecord source)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(source);

            target.DeptLeader = source.DeptLeader?.Trim() ?? string.Empty;
            target.DeptDate = source.DeptDate;
            target.ProdDeptOpinion = source.ProdDeptOpinion?.Trim() ?? string.Empty;
            target.ProdLeader = source.ProdLeader?.Trim() ?? string.Empty;
            target.ProdDate = source.ProdDate;
            target.RndDeptOpinion = source.RndDeptOpinion?.Trim() ?? string.Empty;
            target.RndLeader = source.RndLeader?.Trim() ?? string.Empty;
            target.RndDate = source.RndDate;
            target.DeputyOpinion = source.DeputyOpinion?.Trim() ?? string.Empty;
            target.DeputyLeader = source.DeputyLeader?.Trim() ?? string.Empty;
            target.DeputyDate = source.DeputyDate;
            target.Deliverer = source.Deliverer?.Trim() ?? string.Empty;
            target.DeliverDate = source.DeliverDate;
            target.Administrator = source.Administrator?.Trim() ?? string.Empty;
            target.AdminDate = source.AdminDate;
        }

        /// <summary>
        /// 仅合并资料子项密级，禁止通过审批入口改写申请介质结构与其它字段。
        /// </summary>
        public static void MergeMediaItemConfidentialLevels(
            YearlyArchiveRegisterRecord target,
            IReadOnlyCollection<YearlyArchiveRegisterMedia>? sourceMediaEntries)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (sourceMediaEntries == null || sourceMediaEntries.Count == 0)
            {
                return;
            }

            foreach (var sourceMedia in sourceMediaEntries)
            {
                var targetMedia = target.MediaEntries.FirstOrDefault(item => item.Id > 0 && item.Id == sourceMedia.Id);
                if (targetMedia == null)
                {
                    continue;
                }

                foreach (var sourceItem in sourceMedia.Items)
                {
                    if (sourceItem.Id <= 0)
                    {
                        continue;
                    }

                    var targetItem = targetMedia.Items.FirstOrDefault(item => item.Id == sourceItem.Id);
                    if (targetItem == null)
                    {
                        continue;
                    }

                    targetItem.ConfidentialLevel = NormalizeConfidentialLevel(sourceItem.ConfidentialLevel);
                }
            }
        }
    }
}
