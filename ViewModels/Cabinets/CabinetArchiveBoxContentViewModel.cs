using DocMgr.Models.Cabinets;

using DocMgr.ViewModels.Base;

using DocMgr.ViewModels.Shared;

using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.Linq;

using System.Windows;



namespace DocMgr.ViewModels.Cabinets

{

    public class CabinetArchiveBoxContentViewModel : ViewModelBase

    {

        public CabinetArchiveBoxContentViewModel(

            string locationCode,

            IReadOnlyList<CabinetArchiveBoxContentDescriptor> contents,

            CabinetArchiveContainerViewMode viewMode,

            CabinetElectronicArchiveBagHeader? electronicBagHeader = null,

            CabinetArchiveContainerOccupationLockSummary? occupationLockSummary = null)

        {

            if (string.IsNullOrWhiteSpace(locationCode))

            {

                throw new ArgumentException("位置编号不能为空。", nameof(locationCode));

            }



            ArgumentNullException.ThrowIfNull(contents);



            ViewMode = viewMode;

            LocationCode = locationCode;

            ElectronicBagHeader = electronicBagHeader;

            IsMixedPlacement = contents.Any(item => item.IsMixedPlacement);

            HasYearlyArchiveMediaItems = viewMode != CabinetArchiveContainerViewMode.HistoryArchiveBox

                || contents.Any(item => item.IsYearlyArchiveMediaItem);



            WindowTitle = BuildWindowTitle(locationCode, viewMode, electronicBagHeader);

            HeaderText = BuildHeaderText(locationCode, viewMode, electronicBagHeader, IsMixedPlacement);

            SummaryText = BuildSummaryText(contents, viewMode, IsMixedPlacement, HasYearlyArchiveMediaItems);

            YearlyArchiveHintText = BuildHintText(viewMode);

            BagHeaderText = BuildBagHeaderText(electronicBagHeader);

            BagHeaderVisibility = viewMode == CabinetArchiveContainerViewMode.ElectronicArchiveBag

                && !string.IsNullOrWhiteSpace(BagHeaderText)

                    ? Visibility.Visible

                    : Visibility.Collapsed;



            SimulatedGridVisibility = viewMode == CabinetArchiveContainerViewMode.SimulatedArchiveBox

                ? Visibility.Visible

                : Visibility.Collapsed;

            ElectronicGridVisibility = viewMode == CabinetArchiveContainerViewMode.ElectronicArchiveBag

                ? Visibility.Visible

                : Visibility.Collapsed;

            HistoryGridVisibility = viewMode == CabinetArchiveContainerViewMode.HistoryArchiveBox

                ? Visibility.Visible

                : Visibility.Collapsed;



            Items = new ObservableCollection<CabinetArchiveBoxContentItemViewModel>(

                contents.Select(content => new CabinetArchiveBoxContentItemViewModel(content)));

            ItemDetailsPanel = new ItemDetailsListPresenter<CabinetArchiveBoxContentItemViewModel>(
                "柜内资料明细",
                summaryBuilder: items => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    items,
                    item => item.TitleText,
                    "暂无柜内资料"));
            ItemDetailsPanel.RefreshItems(Items);

            EmptyHint = BuildEmptyHint(viewMode, IsMixedPlacement);

            MixedPlacementNoticeVisibility = IsMixedPlacement ? Visibility.Visible : Visibility.Collapsed;

            MixedPlacementNoticeTitle = IsMixedPlacement ? "混放资料说明" : string.Empty;

            MixedPlacementNoticeText = BuildMixedPlacementNotice(contents);

            var lockSummary = occupationLockSummary ?? CabinetArchiveContainerOccupationLockSummary.Empty;

            HasOccupationLockNotice = lockSummary.HasAnyLock;

            OccupationLockNoticeTitle = lockSummary.NoticeTitle;

            OccupationLockNoticeText = lockSummary.NoticeText;

            OccupationLockNoticeVisibility = lockSummary.HasAnyLock ? Visibility.Visible : Visibility.Collapsed;

            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

        }



        public static CabinetArchiveBoxContentViewModel CreateArchiveBoxView(

            string boxCode,

            IReadOnlyList<CabinetArchiveBoxContentDescriptor> contents,

            bool isYearlyArchiveBox,

            CabinetArchiveContainerOccupationLockSummary? occupationLockSummary = null)

        {

            var viewMode = isYearlyArchiveBox

                ? CabinetArchiveContainerViewMode.SimulatedArchiveBox

                : CabinetArchiveContainerViewMode.HistoryArchiveBox;

            return new CabinetArchiveBoxContentViewModel(boxCode, contents, viewMode, null, occupationLockSummary);

        }



        public static CabinetArchiveBoxContentViewModel CreateElectronicBagView(

            string storageLocationCode,

            IReadOnlyList<CabinetArchiveBoxContentDescriptor> contents,

            CabinetElectronicArchiveBagHeader? bagHeader,

            CabinetArchiveContainerOccupationLockSummary? occupationLockSummary = null)

        {

            return new CabinetArchiveBoxContentViewModel(

                storageLocationCode,

                contents,

                CabinetArchiveContainerViewMode.ElectronicArchiveBag,

                bagHeader,

                occupationLockSummary);

        }



        public CabinetArchiveContainerViewMode ViewMode { get; }



        public string LocationCode { get; }



        public CabinetElectronicArchiveBagHeader? ElectronicBagHeader { get; }



        public string WindowTitle { get; }



        public string HeaderText { get; }



        public string SummaryText { get; }



        public string YearlyArchiveHintText { get; }



        public string BagHeaderText { get; }



        public Visibility BagHeaderVisibility { get; }



        public Visibility SimulatedGridVisibility { get; }



        public Visibility ElectronicGridVisibility { get; }



        public Visibility HistoryGridVisibility { get; }



        public bool HasYearlyArchiveMediaItems { get; }



        public bool IsYearlyArchiveBox => ViewMode == CabinetArchiveContainerViewMode.SimulatedArchiveBox;



        public string EmptyHint { get; }



        public bool IsMixedPlacement { get; }



        public Visibility MixedPlacementNoticeVisibility { get; }



        public string MixedPlacementNoticeTitle { get; }



        public string MixedPlacementNoticeText { get; }



        public bool HasOccupationLockNotice { get; }



        public Visibility OccupationLockNoticeVisibility { get; }



        public string OccupationLockNoticeTitle { get; }



        public string OccupationLockNoticeText { get; }



        public bool HasItems => Items.Count > 0;



        public ObservableCollection<CabinetArchiveBoxContentItemViewModel> Items { get; }



        public ItemDetailsListPresenter<CabinetArchiveBoxContentItemViewModel> ItemDetailsPanel { get; }



        public RelayCommand CloseCommand { get; }



        public event Action<bool?>? RequestClose;



        private static string BuildWindowTitle(

            string locationCode,

            CabinetArchiveContainerViewMode viewMode,

            CabinetElectronicArchiveBagHeader? bagHeader)

        {

            return viewMode switch

            {

                CabinetArchiveContainerViewMode.ElectronicArchiveBag =>

                    $"查看电子介质袋内容 - {bagHeader?.ElectronicArchiveNo ?? locationCode}",

                CabinetArchiveContainerViewMode.SimulatedArchiveBox =>

                    $"查看档案盒内容 - {locationCode}",

                _ => $"查看档案内容 - {locationCode}",

            };

        }



        private static string BuildHeaderText(

            string locationCode,

            CabinetArchiveContainerViewMode viewMode,

            CabinetElectronicArchiveBagHeader? bagHeader,

            bool isMixedPlacement)

        {

            if (viewMode == CabinetArchiveContainerViewMode.ElectronicArchiveBag)

            {

                string bagNo = bagHeader?.ElectronicArchiveNo ?? locationCode;

                return $"电子介质袋：{bagNo}（位置 {locationCode}）";

            }



            return isMixedPlacement

                ? $"档案盒：{locationCode}（混放待梳理）"

                : $"档案盒：{locationCode}";

        }



        private static string BuildSummaryText(

            IReadOnlyList<CabinetArchiveBoxContentDescriptor> contents,

            CabinetArchiveContainerViewMode viewMode,

            bool isMixedPlacement,

            bool hasYearlyArchiveMediaItems)

        {

            if (viewMode == CabinetArchiveContainerViewMode.ElectronicArchiveBag)

            {

                return $"共 {contents.Count} 条资料子项（电子介质按整件管理，显示库存状态）";

            }



            if (isMixedPlacement)

            {

                return $"共 {contents.Count} 条关联历史存档记录（当前未细化到具体盒）";

            }



            return hasYearlyArchiveMediaItems

                ? $"共 {contents.Count} 条资料子项（立档份数 = 库内 + 待还 + 不还 + 出库灭失 + 盘库丢失）"

                : $"共 {contents.Count} 条历史存档记录";

        }



        private static string BuildHintText(CabinetArchiveContainerViewMode viewMode) =>

            viewMode switch

            {

                CabinetArchiveContainerViewMode.SimulatedArchiveBox =>

                    "模拟介质档案盒：表格列展示登记审批确定的子项属性及份数分解。",

                CabinetArchiveContainerViewMode.ElectronicArchiveBag =>

                    "电子介质袋：表格列展示登记审批确定的子项属性、存储目录、目录/文件明细及介质盘库状态。",

                _ => string.Empty,

            };



        private static string BuildBagHeaderText(CabinetElectronicArchiveBagHeader? header)

        {

            if (header == null)

            {

                return string.Empty;

            }



            var segments = new List<string>();

            if (!string.IsNullOrWhiteSpace(header.ProjectName))

            {

                segments.Add($"项目 {header.ProjectName}");

            }



            if (!string.IsNullOrWhiteSpace(header.Year))

            {

                segments.Add($"年度 {header.Year}");

            }



            if (!string.IsNullOrWhiteSpace(header.StorageCarrierType))

            {

                segments.Add($"载体 {header.StorageCarrierType}");

            }



            if (!string.IsNullOrWhiteSpace(header.LinkedMediumCodes))

            {

                segments.Add($"关联介质 {header.LinkedMediumCodes}");

            }



            if (header.MediaCount > 0)

            {

                segments.Add($"介质数 {header.MediaCount}");

            }



            if (!string.IsNullOrWhiteSpace(header.ContentSummary))

            {

                segments.Add($"摘要 {header.ContentSummary}");

            }



            if (!string.IsNullOrWhiteSpace(header.ArchivedBy) || !string.IsNullOrWhiteSpace(header.ArchivedDateText))

            {

                segments.Add($"立档 {header.ArchivedBy} {header.ArchivedDateText}".Trim());

            }



            if (!string.IsNullOrWhiteSpace(header.Remarks))

            {

                segments.Add($"备注 {header.Remarks}");

            }



            return string.Join("；", segments);

        }



        private static string BuildEmptyHint(CabinetArchiveContainerViewMode viewMode, bool isMixedPlacement)

        {

            if (viewMode == CabinetArchiveContainerViewMode.ElectronicArchiveBag)

            {

                return "当前电子介质袋暂无资料子项记录。";

            }



            if (isMixedPlacement)

            {

                return "当前档案盒暂无可显示的关联历史存档记录。";

            }



            return viewMode == CabinetArchiveContainerViewMode.SimulatedArchiveBox

                ? "当前档案盒暂无资料子项记录。"

                : "当前档案盒暂无历史存档记录。";

        }



        private static string BuildMixedPlacementNotice(IReadOnlyList<CabinetArchiveBoxContentDescriptor> contents)

        {

            if (contents.Count == 0 || !contents.Any(item => item.IsMixedPlacement))

            {

                return string.Empty;

            }



            var relatedBoxes = contents

                .Select(item => item.RelatedBoxCodesText)

                .Where(text => !string.IsNullOrWhiteSpace(text))

                .Distinct(StringComparer.OrdinalIgnoreCase)

                .ToList();



            var originalTexts = contents

                .Select(item => item.OriginalBoxNumberText)

                .Where(text => !string.IsNullOrWhiteSpace(text))

                .Distinct(StringComparer.OrdinalIgnoreCase)

                .ToList();



            string relatedText = relatedBoxes.Count > 0 ? string.Join("；", relatedBoxes) : "未提供";

            string originalText = originalTexts.Count > 0 ? string.Join("；", originalTexts) : "未提供";



            return $"该批资料登记时涉及多个档案盒，当前未细化到具体盒。\n涉及档案盒：{relatedText}\n原始登记：{originalText}";

        }

    }

}


