using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class OutboundUsageModeOption
    {
        public OutboundUsageModeOption(string value, string display)
        {
            Value = value;
            Display = display;
        }

        public string Value { get; }

        public string Display { get; }
    }

    /// <summary>
    /// 借出申请明细行，用于编辑对话框中的资料列表。
    /// 领用方式、归还与库内空盘等设置由 <see cref="ArchiveOutboundContainerUnitViewModel"/> 按盒/袋单元统一管理。
    /// </summary>
    public sealed class ArchiveOutboundItemRowViewModel : ViewModelBase
    {
        private readonly YearlyArchiveOutboundItem _item;
        private bool _canEdit;

        public ArchiveOutboundItemRowViewModel(
            YearlyArchiveOutboundItem item,
            bool canEdit,
            IDialogService dialogService,
            Func<ArchiveOutboundItemRowViewModel, Task>? removeAsync = null,
            Func<ArchiveOutboundItemRowViewModel, Task>? viewDetailAsync = null)
        {
            _item = item;
            _canEdit = canEdit;
            _ = dialogService;

            ViewDetailCommand = new RelayCommand(
                async _ =>
                {
                    if (viewDetailAsync != null)
                    {
                        await viewDetailAsync(this);
                    }
                });
            RemoveCommand = new RelayCommand(
                async _ =>
                {
                    if (removeAsync != null)
                    {
                        await removeAsync(this);
                    }
                },
                _ => CanEdit);

            RevokeRegistrationCommand = new RelayCommand(
                async _ =>
                {
                    if (removeAsync != null)
                    {
                        await removeAsync(this);
                    }
                },
                _ => CanEdit && IsFromSearchResultSetRegistration);
        }

        public YearlyArchiveOutboundItem Source => _item;

        public RelayCommand ViewDetailCommand { get; }

        public RelayCommand RemoveCommand { get; }

        /// <summary>
        /// 撤销通过检索集登记的拟领用资料。
        /// </summary>
        public RelayCommand RevokeRegistrationCommand { get; }

        /// <summary>
        /// 是否由「通过检索集登记拟领用资料」写入。
        /// </summary>
        public bool IsFromSearchResultSetRegistration => _item.SourceResultSetId is > 0;

        public bool CanEdit
        {
            get => _canEdit;
            set
            {
                if (SetProperty(ref _canEdit, value))
                {
                    OnPropertyChanged(nameof(IsCopyCountEditable));
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ItemArchiveYearDisplay => _item.ItemArchiveYear?.ToString() ?? string.Empty;

        public string ItemProjectName => _item.ItemProjectName ?? string.Empty;

        public string MediaKind => _item.MediaKind;

        public string MediaType => _item.MediaType;

        public string MaterialName => _item.MaterialName;

        public string ItemName => _item.ItemName;

        public string ConfidentialLevel => _item.ConfidentialLevel;

        public string ArchivePurpose => _item.ArchivePurpose;

        public string SelectionScopeDisplay => _item.SelectionScopeDisplay;

        public string ContainerCode => _item.ContainerCode;

        public string CurrentStorageLocation => _item.CurrentStorageLocation;

        public string UsageModeDisplay => _item.UsageModeDisplay;

        public int? CopyCount
        {
            get => _item.CopyCount;
            set
            {
                if (IsElectronicMedia)
                {
                    if (_item.CopyCount != 1)
                    {
                        _item.CopyCount = 1;
                        OnPropertyChanged();
                    }

                    return;
                }

                if (_item.CopyCount == value)
                {
                    return;
                }

                _item.CopyCount = value;
                OnPropertyChanged();
            }
        }

        public bool IsElectronicMedia =>
            string.Equals(_item.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal);

        public bool IsCopyCountEditable =>
            CanEdit && !IsElectronicMedia;

        public string NeedReturnDisplay => _item.NeedReturnDisplay;

        public string RequisitionedDiskNeedReturnDisplay => _item.RequisitionedDiskNeedReturnDisplay;

        public string RequisitionedDiskCode => _item.RequisitionedDiskCode;

        /// <summary>
        /// 单元级设置写回实体后，刷新行内展示属性。
        /// </summary>
        public void RefreshDisplayProperties()
        {
            OnPropertyChanged(nameof(UsageModeDisplay));
            OnPropertyChanged(nameof(CopyCount));
            OnPropertyChanged(nameof(IsCopyCountEditable));
            OnPropertyChanged(nameof(NeedReturnDisplay));
            OnPropertyChanged(nameof(RequisitionedDiskNeedReturnDisplay));
            OnPropertyChanged(nameof(RequisitionedDiskCode));
        }

        public static IReadOnlyList<OutboundUsageModeOption> BuildUsageModeOptions(
            string mediaKind,
            string? archivePurpose = null)
        {
            if (string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal))
            {
                return new[]
                {
                    new OutboundUsageModeOption(ArchiveOutboundDomainValues.UsageModeWithdrawal, "提档"),
                    new OutboundUsageModeOption(ArchiveOutboundDomainValues.UsageModeCopy, "复制")
                };
            }

            if (string.Equals(mediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
            {
                if (ArchiveOutboundDomainValues.IsLongTermElectronicArchivePurpose(archivePurpose))
                {
                    return new[]
                    {
                        new OutboundUsageModeOption(ArchiveOutboundDomainValues.UsageModeDuplicate, "拷贝")
                    };
                }

                return new[]
                {
                    new OutboundUsageModeOption(ArchiveOutboundDomainValues.UsageModeWithdrawal, "提档"),
                    new OutboundUsageModeOption(ArchiveOutboundDomainValues.UsageModeDuplicate, "拷贝")
                };
            }

            return new[]
            {
                new OutboundUsageModeOption(ArchiveOutboundDomainValues.UsageModeWithdrawal, "提档")
            };
        }
    }
}
