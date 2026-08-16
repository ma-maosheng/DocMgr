using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网申请资料相对路径生成。
/// 部门专用：出网\年度\项目\资料名称；
/// 共用路径：申请部门\出网\年度\项目\资料名称。
/// </summary>
public static class NetworkOutboundMaterialPathSupport
{
    public const string OutboundFolderName = NetworkMaterialPathSupport.OutboundFolderName;

    /// <summary>
    /// 按所选服务器路径与申请信息生成相对路径；未选服务器路径时返回空。
    /// </summary>
    public static string BuildMaterialPath(
        ServerPathSetting? serverPath,
        string? applicantDept,
        string? year,
        string? projectName,
        string? materialName) =>
        NetworkMaterialPathSupport.BuildMaterialPath(
            serverPath,
            applicantDept,
            year,
            projectName,
            materialName,
            NetworkMaterialPathSupport.OutboundFolderName);
}
