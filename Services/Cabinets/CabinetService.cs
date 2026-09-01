using DocMgr.Models.Cabinets;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DocMgr.Services.Cabinets
{
    public class CabinetService : ICabinetService
    {
        private static readonly string[] DefaultCabinetNamePool =
        [
            "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸",
            "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥"
        ];

        private const double StandardTrackLeft = 560;
        private const double StandardTrackWidth = 18;
        private const double StandardTrackRight = 1082;
        private const double StandardTrackTop = 70;
        private const double StandardCabinetThickness = 40;
        private const double StandardCabinetLengthOverflow = 20;
        private const int DefaultStandardCabinetCount = 7;
        private const double DefaultMagneticCabinetLeft = 410;
        private const double DefaultMagneticCabinetTop = 150;
        private const double DefaultMagneticCabinetWidth = 70;
        private const double DefaultMagneticCabinetHeight = 150;
        private const double DefaultMagneticCabinetDepth = 52;

        private readonly ICabinetRepository _cabinetRepository;
        private readonly IUserContextService _userContextService;

        public CabinetService(ICabinetRepository cabinetRepository, IUserContextService userContextService)
        {
            _cabinetRepository = cabinetRepository;
            _userContextService = userContextService;
        }

        public List<Cabinet> GetAllCabinets()
        {
            EnsureDefaultCabinets();
            return _cabinetRepository.GetAll();
        }

        public Cabinet? GetCabinet(int cabinetId)
        {
            return _cabinetRepository.GetById(cabinetId);
        }

        public void AddCabinet(Cabinet cabinet)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            _cabinetRepository.Add(cabinet);
            _cabinetRepository.SaveChanges();
        }

        public void UpdateCabinet(Cabinet cabinet)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            _cabinetRepository.Update(cabinet);
            _cabinetRepository.SaveChanges();
        }

        /// <inheritdoc/>
        public void SetHardDiskDedicatedSlotCategory(int cabinetId, string faceCode, string slotCode, string categoryName)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            if (string.IsNullOrWhiteSpace(faceCode))
            {
                throw new ArgumentException("门别不能为空。", nameof(faceCode));
            }

            if (string.IsNullOrWhiteSpace(slotCode))
            {
                throw new ArgumentException("档口编号不能为空。", nameof(slotCode));
            }

            if (string.IsNullOrWhiteSpace(categoryName))
            {
                throw new ArgumentException("专用类别不能为空。", nameof(categoryName));
            }

            var target = _cabinetRepository.GetById(cabinetId);
            if (target == null)
            {
                throw new InvalidOperationException("未找到要设置的防磁磁盘柜。");
            }

            if (target.Type != CabinetType.MagneticDisk)
            {
                throw new InvalidOperationException("仅防磁磁盘柜支持设置硬盘专用档口。");
            }

            string trimmedFaceCode = faceCode.Trim();
            string trimmedSlotCode = slotCode.Trim();
            string trimmedCategoryName = categoryName.Trim();
            EnsureMagneticDiskSlotIsEmptyForCategoryChange(target, trimmedFaceCode, trimmedSlotCode);

            var existing = _cabinetRepository.GetSlotCategoryAssignment(cabinetId, trimmedFaceCode, trimmedSlotCode);

            DateTime now = DateTime.Now;
            if (existing == null)
            {
                _cabinetRepository.AddSlotCategoryAssignment(new CabinetHardDiskSlotCategoryAssignment
                {
                    CabinetId = cabinetId,
                    FaceCode = trimmedFaceCode,
                    SlotCode = trimmedSlotCode,
                    CategoryName = trimmedCategoryName,
                    CreatedTime = now,
                    UpdatedTime = now
                });
            }
            else
            {
                existing.CategoryName = trimmedCategoryName;
                existing.UpdatedTime = now;
            }

            _cabinetRepository.SaveChanges();
        }

        /// <inheritdoc/>
        public void ClearHardDiskDedicatedSlotCategory(int cabinetId, string faceCode, string slotCode)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            if (string.IsNullOrWhiteSpace(faceCode))
            {
                throw new ArgumentException("门别不能为空。", nameof(faceCode));
            }

            if (string.IsNullOrWhiteSpace(slotCode))
            {
                throw new ArgumentException("档口编号不能为空。", nameof(slotCode));
            }

            var target = _cabinetRepository.GetById(cabinetId);
            if (target == null)
            {
                throw new InvalidOperationException("未找到要设置的防磁磁盘柜。");
            }

            if (target.Type != CabinetType.MagneticDisk)
            {
                throw new InvalidOperationException("仅防磁磁盘柜支持设置硬盘专用档口。");
            }

            string trimmedFaceCode = faceCode.Trim();
            string trimmedSlotCode = slotCode.Trim();
            EnsureMagneticDiskSlotIsEmptyForCategoryChange(target, trimmedFaceCode, trimmedSlotCode);
            var existing = _cabinetRepository.GetSlotCategoryAssignment(cabinetId, trimmedFaceCode, trimmedSlotCode);

            if (existing != null)
            {
                _cabinetRepository.RemoveSlotCategoryAssignment(existing);
            }

            _cabinetRepository.SaveChanges();
        }

        private void EnsureMagneticDiskSlotIsEmptyForCategoryChange(Cabinet cabinet, string faceCode, string slotCode)
        {
            if (_cabinetRepository.HasInStockMediaInMagneticDiskSlot(cabinet.Name, faceCode, slotCode))
            {
                throw new InvalidOperationException($"档口 {faceCode} {slotCode} 仍有介质占用，仅可对空档口变更用途。");
            }
        }

        /// <inheritdoc/>
        public void EnsureAllMagneticDiskSlotsUseBlankCategoryOnStartup()
            => ApplyMagneticDiskSlotBlankCategoryDefaults();

        private void ApplyMagneticDiskSlotBlankCategoryDefaults()
        {
            EnsureDefaultCabinets();

            string blankCategory = CabinetHardDiskSlotCategoryAssignment.CategoryBlank;
            var magneticCabinets = _cabinetRepository.GetAll()
                .Where(item => item.Type == CabinetType.MagneticDisk)
                .Where(item => item.LayerCount > 0 && item.ColumnCount > 0)
                .ToList();

            if (magneticCabinets.Count == 0)
            {
                return;
            }

            DateTime now = DateTime.Now;
            bool changed = false;

            foreach (var cabinet in magneticCabinets)
            {
                var assignmentLookup = _cabinetRepository
                    .GetSlotCategoryAssignmentsByCabinetId(cabinet.Id)
                    .ToDictionary(
                        item => BuildSlotCategoryKey(item.FaceCode, item.SlotCode),
                        item => item,
                        StringComparer.OrdinalIgnoreCase);

                foreach (string faceCode in ResolveFaceCodes(cabinet.FaceCount))
                {
                    for (int layer = 1; layer <= cabinet.LayerCount; layer++)
                    {
                        for (int column = 1; column <= cabinet.ColumnCount; column++)
                        {
                            string slotCode = $"{layer}-{column}";
                            string key = BuildSlotCategoryKey(faceCode, slotCode);
                            if (assignmentLookup.ContainsKey(key))
                            {
                                continue;
                            }

                            _cabinetRepository.AddSlotCategoryAssignment(new CabinetHardDiskSlotCategoryAssignment
                            {
                                CabinetId = cabinet.Id,
                                FaceCode = faceCode,
                                SlotCode = slotCode,
                                CategoryName = blankCategory,
                                CreatedTime = now,
                                UpdatedTime = now
                            });
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                _cabinetRepository.SaveChanges();
            }
        }

        /// <inheritdoc/>
        public void SetArchiveDedicatedSlotCategory(int cabinetId, string faceCode, string slotCode, string categoryName)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            if (string.IsNullOrWhiteSpace(faceCode))
            {
                throw new ArgumentException("门别不能为空。", nameof(faceCode));
            }

            if (string.IsNullOrWhiteSpace(slotCode))
            {
                throw new ArgumentException("档口编号不能为空。", nameof(slotCode));
            }

            if (string.IsNullOrWhiteSpace(categoryName))
            {
                throw new ArgumentException("档口用途不能为空。", nameof(categoryName));
            }

            string trimmedCategoryName = CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(categoryName);
            if (!CabinetArchiveSlotCategoryAssignment.IsKnownCategory(trimmedCategoryName))
            {
                throw new ArgumentException($"不支持的档口用途：{categoryName.Trim()}。", nameof(categoryName));
            }

            var target = _cabinetRepository.GetById(cabinetId);
            if (target == null)
            {
                throw new InvalidOperationException("未找到要设置的标准滑道式档案柜。");
            }

            if (target.Type != CabinetType.Standard)
            {
                throw new InvalidOperationException("仅标准滑道式档案柜支持设置档案盒档口用途。");
            }

            string trimmedFaceCode = faceCode.Trim();
            string trimmedSlotCode = slotCode.Trim();
            EnsureStandardArchiveSlotIsEmptyForCategoryChange(target, trimmedFaceCode, trimmedSlotCode);
            UpsertArchiveSlotCategoryAssignment(cabinetId, trimmedFaceCode, trimmedSlotCode, trimmedCategoryName);
            _cabinetRepository.SaveChanges();
        }

        /// <inheritdoc/>
        public void PromoteUnsetArchiveSlotToHistoricalMaterials(int cabinetId, string faceCode, string slotCode)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            if (string.IsNullOrWhiteSpace(faceCode))
            {
                throw new ArgumentException("门别不能为空。", nameof(faceCode));
            }

            if (string.IsNullOrWhiteSpace(slotCode))
            {
                throw new ArgumentException("档口编号不能为空。", nameof(slotCode));
            }

            var target = _cabinetRepository.GetById(cabinetId);
            if (target == null)
            {
                throw new InvalidOperationException("未找到要设置的标准滑道式档案柜。");
            }

            if (target.Type != CabinetType.Standard)
            {
                throw new InvalidOperationException("仅标准滑道式档案柜支持设置档案盒档口用途。");
            }

            string trimmedFaceCode = faceCode.Trim();
            string trimmedSlotCode = slotCode.Trim();
            var existing = _cabinetRepository.GetArchiveSlotCategoryAssignment(cabinetId, trimmedFaceCode, trimmedSlotCode);
            string normalized = CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(existing?.CategoryName);
            if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetArchiveSlotCategoryAssignment.CategoryHistoricalMaterials)
                || CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetArchiveSlotCategoryAssignment.CategoryMixed))
            {
                return;
            }

            if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetArchiveSlotCategoryAssignment.CategoryYearlyMaterials))
            {
                throw new InvalidOperationException(
                    $"档口 {trimmedFaceCode} {trimmedSlotCode} 为年度资料专用档口，不能改为历史资料专用档口。");
            }

            UpsertArchiveSlotCategoryAssignment(
                cabinetId,
                trimmedFaceCode,
                trimmedSlotCode,
                CabinetArchiveSlotCategoryAssignment.CategoryHistoricalMaterials);
            _cabinetRepository.SaveChanges();
        }

        /// <inheritdoc/>
        public void PromoteUnsetArchiveSlotToYearlyMaterials(int cabinetId, string faceCode, string slotCode)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            var (target, trimmedFaceCode, trimmedSlotCode) = RequireStandardArchiveSlot(cabinetId, faceCode, slotCode);
            _ = target;
            var existing = _cabinetRepository.GetArchiveSlotCategoryAssignment(cabinetId, trimmedFaceCode, trimmedSlotCode);
            string normalized = CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(existing?.CategoryName);
            if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetArchiveSlotCategoryAssignment.CategoryYearlyMaterials)
                || CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetArchiveSlotCategoryAssignment.CategoryMixed))
            {
                return;
            }

            if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetArchiveSlotCategoryAssignment.CategoryHistoricalMaterials))
            {
                throw new InvalidOperationException(
                    $"档口 {trimmedFaceCode} {trimmedSlotCode} 为历史资料专用档口，不能改为年度资料专用档口。");
            }

            UpsertArchiveSlotCategoryAssignment(
                cabinetId,
                trimmedFaceCode,
                trimmedSlotCode,
                CabinetArchiveSlotCategoryAssignment.CategoryYearlyMaterials);
            _cabinetRepository.SaveChanges();
        }

        /// <inheritdoc/>
        public void PromoteArchiveSlotToMixedUse(int cabinetId, string faceCode, string slotCode)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            var (target, trimmedFaceCode, trimmedSlotCode) = RequireStandardArchiveSlot(cabinetId, faceCode, slotCode);
            _ = target;
            var existing = _cabinetRepository.GetArchiveSlotCategoryAssignment(cabinetId, trimmedFaceCode, trimmedSlotCode);
            string normalized = CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(existing?.CategoryName);
            if (CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                    normalized,
                    CabinetArchiveSlotCategoryAssignment.CategoryMixed))
            {
                return;
            }

            UpsertArchiveSlotCategoryAssignment(
                cabinetId,
                trimmedFaceCode,
                trimmedSlotCode,
                CabinetArchiveSlotCategoryAssignment.CategoryMixed);
            _cabinetRepository.SaveChanges();
        }

        private (Cabinet Cabinet, string FaceCode, string SlotCode) RequireStandardArchiveSlot(
            int cabinetId,
            string faceCode,
            string slotCode)
        {
            if (string.IsNullOrWhiteSpace(faceCode))
            {
                throw new ArgumentException("门别不能为空。", nameof(faceCode));
            }

            if (string.IsNullOrWhiteSpace(slotCode))
            {
                throw new ArgumentException("档口编号不能为空。", nameof(slotCode));
            }

            var target = _cabinetRepository.GetById(cabinetId);
            if (target == null)
            {
                throw new InvalidOperationException("未找到要设置的标准滑道式档案柜。");
            }

            if (target.Type != CabinetType.Standard)
            {
                throw new InvalidOperationException("仅标准滑道式档案柜支持设置档案盒档口用途。");
            }

            return (target, faceCode.Trim(), slotCode.Trim());
        }

        /// <inheritdoc/>
        public void ClearArchiveDedicatedSlotCategory(int cabinetId, string faceCode, string slotCode)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            if (string.IsNullOrWhiteSpace(faceCode))
            {
                throw new ArgumentException("门别不能为空。", nameof(faceCode));
            }

            if (string.IsNullOrWhiteSpace(slotCode))
            {
                throw new ArgumentException("档口编号不能为空。", nameof(slotCode));
            }

            var target = _cabinetRepository.GetById(cabinetId);
            if (target == null)
            {
                throw new InvalidOperationException("未找到要设置的标准滑道式档案柜。");
            }

            if (target.Type != CabinetType.Standard)
            {
                throw new InvalidOperationException("仅标准滑道式档案柜支持重设档案盒档口用途。");
            }

            string trimmedFaceCode = faceCode.Trim();
            string trimmedSlotCode = slotCode.Trim();
            EnsureStandardArchiveSlotIsEmptyForCategoryChange(target, trimmedFaceCode, trimmedSlotCode);
            UpsertArchiveSlotCategoryAssignment(
                cabinetId,
                trimmedFaceCode,
                trimmedSlotCode,
                CabinetArchiveSlotCategoryAssignment.CategoryUnset);
            _cabinetRepository.SaveChanges();
        }

        private void EnsureStandardArchiveSlotIsEmptyForCategoryChange(Cabinet cabinet, string faceCode, string slotCode)
        {
            if (_cabinetRepository.HasArchiveBoxesInStandardSlot(cabinet.Name, faceCode, slotCode))
            {
                throw new InvalidOperationException($"档口 {faceCode} {slotCode} 仍有档案盒占用，仅可对空档口变更用途。");
            }
        }

        private void UpsertArchiveSlotCategoryAssignment(int cabinetId, string faceCode, string slotCode, string categoryName)
        {
            var existing = _cabinetRepository.GetArchiveSlotCategoryAssignment(cabinetId, faceCode, slotCode);
            DateTime now = DateTime.Now;
            if (existing == null)
            {
                _cabinetRepository.AddArchiveSlotCategoryAssignment(new CabinetArchiveSlotCategoryAssignment
                {
                    CabinetId = cabinetId,
                    FaceCode = faceCode,
                    SlotCode = slotCode,
                    CategoryName = categoryName,
                    CreatedTime = now,
                    UpdatedTime = now
                });
                return;
            }

            existing.CategoryName = categoryName;
            existing.UpdatedTime = now;
        }

        /// <inheritdoc/>
        public void EnsureAllStandardArchiveSlotsUseUnsetCategoryOnStartup()
            => ApplyStandardArchiveSlotUnsetCategoryDefaults();

        private void ApplyStandardArchiveSlotUnsetCategoryDefaults()
        {
            EnsureDefaultCabinets();

            string unsetCategory = CabinetArchiveSlotCategoryAssignment.CategoryUnset;
            var standardCabinets = _cabinetRepository.GetAll()
                .Where(item => item.Type == CabinetType.Standard)
                .Where(item => item.LayerCount > 0 && item.ColumnCount > 0)
                .ToList();

            if (standardCabinets.Count == 0)
            {
                return;
            }

            DateTime now = DateTime.Now;
            bool changed = false;

            foreach (var cabinet in standardCabinets)
            {
                var assignmentLookup = _cabinetRepository
                    .GetArchiveSlotCategoryAssignmentsByCabinetId(cabinet.Id)
                    .ToDictionary(
                        item => BuildSlotCategoryKey(item.FaceCode, item.SlotCode),
                        item => item,
                        StringComparer.OrdinalIgnoreCase);

                foreach (string faceCode in ResolveFaceCodes(cabinet.FaceCount))
                {
                    for (int layer = 1; layer <= cabinet.LayerCount; layer++)
                    {
                        for (int column = 1; column <= cabinet.ColumnCount; column++)
                        {
                            string slotCode = $"{layer}-{column}";
                            string key = BuildSlotCategoryKey(faceCode, slotCode);
                            if (assignmentLookup.ContainsKey(key))
                            {
                                continue;
                            }

                            _cabinetRepository.AddArchiveSlotCategoryAssignment(new CabinetArchiveSlotCategoryAssignment
                            {
                                CabinetId = cabinet.Id,
                                FaceCode = faceCode,
                                SlotCode = slotCode,
                                CategoryName = unsetCategory,
                                CreatedTime = now,
                                UpdatedTime = now
                            });
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                _cabinetRepository.SaveChanges();
            }
        }

        public void DeleteCabinet(int cabinetId)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            var cab = _cabinetRepository.GetById(cabinetId);
            if (cab != null)
            {
                _cabinetRepository.Remove(cab);
                _cabinetRepository.SaveChanges();
            }
        }
        public async Task<List<Cabinet>> GetAllCabinetsAsync()
        {
            await EnsureDefaultCabinetsAsync();
            return await _cabinetRepository.GetAllAsync();
        }

        private void EnsureDefaultCabinets()
        {
            if (_cabinetRepository.Any())
            {
                return;
            }

            _cabinetRepository.AddRange(CreateDefaultCabinets());
            _cabinetRepository.SaveChanges();
        }

        private async Task EnsureDefaultCabinetsAsync()
        {
            if (await _cabinetRepository.AnyAsync())
            {
                return;
            }

            _cabinetRepository.AddRange(CreateDefaultCabinets());
            await _cabinetRepository.SaveChangesAsync();
        }

        private static List<Cabinet> CreateDefaultCabinets()
        {
            var generatedCabinets = new List<Cabinet>();

            for (int i = 0; i < DefaultStandardCabinetCount; i++)
            {
                generatedCabinets.Add(new Cabinet
                {
                    Name = DefaultCabinetNamePool[i],
                    Type = CabinetType.Standard,
                    FaceCount = 2,
                    LayerCount = 6,
                    ColumnCount = 3,
                    Width = GetStandardCabinetWidth(),
                    Height = StandardCabinetThickness,
                    Depth = 25,
                    CanvasLeft = GetStandardCabinetLeft(),
                    CanvasTop = StandardTrackTop + (i * StandardCabinetThickness),
                    RotationAngle = 0
                });
            }

            generatedCabinets.Add(new Cabinet
            {
                Name = DefaultCabinetNamePool[DefaultStandardCabinetCount],
                Type = CabinetType.MagneticDisk,
                FaceCount = 1,
                LayerCount = 9,
                ColumnCount = 4,
                Width = DefaultMagneticCabinetWidth,
                Height = DefaultMagneticCabinetHeight,
                Depth = DefaultMagneticCabinetDepth,
                CanvasLeft = DefaultMagneticCabinetLeft,
                CanvasTop = DefaultMagneticCabinetTop,
                RotationAngle = 0
            });

            return generatedCabinets;
        }

        private static double GetStandardCabinetWidth()
        {
            double leftTrackCenter = StandardTrackLeft + (StandardTrackWidth / 2d);
            double rightTrackCenter = StandardTrackRight + (StandardTrackWidth / 2d);
            return (rightTrackCenter - leftTrackCenter) + StandardCabinetLengthOverflow;
        }

        private static double GetStandardCabinetLeft()
        {
            double leftTrackCenter = StandardTrackLeft + (StandardTrackWidth / 2d);
            return leftTrackCenter - (StandardCabinetLengthOverflow / 2d);
        }

        private static IEnumerable<string> ResolveFaceCodes(int faceCount)
        {
            int resolvedFaceCount = faceCount <= 1 ? 1 : faceCount;
            for (int index = 0; index < resolvedFaceCount; index++)
            {
                yield return ((char)('A' + index)).ToString();
            }
        }

        private static string BuildSlotCategoryKey(string faceCode, string slotCode)
            => $"{faceCode.Trim()}:{slotCode.Trim()}";
    }
}
