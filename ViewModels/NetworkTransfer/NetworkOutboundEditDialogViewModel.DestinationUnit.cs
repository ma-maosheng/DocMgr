using System.Collections.ObjectModel;
using DocMgr.Models;
using DocMgr.Models.NetworkTransfer;

namespace DocMgr.ViewModels.NetworkTransfer;

public sealed partial class NetworkOutboundEditDialogViewModel
{
    private const string ArchiveRoomDepartmentName = NetworkTransferDomainValues.InboundProvideUnitArchiveRoom;

    private bool _suppressDestinationUnitDefault;
    private string _destinationUnit = string.Empty;

    /// <summary>出网（院内）时可选的院内部门（不含资料室）。</summary>
    public ObservableCollection<Department> InternalDepartments { get; } = new();

    /// <summary>是否为出网（院内）目的地。</summary>
    public bool IsOutboundInternalDestination =>
        NetworkTransferDomainValues.IsOutboundInternalDestination(DestinationKind);

    /// <summary>是否为出网（院外）目的地。</summary>
    public bool IsOutboundExternalDestination =>
        NetworkTransferDomainValues.IsOutboundExternalDestination(DestinationKind);

    /// <summary>院内/院外目的地时显示接收部门（单位）输入。</summary>
    public bool ShowDestinationUnit =>
        NetworkTransferDomainValues.IsExternalOfflineDestination(DestinationKind);

    /// <summary>草稿可编辑且为出网（院内）时，接收部门使用下拉框。</summary>
    public bool ShowDestinationUnitDepartmentCombo => CanEditForm && IsOutboundInternalDestination;

    /// <summary>草稿可编辑且为出网（院外）时，接收单位使用文本框。</summary>
    public bool ShowDestinationUnitExternalTextBox => CanEditForm && IsOutboundExternalDestination;

    /// <summary>不可编辑且仍为院内/院外时，接收部门只读展示。</summary>
    public bool ShowDestinationUnitReadOnlyText => !CanEditForm && ShowDestinationUnit;

    /// <summary>接收部门（单位）。</summary>
    public string DestinationUnit
    {
        get => _destinationUnit;
        set
        {
            if (SetProperty(ref _destinationUnit, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(DestinationUnitDisplay));
            }
        }
    }

    /// <summary>审批只读展示用接收部门（单位）；无值时显示「(无)」。</summary>
    public string DestinationUnitDisplay
    {
        get
        {
            string resolved = NetworkTransferDomainValues.ResolveOutboundDestinationUnit(
                DestinationKind,
                DestinationUnit);
            return string.IsNullOrWhiteSpace(resolved) ? "(无)" : resolved;
        }
    }

    private void LoadInternalDepartments()
    {
        InternalDepartments.Clear();
        foreach (Department department in _userService.GetAllDepartments()
                     .Where(item => !string.Equals(item.Name?.Trim(), ArchiveRoomDepartmentName, StringComparison.Ordinal))
                     .OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            InternalDepartments.Add(department);
        }
    }

    private void BindDestinationUnitFromRecord()
    {
        DestinationUnit = NetworkTransferDomainValues.ResolveOutboundDestinationUnit(
            DestinationKind,
            _record.DestinationUnit);
    }

    private void ApplyDestinationUnitSideEffectsForDestinationKind(string? previousDestinationKind)
    {
        OnPropertyChanged(nameof(IsOutboundInternalDestination));
        OnPropertyChanged(nameof(IsOutboundExternalDestination));
        OnPropertyChanged(nameof(ShowDestinationUnit));
        OnPropertyChanged(nameof(ShowDestinationUnitDepartmentCombo));
        OnPropertyChanged(nameof(ShowDestinationUnitExternalTextBox));
        OnPropertyChanged(nameof(ShowDestinationUnitReadOnlyText));
        OnPropertyChanged(nameof(DestinationUnitDisplay));

        if (_suppressDestinationUnitDefault)
        {
            return;
        }

        if (!NetworkTransferDomainValues.IsExternalOfflineDestination(DestinationKind))
        {
            DestinationUnit = string.Empty;
            return;
        }

        if (NetworkTransferDomainValues.IsOutboundExternalDestination(DestinationKind))
        {
            if (NetworkTransferDomainValues.IsOutboundInternalDestination(previousDestinationKind)
                || NetworkTransferDomainValues.IsArchiveFilingDestination(previousDestinationKind))
            {
                DestinationUnit = string.Empty;
            }

            return;
        }

        if (NetworkTransferDomainValues.IsOutboundInternalDestination(DestinationKind))
        {
            ApplyDefaultDestinationUnitForInternal(onlyWhenEmpty: false);
        }
    }

    /// <summary>出网（院内）时，将接收部门默认设为申请人所在部门。</summary>
    private void ApplyDefaultDestinationUnitForInternal(bool onlyWhenEmpty)
    {
        if (!NetworkTransferDomainValues.IsOutboundInternalDestination(DestinationKind))
        {
            return;
        }

        if (onlyWhenEmpty && !string.IsNullOrWhiteSpace(DestinationUnit))
        {
            return;
        }

        string applicantDept = ApplicantDept?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(applicantDept)
            || string.Equals(applicantDept, ArchiveRoomDepartmentName, StringComparison.Ordinal))
        {
            DestinationUnit = InternalDepartments.FirstOrDefault()?.Name?.Trim() ?? string.Empty;
            return;
        }

        DestinationUnit = applicantDept;
    }

    private string ResolveDraftDestinationUnit() =>
        NetworkTransferDomainValues.ResolveOutboundDestinationUnit(DestinationKind, DestinationUnit);

    private void EnsureDestinationUnitDefaultAfterBind()
    {
        if (NetworkTransferDomainValues.IsOutboundInternalDestination(DestinationKind)
            && string.IsNullOrWhiteSpace(DestinationUnit))
        {
            ApplyDefaultDestinationUnitForInternal(onlyWhenEmpty: true);
        }
    }

    private void NotifyDestinationUnitEditState()
    {
        OnPropertyChanged(nameof(ShowDestinationUnit));
        OnPropertyChanged(nameof(ShowDestinationUnitDepartmentCombo));
        OnPropertyChanged(nameof(ShowDestinationUnitExternalTextBox));
        OnPropertyChanged(nameof(ShowDestinationUnitReadOnlyText));
        OnPropertyChanged(nameof(DestinationUnitDisplay));
    }
}
