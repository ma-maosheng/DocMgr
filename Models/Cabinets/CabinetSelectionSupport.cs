using System;
using System.Collections.Generic;
using System.Linq;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档口选择控件中档案柜列表的过滤、排序与默认选中规则。
    /// </summary>
    public static class CabinetSelectionSupport
    {
        public static IEnumerable<Cabinet> FilterForSimulatedArchive(IEnumerable<Cabinet> cabinets)
        {
            return cabinets.Where(item => item.Type != CabinetType.MagneticDisk);
        }

        public static IEnumerable<Cabinet> FilterForElectronicMagneticDisk(IEnumerable<Cabinet> cabinets)
        {
            return cabinets.Where(item => item.Type == CabinetType.MagneticDisk);
        }

        public static int GetTraditionalCabinetNameOrder(string? cabinetName)
        {
            if (string.IsNullOrWhiteSpace(cabinetName))
            {
                return int.MaxValue;
            }

            return cabinetName.Trim()[0] switch
            {
                '甲' => 0,
                '乙' => 1,
                '丙' => 2,
                '丁' => 3,
                '戊' => 4,
                '己' => 5,
                '庚' => 6,
                '辛' => 7,
                '壬' => 8,
                '癸' => 9,
                _ => 100
            };
        }

        public static IOrderedEnumerable<Cabinet> OrderByTraditionalCabinetName(IEnumerable<Cabinet> cabinets)
        {
            return cabinets
                .OrderBy(item => GetTraditionalCabinetNameOrder(item.Name))
                .ThenBy(item => item.Name, StringComparer.Ordinal);
        }

        public static List<Cabinet> BuildSimulatedArchiveCabinetItems(IEnumerable<Cabinet> allCabinets)
        {
            return OrderByTraditionalCabinetName(FilterForSimulatedArchive(allCabinets)).ToList();
        }

        public static List<Cabinet> BuildElectronicMagneticCabinetItems(IEnumerable<Cabinet> allCabinets)
        {
            return OrderByTraditionalCabinetName(FilterForElectronicMagneticDisk(allCabinets)).ToList();
        }

        public static Cabinet? GetDefaultCabinet(IReadOnlyList<Cabinet> cabinets)
        {
            return cabinets.Count > 0 ? cabinets[0] : null;
        }
    }
}
