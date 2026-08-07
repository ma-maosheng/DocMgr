namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料离库处置签批单打印数据。
    /// </summary>
    public sealed class YearlyArchiveDisposalPrintData
    {
        public string DisposalNo { get; set; } = string.Empty;

        public string MediaKind { get; set; } = string.Empty;

        public string StatusDisplay { get; set; } = string.Empty;

        public string ApplyDateText { get; set; } = string.Empty;

        public string DisposalReason { get; set; } = string.Empty;

        public string DispositionMethod { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public string Remark { get; set; } = string.Empty;

        public string ApplicantName { get; set; } = string.Empty;

        public string ApplicantDept { get; set; } = string.Empty;

        public DateTime ApplyTime { get; set; }

        public string ApprovedBy { get; set; } = string.Empty;

        public string ApprovedDateText { get; set; } = string.Empty;

        public DateTime? ApprovedTime { get; set; }

        public string ApprovalOpinion { get; set; } = string.Empty;

        public string CompletedBy { get; set; } = string.Empty;

        public string CompletedDateText { get; set; } = string.Empty;

        public bool IsCompleted { get; set; }

        /// <summary>资料室负责人（办结后重打预填）。</summary>
        public string ArchiveRoomHead { get; set; } = string.Empty;

        /// <summary>生产科负责人（办结后重打预填）。</summary>
        public string ProductionHead { get; set; } = string.Empty;

        /// <summary>分管资料室副院长（办结后重打预填）。</summary>
        public string ArchiveDeputyPresident { get; set; } = string.Empty;

        /// <summary>分管生产副院长（办结后重打预填）。</summary>
        public string ProductionVicePresident { get; set; } = string.Empty;

        /// <summary>已累计打印次数（不含本次）。</summary>
        public int PrintCount { get; set; }

        public IReadOnlyList<YearlyArchiveDisposalPrintItemRow> Items { get; set; } =
            Array.Empty<YearlyArchiveDisposalPrintItemRow>();
    }

    /// <summary>
    /// 资料离库处置签批单明细行。
    /// </summary>
    public sealed class YearlyArchiveDisposalPrintItemRow
    {
        public int SortOrder { get; set; }

        /// <summary>项目年度。</summary>
        public string ProjectYear { get; set; } = string.Empty;

        /// <summary>项目名称。</summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>档案盒编号（模拟）。</summary>
        public string BoxCode { get; set; } = string.Empty;

        /// <summary>介质袋编号（电子）。</summary>
        public string BagCode { get; set; } = string.Empty;

        public string ContainerCode { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        /// <summary>资料明细（名称/介质编号等）。</summary>
        public string MaterialDetail { get; set; } = string.Empty;

        public string SourceRegisterKind { get; set; } = string.Empty;

        public string DisposalReason { get; set; } = string.Empty;

        public string DispositionMethod { get; set; } = string.Empty;

        public string BeforeStorageLocation { get; set; } = string.Empty;

        public string MediumKind { get; set; } = string.Empty;

        public string MediumCode { get; set; } = string.Empty;

        public string TargetBlankSlotLocation { get; set; } = string.Empty;

        public string FormNo { get; set; } = string.Empty;
    }

    /// <summary>
    /// 资料离库处置候选行（办理台左侧列表）。
    /// </summary>
    public sealed class ArchiveDisposalSelectableItem
    {
        public string MediaKind { get; set; } = string.Empty;

        public int FilingFactId { get; set; }

        public int ContainerId { get; set; }

        public string ContainerCode { get; set; } = string.Empty;

        public string BeforeStorageLocation { get; set; } = string.Empty;

        public string SourceRegisterKind { get; set; } = string.Empty;

        public string DisposalReason { get; set; } = string.Empty;

        public string MaterialName { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string FormNo { get; set; } = string.Empty;

        public int InventoryLostCopyCount { get; set; }

        public int InventoryScrapCopyCount { get; set; }

        public string BeforeLifecycleStatus { get; set; } = string.Empty;

        public string MediumKind { get; set; } = string.Empty;

        public int MediumId { get; set; }

        public string MediumCode { get; set; } = string.Empty;

        public int ElectronicArchiveUnitId { get; set; }

        public string ElectronicArchiveNo { get; set; } = string.Empty;

        public string BeforeMediaStatus { get; set; } = string.Empty;

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(MediumCode))
                {
                    string bag = string.IsNullOrWhiteSpace(ElectronicArchiveNo) ? ContainerCode : ElectronicArchiveNo;
                    return string.IsNullOrWhiteSpace(bag)
                        ? $"{MediumKind} {MediumCode}"
                        : $"{MediumKind} {MediumCode}（{bag}）";
                }

                string name = string.IsNullOrWhiteSpace(ItemName) ? MaterialName : ItemName;
                string box = string.IsNullOrWhiteSpace(ContainerCode) ? string.Empty : $"[{ContainerCode}] ";
                return $"{box}{name}";
            }
        }

        /// <summary>稳定去重键：模拟=F{factId}；电子=M{mediumKind}:{mediumId}。</summary>
        public string SelectionKey =>
            MediumId > 0
                ? $"M:{MediumKind}:{MediumId}"
                : $"F:{FilingFactId}";
    }
}
