using DocMgr.Models.Cabinets;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.Cabinets
{
    /// <summary>
    /// 档案盒摆放服务：直接读写历史/年度档案盒实体的放置方式与规格。
    /// </summary>
    public class CabinetArchiveBoxPlacementService : ICabinetArchiveBoxPlacementService
    {
        private const string SpineOut = "SpineOut";
        private const string FrontOut = "FrontOut";

        private readonly ICabinetArchiveBoxPlacementRepository _placementRepository;
        private readonly IUserContextService _userContextService;

        public CabinetArchiveBoxPlacementService(
            ICabinetArchiveBoxPlacementRepository placementRepository,
            IUserContextService userContextService)
        {
            ArgumentNullException.ThrowIfNull(placementRepository);
            ArgumentNullException.ThrowIfNull(userContextService);
            _placementRepository = placementRepository;
            _userContextService = userContextService;
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

            string normalizedBoxCode = boxCode.Trim();

            var yearlyBox = _placementRepository.GetYearlyArchiveBoxByLocationCode(normalizedBoxCode);
            if (yearlyBox != null)
            {
                return ParsePlacementMode(yearlyBox.PlacementMode);
            }

            var historyBox = _placementRepository.GetHistoryArchiveBoxByCode(normalizedBoxCode);
            return ParsePlacementMode(historyBox?.PlacementMode);
        }

        /// <summary>
        /// 批量更新指定柜体、面别、档口下所有档案盒（历史盒与年度盒）的放置方式。
        /// </summary>
        public int UpdateSlotPlacementMode(string cabinetName, string faceCode, string slotCode, CabinetArchiveBoxPlacementMode placementMode, string updatedBy)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            if (string.IsNullOrWhiteSpace(cabinetName))
            {
                throw new ArgumentException("柜体名称不能为空。", nameof(cabinetName));
            }

            if (string.IsNullOrWhiteSpace(faceCode))
            {
                throw new ArgumentException("面别不能为空。", nameof(faceCode));
            }

            if (string.IsNullOrWhiteSpace(slotCode))
            {
                throw new ArgumentException("档口编号不能为空。", nameof(slotCode));
            }

            string placementModeText = ToStorageValue(placementMode);

            var yearlyBoxes = _placementRepository.GetInUseYearlyArchiveBoxesBySlot(cabinetName, faceCode, slotCode);
            foreach (var yearlyBox in yearlyBoxes)
            {
                yearlyBox.PlacementMode = placementModeText;
            }

            var historyBoxes = _placementRepository.GetInStockHistoryArchiveBoxesBySlot(cabinetName, faceCode, slotCode);
            foreach (var historyBox in historyBoxes)
            {
                historyBox.PlacementMode = placementModeText;
            }

            int updatedCount = yearlyBoxes.Count + historyBoxes.Count;
            if (updatedCount > 0)
            {
                _placementRepository.SaveChanges();
            }

            return updatedCount;
        }

        /// <summary>
        /// 更新单个档案盒的放置方式（优先匹配年度盒，其次历史盒）。
        /// </summary>
        public bool UpdateBoxPlacementMode(string boxCode, CabinetArchiveBoxPlacementMode placementMode, string updatedBy)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                throw new ArgumentException("档案盒编号不能为空。", nameof(boxCode));
            }

            string normalizedBoxCode = boxCode.Trim();
            string placementModeText = ToStorageValue(placementMode);

            var yearlyBox = _placementRepository.GetYearlyArchiveBoxByLocationCode(normalizedBoxCode);
            if (yearlyBox != null)
            {
                yearlyBox.PlacementMode = placementModeText;
                _placementRepository.SaveChanges();
                return true;
            }

            var historyBox = _placementRepository.GetHistoryArchiveBoxByCode(normalizedBoxCode);
            if (historyBox != null)
            {
                historyBox.PlacementMode = placementModeText;
                _placementRepository.SaveChanges();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取可供设置的档案盒规格列表。
        /// </summary>
        public IReadOnlyList<string> GetAvailableBoxSpecifications()
        {
            var names = _placementRepository.GetArchiveBoxSpecifications()
                .Select(item => item.Name?.Trim() ?? string.Empty)
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
        /// 为单个档案盒设置规格（年度盒写 Specs，历史盒写 BoxSpecification）。
        /// </summary>
        public bool ResetBoxSpecification(string boxCode, string boxSpecification, string updatedBy)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
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

            var yearlyBox = _placementRepository.GetYearlyArchiveBoxByLocationCode(normalizedBoxCode);
            if (yearlyBox != null)
            {
                yearlyBox.Specs = normalizedSpecification;
                _placementRepository.SaveChanges();
                return true;
            }

            var historyBox = _placementRepository.GetHistoryArchiveBoxByCode(normalizedBoxCode);
            if (historyBox != null)
            {
                historyBox.BoxSpecification = normalizedSpecification;
                _placementRepository.SaveChanges();
                return true;
            }

            return false;
        }

        private static CabinetArchiveBoxPlacementMode ParsePlacementMode(string? placementMode)
        {
            return string.Equals(placementMode?.Trim(), FrontOut, StringComparison.OrdinalIgnoreCase)
                ? CabinetArchiveBoxPlacementMode.FrontOut
                : CabinetArchiveBoxPlacementMode.SpineOut;
        }

        private static string ToStorageValue(CabinetArchiveBoxPlacementMode placementMode)
        {
            return placementMode == CabinetArchiveBoxPlacementMode.FrontOut ? FrontOut : SpineOut;
        }
    }
}
