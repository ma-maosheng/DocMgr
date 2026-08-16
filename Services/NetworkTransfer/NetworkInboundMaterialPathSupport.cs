using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 入网申请资料路径默认值生成。
/// 部门专用：入网\年度\项目\资料名称；
/// 共用路径：申请部门\入网\年度\项目\资料名称。
/// </summary>
public static class NetworkInboundMaterialPathSupport
{
    /// <summary>
    /// 是否为所有部门共用的服务器路径。
    /// </summary>
    public static bool IsPublicSharedServerPath(ServerPathSetting? serverPath) =>
        NetworkMaterialPathSupport.IsPublicSharedServerPath(serverPath);

    /// <summary>
    /// 生成默认资料路径。未选服务器路径时返回空。
    /// </summary>
    public static string BuildDefaultMaterialPath(
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
            NetworkMaterialPathSupport.InboundFolderName);
}
