using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using DocMgr.Models.HistoryArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.Views.HistoryArchive;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>选择立式或卧式文件柜格口，并带出下一个档内序号。</summary>
public sealed class HistoryMiscSlotPickerViewModel : ViewModelBase
{
    private HistoryMiscSlotOption? _selectedOption;

    public HistoryMiscSlotPickerViewModel(IReadOnlyList<HistoryMiscSlotOption> options)
    {
        foreach (HistoryMiscSlotOption option in options)
        {
            Options.Add(option);
        }

        SelectedOption = Options.FirstOrDefault();
        ConfirmCommand = new RelayCommand(_ => RequestClose?.Invoke(true), _ => SelectedOption != null);
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
    }

    public ObservableCollection<HistoryMiscSlotOption> Options { get; } = new();

    public HistoryMiscSlotOption? SelectedOption
    {
        get => _selectedOption;
        set => SetProperty(ref _selectedOption, value);
    }

    public string SelectedLocation => SelectedOption?.SuggestedLocation ?? string.Empty;

    public RelayCommand ConfirmCommand { get; }

    public RelayCommand CancelCommand { get; }

    public event Action<bool?>? RequestClose;
}

/// <summary>杂项资料编辑窗。</summary>
public sealed class HistoryMiscMaterialEditDialogViewModel : ViewModelBase
{
    private readonly IHistoryMiscMaterialService _service;
    private readonly IDialogService _dialogService;
    private readonly User? _currentUser;
    private readonly int? _materialId;
    private readonly List<int> _removedAttachmentIds = new();
    private HistoryMiscVocabulary _vocabulary = new();
    private string _majorCategory = string.Empty;
    private string _subCategory = string.Empty;
    private string _containerKind = string.Empty;
    private string _containerCode = string.Empty;
    private string _storageLocation = string.Empty;
    private string _remark = string.Empty;
    private bool _canEdit = true;
    private bool _loading;
    private HistoryMiscPhotoRow? _selectedPhoto;

    public HistoryMiscMaterialEditDialogViewModel(
        IHistoryMiscMaterialService service,
        IDialogService dialogService,
        User? currentUser,
        int? materialId)
    {
        _service = service;
        _dialogService = dialogService;
        _currentUser = currentUser;
        _materialId = materialId;
        AddLineCommand = new RelayCommand(_ => AddLine(string.Empty), _ => CanEdit);
        RemoveLineCommand = new RelayCommand(parameter => RemoveLine(parameter as HistoryMiscDetailLineRow), _ => CanEdit);
        MoveLineUpCommand = new RelayCommand(parameter => MoveLine(parameter as HistoryMiscDetailLineRow, -1), _ => CanEdit);
        MoveLineDownCommand = new RelayCommand(parameter => MoveLine(parameter as HistoryMiscDetailLineRow, 1), _ => CanEdit);
        PasteLinesCommand = new RelayCommand(_ => PasteLines(), _ => CanEdit);
        SuggestSlotCommand = new RelayCommand(async _ => await SuggestSlotAsync(), _ => CanEdit);
        CapturePhotoCommand = new RelayCommand(_ => CapturePhoto(), _ => CanEdit);
        UploadPhotoCommand = new RelayCommand(_ => UploadPhoto(), _ => CanEdit);
        ViewPhotoCommand = new RelayCommand(async parameter => await ViewPhotoAsync(parameter as HistoryMiscPhotoRow ?? SelectedPhoto), _ => SelectedPhoto != null);
        DeletePhotoCommand = new RelayCommand(parameter => DeletePhoto(parameter as HistoryMiscPhotoRow ?? SelectedPhoto), _ => CanEdit && SelectedPhoto != null);
        SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanEdit);
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
    }

    public string Title => _materialId.HasValue
        ? (CanEdit ? "编辑杂项资料" : "查看杂项资料")
        : "新建杂项资料";

    public bool CanEdit
    {
        get => _canEdit;
        private set
        {
            if (SetProperty(ref _canEdit, value))
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(ReadOnlyHint));
            }
        }
    }

    public string ReadOnlyHint => CanEdit
        ? "子类随大类提示已有值。档口推荐只含立式、卧式文件柜，不限制容量。保存前须有至少一张实物照片。"
        : "离库锁定或已离库后不能修改位置、编号和照片。";

    public ObservableCollection<string> MajorCategoryOptions { get; } = new();

    public ObservableCollection<string> SubCategoryOptions { get; } = new();

    public ObservableCollection<string> ContainerKindOptions { get; } = new();

    public ObservableCollection<HistoryMiscDetailLineRow> DetailLines { get; } = new();

    public ObservableCollection<HistoryMiscPhotoRow> Photos { get; } = new();

    public string MajorCategory
    {
        get => _majorCategory;
        set
        {
            if (SetProperty(ref _majorCategory, value ?? string.Empty))
            {
                RefreshSubCategories();
                SyncFilmBarrelDetails();
            }
        }
    }

    public string SubCategory
    {
        get => _subCategory;
        set
        {
            if (SetProperty(ref _subCategory, value ?? string.Empty))
            {
                SyncFilmBarrelDetails();
            }
        }
    }

    public string ContainerKind
    {
        get => _containerKind;
        set
        {
            if (SetProperty(ref _containerKind, value ?? string.Empty))
            {
                SyncFilmBarrelDetails();
            }
        }
    }

    public string ContainerCode
    {
        get => _containerCode;
        set => SetProperty(ref _containerCode, value ?? string.Empty);
    }

    public string StorageLocation
    {
        get => _storageLocation;
        set => SetProperty(ref _storageLocation, value ?? string.Empty);
    }

    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value ?? string.Empty);
    }

    public HistoryMiscPhotoRow? SelectedPhoto
    {
        get => _selectedPhoto;
        set => SetProperty(ref _selectedPhoto, value);
    }

    public RelayCommand AddLineCommand { get; }

    public RelayCommand RemoveLineCommand { get; }

    public RelayCommand MoveLineUpCommand { get; }

    public RelayCommand MoveLineDownCommand { get; }

    public RelayCommand PasteLinesCommand { get; }

    public RelayCommand SuggestSlotCommand { get; }

    public RelayCommand CapturePhotoCommand { get; }

    public RelayCommand UploadPhotoCommand { get; }

    public RelayCommand ViewPhotoCommand { get; }

    public RelayCommand DeletePhotoCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public event Action<bool?>? RequestClose;

    /// <summary>打开窗体前加载词汇、详情和照片。</summary>
    public async Task LoadAsync()
    {
        _loading = true;
        _vocabulary = await _service.GetVocabularyAsync();
        Replace(MajorCategoryOptions, _vocabulary.MajorCategories);
        Replace(ContainerKindOptions, _vocabulary.ContainerKinds);
        if (_materialId.HasValue)
        {
            HistoryMiscMaterial? material = await _service.GetAsync(_materialId.Value);
            if (material == null)
            {
                _loading = false;
                _dialogService.ShowError("未找到杂项资料。");
                CanEdit = false;
                return;
            }

            CanEdit = HistoryArchiveLedgerPermissionSupport.CanMaintain(_currentUser)
                && HistoryArchiveDisposalDomainValues.IsInStockLifecycle(material.LifecycleStatus);
            MajorCategory = material.MajorCategory;
            SubCategory = material.SubCategory;
            ContainerKind = material.ContainerKind;
            ContainerCode = material.ContainerCode;
            StorageLocation = HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(material.LifecycleStatus)
                ? material.LastStorageLocation
                : material.StorageLocation;
            Remark = material.Remark;
            foreach (HistoryMiscMaterialLine line in material.Lines.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
            {
                DetailLines.Add(new HistoryMiscDetailLineRow { Text = line.LineText });
            }

            foreach (SystemAttachment photo in await _service.GetPhotosAsync(material.Id))
            {
                Photos.Add(new HistoryMiscPhotoRow
                {
                    AttachmentId = photo.Id,
                    FileName = photo.FileName,
                    Content = photo.FileContent
                });
            }
        }
        else
        {
            CanEdit = HistoryArchiveLedgerPermissionSupport.CanMaintain(_currentUser);
            MajorCategory = HistoryMiscMaterialDefaults.ImageMajorCategory;
            SubCategory = HistoryMiscMaterialDefaults.FilmSubCategory;
            ContainerKind = HistoryMiscMaterialDefaults.BarrelContainerKind;
            ContainerCode = await _service.AllocateContainerCodeAsync();
        }

        _loading = false;
        if (DetailLines.Count == 0)
        {
            if (!_materialId.HasValue
                && HistoryMiscMaterialDefaults.IsFilmBarrel(MajorCategory, SubCategory, ContainerKind))
            {
                ReplaceDetailLines(HistoryMiscMaterialDefaults.FilmBarrelDetailLines);
            }
            else
            {
                DetailLines.Add(new HistoryMiscDetailLineRow());
            }
        }

        RenumberLines();
        RefreshSubCategories();
        SelectedPhoto = Photos.FirstOrDefault();
    }

    private async Task SaveAsync()
    {
        if (_currentUser == null || !CanEdit)
        {
            return;
        }

        if (!Photos.Any(item => item.AttachmentId > 0 || item.Content is { Length: > 0 }))
        {
            _dialogService.ShowError("请至少上传一张实物照片。");
            return;
        }

        try
        {
            await _service.SaveAsync(new HistoryMiscMaterialSaveRequest
            {
                Id = _materialId ?? 0,
                MajorCategory = MajorCategory,
                SubCategory = SubCategory,
                ContainerKind = ContainerKind,
                ContainerCode = ContainerCode,
                StorageLocation = StorageLocation,
                Remark = Remark,
                DetailLines = DetailLines.Select(item => item.Text).ToList(),
                RemovedAttachmentIds = _removedAttachmentIds,
                AddedPhotos = Photos
                    .Where(item => item.IsPending && item.Content != null)
                    .Select(item => new HistoryMiscPhotoDraft
                    {
                        FileName = item.FileName,
                        Content = item.Content!
                    })
                    .ToList()
            }, _currentUser);
            RequestClose?.Invoke(true);
        }
        catch (InvalidOperationException ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    private async Task SuggestSlotAsync()
    {
        IReadOnlyList<HistoryMiscSlotOption> options = await _service.SuggestSlotsAsync();
        if (options.Count == 0)
        {
            _dialogService.ShowMessage("没有可推荐的立式或卧式文件柜格口。");
            return;
        }

        var picker = new HistoryMiscSlotPickerViewModel(options);
        var dialog = new HistoryMiscSlotPickerDialog
        {
            Owner = Application.Current?.MainWindow,
            DataContext = picker
        };
        picker.RequestClose += result => dialog.DialogResult = result;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(picker.SelectedLocation))
        {
            StorageLocation = picker.SelectedLocation;
        }
    }

    private void CapturePhoto()
    {
        DocumentCameraCaptureResult? captured = DocumentCameraAttachmentCaptureSupport.Capture(_dialogService);
        if (captured?.JpegContent == null || captured.JpegContent.Length == 0)
        {
            return;
        }

        string fileName = DocumentCameraAttachmentCaptureSupport.BuildFileName(ContainerCode, "实物照片", "杂项");
        Photos.Add(new HistoryMiscPhotoRow
        {
            FileName = fileName,
            Content = captured.JpegContent
        });
        SelectedPhoto = Photos.Last();
    }

    private void UploadPhoto()
    {
        string? filePath = _dialogService.OpenFileDialog("图片|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff", "上传实物照片");
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        byte[] content = File.ReadAllBytes(filePath);
        if (content.Length == 0)
        {
            _dialogService.ShowMessage("所选文件是空的。");
            return;
        }

        Photos.Add(new HistoryMiscPhotoRow
        {
            FileName = Path.GetFileName(filePath),
            Content = content
        });
        SelectedPhoto = Photos.Last();
    }

    private async Task ViewPhotoAsync(HistoryMiscPhotoRow? photo)
    {
        if (photo == null)
        {
            return;
        }

        SystemAttachment? attachment = photo.IsPending
            ? new SystemAttachment
            {
                FileName = photo.FileName,
                Extension = Path.GetExtension(photo.FileName),
                FileContent = photo.Content,
                FileSize = photo.Content?.LongLength ?? 0,
                FileCategory = HistoryArchiveDisposalDomainValues.MiscAttachmentCategoryPhoto
            }
            : await _service.GetPhotoAsync(photo.AttachmentId);
        if (attachment?.FileContent == null || attachment.FileContent.Length == 0)
        {
            attachment = photo.Content == null
                ? attachment
                : new SystemAttachment
                {
                    FileName = photo.FileName,
                    Extension = Path.GetExtension(photo.FileName),
                    FileContent = photo.Content,
                    FileSize = photo.Content.LongLength,
                    FileCategory = HistoryArchiveDisposalDomainValues.MiscAttachmentCategoryPhoto
                };
        }

        if (attachment?.FileContent == null || attachment.FileContent.Length == 0)
        {
            _dialogService.ShowMessage("照片内容为空，无法浏览。");
            return;
        }

        _dialogService.ShowSystemAttachmentView(attachment);
    }

    private void DeletePhoto(HistoryMiscPhotoRow? photo)
    {
        if (photo == null || !CanEdit)
        {
            return;
        }

        if (!_dialogService.ShowConfirm($"确定删除照片「{photo.FileName}」吗？", "删除照片"))
        {
            return;
        }

        if (!photo.IsPending)
        {
            _removedAttachmentIds.Add(photo.AttachmentId);
        }

        Photos.Remove(photo);
        SelectedPhoto = Photos.FirstOrDefault();
    }

    private void AddLine(string text)
    {
        DetailLines.Add(new HistoryMiscDetailLineRow { Text = text });
        RenumberLines();
    }

    private void RemoveLine(HistoryMiscDetailLineRow? line)
    {
        if (line == null)
        {
            return;
        }

        DetailLines.Remove(line);
        if (DetailLines.Count == 0)
        {
            DetailLines.Add(new HistoryMiscDetailLineRow());
        }

        RenumberLines();
    }

    private void MoveLine(HistoryMiscDetailLineRow? line, int offset)
    {
        if (line == null)
        {
            return;
        }

        int index = DetailLines.IndexOf(line);
        int target = index + offset;
        if (index < 0 || target < 0 || target >= DetailLines.Count)
        {
            return;
        }

        DetailLines.Move(index, target);
        RenumberLines();
    }

    private void PasteLines()
    {
        string text = Clipboard.GetText() ?? string.Empty;
        string[] rows = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        bool added = false;
        foreach (string row in rows)
        {
            string line = row.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            DetailLines.Add(new HistoryMiscDetailLineRow { Text = line });
            added = true;
        }

        if (!added)
        {
            _dialogService.ShowMessage("剪贴板里没有可追加的文本行。");
            return;
        }

        RenumberLines();
    }

    private void SyncFilmBarrelDetails()
    {
        if (_loading || _materialId.HasValue || !CanEdit)
        {
            return;
        }

        bool matches = HistoryMiscMaterialDefaults.IsFilmBarrel(MajorCategory, SubCategory, ContainerKind);
        bool unchangedTemplate = DetailLines.Count == HistoryMiscMaterialDefaults.FilmBarrelDetailLines.Count
            && HistoryMiscMaterialDefaults.CanReplaceWithFilmBarrelTemplate(DetailLines.Select(item => item.Text))
            && DetailLines.Any(item => !string.IsNullOrWhiteSpace(item.Text));
        if (matches)
        {
            if (!unchangedTemplate
                && HistoryMiscMaterialDefaults.CanReplaceWithFilmBarrelTemplate(DetailLines.Select(item => item.Text)))
            {
                ReplaceDetailLines(HistoryMiscMaterialDefaults.FilmBarrelDetailLines);
            }

            return;
        }

        if (unchangedTemplate)
        {
            ReplaceDetailLines([string.Empty]);
        }
    }

    private void ReplaceDetailLines(IReadOnlyList<string> lines)
    {
        DetailLines.Clear();
        foreach (string line in lines)
        {
            DetailLines.Add(new HistoryMiscDetailLineRow { Text = line });
        }

        RenumberLines();
    }

    private void RefreshSubCategories()
    {
        string major = MajorCategory?.Trim() ?? string.Empty;
        IEnumerable<string> values = _vocabulary.SubCategories
            .Where(item => string.Equals(item.MajorCategory, major, StringComparison.Ordinal))
            .Select(item => item.SubCategory);
        string current = SubCategory;
        Replace(SubCategoryOptions, values);
        SubCategory = current;
    }

    private void RenumberLines()
    {
        for (int index = 0; index < DetailLines.Count; index++)
        {
            DetailLines[index].Number = index + 1;
        }
    }

    private static void Replace(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (string value in values.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
        {
            target.Add(value);
        }
    }
}
