namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出入网介质上的多份资料名称摘要。分组校验与建档申请共用 <see cref="ArchiveRegisterMaterialSummarySupport"/>。
/// </summary>
internal static class NetworkRegisterMaterialGroupSupport
{
    internal static string JoinDistinctNames(IEnumerable<string?> names)
    {
        var distinct = new List<string>();
        foreach (string? name in names)
        {
            string trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length == 0 || distinct.Contains(trimmed, StringComparer.Ordinal))
            {
                continue;
            }

            distinct.Add(trimmed);
        }

        return string.Join("、", distinct);
    }

    internal static void Validate(YearlyArchiveRegisterMedia media, int mediaSeq, List<string> errors) =>
        ArchiveRegisterMaterialSummarySupport.AppendMaterialGroupErrors(media, mediaSeq, errors);
}
