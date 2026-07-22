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
    /// 资料建档（登记）申请单只读查看弹窗 ViewModel。仅展示申请、审批流程与附件信息，支持打印与关闭。
    /// 资料室资料管理员可在办结后增补「其他附件」（仅新增、不可删除）。
    /// </summary>
    public sealed class ArchiveRegisterApplicationViewDialogViewModel : ViewModelBase
    {
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IArchiveRegisterWordExportService _archiveRegisterWordExportService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private YearlyArchiveRegisterRecord _record;
        private string[] _uniformOpinions = [];

        public ArchiveRegisterApplicationViewDialogViewModel(
            IArchiveRegisterService archiveRegisterService,
            IArchiveRegisterWordExportService archiveRegisterWordExportService,
            IDialogService dialogService,
            IUserContextService userContextService,
            YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(archiveRegisterService);
            ArgumentNullException.ThrowIfNull(archiveRegisterWordExportService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(userContextService);
            ArgumentNullException.ThrowIfNull(record);

            _archiveRegisterService = archiveRegisterService;
            _archiveRegisterWordExportService = archiveRegisterWordExportService;
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
            PrintCommand = new RelayCommand(_ => Print(), _ => CanPrint);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public string WindowTitle => $"查看申请 · {FormNo} · {StatusDisplay}";

        public string WorkspaceBannerText => CanSupplementOtherAttachments
            ? "本窗口以查看为主；办结后资料室资料管理员可增补「其他附件」，不可删除已有附件。"
            : "本窗口仅用于查看资料建档申请信息，不允许编辑。";

        // 1. 资料信息
        public string MaterialName => EmptyAsPlaceholder(_record.MaterialName);
        public string ProjectName => EmptyAsPlaceholder(_record.ProjectName);
        public string ProvideUnit => EmptyAsPlaceholder(_record.ProvideUnit);
        public string SourceType => EmptyAsPlaceholder(_record.SourceType);
        public string ArchivePurpose => EmptyAsPlaceholder(_record.ArchivePurpose);
        public string OtherRequests => EmptyAsPlaceholder(_record.OtherRequests);
        public string HasProofMaterialDisplay =>
            ArchiveRegisterDomainValues.HasProofMaterial(_record.ProofMaterialNote) ? "是" : "否";
        public string ProofMaterialDisplay =>
            ArchiveRegisterDomainValues.HasProofMaterial(_record.ProofMaterialNote)
                ? EmptyAsPlaceholder(_record.ProofMaterialNote)
                : ArchiveRegisterDomainValues.ProofMaterialNoneText;

        // 2. 申请信息
        public string FormNo => string.IsNullOrWhiteSpace(_record.FormNo) ? "待编单" : _record.FormNo.Trim();
        public string StatusDisplay => _record.StatusStr;
        public string ApplicantName => EmptyAsPlaceholder(_record.ApplicantName);
        public string ApplicantDept => EmptyAsPlaceholder(_record.ApplicantDept);
        public string ApplicantDateDisplay => FormatDate(_record.ApplicantDate);

        // 3. 审批流程（意见栏一致化：空意见不用「(无)」占位）
        public string DeptLeader => EmptyAsPlaceholder(_record.DeptLeader);
        public string DeptDateDisplay => FormatDate(_record.DeptDate);
        public string ProdDeptOpinion => _uniformOpinions[0];
        public string ProdLeader => EmptyAsPlaceholder(_record.ProdLeader);
        public string ProdDateDisplay => FormatDate(_record.ProdDate);
        public string RndDeptOpinion => _uniformOpinions[1];
        public string RndLeader => EmptyAsPlaceholder(_record.RndLeader);
        public string RndDateDisplay => FormatDate(_record.RndDate);
        public string DeputyOpinion => _uniformOpinions[2];
        public string DeputyLeader => EmptyAsPlaceholder(_record.DeputyLeader);
        public string DeputyDateDisplay => FormatDate(_record.DeputyDate);
        public string Deliverer => EmptyAsPlaceholder(_record.Deliverer);
        public string DeliverDateDisplay => FormatDate(_record.DeliverDate);
        public string Administrator => EmptyAsPlaceholder(_record.Administrator);
        public string AdminDateDisplay => FormatDate(_record.AdminDate);

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public bool CanPrint => _record.Id > 0;

        /// <summary>办结后，资料室资料管理员可增补其他附件。</summary>
        public bool CanSupplementOtherAttachments =>
            _record.Id > 0
            && _record.IsArchived
            && _archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser);

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

            var loaded = await _archiveRegisterService.GetByIdAsync(_record.Id);
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
                _record.ProdDeptOpinion,
                _record.RndDeptOpinion,
                _record.DeputyOpinion);
        }

        private async Task LoadAttachmentsAsync()
        {
            Attachments.Clear();
            if (string.IsNullOrWhiteSpace(_record.FormNo))
            {
                return;
            }

            var attachments = await _archiveRegisterService.GetAttachmentsByFormNoAsync(_record.FormNo);
            foreach (var attachment in attachments)
            {
                Attachments.Add(attachment);
            }
        }

        private async Task SupplementOtherAttachmentAsync()
        {
            if (!CanSupplementOtherAttachments)
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
                    var result = await _archiveRegisterService.UploadAttachmentFlowAsync(
                        _record,
                        _userContextService.CurrentUser,
                        ArchiveRegisterDomainValues.AttachmentKindOther,
                        fi.Name,
                        fi.Extension,
                        fi.Length,
                        fileContent);
                    if (result.Success && result.Attachment != null)
                    {
                        Attachments.Add(result.Attachment);
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
                var result = await _archiveRegisterService.PrepareAttachmentViewFlowAsync(attachment);
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

        private void Print()
        {
            if (!CanPrint)
            {
                return;
            }

            try
            {
                var data = _archiveRegisterService.BuildPrintData(_record, _record.SourceType, _record.MediaEntries);
                var document = ArchiveRegisterPrintDocumentFactory.Create(data, isApplicationPrint: false);
                var exportOptions = new PrintPreviewExportOptions
                {
                    ExportAsync = () => ExportArchiveRegisterWordAsync(data)
                };
                var previewWindow = new PrintPreviewWindow(document, exportOptions)
                {
                    Owner = Application.Current.MainWindow
                };
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("打印生成失败：" + ex.Message);
            }
        }

        private Task ExportArchiveRegisterWordAsync(ArchiveRegisterPrintData data)
        {
            try
            {
                string defaultName = string.IsNullOrWhiteSpace(data.FormNo)
                    ? "年度资料入档申请审批单.docx"
                    : $"{data.FormNo}.docx";
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

                _archiveRegisterWordExportService.ExportToFile(data, path);
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
            OnPropertyChanged(nameof(MaterialName));
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProvideUnit));
            OnPropertyChanged(nameof(SourceType));
            OnPropertyChanged(nameof(ArchivePurpose));
            OnPropertyChanged(nameof(OtherRequests));
            OnPropertyChanged(nameof(HasProofMaterialDisplay));
            OnPropertyChanged(nameof(ProofMaterialDisplay));
            OnPropertyChanged(nameof(FormNo));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(ApplicantName));
            OnPropertyChanged(nameof(ApplicantDept));
            OnPropertyChanged(nameof(ApplicantDateDisplay));
            OnPropertyChanged(nameof(DeptLeader));
            OnPropertyChanged(nameof(DeptDateDisplay));
            OnPropertyChanged(nameof(ProdDeptOpinion));
            OnPropertyChanged(nameof(ProdLeader));
            OnPropertyChanged(nameof(ProdDateDisplay));
            OnPropertyChanged(nameof(RndDeptOpinion));
            OnPropertyChanged(nameof(RndLeader));
            OnPropertyChanged(nameof(RndDateDisplay));
            OnPropertyChanged(nameof(DeputyOpinion));
            OnPropertyChanged(nameof(DeputyLeader));
            OnPropertyChanged(nameof(DeputyDateDisplay));
            OnPropertyChanged(nameof(Deliverer));
            OnPropertyChanged(nameof(DeliverDateDisplay));
            OnPropertyChanged(nameof(Administrator));
            OnPropertyChanged(nameof(AdminDateDisplay));
            OnPropertyChanged(nameof(CanPrint));
            OnPropertyChanged(nameof(CanSupplementOtherAttachments));
        }

        private static string EmptyAsPlaceholder(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "(无)" : value.Trim();

        private static string FormatDate(DateTime? date) =>
            date.HasValue ? date.Value.ToString("yyyy-MM-dd") : "(无)";
    }
}
