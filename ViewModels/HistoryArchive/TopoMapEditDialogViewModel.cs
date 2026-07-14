using System;
using DocMgr.ViewModels.Base;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.ViewModels.HistoryArchive
{
    public class TopoMapEditDialogViewModel : ViewModelBase
    {
        private readonly ITopoMapService _topoMapService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly TopoMap _map;

        private string _boxNumber = string.Empty;
        private string _boxSpecification = string.Empty;
        private string _scale = string.Empty;
        private string _mapNumber = string.Empty;
        private string _mapName = string.Empty;
        private string _sheetCount = string.Empty;
        private string _creationDate = string.Empty;
        private string _surveyDate = string.Empty;
        private string _coordinateSystem = string.Empty;
        private string _elevationDatum = string.Empty;
        private string _region = string.Empty;
        private string _remark = string.Empty;

        public TopoMapEditDialogViewModel(
            ITopoMapService topoMapService,
            IUserContextService userContextService,
            IDialogService dialogService,
            TopoMap mapToEdit)
        {
            ArgumentNullException.ThrowIfNull(topoMapService);
            ArgumentNullException.ThrowIfNull(userContextService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(mapToEdit);

            _topoMapService = topoMapService;
            _userContextService = userContextService;
            _dialogService = dialogService;
            _map = new TopoMap
            {
                Id = mapToEdit.Id,
                Scale = mapToEdit.Scale,
                BoxNumber = mapToEdit.BoxNumber,
                BoxSpecification = mapToEdit.BoxSpecification,
                MapNumber = mapToEdit.MapNumber,
                MapName = mapToEdit.MapName,
                SheetCount = mapToEdit.SheetCount,
                CreationDate = mapToEdit.CreationDate,
                SurveyDate = mapToEdit.SurveyDate,
                CoordinateSystem = mapToEdit.CoordinateSystem,
                ElevationDatum = mapToEdit.ElevationDatum,
                Region = mapToEdit.Region,
                Registrant = mapToEdit.Registrant,
                RegistrationDate = mapToEdit.RegistrationDate,
                Modifier = mapToEdit.Modifier,
                ModificationDate = mapToEdit.ModificationDate,
                Remark = mapToEdit.Remark
            };

            BoxNumber = _map.BoxNumber;
            BoxSpecification = _map.BoxSpecification;
            Scale = _map.Scale;
            MapNumber = _map.MapNumber;
            MapName = _map.MapName;
            SheetCount = _map.SheetCount == 0 ? string.Empty : _map.SheetCount.ToString();
            CreationDate = _map.CreationDate;
            SurveyDate = _map.SurveyDate;
            CoordinateSystem = _map.CoordinateSystem;
            ElevationDatum = _map.ElevationDatum;
            Region = _map.Region;
            Remark = _map.Remark;

            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Title => "编辑地形图";

        public string BoxNumber
        {
            get => _boxNumber;
            set => SetProperty(ref _boxNumber, value);
        }

        public string Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        public string BoxSpecification
        {
            get => _boxSpecification;
            set => SetProperty(ref _boxSpecification, value);
        }

        public string MapNumber
        {
            get => _mapNumber;
            set => SetProperty(ref _mapNumber, value);
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

        public string CreationDate
        {
            get => _creationDate;
            set => SetProperty(ref _creationDate, value);
        }

        public string SurveyDate
        {
            get => _surveyDate;
            set => SetProperty(ref _surveyDate, value);
        }

        public string CoordinateSystem
        {
            get => _coordinateSystem;
            set => SetProperty(ref _coordinateSystem, value);
        }

        public string ElevationDatum
        {
            get => _elevationDatum;
            set => SetProperty(ref _elevationDatum, value);
        }

        public string Region
        {
            get => _region;
            set => SetProperty(ref _region, value);
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
            if (string.IsNullOrWhiteSpace(BoxNumber))
            {
                _dialogService.ShowMessage("请输入档案盒编号。");
                return;
            }

            if (string.IsNullOrWhiteSpace(Scale))
            {
                _dialogService.ShowMessage("请输入比例尺。");
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
                _map.BoxNumber = BoxNumber.Trim();
                _map.BoxSpecification = BoxSpecification.Trim();
                _map.Scale = Scale.Trim();
                _map.MapNumber = MapNumber.Trim();
                _map.MapName = MapName.Trim();
                _map.SheetCount = int.TryParse(SheetCount, out int sheetCount) ? sheetCount : 0;
                _map.CreationDate = CreationDate.Trim();
                _map.SurveyDate = SurveyDate.Trim();
                _map.CoordinateSystem = CoordinateSystem.Trim();
                _map.ElevationDatum = ElevationDatum.Trim();
                _map.Region = Region.Trim();
                _map.Remark = Remark.Trim();
                _map.Modifier = _userContextService.CurrentUser?.RealName ?? "Unknown";
                _map.ModificationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                _topoMapService.UpdateTopoMap(_map);
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
