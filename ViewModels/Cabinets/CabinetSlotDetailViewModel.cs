using DocMgr.Models.Cabinets;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace DocMgr.ViewModels.Cabinets
{
    /// <summary>
    /// 单档口详情窗体：以列表展示档口内全部档案盒或硬盘/光盘信息。
    /// </summary>
    public sealed class CabinetSlotDetailViewModel : ViewModelBase
    {
        public CabinetSlotDetailViewModel(
            CabinetOpenRequest request,
            CabinetSlotViewModel slot,
            IDialogService dialogService,
            bool canShowSlotZoom)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(slot);
            ArgumentNullException.ThrowIfNull(dialogService);

            _request = request;
            _slot = slot;
            _dialogService = dialogService;
            CanShowSlotZoom = canShowSlotZoom;

            string sideLabel = request.CabinetType == CabinetType.MagneticDisk ? "门别" : "面别";
            string sideDisplayName = ResolveSideDisplayName(request.CabinetType, request.Face);
            IsMagneticDiskCabinet = slot.IsMagneticDiskSlot;
            WindowTitle = $"档口详情 - {request.CabinetName} {sideDisplayName} {slot.SlotCode}";
            HeaderText = $"档口 {slot.SlotCode}";
            SummaryText = IsMagneticDiskCabinet
                ? BuildMagneticDiskSummary(request, sideLabel, sideDisplayName, slot)
                : BuildArchiveCabinetSummary(request, sideLabel, sideDisplayName, slot);
            ListHintText = IsMagneticDiskCabinet
                ? "双击列表行可查看电子介质袋内容或硬盘/光盘介质信息。"
                : "双击列表行可查看档案盒内容。";

            ArchiveBoxRows = new ObservableCollection<CabinetSlotDetailArchiveBoxRowViewModel>(
                slot.ArchiveBoxes
                    .OrderBy(box => box.SequenceIndex)
                    .Select(box => new CabinetSlotDetailArchiveBoxRowViewModel(box)));
            HardDiskRows = new ObservableCollection<CabinetSlotDetailHardDiskRowViewModel>(
                slot.HardDiskMediaItems
                    .Where(item => !item.IsEmpty)
                    .Select(item => new CabinetSlotDetailHardDiskRowViewModel(item, false))
                    .Concat(slot.PendingReturnMediaItems.Select(item => new CabinetSlotDetailHardDiskRowViewModel(item, true))));

            HasArchiveBoxRows = ArchiveBoxRows.Count > 0;
            HasHardDiskRows = HardDiskRows.Count > 0;
            EmptyHintText = IsMagneticDiskCabinet
                ? "当前档口暂无在位或待归还介质。"
                : "当前档口暂无档案盒。";

            ArchiveBoxListVisibility = IsMagneticDiskCabinet ? Visibility.Collapsed : Visibility.Visible;
            HardDiskListVisibility = IsMagneticDiskCabinet ? Visibility.Visible : Visibility.Collapsed;
            EmptyHintVisibility = (IsMagneticDiskCabinet ? HasHardDiskRows : HasArchiveBoxRows)
                ? Visibility.Collapsed
                : Visibility.Visible;
            SlotZoomButtonVisibility = canShowSlotZoom ? Visibility.Visible : Visibility.Collapsed;

            ShowSlotZoomCommand = new RelayCommand(_ => ShowSlotZoom(), _ => canShowSlotZoom);
            OpenSelectedArchiveBoxCommand = new RelayCommand<CabinetSlotDetailArchiveBoxRowViewModel>(OpenArchiveBoxDetail, row => row?.CanOpenDetail == true);
            OpenSelectedHardDiskCommand = new RelayCommand<CabinetSlotDetailHardDiskRowViewModel>(OpenHardDiskDetail, row => row?.CanOpenDetail == true);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        private readonly CabinetOpenRequest _request;
        private readonly CabinetSlotViewModel _slot;
        private readonly IDialogService _dialogService;

        public event Action<bool?>? RequestClose;

        public string WindowTitle { get; }

        public string HeaderText { get; }

        public string SummaryText { get; }

        public string ListHintText { get; }

        public string EmptyHintText { get; }

        public bool IsMagneticDiskCabinet { get; }

        public bool CanShowSlotZoom { get; }

        public bool HasArchiveBoxRows { get; }

        public bool HasHardDiskRows { get; }

        public Visibility ArchiveBoxListVisibility { get; }

        public Visibility HardDiskListVisibility { get; }

        public Visibility EmptyHintVisibility { get; }

        public Visibility SlotZoomButtonVisibility { get; }

        public ObservableCollection<CabinetSlotDetailArchiveBoxRowViewModel> ArchiveBoxRows { get; }

        public ObservableCollection<CabinetSlotDetailHardDiskRowViewModel> HardDiskRows { get; }

        public RelayCommand ShowSlotZoomCommand { get; }

        public RelayCommand<CabinetSlotDetailArchiveBoxRowViewModel> OpenSelectedArchiveBoxCommand { get; }

        public RelayCommand<CabinetSlotDetailHardDiskRowViewModel> OpenSelectedHardDiskCommand { get; }

        public RelayCommand CloseCommand { get; }

        public void OpenArchiveBoxDetail(CabinetSlotDetailArchiveBoxRowViewModel? row)
        {
            if (row == null || !row.CanOpenDetail)
            {
                return;
            }

            _dialogService.ShowCabinetArchiveBoxContentDialog(row.BoxCode);
        }

        public void OpenHardDiskDetail(CabinetSlotDetailHardDiskRowViewModel? row)
        {
            if (row == null || !row.CanOpenDetail)
            {
                return;
            }

            if (row.IsArchiveInfoPreferred)
            {
                if (row.ElectronicArchiveUnitId > 0)
                {
                    _dialogService.ShowCabinetElectronicBagContentDialog(row.ElectronicArchiveUnitId);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(row.ElectronicArchiveLocationText)
                    && !string.Equals(row.ElectronicArchiveLocationText, "—", StringComparison.Ordinal))
                {
                    _dialogService.ShowCabinetElectronicBagContentDialogByLocation(row.ElectronicArchiveLocationText);
                    return;
                }
            }

            string message = row.IsArchiveInfoPreferred && !string.IsNullOrWhiteSpace(row.ArchiveInfoText)
                ? row.ArchiveInfoText
                : row.InfoText;
            _dialogService.ShowMessage(message, row.IsArchiveInfoPreferred ? "电子介质袋资料" : "硬盘介质信息");
        }

        private void ShowSlotZoom()
        {
            if (!CanShowSlotZoom)
            {
                return;
            }

            _dialogService.ShowCabinetOpenDialog(BuildSlotZoomRequest(_request, _slot.SlotCode));
        }

        internal static CabinetOpenRequest BuildSlotZoomRequest(CabinetOpenRequest request, string slotCode)
        {
            return new CabinetOpenRequest
            {
                CabinetId = request.CabinetId,
                CabinetName = request.CabinetName,
                CabinetType = request.CabinetType,
                Face = request.Face,
                LayerCount = request.LayerCount,
                ColumnCount = request.ColumnCount,
                TargetSlotCode = slotCode,
                WidthCm = request.WidthCm,
                HeightCm = request.HeightCm,
                DepthCm = request.DepthCm
            };
        }

        private static string BuildArchiveCabinetSummary(CabinetOpenRequest request, string sideLabel, string sideDisplayName, CabinetSlotViewModel slot)
        {
            var lines = new List<string>
            {
                $"柜体：{request.CabinetName}",
                $"{sideLabel}：{sideDisplayName}",
                $"利用率：{slot.UtilizationText}",
                $"容量：{slot.CapacitySummaryText}",
                $"剩余：{slot.RemainingSummaryText}",
                $"布局：{slot.LayoutModeText}",
                $"档案盒：{slot.ArchiveBoxes.Count} 盒"
            };

            if (slot.MixedArchiveBoxCount > 0)
            {
                lines.Add($"混放盒：{slot.MixedArchiveBoxCount} 盒");
            }

            if (slot.PendingSortingRecordCount > 0)
            {
                lines.Add($"待梳理关联记录：{slot.PendingSortingRecordCount} 条");
            }

            if (slot.IsSpecialRule && !string.IsNullOrWhiteSpace(slot.SpecialRuleText))
            {
                lines.Add($"特例：{slot.SpecialRuleText}");
            }

            return string.Join(" · ", lines);
        }

        private static string BuildMagneticDiskSummary(CabinetOpenRequest request, string sideLabel, string sideDisplayName, CabinetSlotViewModel slot)
        {
            var lines = new List<string>
            {
                $"柜体：{request.CabinetName}",
                $"{sideLabel}：{sideDisplayName}",
                $"用途：{slot.PurposeDisplayText}",
                $"利用率：{slot.UtilizationText}",
                $"容量：{slot.CapacitySummaryText}",
                $"剩余：{slot.RemainingSummaryText}",
                $"布局：{slot.LayoutModeText}",
                $"在位介质：{slot.HardDiskPresentCount} 盘"
            };

            if (slot.PendingReturnMediumCount > 0)
            {
                lines.Add($"待归还：{slot.PendingReturnMediumCount} 盘");
            }

            return string.Join(" · ", lines);
        }

        private static string ResolveSideDisplayName(CabinetType cabinetType, CabinetFace face)
        {
            if (cabinetType == CabinetType.MagneticDisk)
            {
                return face switch
                {
                    CabinetFace.A => "左门",
                    CabinetFace.B => "右门",
                    _ => "左门"
                };
            }

            return face switch
            {
                CabinetFace.A => "A面",
                CabinetFace.B => "B面",
                _ => "A面"
            };
        }
    }
}
