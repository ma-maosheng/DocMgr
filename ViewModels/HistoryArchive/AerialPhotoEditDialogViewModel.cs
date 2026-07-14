using System;
using DocMgr.ViewModels.Base;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.ViewModels.HistoryArchive
{
    public class AerialPhotoEditDialogViewModel : ViewModelBase
    {
        private readonly IAerialPhotoService _aerialPhotoService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly AerialPhoto _photo;

        private string _boxNumber = string.Empty;
        private string _boxSpecification = string.Empty;
        private string _surveyArea = string.Empty;
        private string _scale = string.Empty;
        private string _photographyDate = string.Empty;
        private string _boxContents = string.Empty;
        private string _photoCount = string.Empty;
        private string _remark = string.Empty;

        public AerialPhotoEditDialogViewModel(
            IAerialPhotoService aerialPhotoService,
            IUserContextService userContextService,
            IDialogService dialogService,
            AerialPhoto photoToEdit)
        {
            ArgumentNullException.ThrowIfNull(aerialPhotoService);
            ArgumentNullException.ThrowIfNull(userContextService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(photoToEdit);

            _aerialPhotoService = aerialPhotoService;
            _userContextService = userContextService;
            _dialogService = dialogService;
            _photo = new AerialPhoto
            {
                Id = photoToEdit.Id,
                Category = photoToEdit.Category,
                BoxNumber = photoToEdit.BoxNumber,
                BoxSpecification = photoToEdit.BoxSpecification,
                SurveyArea = photoToEdit.SurveyArea,
                Scale = photoToEdit.Scale,
                PhotographyDate = photoToEdit.PhotographyDate,
                BoxContents = photoToEdit.BoxContents,
                PhotoCount = photoToEdit.PhotoCount,
                Registrant = photoToEdit.Registrant,
                RegistrationDate = photoToEdit.RegistrationDate,
                Modifier = photoToEdit.Modifier,
                ModificationDate = photoToEdit.ModificationDate,
                Remark = photoToEdit.Remark
            };

            BoxNumber = _photo.BoxNumber;
            BoxSpecification = _photo.BoxSpecification;
            SurveyArea = _photo.SurveyArea;
            Scale = _photo.Scale;
            PhotographyDate = _photo.PhotographyDate;
            BoxContents = _photo.BoxContents;
            PhotoCount = _photo.PhotoCount == 0 ? string.Empty : _photo.PhotoCount.ToString();
            Remark = _photo.Remark;

            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Title => "编辑航摄影像";

        public string BoxNumber
        {
            get => _boxNumber;
            set => SetProperty(ref _boxNumber, value);
        }

        public string SurveyArea
        {
            get => _surveyArea;
            set => SetProperty(ref _surveyArea, value);
        }

        public string BoxSpecification
        {
            get => _boxSpecification;
            set => SetProperty(ref _boxSpecification, value);
        }

        public string Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        public string PhotographyDate
        {
            get => _photographyDate;
            set => SetProperty(ref _photographyDate, value);
        }

        public string BoxContents
        {
            get => _boxContents;
            set => SetProperty(ref _boxContents, value);
        }

        public string PhotoCount
        {
            get => _photoCount;
            set => SetProperty(ref _photoCount, value);
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

            if (string.IsNullOrWhiteSpace(SurveyArea))
            {
                _dialogService.ShowMessage("请输入测区名称。");
                return;
            }

            if (!string.IsNullOrWhiteSpace(PhotoCount) && !int.TryParse(PhotoCount, out _))
            {
                _dialogService.ShowMessage("相片张数必须为整数。");
                return;
            }

            try
            {
                _photo.BoxNumber = BoxNumber.Trim();
                _photo.BoxSpecification = BoxSpecification.Trim();
                _photo.SurveyArea = SurveyArea.Trim();
                _photo.Scale = Scale.Trim();
                _photo.PhotographyDate = PhotographyDate.Trim();
                _photo.BoxContents = BoxContents.Trim();
                _photo.PhotoCount = int.TryParse(PhotoCount, out int photoCount) ? photoCount : 0;
                _photo.Remark = Remark.Trim();
                _photo.Modifier = _userContextService.CurrentUser?.RealName ?? "Unknown";
                _photo.ModificationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                _aerialPhotoService.UpdateAerialPhoto(_photo);
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
