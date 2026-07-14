using DocMgr.Services.YearlyArchive;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 防磁磁盘柜专用档口占用统计：合并档内序号与仅档口键的历史位置。
    /// </summary>
    internal static class MagneticDedicatedSlotOccupancySupport
    {
        public static List<int> CollectOccupiedSequenceIndexes(
            string slotCode,
            IEnumerable<string?> locations)
        {
            string normalizedSlotCode = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(slotCode);
            var occupiedIndexes = new HashSet<int>();
            int legacySlotOnlyCount = 0;

            foreach (string? location in locations)
            {
                if (string.IsNullOrWhiteSpace(location))
                {
                    continue;
                }

                if (!string.Equals(
                        HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(location),
                        normalizedSlotCode,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ArchiveSlotLocationSupport.TryParseSequenceIndex(location, out int sequenceIndex))
                {
                    occupiedIndexes.Add(sequenceIndex);
                }
                else
                {
                    legacySlotOnlyCount++;
                }
            }

            for (int index = 1; index <= legacySlotOnlyCount; index++)
            {
                occupiedIndexes.Add(index);
            }

            return occupiedIndexes.OrderBy(index => index).ToList();
        }

        public static bool IsSlotFull(IReadOnlyCollection<int> occupiedIndexes, int slotCapacity)
            => occupiedIndexes.Count >= slotCapacity;

        public static int ResolveNextSequenceIndex(IReadOnlyCollection<int> occupiedIndexes, int slotCapacity)
        {
            if (IsSlotFull(occupiedIndexes, slotCapacity))
            {
                throw new InvalidOperationException("目标档口已满，无法继续分配档内序号。");
            }

            return ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(occupiedIndexes);
        }
    }
}
