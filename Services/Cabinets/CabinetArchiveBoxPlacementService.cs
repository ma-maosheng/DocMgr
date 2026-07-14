using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Models.Cabinets;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.Cabinets
{
    public class CabinetArchiveBoxPlacementService : ICabinetArchiveBoxPlacementService
    {
        private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

        private readonly ICabinetArchiveBoxPlacementRepository _placementRepository;

        public CabinetArchiveBoxPlacementService(ICabinetArchiveBoxPlacementRepository placementRepository)
        {
            ArgumentNullException.ThrowIfNull(placementRepository);
            _placementRepository = placementRepository;
        }

        /// <summary>
        /// 获取指定档案盒当前的放置方式，未登记时返回默认值。
        /// </summary>
        public CabinetArchiveBoxPlacementMode GetPlacementMode(string boxCode)
        {
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                throw new ArgumentException("档案盒编号不能为空。", nameof(boxCode));
            }

            var placement = _placementRepository.GetPlacementByBoxCode(boxCode.Trim());

            return ParsePlacementMode(placement?.PlacementMode);
        }

        /// <summary>
        /// 批量更新指定柜体、面别、档口下所有档案盒的放置方式。
        /// </summary>
        public int UpdateSlotPlacementMode(string cabinetName, string faceCode, string slotCode, CabinetArchiveBoxPlacementMode placementMode, string updatedBy)
        {
            if (string.IsNullOrWhiteSpace(cabinetName))
            {
                throw new ArgumentException("柜号不能为空。", nameof(cabinetName));
            }

            if (string.IsNullOrWhiteSpace(faceCode))
            {
                throw new ArgumentException("面别不能为空。", nameof(faceCode));
            }

            if (string.IsNullOrWhiteSpace(slotCode))
            {
                throw new ArgumentException("档口编号不能为空。", nameof(slotCode));
            }

            string normalizedCabinetName = CabinetNameNormalizer.Normalize(cabinetName);
            string normalizedFaceCode = faceCode.Trim().ToUpperInvariant();
            string normalizedSlotCode = slotCode.Trim();
            string normalizedUpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "System" : updatedBy.Trim();
            string nowText = DateTime.Now.ToString(TimestampFormat);
            string placementModeText = ToStorageValue(placementMode);

            var placements = _placementRepository.GetPlacementsBySlot(normalizedCabinetName, normalizedFaceCode, normalizedSlotCode);

            foreach (var placement in placements)
            {
                placement.PlacementMode = placementModeText;
                placement.UpdatedAt = nowText;
                placement.UpdatedBy = normalizedUpdatedBy;
            }

            if (placements.Count > 0)
            {
                var boxCodes = placements
                    .Select(item => item.BoxCode)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                UpdateYearlyArchiveBoxPlacementModes(boxCodes, placementModeText);
                _placementRepository.SaveChanges();
            }

            return placements.Count;
        }

        /// <summary>
        /// 更新单个档案盒的放置方式。
        /// </summary>
        public bool UpdateBoxPlacementMode(string boxCode, CabinetArchiveBoxPlacementMode placementMode, string updatedBy)
        {
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                throw new ArgumentException("档案盒编号不能为空。", nameof(boxCode));
            }

            string normalizedBoxCode = boxCode.Trim();
            string normalizedUpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "System" : updatedBy.Trim();
            string nowText = DateTime.Now.ToString(TimestampFormat);
            string placementModeText = ToStorageValue(placementMode);

            var placement = _placementRepository.GetPlacementByBoxCode(normalizedBoxCode);

            if (placement == null)
            {
                if (!TryParseBoxCode(normalizedBoxCode, out string cabinetName, out string faceCode, out string slotCode))
                {
                    return false;
                }

                placement = new CabinetArchiveBoxPlacement
                {
                    BoxCode = normalizedBoxCode,
                    CabinetName = cabinetName,
                    FaceCode = faceCode,
                    SlotCode = slotCode,
                    BoxSpecification = string.Empty,
                    SourceType = "Manual",
                    SourceRecordKey = string.Empty,
                    CreatedAt = nowText,
                    UpdatedAt = nowText,
                    UpdatedBy = normalizedUpdatedBy,
                    PlacementMode = placementModeText
                };

                _placementRepository.AddPlacement(placement);
            }
            else
            {
                placement.PlacementMode = placementModeText;
                placement.UpdatedAt = nowText;
                placement.UpdatedBy = normalizedUpdatedBy;
            }

            UpdateYearlyArchiveBoxPlacementMode(normalizedBoxCode, placementModeText);
            _placementRepository.SaveChanges();
            return true;
        }

        /// <summary>
        /// 获取可供设置的档案盒规格列表。
        /// </summary>
        public IReadOnlyList<string> GetAvailableBoxSpecifications()
        {
            var names = _placementRepository.GetArchiveBoxSpecifications()
                .Select(item => item.Name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
            {
                names.Add("标准(10cm)");
                names.Add("标准(5cm)");
                names.Add("标准(3cm)");
                names.Add("标准(2cm)");
                names.Add("非标(10cm)");
            }

            return names;
        }

        /// <summary>
        /// 为单个档案盒设置规格。
        /// </summary>
        public bool ResetBoxSpecification(string boxCode, string boxSpecification, string updatedBy)
        {
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                throw new ArgumentException("档案盒编号不能为空。", nameof(boxCode));
            }

            if (string.IsNullOrWhiteSpace(boxSpecification))
            {
                throw new ArgumentException("档案盒规格不能为空。", nameof(boxSpecification));
            }

            string normalizedBoxCode = boxCode.Trim();
            string normalizedSpecification = boxSpecification.Trim();
            var placement = _placementRepository.GetPlacementByBoxCode(normalizedBoxCode);
            if (placement == null)
            {
                return false;
            }

            placement.BoxSpecification = normalizedSpecification;
            placement.UpdatedAt = DateTime.Now.ToString(TimestampFormat);
            placement.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "System" : updatedBy.Trim();
            _placementRepository.SaveChanges();
            return true;
        }

        private static CabinetArchiveBoxPlacementMode ParsePlacementMode(string? placementMode)
        {
            return string.Equals(placementMode?.Trim(), "FrontOut", StringComparison.OrdinalIgnoreCase)
                ? CabinetArchiveBoxPlacementMode.FrontOut
                : CabinetArchiveBoxPlacementMode.SpineOut;
        }

        private static string ToStorageValue(CabinetArchiveBoxPlacementMode placementMode)
        {
            return placementMode == CabinetArchiveBoxPlacementMode.FrontOut ? "FrontOut" : "SpineOut";
        }

        private void UpdateYearlyArchiveBoxPlacementModes(IReadOnlyCollection<string> boxCodes, string placementModeText)
        {
            if (boxCodes.Count == 0)
            {
                return;
            }

            var yearlyBoxes = _placementRepository.GetYearlyArchiveBoxesByLocationCodes(boxCodes);

            foreach (var yearlyBox in yearlyBoxes)
            {
                yearlyBox.PlacementMode = placementModeText;
            }
        }

        private void UpdateYearlyArchiveBoxPlacementMode(string boxCode, string placementModeText)
        {
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                return;
            }

            var yearlyBox = _placementRepository.GetYearlyArchiveBoxByLocationCode(boxCode);

            if (yearlyBox != null)
            {
                yearlyBox.PlacementMode = placementModeText;
            }
        }

        private static bool TryParseBoxCode(string boxCode, out string cabinetName, out string faceCode, out string slotCode)
        {
            cabinetName = string.Empty;
            faceCode = string.Empty;
            slotCode = string.Empty;

            if (string.IsNullOrWhiteSpace(boxCode))
            {
                return false;
            }

            var parts = boxCode.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4)
            {
                return false;
            }

            string cabinetAndFace = parts[0].Trim();
            if (cabinetAndFace.Length < 2)
            {
                return false;
            }

            char faceToken = cabinetAndFace[^1];
            if (faceToken != 'A' && faceToken != 'a' && faceToken != 'B' && faceToken != 'b')
            {
                return false;
            }

            if (!int.TryParse(parts[1], out int layerIndex) || !int.TryParse(parts[2], out int columnIndex))
            {
                return false;
            }

            cabinetName = CabinetNameNormalizer.Normalize(cabinetAndFace[..^1]);
            faceCode = char.ToUpperInvariant(faceToken).ToString();
            slotCode = $"{layerIndex}-{columnIndex}";
            return true;
        }
    }
}
