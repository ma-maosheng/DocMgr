using System.Text;
using DocMgr.Models.Cabinets;

namespace DocMgr.Services.Cabinets;

/// <summary>
/// 编辑已有内容柜体时的提示文案。编辑本身不改写存放记录。
/// </summary>
public static class CabinetStoredContentEditNotice
{
    public static string Build(string? cabinetName, IReadOnlyList<CabinetStoredContentSlot> slots)
    {
        string name = string.IsNullOrWhiteSpace(cabinetName) ? "当前柜体" : cabinetName.Trim();
        var builder = new StringBuilder();
        builder.Append("柜体「").Append(name).AppendLine("」内下列档口存有资料或其他内容：");
        builder.AppendLine();
        foreach (CabinetStoredContentSlot slot in slots)
        {
            builder.AppendLine(slot.DisplayText);
        }

        builder.AppendLine();
        builder.AppendLine("本次编辑不会改写数据库中的已有存放记录。");
        builder.AppendLine("不得修改柜体编号或种类，也不得缩小面数、层数或列数，使上述档口落在柜体之外。");
        builder.Append("否则会造成位置对不上、记录丢失或不完整；确定保存时会核验并拒绝这类修改。");
        return builder.ToString();
    }
}
