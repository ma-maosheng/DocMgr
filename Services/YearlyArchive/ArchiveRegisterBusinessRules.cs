using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Models.NetworkTransfer;
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
        /// 是否为出网办结自动生成的建档草稿。
        /// </summary>
        public static bool IsNetworkOutboundTransferRegister(YearlyArchiveRegisterRecord? record)
        {
            if (record == null)
            {
                return false;
            }

            if (record.SourceNetworkOutboundRecordId is int outboundRecordId && outboundRecordId > 0)
            {
                return true;
            }

            return string.Equals(
                record.SourceType?.Trim(),
                NetworkTransferDomainValues.RegisterSourceTypeNetworkOutbound,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// 手工新建建档申请时可选的电子介质类型（隐藏「内网」，由出网转立档自动带入）。
        /// </summary>
        public static IReadOnlyList<string> FilterManualSelectableElectronicMediaTypes(
            IReadOnlyCollection<string> options)
        {
            if (options == null || options.Count == 0)
            {
                return Array.Empty<string>();
            }

            return options
                .Where(option => !string.Equals(
                    option?.Trim(),
                    ArchiveRegisterDomainValues.ElectronicMediaTypeInnerNetwork,
                    StringComparison.Ordinal))
                .ToList();
        }

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

        /// <summary>
        /// 资料室资料管理员：所属部门为「资料室」的部门资料管理员，或系统管理员。
        /// 仅用于审批及后续办理（交接/办结等）。
        /// </summary>
        public static bool IsArchiveAdminUser(User? user)
        {
            if (user == null)
            {
                return false;
            }

            string role = user.Role?.Trim() ?? string.Empty;
            if (string.Equals(role, "Administrator", StringComparison.Ordinal)
                || string.Equals(role, "管理员", StringComparison.Ordinal))
            {
                return true;
            }

            string dept = user.Department?.Trim() ?? string.Empty;
            return string.Equals(dept, "资料室", StringComparison.Ordinal)
                   && (string.Equals(role, "部门资料管理员", StringComparison.Ordinal)
                       || string.Equals(role, "资料室资料管理员", StringComparison.Ordinal)
                       || string.Equals(role, "资料室管理员", StringComparison.Ordinal));
        }

        /// <summary>
        /// 部门资料管理员（不含资料室）：仅可发起各类申请业务。
        /// </summary>
        public static bool IsDepartmentArchiveAdmin(User? user)
        {
            if (user == null)
            {
                return false;
            }

            string role = user.Role?.Trim() ?? string.Empty;
            if (!string.Equals(role, "部门资料管理员", StringComparison.Ordinal))
            {
                return false;
            }

            // 资料室部门的「部门资料管理员」即资料室资料管理员，不得发起申请。
            string dept = user.Department?.Trim() ?? string.Empty;
            return !string.Equals(dept, "资料室", StringComparison.Ordinal);
        }

        /// <summary>
        /// 申请侧操作人：部门资料管理员（不含资料室）。
        /// </summary>
        public static bool IsApplicantUser(User? user) => IsDepartmentArchiveAdmin(user);

        /// <summary>
        /// 是否允许发起申请（部门资料管理员，或系统管理员例外）。
        /// </summary>
        public static bool CanSubmitApplication(User? user) =>
            IsDepartmentArchiveAdmin(user) || IsSystemAdministrator(user);

        /// <summary>
        /// 系统管理员（超管例外）。
        /// </summary>
        public static bool IsSystemAdministrator(User? user)
        {
            if (user == null)
            {
                return false;
            }

            string role = user.Role?.Trim() ?? string.Empty;
            return string.Equals(role, "Administrator", StringComparison.Ordinal)
                   || string.Equals(role, "管理员", StringComparison.Ordinal);
        }

        public static ArchiveRegisterUiPermissionState ResolveUiPermissionState(User? user, YearlyArchiveRegisterRecord? currentRecord)
        {
            if (user == null)
            {
                return new ArchiveRegisterUiPermissionState(false, false, false, false, false, false);
            }

            bool isDraft = currentRecord == null || currentRecord.IsDraft;
            bool isSubmitted = currentRecord?.IsSubmitted == true;
            bool isApproved = currentRecord?.IsApprovedReceived == true;
            bool isSignedUploaded = currentRecord?.IsSignedUploaded == true;
            bool isCompleted = currentRecord?.IsArchived == true;

            bool isSysAdmin = IsSystemAdministrator(user);
            bool isArchiveAdmin = IsArchiveAdminUser(user);
            bool isApplicant = IsDepartmentArchiveAdmin(user);

            bool canEditForm = (isApplicant && (isDraft || isSubmitted)) || isSysAdmin;
            bool canApprove = isArchiveAdmin && isSubmitted;
            bool canUpload = isArchiveAdmin && (isApproved || isSignedUploaded);
            bool canEditItemConfidentialLevel = !isCompleted
                && (canEditForm
                    || (isArchiveAdmin && isSubmitted));

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
            // 登记审批 UI 为「仅签字、无需意见」；落库时统一清空意见，避免部分节点残留「同意」。
            target.ProdDeptOpinion = string.Empty;
            target.ProdLeader = source.ProdLeader?.Trim() ?? string.Empty;
            target.ProdDate = source.ProdDate;
            target.RndDeptOpinion = string.Empty;
            target.RndLeader = source.RndLeader?.Trim() ?? string.Empty;
            target.RndDate = source.RndDate;
            target.DeputyOpinion = string.Empty;
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
