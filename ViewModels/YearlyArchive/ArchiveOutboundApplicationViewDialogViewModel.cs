using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using DocMgr.Views.Shared;
using Microsoft.Win32;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料借出（出库）申请单只读查看弹窗 ViewModel。仅展示申请、审批信息、出库明细与附件，支持打印与关闭。
    /// 资料室资料管理员可在办结后增补「其他附件」（仅新增、不可删除）。
    /// </summary>
    public sealed class ArchiveOutboundApplicationViewDialogViewModel : ViewModelBase
    {
        private readonly IArchiveOutboundService _outboundService;
        private readonly IArchiveOutboundWordExportService _outboundWordExportService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private YearlyArchiveOutboundRecord _record;
        private string[] _uniformOpinions = [];

        public ArchiveOutboundApplicationViewDialogViewModel(
            IArchiveOutboundService outboundService,
            IArchiveOutboundWordExportService outboundWordExportService,
            IDialogService dialogService,
            IUserContextService userContextService,
            YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(outboundService);
            ArgumentNullException.ThrowIfNull(outboundWordExportService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(userContextService);
            ArgumentNullException.ThrowIfNull(record);

            _outboundService = outboundService;
            _outboundWordExportService = outboundWordExportService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _record = record;
            RefreshUniformOpinions();

            ViewAttachmentCommand = new RelayCommand(
                async attachment => await ViewAttachmentAsync(attachment as SystemAttachment),
                attachment => attachment is SystemAttachment);
            SupplementOtherAttachmentCommand = new RelayCommand(
                async _ => await SupplementOtherAttachmentAsync(),
                _ => CanSupplementOtherAttachments);
            PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => CanPrint);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public string WindowTitle => $"查看申请 · {OutboundNo} · {StatusDisplay}";

        public string WorkspaceBannerText => CanSupplementOtherAttachments
            ? "本窗口以查看为主；办结后资料室资料管理员可增补「其他附件」，不可删除已有附件。"
            : "本窗口仅用于查看资料借出申请信息，不允许编辑。";

        // 1. 申请信息
        public string OutboundNo => EmptyAsPlaceholder(_record.OutboundNo);
        public string StatusDisplay => _record.StatusStr;
        public string ProjectName => EmptyAsPlaceholder(_record.ProjectName);
        public string ApplicantName => EmptyAsPlaceholder(_record.ApplicantName);
        public string ApplicantDept => EmptyAsPlaceholder(_record.ApplicantDept);
        public string ApplyDateDisplay => _record.ApplyDate == default ? "(无)" : _record.ApplyDate.ToString("yyyy-MM-dd");
        public string Reason => EmptyAsPlaceholder(_record.Reason);
        public string DestinationDisplayText => ArchiveOutboundDomainValues.IsExternalDestination(_record.DestinationKind)
            ? (string.IsNullOrWhiteSpace(_record.ExternalUnit) ? "外部（单位）" : $"外部（单位）· {_record.ExternalUnit.Trim()}")
            : "本部门（内部）";
        public string ProofMaterialDisplay => ArchiveOutboundDomainValues.HasProofMaterial(_record.ProofMaterialNote)
            ? _record.ProofMaterialNote.Trim()
            : ArchiveOutboundDomainValues.ProofMaterialNoneText;
        public string ExpectedReturnDateDisplay => _record.ExpectedReturnDate?.ToString("yyyy-MM-dd") ?? "无";
        public string MaterialSummary => EmptyAsPlaceholder(_record.MaterialSummary);

        // 2. 审批信息（意见栏一致化：全有「同意」或全空，空意见不用「(无)」占位）
        public string DeptAuditOpinion => _uniformOpinions[0];
        public string DeptAuditor => EmptyAsPlaceholder(_record.DeptAuditor);
        public string DeptAuditDateDisplay => FormatDate(_record.DeptAuditDate);
        public string ArchiveRoomHeadOpinion => _uniformOpinions[1];
        public string ArchiveRoomHead => EmptyAsPlaceholder(_record.ArchiveRoomHead);
        public string ArchiveRoomHeadDateDisplay => FormatDate(_record.ArchiveRoomHeadDate);
        public string ProductionHeadOpinion => _uniformOpinions[2];
        public string ProductionHead => EmptyAsPlaceholder(_record.ProductionHead);
        public string ProductionHeadDateDisplay => FormatDate(_record.ProductionHeadDate);
        public string VicePresidentOpinion => _uniformOpinions[3];
        public string VicePresident => EmptyAsPlaceholder(_record.VicePresident);
        public string VicePresidentDateDisplay => FormatDate(_record.VicePresidentDate);

        // 3. 出库明细
        public ObservableCollection<YearlyArchiveOutboundItem> Items { get; } = new();

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public bool CanPrint => _record.Id > 0;

        /// <summary>办结后，资料室资料管理员可增补其他附件。</summary>
        public bool CanSupplementOtherAttachments =>
            _record.Id > 0
            && _record.IsCompleted
            && _outboundService.IsArchiveAdminUser(_userContextService.CurrentUser);

        public string SupplementAttachmentHint =>
            "办结后仅可增补「其他附件」；请确保文件命名清晰准确。本页不可删除已有附件。";

        public ICommand ViewAttachmentCommand { get; }
        public ICommand SupplementOtherAttachmentCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand CloseCommand { get; }

        public event Action<bool?>? RequestClose;

        private async Task InitializeAsync()
        {
            await EnsureRecordLoadedAsync();
            SyncItems();
            await LoadAttachmentsAsync();
            OnPropertyChanged(nameof(CanSupplementOtherAttachments));
            OnPropertyChanged(nameof(WorkspaceBannerText));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private async Task EnsureRecordLoadedAsync()
        {
            if (_record.Id <= 0)
            {
                return;
            }

            var loaded = await _outboundService.GetRecordAsync(_record.Id);
            if (loaded == null)
            {
                return;
            }

            _record = loaded;
            RefreshUniformOpinions();
            RaiseAllDisplayPropertiesChanged();
        }

        private void RefreshUniformOpinions()
        {
            _uniformOpinions = ApprovalOpinionUniformitySupport.NormalizeUniform(
                _record.DeptAuditOpinion,
                _record.ArchiveRoomHeadOpinion,
                _record.ProductionHeadOpinion,
                _record.VicePresidentOpinion);
        }

        private void SyncItems()
        {
            Items.Clear();
            foreach (var item in _record.Items)
            {
                Items.Add(item);
            }
        }

        private async Task LoadAttachmentsAsync()
        {
            Attachments.Clear();
            if (_record.Id <= 0)
            {
                return;
            }

            var attachments = await _outboundService.GetAttachmentsAsync(_record.Id);
            foreach (var attachment in attachments)
            {
                Attachments.Add(attachment);
            }
        }

        private async Task SupplementOtherAttachmentAsync()
        {
            if (!CanSupplementOtherAttachments || _userContextService.CurrentUser == null)
            {
                return;
            }

            var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Title = $"请选择其他附件（{SystemAttachmentUploadSupport.AllowedFormatsDescription}）；请确保文件命名清晰准确",
                Filter = SystemAttachmentUploadSupport.OpenFileDialogFilter
            };
            if (dlg.ShowDialog() != true)
            {
                return;
            }

            bool anySuccess = false;
            foreach (var path in dlg.FileNames)
            {
                try
                {
                    var fi = new FileInfo(path);
                    var fileContent = await File.ReadAllBytesAsync(path);
                    var attachment = new SystemAttachment
                    {
                        FileName = fi.Name,
                        Extension = fi.Extension,
                        FileSize = fi.Length,
                        FileContent = fileContent
                    };
                    var result = await _outboundService.UploadAttachmentFlowAsync(
                        _record.Id,
                        ArchiveOutboundDomainValues.AttachmentKindOther,
                        attachment,
                        _userContextService.CurrentUser);
                    if (result.Success)
                    {
                        anySuccess = true;
                    }
                    else
                    {
                        _dialogService.ShowMessage(result.Message);
                    }
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError($"增补附件失败：{ex.Message}");
                }
            }

            if (anySuccess)
            {
                await LoadAttachmentsAsync();
                _dialogService.ShowMessage("其他附件已增补。", "增补附件");
            }
        }

        private async Task ViewAttachmentAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return;
            }

            try
            {
                var result = await _outboundService.PrepareAttachmentViewFlowAsync(attachment);
                if (!result.Success || result.Attachment?.FileContent == null)
                {
                    _dialogService.ShowMessage(result.Message);
                    return;
                }

                _dialogService.ShowSystemAttachmentView(result.Attachment);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"查看附件失败：{ex.Message}");
            }
        }

        private async Task PrintAsync()
        {
            if (!CanPrint)
            {
                return;
            }

            try
            {
                bool blankApproval = !_record.IsApproved && !_record.IsSignedUploaded && !_record.IsCompleted;
                var data = await _outboundService.BuildPrintDataAsync(_record.Id, blankApproval);
                var document = ArchiveOutboundPrintDocumentFactory.Create(data);
                var exportOptions = new PrintPreviewExportOptions
                {
                    ExportAsync = () => ExportOutboundWordAsync(data)
                };
                var previewWindow = new PrintPreviewWindow(document, exportOptions)
                {
                    Owner = Application.Current.MainWindow
                };

                await _outboundService.RecordPrintAsync(_record.Id);
                previewWindow.ShowDialog();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("打印生成失败：" + ex.Message);
            }
        }

        private Task ExportOutboundWordAsync(ArchiveOutboundPrintData data)
        {
            try
            {
                string defaultName = string.IsNullOrWhiteSpace(data.OutboundNo)
                    ? "年度资料出库申请审批单.docx"
                    : $"{data.OutboundNo}.docx";
                string? path = _dialogService.SaveFileDialog(
                    "Word 文档|*.docx",
                    "导出 Word",
                    defaultName);
                if (string.IsNullOrWhiteSpace(path))
                {
                    return Task.CompletedTask;
                }

                if (!path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".docx";
                }

                _outboundWordExportService.ExportToFile(data, path);
                _dialogService.ShowMessage($"Word 文档已保存：\n{path}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出 Word 失败：" + ex.Message);
            }

            return Task.CompletedTask;
        }

        private void RaiseAllDisplayPropertiesChanged()
        {
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(WorkspaceBannerText));
            OnPropertyChanged(nameof(OutboundNo));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ApplicantName));
            OnPropertyChanged(nameof(ApplicantDept));
            OnPropertyChanged(nameof(ApplyDateDisplay));
            OnPropertyChanged(nameof(Reason));
            OnPropertyChanged(nameof(DestinationDisplayText));
            OnPropertyChanged(nameof(ProofMaterialDisplay));
            OnPropertyChanged(nameof(ExpectedReturnDateDisplay));
            OnPropertyChanged(nameof(MaterialSummary));
            OnPropertyChanged(nameof(DeptAuditOpinion));
            OnPropertyChanged(nameof(DeptAuditor));
            OnPropertyChanged(nameof(DeptAuditDateDisplay));
            OnPropertyChanged(nameof(ArchiveRoomHeadOpinion));
            OnPropertyChanged(nameof(ArchiveRoomHead));
            OnPropertyChanged(nameof(ArchiveRoomHeadDateDisplay));
            OnPropertyChanged(nameof(ProductionHeadOpinion));
            OnPropertyChanged(nameof(ProductionHead));
            OnPropertyChanged(nameof(ProductionHeadDateDisplay));
            OnPropertyChanged(nameof(VicePresidentOpinion));
            OnPropertyChanged(nameof(VicePresident));
            OnPropertyChanged(nameof(VicePresidentDateDisplay));
            OnPropertyChanged(nameof(CanPrint));
            OnPropertyChanged(nameof(CanSupplementOtherAttachments));
        }

        private static string EmptyAsPlaceholder(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "(无)" : value.Trim();

        private static string FormatDate(DateTime? date) =>
            date.HasValue ? date.Value.ToString("yyyy-MM-dd") : "(无)";
    }
}
