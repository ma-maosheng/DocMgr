using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.Views.HistoryArchive;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>
/// 杂项资料当前记录的实物照片浏览。只读，按上传时间翻页。
/// </summary>
public sealed class HistoryMiscPhotoBrowseDialogViewModel : ViewModelBase
{
    private readonly IHistoryMiscMaterialService _service;
    private readonly IDialogService _dialogService;
    private readonly int _materialId;
    private HistoryMiscPhotoCatalogItem? _selectedPhoto;
    private BitmapSource? _previewImage;
    private string _previewHint = string.Empty;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private SystemAttachment? _currentAttachment;
    private int _loadGeneration;

    private HistoryMiscPhotoBrowseDialogViewModel(
        IHistoryMiscMaterialService service,
        IDialogService dialogService,
        int materialId,
        string caption)
    {
        _service = service;
        _dialogService = dialogService;
        _materialId = materialId;
        Caption = caption;
        WindowTitle = string.IsNullOrWhiteSpace(caption) ? "实物照片" : $"实物照片 · {caption}";
        PreviousCommand = new RelayCommand(_ => Move(-1), _ => CanMove(-1));
        NextCommand = new RelayCommand(_ => Move(1), _ => CanMove(1));
        OpenCommand = new RelayCommand(_ => OpenCurrent(), _ => CanOpenCurrent);
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
    }

    public event Action? RequestClose;

    public ObservableCollection<HistoryMiscPhotoCatalogItem> Photos { get; } = new();

    public string Caption { get; }

    public string WindowTitle { get; }

    public string PositionText => Photos.Count == 0
        ? "没有照片"
        : $"第 {SelectedIndex + 1} / {Photos.Count} 张";

    public HistoryMiscPhotoCatalogItem? SelectedPhoto
    {
        get => _selectedPhoto;
        set
        {
            if (!SetProperty(ref _selectedPhoto, value))
            {
                return;
            }

            OnPropertyChanged(nameof(PositionText));
            CommandManager.InvalidateRequerySuggested();
            _ = LoadSelectedAsync();
        }
    }

    public BitmapSource? PreviewImage
    {
        get => _previewImage;
        private set => SetProperty(ref _previewImage, value);
    }

    public string PreviewHint
    {
        get => _previewHint;
        private set => SetProperty(ref _previewHint, value);
    }

    public RelayCommand PreviousCommand { get; }

    public RelayCommand NextCommand { get; }

    public RelayCommand OpenCommand { get; }

    public RelayCommand CloseCommand { get; }

    /// <summary>打开当前杂项记录的实物照片。没有照片时只提示，不弹窗。</summary>
    public static async Task ShowAsync(
        IHistoryMiscMaterialService service,
        IDialogService dialogService,
        int materialId,
        string? containerKind,
        string? containerCode,
        string? majorCategory,
        string? subCategory,
        string? location)
    {
        try
        {
            var viewModel = new HistoryMiscPhotoBrowseDialogViewModel(
                service,
                dialogService,
                materialId,
                BuildCaption(containerKind, containerCode, majorCategory, subCategory, location));
            await viewModel.LoadCatalogAsync();
            if (viewModel.Photos.Count == 0)
            {
                dialogService.ShowMessage("当前记录没有实物照片。");
                return;
            }

            var dialog = new HistoryMiscPhotoBrowseDialog
            {
                Owner = Application.Current?.MainWindow,
                DataContext = viewModel
            };
            viewModel.RequestClose += () => dialog.Close();
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            dialogService.ShowError($"打开照片失败：{ex.Message}");
        }
    }

    private async Task LoadCatalogAsync()
    {
        IReadOnlyList<HistoryMiscPhotoCatalogItem> photos = await _service.GetPhotoCatalogAsync(_materialId);
        Photos.Clear();
        foreach (HistoryMiscPhotoCatalogItem photo in photos)
        {
            Photos.Add(photo);
        }

        if (Photos.Count > 0)
        {
            SelectedPhoto = Photos[0];
        }
    }

    private int SelectedIndex => SelectedPhoto == null ? -1 : Photos.IndexOf(SelectedPhoto);

    private bool CanOpenCurrent => _currentAttachment?.FileContent is { Length: > 0 };

    private bool CanMove(int delta)
    {
        int index = SelectedIndex;
        int target = index + delta;
        return index >= 0 && target >= 0 && target < Photos.Count;
    }

    private void Move(int delta)
    {
        int target = SelectedIndex + delta;
        if (target < 0 || target >= Photos.Count)
        {
            return;
        }

        SelectedPhoto = Photos[target];
    }

    private void OpenCurrent()
    {
        if (_currentAttachment?.FileContent == null || _currentAttachment.FileContent.Length == 0)
        {
            _dialogService.ShowMessage("照片内容为空，无法打开。");
            return;
        }

        _dialogService.ShowSystemAttachmentView(_currentAttachment);
    }

    private async Task LoadSelectedAsync()
    {
        int generation = ++_loadGeneration;
        HistoryMiscPhotoCatalogItem? photo = SelectedPhoto;
        PreviewImage = null;
        _currentAttachment = null;
        CommandManager.InvalidateRequerySuggested();
        if (photo == null)
        {
            PreviewHint = "请选择一张照片。";
            return;
        }

        PreviewHint = "正在加载…";
        await _loadGate.WaitAsync();
        try
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            SystemAttachment? attachment = await _service.GetPhotoAsync(photo.AttachmentId);
            if (generation != _loadGeneration)
            {
                return;
            }

            if (attachment?.FileContent == null || attachment.FileContent.Length == 0)
            {
                PreviewHint = "照片内容为空。";
                return;
            }

            _currentAttachment = attachment;
            if (SystemAttachmentViewSupport.TryCreateImageSource(attachment, out BitmapSource? imageSource)
                && imageSource != null)
            {
                PreviewImage = imageSource;
                PreviewHint = string.Empty;
            }
            else
            {
                PreviewHint = "窗口内无法预览这张照片，请用系统程序打开。";
            }
        }
        catch (Exception ex)
        {
            if (generation == _loadGeneration)
            {
                PreviewHint = $"加载失败：{ex.Message}";
            }
        }
        finally
        {
            _loadGate.Release();
            if (generation == _loadGeneration)
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private static string BuildCaption(
        string? containerKind,
        string? containerCode,
        string? majorCategory,
        string? subCategory,
        string? location)
    {
        string container = $"{containerKind} {containerCode}".Trim();
        string category = string.Join(
            " / ",
            new[] { majorCategory, subCategory }.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!.Trim()));
        string place = location?.Trim() ?? string.Empty;
        return string.Join(
            " · ",
            new[] { container, category, place }.Where(item => !string.IsNullOrWhiteSpace(item)));
    }
}
