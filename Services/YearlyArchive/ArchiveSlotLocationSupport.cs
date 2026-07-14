using System;

namespace DocMgr.Services.YearlyArchive
{
    public static class ArchiveSlotLocationSupport
    {
        public static bool TryParseSlotLocation(
            string? location,
            out string cabinetName,
            out string side,
            out int row,
            out int column)
        {
            cabinetName = string.Empty;
            side = string.Empty;
            row = 0;
            column = 0;

            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            var parts = location.Trim().Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return false;
            }

            string cabinetAndSide = parts[0];
            if (cabinetAndSide.Length < 2)
            {
                return false;
            }

            side = cabinetAndSide[^1].ToString();
            cabinetName = cabinetAndSide[..^1];
            if (!int.TryParse(parts[1], out row) || !int.TryParse(parts[2], out column))
            {
                return false;
            }

            return true;
        }

        public static string BuildSlotKey(string? location)
        {
            if (!TryParseSlotLocation(location, out string cabinetName, out string side, out int row, out int column))
            {
                return string.Empty;
            }

            return BuildSlotKey(cabinetName, side, row, column);
        }

        public static string BuildSlotKey(string cabinetName, string side, int row, int column)
            => $"{cabinetName.Trim()}{side.Trim().ToUpperInvariant()}-{row}-{column}";

        public static bool IsSameSlot(string? sourceLocation, string? targetLocation)
        {
            string sourceKey = BuildSlotKey(sourceLocation);
            string targetKey = BuildSlotKey(targetLocation);
            return !string.IsNullOrWhiteSpace(sourceKey)
                   && string.Equals(sourceKey, targetKey, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParseSequenceIndex(string? location, out int sequenceIndex)
        {
            sequenceIndex = 0;
            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            string[] parts = location.Trim().Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || !int.TryParse(parts[3], out sequenceIndex) || sequenceIndex <= 0)
            {
                return false;
            }

            return true;
        }

        public static string BuildFullElectronicLocation(string cabinetName, string side, int row, int column, int sequenceIndex)
            => $"{BuildSlotKey(cabinetName, side, row, column)}-{sequenceIndex:D2}";

        public static int ResolveMinimumAvailableSequence(IEnumerable<int> occupiedIndexes)
        {
            var occupied = occupiedIndexes
                .Where(index => index > 0)
                .ToHashSet();

            for (int index = 1; index <= 99; index++)
            {
                if (!occupied.Contains(index))
                {
                    return index;
                }
            }

            throw new InvalidOperationException("目标档口内序号已用尽，无法分配新的存放位置。");
        }
    }
}
