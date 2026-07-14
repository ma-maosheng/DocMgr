using DocMgr.Models.Cabinets;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 防磁磁盘柜硬盘存放位置。
    /// 空白盘：仅档口键（如 壬A-1-2）；数据盘：档口键 + 档内序号（如 壬A-1-2-01）。详见 .cursor/rules/hard-disk-storage-location-encoding.mdc。
    /// </summary>
    public static class HardDiskBlankSlotLocationSupport
    {
        public const int DefaultSlotCapacity = 10;

        public static string NormalizeToSlotCode(string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return string.Empty;
            }

            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(
                    location,
                    out string cabinetName,
                    out string side,
                    out int row,
                    out int column))
            {
                return location.Trim();
            }

            return ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, side, row, column);
        }

        public static string BuildLocationCode(string cabinetName, string faceCode, string slotCode)
            => $"{cabinetName.Trim()}{faceCode.Trim().ToUpperInvariant()}-{slotCode.Trim()}";

        public static string BuildFullLocationFromSlotCode(string slotCode, int sequenceIndex)
        {
            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(slotCode, out string cabinetName, out string side, out int row, out int column))
            {
                throw new InvalidOperationException($"无法解析档口位置 [{slotCode}]。");
            }

            return ArchiveSlotLocationSupport.BuildFullElectronicLocation(cabinetName, side, row, column, sequenceIndex);
        }

        public static int CompareLocationCodes(string? left, string? right)
        {
            if (!TryParseLocationCode(left, out string leftCabinet, out string leftFace, out int leftRow, out int leftColumn))
            {
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            }

            if (!TryParseLocationCode(right, out string rightCabinet, out string rightFace, out int rightRow, out int rightColumn))
            {
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            }

            int cabinetCompare = string.Compare(leftCabinet, rightCabinet, StringComparison.OrdinalIgnoreCase);
            if (cabinetCompare != 0)
            {
                return cabinetCompare;
            }

            int faceCompare = string.Compare(leftFace, rightFace, StringComparison.OrdinalIgnoreCase);
            if (faceCompare != 0)
            {
                return faceCompare;
            }

            int rowCompare = leftRow.CompareTo(rightRow);
            return rowCompare != 0 ? rowCompare : leftColumn.CompareTo(rightColumn);
        }

        public static int CompareDedicatedSlots(
            CabinetHardDiskSlotCategoryAssignment? left,
            CabinetHardDiskSlotCategoryAssignment? right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            if (left.Cabinet == null && right.Cabinet == null)
            {
                return 0;
            }

            if (left.Cabinet == null)
            {
                return 1;
            }

            if (right.Cabinet == null)
            {
                return -1;
            }

            int cabinetCompare = string.Compare(left.Cabinet.Name, right.Cabinet.Name, StringComparison.OrdinalIgnoreCase);
            if (cabinetCompare != 0)
            {
                return cabinetCompare;
            }

            int faceCompare = string.Compare(left.FaceCode, right.FaceCode, StringComparison.OrdinalIgnoreCase);
            if (faceCompare != 0)
            {
                return faceCompare;
            }

            (int leftLayer, int leftColumn) = ParseSlotCode(left.SlotCode);
            (int rightLayer, int rightColumn) = ParseSlotCode(right.SlotCode);
            int layerCompare = leftLayer.CompareTo(rightLayer);
            return layerCompare != 0 ? layerCompare : leftColumn.CompareTo(rightColumn);
        }

        public static bool TryParseLocationCode(
            string? location,
            out string cabinetName,
            out string face,
            out int row,
            out int column)
        {
            cabinetName = string.Empty;
            face = string.Empty;
            row = 0;
            column = 0;

            string normalized = NormalizeToSlotCode(location);
            return ArchiveSlotLocationSupport.TryParseSlotLocation(normalized, out cabinetName, out face, out row, out column);
        }

        private static (int Layer, int Column) ParseSlotCode(string slotCode)
        {
            if (string.IsNullOrWhiteSpace(slotCode))
            {
                return (int.MaxValue, int.MaxValue);
            }

            string[] parts = slotCode.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2
                && int.TryParse(parts[0], out int layer)
                && int.TryParse(parts[1], out int column))
            {
                return (layer, column);
            }

            return (int.MaxValue, int.MaxValue);
        }
    }
}
