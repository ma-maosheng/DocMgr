using System;
using DocMgr.ViewModels.Base;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.ViewModels.HistoryArchive
{
    public class OtherMapEditDialogViewModel : ViewModelBase
    {
        private readonly IOtherMapService _otherMapService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly OtherMap _map;

        private string _sequenceNumber = string.Empty;
        private string _scale = string.Empty;
        private string _boxNumber = string.Empty;
        private string _boxSpecification = string.Empty;
        private string _mapName = string.Empty;
        private string _sheetCount = string.Empty;
        private string _remark = string.Empty;

        public OtherMapEditDialogViewModel(
            IOtherMapService otherMapService,
            IUserContextService userContextService,
            IDialogService dialogService,
            OtherMap mapToEdit)
        {
            ArgumentNullException.ThrowIfNull(otherMapService);
            ArgumentNullException.ThrowIfNull(userContextService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(mapToEdit);

            _otherMapService = otherMapService;
            _userContextService = userContextService;
            _dialogService = dialogService;
            _map = new OtherMap
            {
                Id = mapToEdit.Id,
                Category = mapToEdit.Category,
                SequenceNumber = mapToEdit.SequenceNumber,
                Scale = mapToEdit.Scale,
                BoxNumber = mapToEdit.BoxNumber,
                BoxSpecification = mapToEdit.BoxSpecification,
                MapName = mapToEdit.MapName,
                SheetCount = mapToEdit.SheetCount,
                Registrant = mapToEdit.Registrant,
                RegistrationDate = mapToEdit.RegistrationDate,
                Modifier = mapToEdit.Modifier,
                ModificationDate = mapToEdit.ModificationDate,
                Remark = mapToEdit.Remark
            };

            SequenceNumber = _map.SequenceNumber;
            Scale = _map.Scale;
            BoxNumber = _map.BoxNumber;
            BoxSpecification = _map.BoxSpecification;
            MapName = _map.MapName;
            SheetCount = _map.SheetCount == 0 ? string.Empty : _map.SheetCount.ToString();
            Remark = _map.Remark;

            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Title => "编辑其他图件";

        public string SequenceNumber
        {
            get => _sequenceNumber;
            set => SetProperty(ref _sequenceNumber, value);
        }

        public string Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        public string BoxNumber
        {
            get => _boxNumber;
            set => SetProperty(ref _boxNumber, value);
        }

        public string BoxSpecification
        {
            get => _boxSpecification;
            set => SetProperty(ref _boxSpecification, value);
        }

        public string MapName
        {
            get => _mapName;
            set => SetProperty(ref _mapName, value);
        }

        public string SheetCount
        {
            get => _sheetCount;
            set => SetProperty(ref _sheetCount, value);
        }

        public string Remark
        {
            get => _remark;
            set => SetProperty(ref _remark, value);
        }

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(Scale))
            {
                _dialogService.ShowMessage("请输入比例尺。");
                return;
            }

            if (string.IsNullOrWhiteSpace(BoxNumber))
            {
                _dialogService.ShowMessage("请输入档案盒编号。");
                return;
            }

            if (string.IsNullOrWhiteSpace(MapName))
            {
                _dialogService.ShowMessage("请输入图名。");
                return;
            }

            if (!string.IsNullOrWhiteSpace(SheetCount) && !int.TryParse(SheetCount, out _))
            {
                _dialogService.ShowMessage("幅数必须为整数。");
                return;
            }

            try
            {
                _map.SequenceNumber = SequenceNumber.Trim();
                _map.Scale = Scale.Trim();
                _map.BoxNumber = BoxNumber.Trim();
                _map.BoxSpecification = BoxSpecification.Trim();
                _map.MapName = MapName.Trim();
                _map.SheetCount = int.TryParse(SheetCount, out int sheetCount) ? sheetCount : 0;
                _map.Remark = Remark.Trim();
                _map.Modifier = _userContextService.CurrentUser?.RealName ?? "Unknown";
                _map.ModificationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                _otherMapService.UpdateOtherMap(_map);
                RequestClose?.Invoke(true);
            }
            catch (DbUpdateException ex)
            {
                _dialogService.ShowError($"保存失败: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError($"保存失败: {ex.Message}");
            }
        }
    }
}
