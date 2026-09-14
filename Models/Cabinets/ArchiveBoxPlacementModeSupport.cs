using System;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档案盒放置方式域值与归一化（SpineOut=盒脊向外 / FrontOut=盒面向外）。
    /// </summary>
    public static class ArchiveBoxPlacementModeSupport
    {
        public const string SpineOut = "SpineOut";
        public const string FrontOut = "FrontOut";

        /// <summary>归一化放置方式文本；无法识别时返回 SpineOut。</summary>
        public static string Normalize(string? placementMode)
        {
            return string.Equals(placementMode?.Trim(), FrontOut, StringComparison.OrdinalIgnoreCase)
                ? FrontOut
                : SpineOut;
        }

        /// <summary>是否为盒面向外。</summary>
        public static bool IsFrontOut(string? placementMode)
        {
            return string.Equals(Normalize(placementMode), FrontOut, StringComparison.Ordinal);
        }
    }
}
