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
        private string _materialCategory = string.Empty;
        private string _startYear = string.Empty;
        private string _endYear = string.Empty;
        private string _boxNumber = string.Empty;
        private string _boxSpecification = string.Empty;
        private string _mapName = string.Empty;
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
                MaterialCategory = mapToEdit.MaterialCategory,
                StartYear = mapToEdit.StartYear,
                EndYear = mapToEdit.EndYear,
                Scale = mapToEdit.Scale,
                BoxNumber = mapToEdit.BoxNumber,
                BoxSpecification = mapToEdit.BoxSpecification,
                MapName = mapToEdit.MapName,
                Registrant = mapToEdit.Registrant,
                RegistrationDate = mapToEdit.RegistrationDate,
                Modifier = mapToEdit.Modifier,
                ModificationDate = mapToEdit.ModificationDate,
                Remark = mapToEdit.Remark,
                LifecycleStatus = mapToEdit.LifecycleStatus,
                LastStorageLocation = mapToEdit.LastStorageLocation
            };

            SequenceNumber = _map.SequenceNumber;
            MaterialCategory = _map.MaterialCategory;
            StartYear = _map.StartYear;
            EndYear = _map.EndYear;
            BoxNumber = _map.BoxNumber;
            BoxSpecification = _map.BoxSpecification;
            MapName = _map.MapName;
            Remark = _map.Remark;

            ConfirmCommand = new RelayCommand(_ => Confirm(), _ => CanEditBoxNumber);
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Title => "编辑其他资料";

        /// <summary>已离库记录禁止改盒号，且不可保存。</summary>
        public bool CanEditBoxNumber =>
            !HistoryArchiveDisposalDomainValues.IsDisposedLifecycle(_map.LifecycleStatus);

        public string SequenceNumber
        {
            get => _sequenceNumber;
            set => SetProperty(ref _sequenceNumber, value);
        }

        public string MaterialCategory
        {
            get => _materialCategory;
            set => SetProperty(ref _materialCategory, value);
        }

        public string StartYear
        {
            get => _startYear;
            set => SetProperty(ref _startYear, value);
        }

        public string EndYear
        {
            get => _endYear;
            set => SetProperty(ref _endYear, value);
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
            if (!CanEditBoxNumber)
            {
                _dialogService.ShowMessage("已离库记录只读，禁止修改。");
                return;
            }

            if (string.IsNullOrWhiteSpace(BoxNumber))
            {
                _dialogService.ShowMessage("请输入档案盒编号。");
                return;
            }

            if (string.IsNullOrWhiteSpace(MapName))
            {
                _dialogService.ShowMessage("请输入资料内容。");
                return;
            }

            try
            {
                _map.SequenceNumber = SequenceNumber.Trim();
                _map.MaterialCategory = MaterialCategory.Trim();
                _map.StartYear = StartYear.Trim();
                _map.EndYear = EndYear.Trim();
                _map.BoxNumber = BoxNumber.Trim();
                _map.BoxSpecification = BoxSpecification.Trim();
                _map.MapName = MapName.Trim();
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
