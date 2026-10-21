using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HistoryArchive;

/// <summary>杂项详情中的一行。编号由所在位置生成。</summary>
public sealed class HistoryMiscDetailLineRow : ViewModelBase
{
    private string _text = string.Empty;
    private int _number = 1;

    public int Number
    {
        get => _number;
        set
        {
            if (SetProperty(ref _number, value))
            {
                OnPropertyChanged(nameof(NumberText));
            }
        }
    }

    public string NumberText => $"{Number}.";

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value ?? string.Empty);
    }
}

/// <summary>编辑窗中的实物照片行。</summary>
public sealed class HistoryMiscPhotoRow
{
    public int AttachmentId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public byte[]? Content { get; init; }

    public bool IsPending => AttachmentId <= 0;
}
