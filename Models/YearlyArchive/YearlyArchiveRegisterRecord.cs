using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;

namespace DocMgr.Models.YearlyArchive
{
    // 年度资料【登记】申请记录表
    public class YearlyArchiveRegisterRecord : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public const int Unsubmitted = ApplicationWorkflowStatus.Draft;
        public const int Submitted = ApplicationWorkflowStatus.Submitted;
        public const int Approved = ApplicationWorkflowStatus.Approved;
        public const int SignedUploaded = ApplicationWorkflowStatus.SignedUploaded;
        public const int Completed = ApplicationWorkflowStatus.Completed;
        public const int WithdrawnVoid = ApplicationWorkflowStatus.Withdrawn;
        public const int ForceVoided = ApplicationWorkflowStatus.ForceWithdrawn;
        public const int Draft = Unsubmitted;
        public const int ApprovedReceived = Approved;
        public const int Archived = Completed;
        public const int TrackPending = 0;
        public const int TrackArchived = 1;

        /// <summary>
        /// 主键
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 申请单编号
        /// </summary>
        public string FormNo { get; set; } = string.Empty;

        /// <summary>来源出网申请单 Id；非出网转入草稿时为空。</summary>
        public int? SourceNetworkOutboundRecordId { get; set; }

        /// <summary>来源出网申请单编号快照。</summary>
        public string SourceNetworkOutboundNo { get; set; } = string.Empty;

        /// <summary>跨域业务链 Id；用于从建档申请反查来源出网业务。</summary>
        public int? BusinessChainId { get; set; }

        public NetworkArchiveBusinessChain? BusinessChain { get; set; }

        /// <summary>
        /// 状态
        /// </summary>
        public int Status { get; set; } = Unsubmitted;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// 归档时间
        /// </summary>
        public DateTime? ArchivedDate { get; set; }

        /// <summary>
        /// 模拟介质立档状态
        /// </summary>
        public int SimulatedArchiveStatus { get; set; } = TrackPending;

        /// <summary>
        /// 电子介质立档状态
        /// </summary>
        public int ElectronicArchiveStatus { get; set; } = TrackPending;

        /// <summary>
        /// 归档盒集合
        /// </summary>
        public virtual List<YearlyArchiveBox> ArchiveBoxes { get; set; } = new List<YearlyArchiveBox>();

        /// <summary>
        /// 电子介质立档单元集合
        /// </summary>
        public virtual List<YearlyElectronicArchiveUnit> ElectronicArchiveUnits { get; set; } = new List<YearlyElectronicArchiveUnit>();

        [NotMapped]
        public string ArchiveBoxNos
        {
            get
            {
                return JoinDisplayValues(
                    ArchiveBoxes.Select(b => b.ArchiveSequenceNo)
                        .Concat(ElectronicArchiveUnits.Select(unit => unit.ElectronicArchiveNo)));
            }
        }

        [NotMapped]
        public string ArchiveBoxLocations
        {
            get
            {
                return JoinDisplayValues(
                    ArchiveBoxes.Select(b => b.BoxLocationCode)
                        .Concat(ElectronicArchiveUnits.Select(unit => unit.StorageLocation)));
            }
        }

        private static string JoinDisplayValues(IEnumerable<string?> values)
        {
            var items = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct()
                .ToList();

            return items.Count == 0 ? "-" : string.Join("; ", items);
        }

        /// <summary>
        /// 所属项目Id
        /// </summary>
        public int? ProjectId { get; set; }

        /// <summary>
        /// 所属项目
        /// </summary>
        public string? ProjectName
        {
            get => _projectName;
            set
            {
                if (string.Equals(_projectName, value, StringComparison.Ordinal))
                {
                    return;
                }

                _projectName = value;
                NotifyPropertyChanged();
            }
        }

        private string? _projectName;

        /// <summary>
        /// 资料名称
        /// </summary>
        public string MaterialName
        {
            get => _materialName;
            set
            {
                string v = value ?? string.Empty;
                if (string.Equals(_materialName, v, StringComparison.Ordinal))
                {
                    return;
                }

                _materialName = v;
                NotifyPropertyChanged();
            }
        }

        private string _materialName = string.Empty;

        /// <summary>
        /// 资料来源
        /// </summary>
        public string SourceType
        {
            get => _sourceType;
            set
            {
                string v = value ?? string.Empty;
                if (string.Equals(_sourceType, v, StringComparison.Ordinal))
                {
                    return;
                }

                _sourceType = v;
                NotifyPropertyChanged();
            }
        }

        private string _sourceType = string.Empty;

        /// <summary>
        /// 提供单位
        /// </summary>
        public string ProvideUnit
        {
            get => _provideUnit;
            set
            {
                string v = value ?? string.Empty;
                if (string.Equals(_provideUnit, v, StringComparison.Ordinal))
                {
                    return;
                }

                _provideUnit = v;
                NotifyPropertyChanged();
            }
        }

        private string _provideUnit = string.Empty;

        /// <summary>
        /// 存档目的
        /// </summary>
        public string ArchivePurpose
        {
            get => _archivePurpose;
            set
            {
                string v = value ?? string.Empty;
                if (string.Equals(_archivePurpose, v, StringComparison.Ordinal))
                {
                    return;
                }

                _archivePurpose = v;
                NotifyPropertyChanged();
            }
        }

        private string _archivePurpose = string.Empty;

        /// <summary>
        /// 证明材料备注（「无」表示未附证明材料；有材料时填写名称）。
        /// </summary>
        public string ProofMaterialNote
        {
            get => _proofMaterialNote;
            set
            {
                string v = value ?? string.Empty;
                if (string.Equals(_proofMaterialNote, v, StringComparison.Ordinal))
                {
                    return;
                }

                _proofMaterialNote = v;
                NotifyPropertyChanged();
            }
        }

        private string _proofMaterialNote = ArchiveRegisterDomainValues.ProofMaterialNoneText;

        /// <summary>
        /// 其他要求
        /// </summary>
        public string OtherRequests
        {
            get => _otherRequests;
            set
            {
                string v = value ?? string.Empty;
                if (string.Equals(_otherRequests, v, StringComparison.Ordinal))
                {
                    return;
                }

                _otherRequests = v;
                NotifyPropertyChanged();
            }
        }

        private string _otherRequests = string.Empty;

        /// <summary>
        /// 申请人
        /// </summary>
        public string ApplicantName
        {
            get => _applicantName;
            set
            {
                string v = value ?? string.Empty;
                if (string.Equals(_applicantName, v, StringComparison.Ordinal))
                {
                    return;
                }

                _applicantName = v;
                NotifyPropertyChanged();
            }
        }

        private string _applicantName = string.Empty;

        /// <summary>
        /// 申请部门
        /// </summary>
        public string ApplicantDept
        {
            get => _applicantDept;
            set
            {
                string v = value ?? string.Empty;
                if (string.Equals(_applicantDept, v, StringComparison.Ordinal))
                {
                    return;
                }

                _applicantDept = v;
                NotifyPropertyChanged();
            }
        }

        private string _applicantDept = string.Empty;

        /// <summary>
        /// 申请日期
        /// </summary>
        public DateTime ApplicantDate
        {
            get => _applicantDate;
            set
            {
                if (_applicantDate == value)
                {
                    return;
                }

                _applicantDate = value;
                NotifyPropertyChanged();
            }
        }

        private DateTime _applicantDate;

        /// <summary>
        /// 生产管理科意见
        /// </summary>
        public string ProdDeptOpinion { get; set; } = string.Empty;

        /// <summary>
        /// 生产管理科签字
        /// </summary>
        public string ProdLeader { get; set; } = string.Empty;

        /// <summary>
        /// 生产管理科日期
        /// </summary>
        public DateTime? ProdDate { get; set; }

        /// <summary>
        /// 科研开发室意见
        /// </summary>
        public string RndDeptOpinion { get; set; } = string.Empty;

        /// <summary>
        /// 科研开发室签字
        /// </summary>
        public string RndLeader { get; set; } = string.Empty;

        /// <summary>
        /// 科研开发室日期
        /// </summary>
        public DateTime? RndDate { get; set; }

        /// <summary>
        /// 分管领导意见
        /// </summary>
        public string DeputyOpinion { get; set; } = string.Empty;

        /// <summary>
        /// 分管领导签字
        /// </summary>
        public string DeputyLeader { get; set; } = string.Empty;

        /// <summary>
        /// 分管领导日期
        /// </summary>
        public DateTime? DeputyDate { get; set; }

        /// <summary>
        /// 移交人
        /// </summary>
        public string Deliverer { get; set; } = string.Empty;

        /// <summary>
        /// 移交日期
        /// </summary>
        public DateTime? DeliverDate { get; set; }

        /// <summary>
        /// 资料管理员
        /// </summary>
        public string Administrator { get; set; } = string.Empty;

        /// <summary>
        /// 接收日期
        /// </summary>
        public DateTime? AdminDate { get; set; }

        /// <summary>
        /// 部门负责人
        /// </summary>
        public string DeptLeader { get; set; } = string.Empty;

        /// <summary>
        /// 部门审核日期
        /// </summary>
        public DateTime? DeptDate { get; set; }

        [NotMapped]
        public bool IsDraft => Status == Unsubmitted;

        [NotMapped]
        public bool IsSubmitted => Status == Submitted;

        [NotMapped]
        public bool IsApprovedReceived => Status == Approved;

        [NotMapped]
        public bool IsSignedUploaded => Status == SignedUploaded;

        [NotMapped]
        public bool IsArchived => Status == Completed;

        [NotMapped]
        public bool IsWithdrawnVoid => Status == WithdrawnVoid;

        [NotMapped]
        public bool IsForceVoided => Status == ForceVoided;

        [NotMapped]
        public bool HasElectronicMedia => MediaEntries.Any(m => string.Equals(m.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal));

        [NotMapped]
        public bool HasSimulatedMedia => MediaEntries.Any(m => string.Equals(m.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal));

        [NotMapped]
        public bool IsElectronicArchived => !HasElectronicMedia || ElectronicArchiveStatus == TrackArchived;

        [NotMapped]
        public bool IsSimulatedArchived => !HasSimulatedMedia || SimulatedArchiveStatus == TrackArchived;

        [NotMapped]
        public bool IsApprovedOrArchived => IsApprovedReceived || IsSignedUploaded || IsArchived;

        [NotMapped]
        public bool HasApprovalInput =>
            !string.IsNullOrWhiteSpace(ProdDeptOpinion)
            || !string.IsNullOrWhiteSpace(ProdLeader)
            || ProdDate.HasValue
            || !string.IsNullOrWhiteSpace(RndDeptOpinion)
            || !string.IsNullOrWhiteSpace(RndLeader)
            || RndDate.HasValue
            || !string.IsNullOrWhiteSpace(DeputyOpinion)
            || !string.IsNullOrWhiteSpace(DeputyLeader)
            || DeputyDate.HasValue
            || !string.IsNullOrWhiteSpace(Deliverer)
            || DeliverDate.HasValue
            || !string.IsNullOrWhiteSpace(Administrator)
            || AdminDate.HasValue
            || !string.IsNullOrWhiteSpace(DeptLeader)
            || DeptDate.HasValue;

        [NotMapped]
        public bool CanApplicantModifyOrDelete => IsDraft || IsSubmitted;

        [NotMapped]
        public bool CanCancelRegister => Id > 0 && (IsDraft || IsSubmitted) && !HasApprovalInput;

        [NotMapped]
        public bool CanForceCleanupRegister => CanCancelRegister;

        public void MarkAsDraft() => Status = Draft;

        public void MarkAsSubmitted() => Status = Submitted;

        public void MarkAsApprovedReceived() => Status = Approved;

        public void MarkAsSignedUploaded() => Status = SignedUploaded;

        public void MarkAsCompleted() => Status = Completed;

        public void MarkAsWithdrawnVoid() => Status = WithdrawnVoid;

        public void MarkAsForceVoided() => Status = ForceVoided;

        public void MarkSimulatedAsArchived()
        {
            SimulatedArchiveStatus = TrackArchived;
            RefreshOverallArchiveStatus();
        }

        public void MarkElectronicAsArchived()
        {
            ElectronicArchiveStatus = TrackArchived;
            RefreshOverallArchiveStatus();
        }

        public void RefreshOverallArchiveStatus()
        {
            ArchivedDate = IsElectronicArchived && IsSimulatedArchived
                ? (ArchivedDate ?? DateTime.Now)
                : null;
        }

        [NotMapped]
        public string StatusStr => ApplicationWorkflowStatus.ToDisplay(Status);

        [NotMapped]
        public string StatusColor => Status switch
        {
            Unsubmitted => "#FF9800", // 未提交
            Submitted => "#2196F3", // 已提交
            Approved => "#4CAF50", // 已审批
            SignedUploaded => "#7E57C2", // 已上传签字件
            Completed => "#00796B", // 已办结
            WithdrawnVoid => "#9E9E9E", // 已撤回作废
            ForceVoided => "#616161", // 已强制作废
            _ => "#9E9E9E"
        };

        /// <summary>
        /// 资料立档进度摘要（模拟介质 + 电子介质）。
        /// </summary>
        [NotMapped]
        public string FilingStatusStr
        {
            get
            {
                bool hasSimulated = HasSimulatedMedia;
                bool hasElectronic = HasElectronicMedia;

                if (!hasSimulated && !hasElectronic)
                {
                    return "无需立档";
                }

                bool simulatedDone = !hasSimulated || SimulatedArchiveStatus == TrackArchived;
                bool electronicDone = !hasElectronic || ElectronicArchiveStatus == TrackArchived;

                if (simulatedDone && electronicDone)
                {
                    return "已全部立档";
                }

                bool simulatedStarted = hasSimulated
                    && (SimulatedArchiveStatus == TrackArchived || ArchiveBoxes.Count > 0);
                bool electronicStarted = hasElectronic
                    && (ElectronicArchiveStatus == TrackArchived || ElectronicArchiveUnits.Count > 0);

                if (!simulatedStarted && !electronicStarted)
                {
                    return "未立档";
                }

                return "部分立档";
            }
        }

        /// <summary>
        /// 介质明细
        /// </summary>
        public virtual List<YearlyArchiveRegisterMedia> MediaEntries { get; set; } = new List<YearlyArchiveRegisterMedia>();
    }
}