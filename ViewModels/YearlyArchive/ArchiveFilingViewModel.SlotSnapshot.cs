using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using DocMgr.Models.Cabinets;

namespace DocMgr.ViewModels.YearlyArchive
{
    public partial class ArchiveFilingViewModel
    {
        private static readonly HashSet<string> InvalidSlotSnapshotLocationPlaceholders = new(StringComparer.Ordinal)
        {
            "请先选择位置",
            "位置信息不全",
            "位置计算失败"
        };

        public bool CanShowSimulatedSlotSnapshot =>
            IsSimulatedTrack && TryResolveSimulatedSlotSnapshotContext(out _, out _, out _, out _);

        public bool CanShowElectronicSlotSnapshot =>
            IsElectronicTrack && TryResolveElectronicSlotSnapshotContext(out _, out _, out _, out _);

        public bool CanShowExternalHardDiskBlankTargetSlotSnapshot =>
            IsElectronicTrack
            && CanResolveSlotSnapshotLocation(ExternalHardDiskFormattedBlankTargetLocation, ElectronicCabinets);

        private void RaiseSlotSnapshotAvailabilityChanged()
        {
            OnPropertyChanged(nameof(CanShowSimulatedSlotSnapshot));
            OnPropertyChanged(nameof(CanShowElectronicSlotSnapshot));
            OnPropertyChanged(nameof(CanShowExternalHardDiskBlankTargetSlotSnapshot));
            OnPropertyChanged(nameof(ElectronicStepEightSlotSnapshotVisibility));
            CommandManager.InvalidateRequerySuggested();
        }

        private static bool IsMeaningfulSlotSnapshotLocation(string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            return !InvalidSlotSnapshotLocationPlaceholders.Contains(location.Trim());
        }

        private static bool TryParseSlotSnapshotLocation(
            string? location,
            out string cabinetName,
            out string side,
            out string row,
            out string column)
        {
            cabinetName = string.Empty;
            side = string.Empty;
            row = string.Empty;
            column = string.Empty;

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
            row = parts[1];
            column = parts[2];
            return true;
        }

        private static bool CanResolveSlotSnapshotLocation(string? location, IEnumerable<Cabinet> cabinets)
        {
            if (!IsMeaningfulSlotSnapshotLocation(location))
            {
                return false;
            }

            if (!TryParseSlotSnapshotLocation(location, out string cabinetName, out _, out _, out _))
            {
                return false;
            }

            return cabinets.Any(item => string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
        }

        private string? GetSimulatedSlotSnapshotLocationCandidate()
        {
            if (IsMeaningfulSlotSnapshotLocation(PhysicalCodeResult))
            {
                return PhysicalCodeResult.Trim();
            }

            if (!IsNewBoxMode
                && SelectedExistingBox != null
                && IsMeaningfulSlotSnapshotLocation(SelectedExistingBox.BoxLocationCode))
            {
                return SelectedExistingBox.BoxLocationCode.Trim();
            }

            return null;
        }

        private string? GetElectronicSlotSnapshotLocationCandidate()
        {
            if (IsMeaningfulSlotSnapshotLocation(ElectronicStorageLocation))
            {
                return ElectronicStorageLocation.Trim();
            }

            if (IsMeaningfulSlotSnapshotLocation(ElectronicOriginalStorageLocation))
            {
                return ElectronicOriginalStorageLocation.Trim();
            }

            if (!IsNewBoxMode
                && SelectedExistingElectronicUnit != null
                && IsMeaningfulSlotSnapshotLocation(SelectedExistingElectronicUnit.StorageLocation))
            {
                return SelectedExistingElectronicUnit.StorageLocation.Trim();
            }

            return null;
        }

        private bool TryResolveSimulatedSlotSnapshotContext(
            out Cabinet cabinet,
            out string side,
            out string row,
            out string column)
        {
            cabinet = null!;
            side = string.Empty;
            row = string.Empty;
            column = string.Empty;

            string? location = GetSimulatedSlotSnapshotLocationCandidate();
            if (CanResolveSlotSnapshotLocation(location, Cabinets)
                && TryParseSlotSnapshotLocation(location, out string cabinetName, out side, out row, out column))
            {
                var matchedCabinet = Cabinets.FirstOrDefault(item => string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
                if (matchedCabinet != null)
                {
                    cabinet = matchedCabinet;
                    return true;
                }
            }

            if (SelectedCabinet != null
                && !string.IsNullOrWhiteSpace(SelectedRow)
                && !string.IsNullOrWhiteSpace(SelectedColumn))
            {
                cabinet = SelectedCabinet;
                side = SelectedSide;
                row = SelectedRow;
                column = SelectedColumn;
                return true;
            }

            return false;
        }

        private bool TryResolveElectronicSlotSnapshotContext(
            out Cabinet cabinet,
            out string side,
            out string row,
            out string column)
        {
            cabinet = null!;
            side = string.Empty;
            row = string.Empty;
            column = string.Empty;

            string? location = GetElectronicSlotSnapshotLocationCandidate();
            if (CanResolveSlotSnapshotLocation(location, ElectronicCabinets)
                && TryParseSlotSnapshotLocation(location, out string cabinetName, out side, out row, out column))
            {
                var matchedCabinet = ElectronicCabinets.FirstOrDefault(item => string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
                if (matchedCabinet != null)
                {
                    cabinet = matchedCabinet;
                    return true;
                }
            }

            if (SelectedElectronicCabinet != null
                && !string.IsNullOrWhiteSpace(SelectedElectronicRow)
                && !string.IsNullOrWhiteSpace(SelectedElectronicColumn))
            {
                cabinet = SelectedElectronicCabinet;
                side = SelectedElectronicSide;
                row = SelectedElectronicRow;
                column = SelectedElectronicColumn;
                return true;
            }

            return false;
        }

        private void ShowSlotSnapshot(Cabinet cabinet, string side, string row, string column)
        {
            CabinetFace face = string.Equals(side, "B", StringComparison.OrdinalIgnoreCase)
                ? CabinetFace.B
                : CabinetFace.A;

            _dialogService.ShowCabinetOpenDialog(new CabinetOpenRequest
            {
                CabinetId = cabinet.Id,
                CabinetName = cabinet.Name,
                CabinetType = cabinet.Type,
                Face = face,
                LayerCount = cabinet.LayerCount,
                ColumnCount = cabinet.ColumnCount,
                TargetSlotCode = $"{row}-{column}",
                WidthCm = cabinet.Width,
                HeightCm = cabinet.Height,
                DepthCm = cabinet.Depth
            });
        }

        private bool TryShowSlotSnapshotByLocation(string? location, IEnumerable<Cabinet> cabinets)
        {
            if (!CanResolveSlotSnapshotLocation(location, cabinets)
                || !TryParseSlotSnapshotLocation(location, out string cabinetName, out string side, out string row, out string column))
            {
                return false;
            }

            var targetCabinet = cabinets.FirstOrDefault(item => string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (targetCabinet == null)
            {
                return false;
            }

            ShowSlotSnapshot(targetCabinet, side, row, column);
            return true;
        }
    }
}
