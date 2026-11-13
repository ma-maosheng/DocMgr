using System.ComponentModel;

using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.YearlyArchive;

public partial class ArchiveRegisterViewModel
{
    private bool _suppressRequestHeaderWriteBack;

    /// <summary>与入网申请共用的申请信息头。</summary>
    public ApplicationRequestHeaderModel RequestHeader { get; } = new();

    private void AttachRequestHeader()
    {
        RequestHeader.SaveDraftCommand = SaveDraftCommand;
        RequestHeader.SubmitCommand = SubmitApplicationsCommand;
        RequestHeader.PrintCommand = PrintApplicationsCommand;
        RequestHeader.PropertyChanged += OnRequestHeaderPropertyChanged;
        SyncRequestHeaderChrome();
    }

    private void LoadRequestHeaderFromRecord()
    {
        if (CurrentRecord == null)
        {
            return;
        }

        _suppressRequestHeaderWriteBack = true;
        try
        {
            RequestHeader.ApplicantName = CurrentRecord.ApplicantName;
            RequestHeader.ApplicantDept = CurrentRecord.ApplicantDept;
            RequestHeader.ApplicationDate = CurrentRecord.ApplicantDate == default
                ? DateTime.Today
                : CurrentRecord.ApplicantDate;
            RequestHeader.ApplicationReason = CurrentRecord.Reason;
            RequestHeader.OtherRequests = CurrentRecord.OtherRequests;
            RequestHeader.HasProofMaterial = HasProofMaterial;
            RequestHeader.ProofMaterialName = ProofMaterialName;
            SyncRequestHeaderChrome();
        }
        finally
        {
            _suppressRequestHeaderWriteBack = false;
        }
    }

    private void SyncRequestHeaderChrome()
    {
        RequestHeader.CanEdit = CanEditForm;
        RequestHeader.CanEditApplicationDate = CanEditForm;
        RequestHeader.ShowSubmitActions = ShowApplicationSubmitActions;
        RequestHeader.ShowEmbeddedActionButtons = ShowEmbeddedActionButtons;
    }

    private void OnRequestHeaderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressRequestHeaderWriteBack || CurrentRecord == null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ApplicationRequestHeaderModel.ApplicationReason):
                CurrentRecord.Reason = RequestHeader.ApplicationReason;
                break;
            case nameof(ApplicationRequestHeaderModel.OtherRequests):
                CurrentRecord.OtherRequests = RequestHeader.OtherRequests;
                break;
            case nameof(ApplicationRequestHeaderModel.ApplicationDate):
                if (RequestHeader.ApplicationDate is DateTime applicationDate)
                {
                    CurrentRecord.ApplicantDate = applicationDate;
                }

                break;
            case nameof(ApplicationRequestHeaderModel.HasProofMaterial):
                HasProofMaterial = RequestHeader.HasProofMaterial;
                if (!RequestHeader.HasProofMaterial && RequestHeader.ProofMaterialName.Length > 0)
                {
                    _suppressRequestHeaderWriteBack = true;
                    RequestHeader.ProofMaterialName = string.Empty;
                    _suppressRequestHeaderWriteBack = false;
                }

                break;
            case nameof(ApplicationRequestHeaderModel.ProofMaterialName):
                ProofMaterialName = RequestHeader.ProofMaterialName;
                break;
        }
    }
}
