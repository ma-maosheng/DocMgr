using System.Windows;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.HistoryArchive;
using DocMgr.ViewModels.YearlyArchive;
using DocMgr.Views.HistoryArchive;

namespace DocMgr.Services.Cabinets;

/// <summary>
/// 从台账当前行打开已有的资料详情、档案盒内容、电子介质袋内容或杂项只读窗。
/// 容器已不在柜、历史盒已离库或介质未入袋时只提示，不弹出空窗口。
/// </summary>
public sealed class LedgerStoredMaterialOpener
{
    private readonly IDialogService _dialogService;
    private readonly ICabinetArchiveBoxContentService _contentService;
    private readonly ICabinetOpenLayoutRepository _layoutRepository;
    private readonly IArchiveFilingFactRepository _filingFactRepository;
    private readonly IHistoryMiscMaterialService _miscMaterialService;
    private readonly IUserContextService _userContextService;

    public LedgerStoredMaterialOpener(
        IDialogService dialogService,
        ICabinetArchiveBoxContentService contentService,
        ICabinetOpenLayoutRepository layoutRepository,
        IArchiveFilingFactRepository filingFactRepository,
        IHistoryMiscMaterialService miscMaterialService,
        IUserContextService userContextService)
    {
        _dialogService = dialogService;
        _contentService = contentService;
        _layoutRepository = layoutRepository;
        _filingFactRepository = filingFactRepository;
        _miscMaterialService = miscMaterialService;
        _userContextService = userContextService;
    }

    /// <summary>存放位置上是否仍有在用的年度档案盒或电子介质袋。</summary>
    public bool CanOpenContainer(ArchiveContainerKind containerKind, string? locationCode)
    {
        if (string.IsNullOrWhiteSpace(locationCode))
        {
            return false;
        }

        return containerKind == ArchiveContainerKind.ElectronicBag
            ? _contentService.IsElectronicArchiveBagAtLocation(locationCode)
            : _contentService.IsYearlyArchiveBoxAtLocation(locationCode);
    }

    /// <summary>硬盘是否关联仍在用的电子介质袋。返回袋子 Id。</summary>
    public int? FindInUseElectronicUnitIdByHardDisk(int mediumId) =>
        _layoutRepository.FindInUseElectronicUnitIdByHardDiskMediumId(mediumId);

    /// <summary>光盘是否关联仍在用的电子介质袋。返回袋子 Id。</summary>
    public int? FindInUseElectronicUnitIdByOpticalDisc(int mediumId) =>
        _layoutRepository.FindInUseElectronicUnitIdByOpticalDiscMediumId(mediumId);

    /// <summary>打开这一条立档事实的资料详情。</summary>
    public async Task OpenFilingFactAsync(int filingFactId)
    {
        if (filingFactId <= 0)
        {
            _dialogService.ShowMessage("未选中立档记录，无法查看资料。");
            return;
        }

        int? registerRecordId = await _filingFactRepository.GetRegisterRecordIdAsync(filingFactId);
        if (registerRecordId is not int recordId)
        {
            _dialogService.ShowMessage("该记录未关联建档申请，无法查看资料。");
            return;
        }

        _dialogService.ShowArchiveDetailWindow(new ArchiveDetailOpenRequest(recordId, null, null, filingFactId));
    }

    /// <summary>按盒位编码打开在用的年度档案盒内容。</summary>
    public void OpenYearlyArchiveBoxByLocation(string? locationCode)
    {
        if (string.IsNullOrWhiteSpace(locationCode))
        {
            _dialogService.ShowMessage("未找到存放位置，无法查看存放资料。");
            return;
        }

        if (!_contentService.IsYearlyArchiveBoxAtLocation(locationCode))
        {
            _dialogService.ShowMessage("该档案盒已不在柜，无法查看存放资料。");
            return;
        }

        _dialogService.ShowCabinetArchiveBoxContentDialog(locationCode.Trim());
    }

    /// <summary>按存放位置打开在用的电子介质袋内容。</summary>
    public void OpenElectronicBagByLocation(string? locationCode)
    {
        if (string.IsNullOrWhiteSpace(locationCode))
        {
            _dialogService.ShowMessage("未找到存放位置，无法查看存放资料。");
            return;
        }

        if (!_contentService.IsElectronicArchiveBagAtLocation(locationCode))
        {
            _dialogService.ShowMessage("该电子介质袋已不在柜，无法查看存放资料。");
            return;
        }

        _dialogService.ShowCabinetElectronicBagContentDialogByLocation(locationCode.Trim());
    }

    /// <summary>打开硬盘所在的在用电子介质袋。</summary>
    public void OpenElectronicBagByHardDisk(int mediumId)
    {
        int? unitId = FindInUseElectronicUnitIdByHardDisk(mediumId);
        if (unitId is not int id)
        {
            _dialogService.ShowMessage("该硬盘未关联在用电子介质袋，无法查看存放资料。");
            return;
        }

        _dialogService.ShowCabinetElectronicBagContentDialog(id);
    }

    /// <summary>打开光盘所在的在用电子介质袋。</summary>
    public void OpenElectronicBagByOpticalDisc(int mediumId)
    {
        int? unitId = FindInUseElectronicUnitIdByOpticalDisc(mediumId);
        if (unitId is not int id)
        {
            _dialogService.ShowMessage("该光盘未关联在用电子介质袋，无法查看存放资料。");
            return;
        }

        _dialogService.ShowCabinetElectronicBagContentDialog(id);
    }

    /// <summary>打开在库历史档案盒内容。已离库盒不打开。</summary>
    public void OpenHistoryArchiveBox(string? boxCode, bool isDisposed)
    {
        if (isDisposed)
        {
            _dialogService.ShowMessage("该历史档案盒已离库，开柜内容不再展示。");
            return;
        }

        if (string.IsNullOrWhiteSpace(boxCode))
        {
            _dialogService.ShowMessage("未找到档案盒号，无法查看存放资料。");
            return;
        }

        _dialogService.ShowCabinetArchiveBoxContentDialog(boxCode.Trim());
    }

    /// <summary>以只读方式打开杂项资料。已离库记录仍可查看。</summary>
    public async Task OpenMiscMaterialAsync(int materialId)
    {
        if (materialId <= 0)
        {
            _dialogService.ShowMessage("未选中杂项资料，无法查看。");
            return;
        }

        var editViewModel = new HistoryMiscMaterialEditDialogViewModel(
            _miscMaterialService,
            _dialogService,
            _userContextService.CurrentUser,
            materialId,
            viewOnly: true);
        await editViewModel.LoadAsync();
        var dialog = new HistoryMiscMaterialEditDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = editViewModel
        };
        editViewModel.RequestClose += result => dialog.DialogResult = result;
        dialog.ShowDialog();
    }
}
