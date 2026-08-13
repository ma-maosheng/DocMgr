using System.Collections.ObjectModel;
using DocMgr.Models;
using DocMgr.Models.NetworkTransfer;

namespace DocMgr.ViewModels.NetworkTransfer;

public sealed partial class NetworkInboundEditDialogViewModel
{
    private const string ArchiveRoomDepartmentName = NetworkTransferDomainValues.InboundProvideUnitArchiveRoom;

    private bool _suppressProvideUnitDefault;
    private string _provideUnit = string.Empty;

    /// <summary>档外资料（内部）时可选的院内部门（不含资料室）。</summary>
    public ObservableCollection<Department> InternalDepartments { get; } = new();

    /// <summary>是否为档外资料（内部）来源。</summary>
    public bool IsExternalOfflineInternalSource =>
        NetworkTransferDomainValues.IsExternalOfflineInternalSource(SourceKind);

    /// <summary>是否为档外资料（外部）来源。</summary>
    public bool IsExternalOfflineExternalSource =>
        NetworkTransferDomainValues.IsExternalOfflineExternalSource(SourceKind);

    /// <summary>草稿可编辑且为档外资料（内部）时，提供部门使用下拉框。</summary>
    public bool ShowProvideUnitDepartmentCombo => CanEditForm && IsExternalOfflineInternalSource;

    /// <summary>草稿可编辑且为档外资料（外部）时，提供部门使用文本框。</summary>
    public bool ShowProvideUnitExternalTextBox => CanEditForm && IsExternalOfflineExternalSource;

    /// <summary>立档资料或不可编辑时，提供部门只读展示。</summary>
    public bool ShowProvideUnitReadOnlyText =>
        !CanEditForm || NetworkTransferDomainValues.IsArchivedElectronicSearchSource(SourceKind);

    /// <summary>提供部门（单位）。</summary>
    public string ProvideUnit
    {
        get => _provideUnit;
        set => SetProperty(ref _provideUnit, value);
    }

    /// <summary>审批只读展示用提供部门（单位）。</summary>
    public string ProvideUnitDisplay =>
        NetworkTransferDomainValues.ResolveInboundProvideUnit(_record.SourceKind, _record.ProvideUnit);

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

    private void BindProvideUnitFromRecord()
    {
        ProvideUnit = NetworkTransferDomainValues.ResolveInboundProvideUnit(
            _record.SourceKind,
            _record.ProvideUnit);
    }

    private void ApplyProvideUnitSideEffectsForSourceKind(string? previousSourceKind)
    {
        OnPropertyChanged(nameof(IsExternalOfflineInternalSource));
        OnPropertyChanged(nameof(IsExternalOfflineExternalSource));
        OnPropertyChanged(nameof(ShowProvideUnitDepartmentCombo));
        OnPropertyChanged(nameof(ShowProvideUnitExternalTextBox));
        OnPropertyChanged(nameof(ShowProvideUnitReadOnlyText));
        OnPropertyChanged(nameof(ProvideUnitDisplay));

        if (_suppressProvideUnitDefault)
        {
            return;
        }

        if (NetworkTransferDomainValues.IsArchivedElectronicSearchSource(SourceKind))
        {
            ProvideUnit = ArchiveRoomDepartmentName;
            return;
        }

        if (NetworkTransferDomainValues.IsExternalOfflineExternalSource(SourceKind))
        {
            if (NetworkTransferDomainValues.IsArchivedElectronicSearchSource(previousSourceKind)
                || NetworkTransferDomainValues.IsExternalOfflineInternalSource(previousSourceKind))
            {
                ProvideUnit = string.Empty;
            }

            return;
        }

        if (NetworkTransferDomainValues.IsExternalOfflineInternalSource(SourceKind))
        {
            ApplyDefaultProvideUnitForInternalOffline(onlyWhenEmpty: false);
        }
    }

    /// <summary>档外资料（内部）时，将提供部门默认设为申请人所在部门。</summary>
    private void ApplyDefaultProvideUnitForInternalOffline(bool onlyWhenEmpty)
    {
        if (!NetworkTransferDomainValues.IsExternalOfflineInternalSource(SourceKind))
        {
            return;
        }

        if (onlyWhenEmpty && !string.IsNullOrWhiteSpace(ProvideUnit))
        {
            return;
        }

        string applicantDept = ApplicantDept?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(applicantDept)
            || string.Equals(applicantDept, ArchiveRoomDepartmentName, StringComparison.Ordinal))
        {
            ProvideUnit = InternalDepartments.FirstOrDefault()?.Name?.Trim() ?? string.Empty;
            return;
        }

        ProvideUnit = applicantDept;
    }

    private string ResolveDraftProvideUnit() =>
        NetworkTransferDomainValues.ResolveInboundProvideUnit(SourceKind, ProvideUnit);
}
