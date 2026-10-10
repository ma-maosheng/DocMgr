using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网申请电子介质规则（目的地决定类型与处置方式约束）。
/// </summary>
internal static class NetworkOutboundRegisterMediaRulesSupport
{
    /// <summary>
    /// 资料室存档：申请时确定归档载体，只允许光盘或硬盘。
    /// </summary>
    private static readonly string[] ArchiveFilingElectronicMediaTypes =
    [
        ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc,
        ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk
    ];

    private static readonly string[] ExternalOfflineElectronicMediaTypes =
    [
        ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc,
        ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk,
        ArchiveRegisterDomainValues.ElectronicMediaTypeUsbDrive
    ];

    /// <summary>
    /// 出网：资料室存档允许光盘/硬盘；外部离线允许光盘/硬盘/U 盘。
    /// </summary>
    internal static IReadOnlyList<string> GetAllowedElectronicMediaTypes(
        string? destinationKind,
        IReadOnlyCollection<string> allMediaTypeOptions)
    {
        string[] allowedTypes = ResolveAllowedMediaTypes(destinationKind);
        if (allMediaTypeOptions == null || allMediaTypeOptions.Count == 0)
        {
            return allowedTypes.ToList();
        }

        // 按业务优先级（光盘→硬盘→U盘）排序，勿沿用域值种子顺序（U盘 在首）。
        var ordered = new List<string>();
        foreach (string allowed in allowedTypes)
        {
            string? matched = allMediaTypeOptions.FirstOrDefault(option =>
                string.Equals(option?.Trim(), allowed, StringComparison.OrdinalIgnoreCase));
            string candidate = string.IsNullOrWhiteSpace(matched) ? allowed : matched.Trim();
            if (ordered.Any(option => string.Equals(option, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            ordered.Add(candidate);
        }

        return ordered;
    }

    /// <summary>
    /// 出网：资料室存档→无需处置；外部离线（含 U 盘）→介质带走。
    /// </summary>
    internal static IReadOnlyList<string> GetAllowedElectronicDispositions(
        string? destinationKind,
        string? mediaType,
        IReadOnlyCollection<string> allDispositionOptions)
    {
        _ = mediaType;
        string requiredDisposition = ResolveRequiredDisposition(destinationKind);
        if (string.IsNullOrWhiteSpace(requiredDisposition))
        {
            return allDispositionOptions?.Count > 0
                ? allDispositionOptions.ToList()
                : Array.Empty<string>();
        }

        return [requiredDisposition];
    }

    internal static string ResolveRequiredDisposition(string? destinationKind)
    {
        if (NetworkTransferDomainValues.IsArchiveFilingDestination(destinationKind))
        {
            return ArchiveRegisterDomainValues.ElectronicDispositionNone;
        }

        if (NetworkTransferDomainValues.IsExternalOfflineDestination(destinationKind))
        {
            return NetworkTransferDomainValues.OutboundElectronicDispositionTakeAway;
        }

        return string.Empty;
    }

    internal static bool IsAllowedOutboundElectronicMediaType(string? destinationKind, string? mediaType)
    {
        string[] allowedTypes = ResolveAllowedMediaTypes(destinationKind);
        return allowedTypes.Any(allowed =>
            string.Equals(mediaType?.Trim(), allowed, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsDispositionAllowedForDestination(string? destinationKind, string? disposition)
    {
        string required = ResolveRequiredDisposition(destinationKind);
        if (string.IsNullOrWhiteSpace(required))
        {
            return true;
        }

        if (NetworkTransferDomainValues.IsExternalOfflineDestination(destinationKind))
        {
            return NetworkTransferDomainValues.IsOutboundTakeAwayDisposition(disposition);
        }

        return string.Equals(disposition?.Trim(), required, StringComparison.OrdinalIgnoreCase);
    }

    internal static string FormatAllowedMediaTypesHint(string? destinationKind)
    {
        string[] allowedTypes = ResolveAllowedMediaTypes(destinationKind);
        return string.Join("、", allowedTypes);
    }

    internal static bool IsArchiveFilingHardDisk(YearlyArchiveRegisterMedia media) =>
        RegisterMediaTreeSupport.IsElectronicMediaEntity(media)
        && string.Equals(
            media.MediaType?.Trim(),
            ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk,
            StringComparison.OrdinalIgnoreCase);

    internal static bool IsArchiveFilingOpticalDisc(YearlyArchiveRegisterMedia media) =>
        RegisterMediaTreeSupport.IsElectronicMediaEntity(media)
        && string.Equals(
            media.MediaType?.Trim(),
            ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 资料室存档：保留申请时选定的光盘/硬盘，处置固定为无需处置，并清掉与该载体无关的硬盘字段。
    /// </summary>
    internal static void NormalizeArchiveFilingCarrier(IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries)
    {
        foreach (YearlyArchiveRegisterMedia media in mediaEntries ?? [])
        {
            if (!RegisterMediaTreeSupport.IsElectronicMediaEntity(media))
            {
                continue;
            }

            media.Disposition = ArchiveRegisterDomainValues.ElectronicDispositionNone;
            media.IsBorrowedHardDisk = false;
            media.BorrowedHardDiskCode = string.Empty;

            if (!IsArchiveFilingHardDisk(media))
            {
                ClearBlankReservation(media);
                ClearAppendTarget(media);
                continue;
            }

            if (media.UseInStockBlankHardDisk)
            {
                media.RequisitionedDiskNeedReturn = false;
                media.ExpectedReturnDate = null;
                media.RequisitionedHardDiskCode = media.RequisitionedHardDiskCode?.Trim() ?? string.Empty;
                ClearAppendTarget(media);
                continue;
            }

            ClearBlankReservation(media);
            media.AppendTargetHardDiskCode = media.AppendTargetHardDiskCode?.Trim() ?? string.Empty;
            if (media.AppendTargetElectronicUnitId is not > 0)
            {
                media.AppendTargetElectronicUnitId = null;
                media.AppendTargetHardDiskCode = string.Empty;
            }
        }
    }

    /// <summary>
    /// 资料室存档提交校验：光盘不带硬盘；硬盘必须在库内空盘与并档目标中二选一。
    /// </summary>
    internal static void ValidateArchiveFilingCarrier(
        IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries,
        List<string> errors)
    {
        List<YearlyArchiveRegisterMedia> entries = mediaEntries?
            .Where(RegisterMediaTreeSupport.IsElectronicMediaEntity)
            .ToList()
            ?? [];
        if (entries.Count == 0)
        {
            return;
        }

        bool seenBlank = false;
        bool seenAppend = false;
        int? appendUnitId = null;
        var blankCodes = new List<string>();
        int mediaSeq = 0;
        foreach (YearlyArchiveRegisterMedia media in entries)
        {
            mediaSeq++;
            if (IsArchiveFilingOpticalDisc(media))
            {
                if (media.UseInStockBlankHardDisk
                    || media.RequisitionedMediumId is > 0
                    || media.AppendTargetElectronicUnitId is > 0)
                {
                    errors.Add($"• 第{mediaSeq}条介质：类型为光盘时不能选择库内空盘或并档硬盘");
                }

                continue;
            }

            if (!IsArchiveFilingHardDisk(media))
            {
                continue;
            }

            bool hasBlank = media.UseInStockBlankHardDisk;
            bool hasAppend = media.AppendTargetElectronicUnitId is > 0
                && !string.IsNullOrWhiteSpace(media.AppendTargetHardDiskCode);
            if (hasBlank == hasAppend)
            {
                errors.Add(hasBlank
                    ? $"• 第{mediaSeq}条介质：使用库内空盘时不能同时指定并档硬盘"
                    : $"• 第{mediaSeq}条介质：请选择库内空盘，或选择本项目当年可并档硬盘");
                continue;
            }

            if (hasBlank)
            {
                seenBlank = true;
                if (media.RequisitionedMediumId is not > 0
                    || string.IsNullOrWhiteSpace(media.RequisitionedHardDiskCode))
                {
                    errors.Add($"• 第{mediaSeq}条介质：使用库内空盘时须选择资料室库存空盘硬盘编号");
                }
                else
                {
                    blankCodes.Add(media.RequisitionedHardDiskCode.Trim());
                }

                if (media.RequisitionedDiskNeedReturn || media.ExpectedReturnDate != null)
                {
                    errors.Add($"• 第{mediaSeq}条介质：资料室存档的硬盘不出资料室，不需要填写归还信息");
                }
            }
            else
            {
                seenAppend = true;
                if (appendUnitId == null)
                {
                    appendUnitId = media.AppendTargetElectronicUnitId;
                }
                else if (appendUnitId != media.AppendTargetElectronicUnitId)
                {
                    errors.Add("• 同一申请单只能并入一块本项目当年已立档硬盘");
                }
            }
        }

        if (seenBlank && seenAppend)
        {
            errors.Add("• 同一申请单只能预定一种硬盘归档方式（库内空盘或并档）");
        }

        List<string> duplicatedBlankCodes = blankCodes
            .GroupBy(code => code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicatedBlankCodes.Count > 0)
        {
            errors.Add($"• 同一申请单内“库内空盘硬盘编号”不能重复：{string.Join("、", duplicatedBlankCodes)}");
        }
    }

    private static void ClearBlankReservation(YearlyArchiveRegisterMedia media)
    {
        media.UseInStockBlankHardDisk = false;
        media.RequisitionedMediumId = null;
        media.RequisitionedHardDiskCode = string.Empty;
        media.RequisitionedDiskNeedReturn = false;
        media.ExpectedReturnDate = null;
    }

    private static void ClearAppendTarget(YearlyArchiveRegisterMedia media)
    {
        media.AppendTargetElectronicUnitId = null;
        media.AppendTargetHardDiskCode = string.Empty;
    }

    private static string[] ResolveAllowedMediaTypes(string? destinationKind)
    {
        if (NetworkTransferDomainValues.IsExternalOfflineDestination(destinationKind))
        {
            return ExternalOfflineElectronicMediaTypes;
        }

        return ArchiveFilingElectronicMediaTypes;
    }
}
