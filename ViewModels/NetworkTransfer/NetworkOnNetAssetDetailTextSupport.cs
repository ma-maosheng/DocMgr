using System.Text;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Services.Interfaces;

namespace DocMgr.ViewModels.NetworkTransfer;

/// <summary>
/// 在网对象详情文本与弹窗。
/// </summary>
internal static class NetworkOnNetAssetDetailTextSupport
{
    internal static NetworkOnNetAsset? Resolve(object? parameter, NetworkOnNetAsset? selected = null) =>
        parameter switch
        {
            NetworkOnNetAsset asset => asset,
            NetworkOnNetAssetCandidate candidate => candidate.Asset,
            NetworkOnNetDisposalItemRow row => row.Asset,
            _ => selected
        };

    internal static async Task ShowAsync(
        INetworkTransferService service,
        IDialogService dialogService,
        NetworkOnNetAsset asset)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(asset);

        string title = string.IsNullOrWhiteSpace(asset.AssetNo)
            ? "在网对象详情"
            : $"在网对象详情 · {asset.AssetNo}";
        string summary = Build(asset);
        if (asset.ElectronicMediaItemId is > 0)
        {
            var entries = await service.GetOnNetAssetContentEntriesAsync(asset.ElectronicMediaItemId.Value);
            if (entries.Count > 0)
            {
                dialogService.ShowElectronicMediaItemEntriesDialog(title, entries, summary);
                return;
            }
        }

        dialogService.ShowTextDetailDialog(summary, title);
    }

    internal static string Build(NetworkOnNetAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var builder = new StringBuilder();
        Append(builder, "资产编号", asset.AssetNo);
        Append(builder, "申请单号", asset.ApplicationNo);
        Append(builder, "来源", asset.OriginKind);
        Append(builder, "申请部门", asset.ApplicantDept);
        Append(builder, "年度", asset.Year);
        Append(builder, "项目名称", asset.ProjectName);
        Append(builder, "资料名称", asset.MaterialName);
        Append(builder, "资料子项", asset.AssetName);
        Append(builder, "提供部门", asset.ProvideUnit);
        Append(builder, "资料存储区", asset.DepartmentName);
        Append(builder, "物理地址", asset.PhysicalPath);
        Append(builder, "服务器路径", asset.ServerPath);
        Append(builder, "资料相对路径", asset.MaterialPath);
        Append(builder, "完整存储路径", asset.FullStorageAddress);
        Append(builder, "类别", asset.AssetKind);
        Append(builder, "密级", asset.ConfidentialLevel);
        Append(builder, "组织形式", asset.DataOrganizationForm);
        Append(builder, "个数", asset.EntryCountDisplay);
        Append(builder, "数据量", asset.DataSizeText);
        Append(builder, "生命周期", asset.LifecycleStatus);
        Append(builder, "版本", asset.VersionText);
        Append(builder, "备注", asset.Remark);
        Append(builder, "登记人", asset.RegisteredBy);
        builder.AppendLine($"登记时间：{asset.RegisteredAt:yyyy-MM-dd HH:mm}");
        return builder.ToString().TrimEnd();
    }

    private static void Append(StringBuilder builder, string label, string? value)
    {
        string text = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        builder.AppendLine($"{label}：{text}");
    }
}
