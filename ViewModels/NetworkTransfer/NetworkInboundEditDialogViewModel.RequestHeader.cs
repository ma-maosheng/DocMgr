using System.ComponentModel;

using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.NetworkTransfer;

public sealed partial class NetworkInboundEditDialogViewModel
{
    private bool _suppressRequestHeaderWriteBack;

    /// <summary>与建档申请共用的申请信息头。</summary>
    public ApplicationRequestHeaderModel RequestHeader { get; } = new();

    private void AttachRequestHeader()
    {
        RequestHeader.SaveDraftCommand = SaveDraftCommand;
        RequestHeader.SubmitCommand = SubmitApplicationsCommand;
        RequestHeader.PrintCommand = PrintApplicationsCommand;
        RequestHeader.CanEditApplicationDate = false;
        RequestHeader.PropertyChanged += OnRequestHeaderPropertyChanged;
        SyncRequestHeaderChrome();
    }

    private void LoadRequestHeaderFromRecord()
    {
        _suppressRequestHeaderWriteBack = true;
        try
        {
            RequestHeader.ApplicantName = _record.ApplicantName;
            RequestHeader.ApplicantDept = _record.ApplicantDept;
            RequestHeader.ApplicationDate = _record.ApplyTime == default ? DateTime.Now : _record.ApplyTime;
            RequestHeader.ApplicationReason = _record.Reason;
            RequestHeader.OtherRequests = _record.OtherRequests;
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
        RequestHeader.CanEditApplicationDate = false;
        RequestHeader.ShowSubmitActions = ShowApplicationSubmitActions;
        RequestHeader.ShowEmbeddedActionButtons = ShowEmbeddedActionButtons;
    }

    private void OnRequestHeaderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressRequestHeaderWriteBack)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ApplicationRequestHeaderModel.ApplicationReason):
                _record.Reason = RequestHeader.ApplicationReason;
                if (!string.Equals(Reason, _record.Reason, StringComparison.Ordinal))
                {
                    Reason = _record.Reason;
                }

                break;
            case nameof(ApplicationRequestHeaderModel.OtherRequests):
                _record.OtherRequests = RequestHeader.OtherRequests;
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
