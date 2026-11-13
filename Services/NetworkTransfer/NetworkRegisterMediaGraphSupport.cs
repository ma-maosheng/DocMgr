using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出入网替换介质图时，把资料名称行和子项外键复位成新增。
/// </summary>
internal static class NetworkRegisterMediaGraphSupport
{
    internal static void ResetMaterialGraph(YearlyArchiveRegisterMedia media)
    {
        ArgumentNullException.ThrowIfNull(media);
        if (media.Materials == null)
        {
            return;
        }

        foreach (YearlyArchiveRegisterMaterial material in media.Materials)
        {
            material.Id = 0;
            material.YearlyArchiveRegisterMediaId = 0;
            material.MediaEntry = media;
            if (material.Items == null)
            {
                continue;
            }

            foreach (YearlyArchiveRegisterMediaItem item in material.Items)
            {
                item.RegisterMaterial = material;
                item.YearlyArchiveRegisterMaterialId = null;
            }
        }
    }
}
