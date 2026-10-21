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

        public void UpdateCabinet(Cabinet cabinet)
        {
            ArgumentNullException.ThrowIfNull(cabinet);
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            if (CabinetStructureChanged(cabinet))
            {
                EnsureCabinetEditKeepsStoredContent(cabinet);
            }
            else
            {
                EnsureMagneticSlotCapacitiesAllowCurrentOccupancy(cabinet);
            }

            _cabinetRepository.Update(cabinet);
            _cabinetRepository.SaveChanges();
        }

        public void AddCabinet(Cabinet cabinet)
        {
            ArgumentNullException.ThrowIfNull(cabinet);
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            EnsureCabinetEditKeepsStoredContent(cabinet);
            NormalizeMagneticSlotCapacities(cabinet);
            _cabinetRepository.Add(cabinet);
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
        public string? GetHardDiskDedicatedSlotCategoryName(int cabinetId, string faceCode, string slotCode)
        {
            if (string.IsNullOrWhiteSpace(faceCode) || string.IsNullOrWhiteSpace(slotCode))
            {
                return null;
            }

            var existing = _cabinetRepository.GetSlotCategoryAssignment(
                cabinetId,
                faceCode.Trim(),
                slotCode.Trim());
            string normalized = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(existing?.CategoryName);
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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

        /// <inheritdoc />
        public bool HasStoredContent(int cabinetId)
        {
            Cabinet? cabinet = _cabinetRepository.GetById(cabinetId);
            if (cabinet == null || string.IsNullOrWhiteSpace(cabinet.Name))
            {
                return false;
            }

            return _cabinetRepository.HasStoredContent(cabinet.Name);
        }

        /// <inheritdoc />
        public IReadOnlyList<CabinetStoredContentSlot> GetStoredContentSlots(int cabinetId)
        {
            Cabinet? cabinet = _cabinetRepository.GetById(cabinetId);
            if (cabinet == null || string.IsNullOrWhiteSpace(cabinet.Name))
            {
                return Array.Empty<CabinetStoredContentSlot>();
            }

            return _cabinetRepository.GetStoredContentSlots(cabinet.Name);
        }

        /// <inheritdoc />
        public void EnsureCabinetEditKeepsStoredContent(Cabinet proposed)
        {
            ArgumentNullException.ThrowIfNull(proposed);
            string proposedName = proposed.Name?.Trim() ?? string.Empty;
            if (proposed.Id <= 0)
            {
                EnsureCabinetNameIsUnique(proposedName, 0);
                return;
            }

            CabinetStructureSnapshot? baseline = _cabinetRepository.GetStructureSnapshot(proposed.Id);
            if (baseline == null)
            {
                throw new InvalidOperationException("未找到要保存的资料柜。");
            }

            var issues = new List<string>();
            IReadOnlyList<CabinetStoredContentSlot> slots = _cabinetRepository.GetStoredContentSlots(baseline.Name);
            bool hasContent = slots.Count > 0;
            bool nameChanged = !CabinetNameNormalizer.AreSame(baseline.Name, proposedName);

            if (hasContent && nameChanged)
            {
                issues.Add($"不能将柜体编号从「{baseline.Name}」改为「{proposedName}」。已有存放位置按原编号书写，改号后这些记录将与柜体脱节。本次保存不会改写已有存放记录。");
            }

            if (hasContent && baseline.Type != proposed.Type)
            {
                issues.Add($"不能将柜体种类从「{FormatCabinetType(baseline.Type)}」改为「{FormatCabinetType(proposed.Type)}」。种类不同，已有存放无法按新柜种对应，会造成数据矛盾。");
            }

            if (hasContent)
            {
                List<CabinetStoredContentSlot> dropped = slots
                    .Where(slot => !SlotStaysInsideCabinet(slot, proposed))
                    .ToList();
                if (dropped.Count > 0)
                {
                    issues.Add(
                        "下列档口已有内容，但编辑后的柜体不再包含这些档口，会造成存放记录丢失或不完整："
                        + Environment.NewLine
                        + string.Join(Environment.NewLine, dropped.Select(slot => slot.DisplayText)));
                }
            }

            if (nameChanged && IsCabinetNameTaken(proposedName, proposed.Id))
            {
                issues.Add($"编号「{proposedName}」已被其他柜体使用。两个柜体共用编号后，存放位置将无法区分属于哪一柜。");
            }

            if (proposed.Type == CabinetType.MagneticDisk)
            {
                try
                {
                    EnsureMagneticSlotCapacitiesAllowCurrentOccupancy(proposed);
                }
                catch (InvalidOperationException ex)
                {
                    issues.Add(ex.Message);
                }
            }
            else
            {
                NormalizeMagneticSlotCapacities(proposed);
            }

            if (issues.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine + Environment.NewLine, issues));
            }
        }

        private bool CabinetStructureChanged(Cabinet proposed)
        {
            if (proposed.Id <= 0)
            {
                return true;
            }

            CabinetStructureSnapshot? baseline = _cabinetRepository.GetStructureSnapshot(proposed.Id);
            if (baseline == null)
            {
                return true;
            }

            return !CabinetNameNormalizer.AreSame(baseline.Name, proposed.Name)
                || baseline.Type != proposed.Type
                || baseline.FaceCount != proposed.FaceCount
                || baseline.LayerCount != proposed.LayerCount
                || baseline.ColumnCount != proposed.ColumnCount
                || baseline.HardDiskSlotCapacity != proposed.HardDiskSlotCapacity
                || baseline.OpticalDiscSlotCapacity != proposed.OpticalDiscSlotCapacity;
        }

        private void EnsureCabinetNameIsUnique(string cabinetName, int cabinetId)
        {
            if (IsCabinetNameTaken(cabinetName, cabinetId))
            {
                throw new InvalidOperationException($"编号「{cabinetName}」已被其他柜体使用。两个柜体共用编号后，存放位置将无法区分属于哪一柜。");
            }
        }

        private bool IsCabinetNameTaken(string cabinetName, int cabinetId)
        {
            if (string.IsNullOrWhiteSpace(cabinetName))
            {
                return false;
            }

            return _cabinetRepository.GetCabinetNamesExcept(cabinetId)
                .Any(name => CabinetNameNormalizer.AreSame(name, cabinetName));
        }

        private static bool SlotStaysInsideCabinet(CabinetStoredContentSlot slot, Cabinet proposed)
        {
            int faceIndex = slot.FaceCode.Trim().ToUpperInvariant() switch
            {
                "A" => 0,
                "B" => 1,
                _ => -1
            };
            int faceCount = proposed.FaceCount <= 0 ? 1 : proposed.FaceCount;
            return faceIndex >= 0
                && faceIndex < faceCount
                && slot.LayerIndex >= 1
                && slot.LayerIndex <= proposed.LayerCount
                && slot.ColumnIndex >= 1
                && slot.ColumnIndex <= proposed.ColumnCount;
        }

        private static string FormatCabinetType(CabinetType type)
        {
            return type switch
            {
                CabinetType.Standard => "标准滑道式档案柜",
                CabinetType.Vertical => "立式文件柜",
                CabinetType.Horizontal => "卧式文件柜",
                CabinetType.MagneticDisk => "防磁磁盘柜",
                _ => type.ToString()
            };
        }

        public void DeleteCabinet(int cabinetId)
        {
            CabinetManagementPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            var cab = _cabinetRepository.GetById(cabinetId);
            if (cab == null)
            {
                return;
            }

            if (_cabinetRepository.HasStoredContent(cab.Name))
            {
                throw new InvalidOperationException($"资料柜 [{cab.Name}] 内存有资料或其他内容，不能删除。");
            }

            _cabinetRepository.Remove(cab);
            _cabinetRepository.SaveChanges();
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

        private static void NormalizeMagneticSlotCapacities(Cabinet cabinet)
        {
            ArgumentNullException.ThrowIfNull(cabinet);
            if (cabinet.Type != CabinetType.MagneticDisk)
            {
                cabinet.HardDiskSlotCapacity = CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity;
                cabinet.OpticalDiscSlotCapacity = CabinetHardDiskSlotCategoryAssignment.DedicatedOpticalDiscSlotCapacity;
                return;
            }

            if (cabinet.HardDiskSlotCapacity <= 0)
            {
                cabinet.HardDiskSlotCapacity = CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity;
            }

            if (cabinet.OpticalDiscSlotCapacity <= 0)
            {
                cabinet.OpticalDiscSlotCapacity = CabinetHardDiskSlotCategoryAssignment.DedicatedOpticalDiscSlotCapacity;
            }
        }

        private void EnsureMagneticSlotCapacitiesAllowCurrentOccupancy(Cabinet cabinet)
        {
            ArgumentNullException.ThrowIfNull(cabinet);
            NormalizeMagneticSlotCapacities(cabinet);
            if (cabinet.Type != CabinetType.MagneticDisk || cabinet.Id <= 0)
            {
                return;
            }

            var (maxHardDiskOccupancy, maxOpticalDiscOccupancy) = _cabinetRepository.GetMaxMagneticSlotOccupancy(cabinet);
            if (cabinet.HardDiskSlotCapacity < maxHardDiskOccupancy)
            {
                throw new InvalidOperationException(
                    $"硬盘档口最大容量不能小于当前已占用数（当前最高占用 {maxHardDiskOccupancy} 块/格，拟设为 {cabinet.HardDiskSlotCapacity}）。");
            }

            if (cabinet.OpticalDiscSlotCapacity < maxOpticalDiscOccupancy)
            {
                throw new InvalidOperationException(
                    $"光盘档口最大容量不能小于当前已占用数（当前最高占用 {maxOpticalDiscOccupancy} 张/格，拟设为 {cabinet.OpticalDiscSlotCapacity}）。");
            }
        }
    }
}
