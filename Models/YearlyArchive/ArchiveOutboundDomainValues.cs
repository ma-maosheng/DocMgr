namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料出库（借出）领域常量。
    /// </summary>
    public static class ArchiveOutboundDomainValues
    {
        public const string BusinessTypeAttachment = "ArchiveOutbound";

        public const string DestinationInternal = "Internal";
        public const string DestinationExternal = "External";

        public const string SelfRetainDispositionSelfDestroy = "SelfDestroy";

        public const string UsageModeWithdrawal = "Withdrawal";
        public const string UsageModeCopy = "Copy";
        public const string UsageModeDuplicate = "Duplicate";

        public const string ElectronicMediaSourceInStockBlank = "InStockBlank";
        public const string ElectronicMediaSourceSelfProvided = "SelfProvided";

        public const string ElectronicMediumTypeHardDisk = "HardDisk";
        public const string ElectronicMediumTypeOpticalDisc = "OpticalDisc";

        /// <summary>拷贝所用介质：自备 U 盘。</summary>
        public const string DuplicateMediumSelfUsb = "SelfUsb";

        /// <summary>拷贝所用介质：自备硬盘。</summary>
        public const string DuplicateMediumSelfHardDisk = "SelfHardDisk";

        /// <summary>拷贝所用介质：光盘。</summary>
        public const string DuplicateMediumOpticalDisc = "OpticalDisc";

        /// <summary>拷贝所用介质：库内空盘（出库办理时由管理员指定）。</summary>
        public const string DuplicateMediumInStockBlank = "InStockBlank";

        public static IReadOnlyList<(string Value, string Display)> DuplicateMediumOptions { get; } =
        [
            (DuplicateMediumSelfUsb, "自备U盘"),
            (DuplicateMediumSelfHardDisk, "自备硬盘"),
            (DuplicateMediumOpticalDisc, "光盘"),
            (DuplicateMediumInStockBlank, "库内空盘"),
        ];

        public static string GetDuplicateMediumDisplay(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            foreach (var option in DuplicateMediumOptions)
            {
                if (string.Equals(option.Value, value.Trim(), StringComparison.Ordinal))
                {
                    return option.Display;
                }
            }

            return value.Trim();
        }

        public static void ApplyDuplicateMediumSelection(YearlyArchiveOutboundItem item, string duplicateMediumKind)
        {
            ArgumentNullException.ThrowIfNull(item);

            string normalized = duplicateMediumKind?.Trim() ?? string.Empty;
            item.ElectronicMediumType = normalized;
            item.ElectronicMediaSource = string.Equals(
                normalized,
                DuplicateMediumInStockBlank,
                StringComparison.Ordinal)
                ? ElectronicMediaSourceInStockBlank
                : ElectronicMediaSourceSelfProvided;

            if (!string.Equals(normalized, DuplicateMediumInStockBlank, StringComparison.Ordinal))
            {
                item.RequisitionedMediumId = null;
                item.RequisitionedDiskCode = string.Empty;
                item.RequisitionedDiskNeedReturn = false;
            }
            else if (item.UsageMode == UsageModeDuplicate)
            {
                item.RequisitionedDiskNeedReturn = true;
            }
        }

        public static string NormalizeElectronicStorageCarrierDisplay(string? storageCarrierType)
        {
            string raw = storageCarrierType?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            if (raw.Contains("光盘", StringComparison.OrdinalIgnoreCase))
            {
                return ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc;
            }

            if (raw.Contains("硬盘", StringComparison.OrdinalIgnoreCase))
            {
                return ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk;
            }

            return raw;
        }

        public const string SyncEntryKindWithdrawalReservation = "WithdrawalReservation";
        public const string SyncEntryKindWithdrawalLedger = "WithdrawalLedger";
        public const string SyncEntryKindWithdrawalReturned = "WithdrawalReturned";
        public const string SyncEntryKindCopyLedger = "CopyLedger";
        public const string SyncEntryKindDuplicateLedger = "DuplicateLedger";

        public const string SyncEntryPhaseActive = "Active";
        public const string SyncEntryPhasePending = "Pending";
        public const string SyncEntryPhaseCancelled = "Cancelled";
        public const string SyncEntryPhaseConfirmed = "Confirmed";

        /// <summary>提档项已归还入库（出库明细 ReservationStatus 终态）。</summary>
        public const string SyncEntryPhaseReturned = "Returned";

        /// <summary>待归还期间容器状态提示：无异常。</summary>
        public const string ContainerStatusHintNone = "";

        /// <summary>待归还期间容器状态提示：盒位已变。</summary>
        public const string ContainerStatusHintLocationChanged = "LocationChanged";

        /// <summary>待归还期间容器状态提示：盒已失效。</summary>
        public const string ContainerStatusHintBoxInvalid = "BoxInvalid";

        public static string GetContainerStatusHintDisplay(string? hint) =>
            string.Equals(hint?.Trim(), ContainerStatusHintLocationChanged, StringComparison.Ordinal) ? "盒位已变"
            : string.Equals(hint?.Trim(), ContainerStatusHintBoxInvalid, StringComparison.Ordinal) ? "盒已失效"
            : string.Empty;

        public const string AttachmentKindSignedApprovalForm = "SignedApprovalForm";
        public const string AttachmentKindSignedHandoverForm = "SignedHandoverForm";
        public const string AttachmentKindMaterialPhoto = "MaterialPhoto";
        public const string AttachmentKindProofMaterialScan = "ProofMaterialScan";

        public const string ProofMaterialNoneText = "无";

        /// <summary>归档目的：院管资料、长期存档（电子介质仅允许拷贝借出）。</summary>
        public const string ArchivePurposeLongTermStorage = "院管资料、长期存档";

        public static bool IsLongTermElectronicArchivePurpose(string? archivePurpose) =>
            IsLongTermArchivePurpose(archivePurpose);

        /// <summary>归档目的是否为「院管资料、长期存档」（电子/模拟介质共用）。</summary>
        public static bool IsLongTermArchivePurpose(string? archivePurpose) =>
            string.Equals(archivePurpose?.Trim(), ArchivePurposeLongTermStorage, StringComparison.Ordinal);

        public static bool IsHardDiskStorageCarrier(string? storageCarrierType) =>
            !string.IsNullOrWhiteSpace(storageCarrierType)
            && storageCarrierType.Contains("硬盘", StringComparison.OrdinalIgnoreCase);

        public static bool IsOpticalDiscStorageCarrier(string? storageCarrierType) =>
            !string.IsNullOrWhiteSpace(storageCarrierType)
            && storageCarrierType.Contains("光盘", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 解析电子介质默认领用方式：长期存档仅拷贝，其余默认提档。
        /// </summary>
        public static string ResolveDefaultElectronicUsageMode(string? archivePurpose) =>
            IsLongTermElectronicArchivePurpose(archivePurpose)
                ? UsageModeDuplicate
                : UsageModeWithdrawal;

        public const string ForceVoidKindOverdueAuto = "OverdueAuto";
        public const string ForceVoidKindAdminManual = "AdminManual";

        public const int DefaultApprovalDeadlineDays = 7;

        public static bool IsExternalDestination(string? destinationKind) =>
            string.Equals(destinationKind, DestinationExternal, StringComparison.Ordinal);

        /// <summary>申请人是否声明借出时提供了资料使用证明材料（<see cref="ProofMaterialNote"/> 不为「无」）。</summary>
        public static bool HasProofMaterial(string? proofMaterialNote)
        {
            string note = proofMaterialNote?.Trim() ?? string.Empty;
            return note.Length > 0
                && !string.Equals(note, ProofMaterialNoneText, StringComparison.Ordinal);
        }

        /// <summary>是否须在审批阶段上传证明材料扫描件。</summary>
        public static bool RequiresProofMaterialScan(string? proofMaterialNote) =>
            HasProofMaterial(proofMaterialNote);

        /// <summary>
        /// 是否须上传证明材料扫描件（与 <see cref="RequiresProofMaterialScan(string?)"/> 相同，保留旧签名兼容）。
        /// </summary>
        public static bool RequiresProofMaterialScan(string? destinationKind, string? proofMaterialNote) =>
            RequiresProofMaterialScan(proofMaterialNote);
    }

    public enum ArchiveOutboundWorkspaceMode
    {
        Application = 1,
        Approval = 2,
        Handover = 3
    }
}
