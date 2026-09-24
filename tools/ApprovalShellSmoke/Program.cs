using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.ViewModels.HardDiskMedia;
using DocMgr.ViewModels.HistoryArchive;
using DocMgr.ViewModels.NetworkTransfer;
using DocMgr.ViewModels.YearlyArchive;
using DocMgr.Views.HardDiskMedia;
using DocMgr.Views.HistoryArchive;
using DocMgr.Views.NetworkTransfer;
using DocMgr.Views.Shared;
using DocMgr.Views.YearlyArchive;

namespace DocMgr.Tools.ApprovalShellSmoke;

/// <summary>
/// A/B 审批办理壳自动烟测：阶段机、文案、日期下限、VM 壳契约、可无 DI 实例化的视图加载。
/// 用法：dotnet run --project tools/ApprovalShellSmoke
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    [STAThread]
    private static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== 审批办理壳 · 自动烟测 ===");
        Console.WriteLine();

        VerifyButtonSupport();
        VerifyLifecycleSupport();
        VerifyListActionSupport();
        VerifyAttachmentPartitionSupport();
        VerifyAttachmentPolicySupport();
        VerifyPermissionSupport();
        VerifyShellCopy();
        VerifySignatureDateSupport();
        VerifyShellContracts();
        VerifyXamlStructure();
        VerifyUiInstantiation();

        Console.WriteLine();
        Console.WriteLine($"结果：通过 {_passed}，失败 {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    private static void VerifyListActionSupport()
    {
        Console.WriteLine("[列表动作 ApplicationListActionSupport]");

        Assert(
            "提交·草稿可",
            ApplicationListActionSupport.CanSubmit(ApplicationWorkflowStatus.Draft, isOwnerApplicant: true));
        Assert(
            "提交·已提交不可",
            !ApplicationListActionSupport.CanSubmit(ApplicationWorkflowStatus.Submitted, isOwnerApplicant: true));
        Assert(
            "A撤回·已提交可",
            ApplicationListActionSupport.CanApplicantWithdraw(ApplicationWorkflowStatus.Submitted, isOwnerApplicant: true));
        Assert(
            "A撤回·已审批不可",
            !ApplicationListActionSupport.CanApplicantWithdraw(ApplicationWorkflowStatus.Approved, isOwnerApplicant: true));
        Assert(
            "A撤回·domainAllows=false 不可",
            !ApplicationListActionSupport.CanApplicantWithdraw(
                ApplicationWorkflowStatus.Submitted,
                isOwnerApplicant: true,
                domainAllows: false));
        Assert(
            "B撤回·已审批可",
            ApplicationListActionSupport.CanAdminWithdrawDisposal(
                ApplicationWorkflowStatus.Approved,
                isArchiveAdmin: true));
        Assert(
            "B撤回·办结不可",
            !ApplicationListActionSupport.CanAdminWithdrawDisposal(
                ApplicationWorkflowStatus.Completed,
                isArchiveAdmin: true));
        Assert(
            "强制作废·逾期资格通过",
            ApplicationListActionSupport.CanForceVoid(
                ApplicationWorkflowStatus.Submitted,
                isArchiveAdmin: true,
                forceVoidEligible: true));
        Assert(
            "强制作废·无逾期资格不可",
            !ApplicationListActionSupport.CanForceVoid(
                ApplicationWorkflowStatus.Submitted,
                isArchiveAdmin: true,
                forceVoidEligible: false));
        Assert(
            "打开办理·Approved 可",
            ApplicationListActionSupport.CanOpenApprovalProcessing(ApplicationWorkflowStatus.Approved));
        Assert(
            "打开办理·Draft 不可",
            !ApplicationListActionSupport.CanOpenApprovalProcessing(ApplicationWorkflowStatus.Draft));

        Console.WriteLine();
    }

    private static void VerifyAttachmentPartitionSupport()
    {
        Console.WriteLine("[附件分区 ApprovalAttachmentPartitionSupport]");

        var signed = new List<SystemAttachment>();
        var photos = new List<SystemAttachment>();
        var other = new List<SystemAttachment>();
        var source = new[]
        {
            new SystemAttachment { FileCategory = "签批单", FileName = "a.pdf" },
            new SystemAttachment { FileCategory = "现场照片", FileName = "b.jpg" },
            new SystemAttachment { FileCategory = "其他附件", FileName = "c.txt" },
            new SystemAttachment { FileCategory = "未知", FileName = "d.bin" }
        };

        ApprovalAttachmentPartitionSupport.ClearAndPartition(
            source,
            all: null,
            signed,
            photos,
            proof: null,
            other,
            signedCategory: "签批单",
            isPhotoCategory: c => c == "现场照片");

        Assert("分区·签批 1", signed.Count == 1);
        Assert("分区·照片 1", photos.Count == 1);
        Assert("分区·其他含未知 2", other.Count == 2);

        Console.WriteLine();
    }

    private static void VerifyAttachmentPolicySupport()
    {
        Console.WriteLine("[附件策略 ApprovalAttachmentPolicySupport]");

        Assert(
            "策略·硬盘出库可解析",
            ApprovalAttachmentPolicySupport.TryGet(ApprovalWorkflowBusinessTypes.HardDiskOutbound, out _));
        Assert(
            "策略·别名处置附件类型可解析",
            ApprovalAttachmentPolicySupport.TryGet(
                DocMgr.Models.HardDiskMedia.HardDiskDisposalDomainValues.AttachmentBusinessType,
                out _));

        var policyB = ApprovalAttachmentPolicySupport.Get(ApprovalWorkflowBusinessTypes.HardDiskDisposal);
        Assert("策略·B 流 AllowOtherWhileApproved", policyB.AllowOtherWhileApproved);
        Assert(
            "策略·B 流 FlowKind",
            policyB.FlowKind == OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload);

        var signed = new List<SystemAttachment>();
        var photos = new List<SystemAttachment>();
        var other = new List<SystemAttachment>();
        ApprovalAttachmentPolicySupport.Partition(
            policyB,
            new[]
            {
                new SystemAttachment { FileCategory = policyB.PrimarySignedCategory, FileName = "s.pdf" },
                new SystemAttachment { FileCategory = "硬盘照片", FileName = "p.jpg" },
                new SystemAttachment { FileCategory = policyB.OtherCategory, FileName = "o.txt" },
                new SystemAttachment { FileCategory = "未知", FileName = "u.bin" }
            },
            all: null,
            signed,
            photos,
            proof: null,
            other);
        Assert("策略分区·签批 1", signed.Count == 1);
        Assert("策略分区·照片 1", photos.Count == 1);
        Assert("策略分区·其他含未知 2", other.Count == 2);

        var denyApprovedSigned = ApprovalAttachmentPolicySupport.EvaluateUpload(
            ApprovalAttachmentPolicySupport.Get(ApprovalWorkflowBusinessTypes.NetworkInbound),
            ApplicationWorkflowStatus.Approved,
            signedAttachmentUploaded: false,
            fileCategory: DocMgr.Models.NetworkTransfer.NetworkTransferDomainValues.AttachmentCategorySignedForm,
            isArchiveAdmin: true);
        Assert("策略门禁·A·Approved 不可传签批", !denyApprovedSigned.Allowed);

        var allowBOther = ApprovalAttachmentPolicySupport.EvaluateUpload(
            policyB,
            ApplicationWorkflowStatus.Approved,
            signedAttachmentUploaded: false,
            fileCategory: policyB.OtherCategory,
            isArchiveAdmin: true);
        Assert("策略门禁·B·Approved 可传其他", allowBOther.Allowed);

        Console.WriteLine();
    }

    private static void VerifyPermissionSupport()
    {
        Console.WriteLine("[权限 OfflineApprovalPermissionSupport]");

        var admin = new DocMgr.Models.SystemSettings.User
        {
            Role = DocMgr.Models.SystemSettings.UserRoleDomainValues.ArchiveAdmin
        };
        var clerk = new DocMgr.Models.SystemSettings.User
        {
            Role = DocMgr.Models.SystemSettings.UserRoleDomainValues.DepartmentArchiveClerk
        };

        Assert(
            "权限·A 流新建仅资料员",
            OfflineApprovalPermissionSupport.CanCreateDraft(clerk, ApprovalWorkflowBusinessTypes.YearlyArchiveOutbound)
            && !OfflineApprovalPermissionSupport.CanCreateDraft(admin, ApprovalWorkflowBusinessTypes.YearlyArchiveOutbound));
        Assert(
            "权限·B 流新建仅资料管理员",
            OfflineApprovalPermissionSupport.CanCreateDraft(admin, ApprovalWorkflowBusinessTypes.HardDiskDisposal)
            && !OfflineApprovalPermissionSupport.CanCreateDraft(clerk, ApprovalWorkflowBusinessTypes.HardDiskDisposal));
        Assert(
            "权限·审批台仅资料管理员",
            OfflineApprovalPermissionSupport.CanOperateApprovalWorkbench(admin)
            && !OfflineApprovalPermissionSupport.CanOperateApprovalWorkbench(clerk));
        Assert(
            "权限·办结后增补仅资料管理员",
            OfflineApprovalPermissionSupport.CanSupplementOtherAfterComplete(admin)
            && !OfflineApprovalPermissionSupport.CanSupplementOtherAfterComplete(clerk));

        Console.WriteLine();
    }

    private static void VerifyLifecycleSupport()
    {
        Console.WriteLine("[生命周期 OfflineApprovalLifecycleSupport]");

        static OfflineApprovalLifecycleSupport.GateContext Ctx(
            int status,
            bool signed,
            OfflineApprovalLifecycleSupport.FlowKind flow,
            OfflineApprovalLifecycleSupport.ActorRole actor,
            bool forceVoidEligible = false)
            => new(status, signed, flow, actor, forceVoidEligible);

        var aApprove = OfflineApprovalLifecycleSupport.TryTransition(
            Ctx(ApplicationWorkflowStatus.Submitted, false,
                OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.ApprovePass);
        Assert("A·审批通过 Submitted→Approved", aApprove is { Allowed: true, NextStatus: ApplicationWorkflowStatus.Approved });

        var aMid = OfflineApprovalLifecycleSupport.TryTransition(
            Ctx(ApplicationWorkflowStatus.Approved, false,
                OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.ConfirmMidStep);
        Assert("A·确认交接 Approved→SignedUploaded", aMid is { Allowed: true, NextStatus: ApplicationWorkflowStatus.SignedUploaded });

        var aCompleteDeny = OfflineApprovalLifecycleSupport.TryTransition(
            Ctx(ApplicationWorkflowStatus.SignedUploaded, false,
                OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.Complete);
        Assert("A·办结缺签批单拒绝", !aCompleteDeny.Allowed);

        var aComplete = OfflineApprovalLifecycleSupport.TryTransition(
            Ctx(ApplicationWorkflowStatus.SignedUploaded, true,
                OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.Complete);
        Assert("A·办结 SignedUploaded→Completed", aComplete is { Allowed: true, NextStatus: ApplicationWorkflowStatus.Completed });

        var aWithdraw = OfflineApprovalLifecycleSupport.TryTransition(
            Ctx(ApplicationWorkflowStatus.Submitted, false,
                OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                OfflineApprovalLifecycleSupport.ActorRole.Applicant),
            OfflineApprovalLifecycleSupport.Action.Withdraw);
        Assert("A·申请人撤回 Submitted→Withdrawn", aWithdraw is { Allowed: true, NextStatus: ApplicationWorkflowStatus.Withdrawn });

        var aForce = OfflineApprovalLifecycleSupport.TryTransition(
            Ctx(ApplicationWorkflowStatus.Submitted, false,
                OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin,
                forceVoidEligible: true),
            OfflineApprovalLifecycleSupport.Action.ForceVoid);
        Assert("A·强制作废 Submitted→ForceWithdrawn", aForce is { Allowed: true, NextStatus: ApplicationWorkflowStatus.ForceWithdrawn });

        var bMid = OfflineApprovalLifecycleSupport.TryTransition(
            Ctx(ApplicationWorkflowStatus.Approved, false,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.ConfirmMidStep);
        Assert("B·确认可上传 Approved→SignedUploaded", bMid is { Allowed: true, NextStatus: ApplicationWorkflowStatus.SignedUploaded });

        var bWithdrawLate = OfflineApprovalLifecycleSupport.TryTransition(
            Ctx(ApplicationWorkflowStatus.Approved, false,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            OfflineApprovalLifecycleSupport.Action.Withdraw);
        Assert("B·管理员可撤回已审批", bWithdrawLate is { Allowed: true, NextStatus: ApplicationWorkflowStatus.Withdrawn });

        var uploadApprovedA = OfflineApprovalLifecycleSupport.EvaluateAttachmentUpload(
            Ctx(ApplicationWorkflowStatus.Approved, false,
                OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            isOtherCategory: false,
            isArchiveAdmin: true);
        Assert("A·Approved 不可传签批", !uploadApprovedA.Allowed);

        var uploadSignedA = OfflineApprovalLifecycleSupport.EvaluateAttachmentUpload(
            Ctx(ApplicationWorkflowStatus.SignedUploaded, false,
                OfflineApprovalLifecycleSupport.FlowKind.ApplicationHandover,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            isOtherCategory: false,
            isArchiveAdmin: true);
        Assert("A·SignedUploaded 可传附件", uploadSignedA.Allowed);

        var uploadOtherB = OfflineApprovalLifecycleSupport.EvaluateAttachmentUpload(
            Ctx(ApplicationWorkflowStatus.Approved, false,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            isOtherCategory: true,
            isArchiveAdmin: true,
            allowOtherWhileApproved: true);
        Assert("B·Approved 可传其他附件", uploadOtherB.Allowed);

        var uploadSignedBDeny = OfflineApprovalLifecycleSupport.EvaluateAttachmentUpload(
            Ctx(ApplicationWorkflowStatus.Approved, false,
                OfflineApprovalLifecycleSupport.FlowKind.DisposalUnlockUpload,
                OfflineApprovalLifecycleSupport.ActorRole.ArchiveAdmin),
            isOtherCategory: false,
            isArchiveAdmin: true,
            allowOtherWhileApproved: true);
        Assert("B·Approved 不可传签批/照片", !uploadSignedBDeny.Allowed);

        var deleteCompleted = OfflineApprovalLifecycleSupport.EvaluateAttachmentDelete(ApplicationWorkflowStatus.Completed);
        Assert("办结后不可删附件", !deleteCompleted.Allowed);

        Assert(
            "ResolvePhase 与 ButtonSupport 同源",
            OfflineApprovalLifecycleSupport.ResolvePhase(ApplicationWorkflowStatus.Submitted, false)
            == ApprovalWorkflowButtonSupport.Phase.PendingApproval);

        Console.WriteLine();
    }

    private static void VerifyButtonSupport()
    {
        Console.WriteLine("[阶段机 ApprovalWorkflowButtonSupport]");

        var pending = ApprovalWorkflowButtonSupport.Resolve(
            ApprovalWorkflowButtonSupport.Phase.PendingApproval, isOperatorAllowed: true);
        Assert("待审批·仅可审批", pending is { CanApprovePass: true, CanConfirmPhysicalHandover: false, CanUploadSignedAttachment: false, CanConfirmComplete: false, CanPrintHandoverSheet: false });

        var handover = ApprovalWorkflowButtonSupport.Resolve(
            ApprovalWorkflowButtonSupport.Phase.PendingPhysicalHandover, isOperatorAllowed: true);
        Assert("待交接·可交接+打印", handover is { CanApprovePass: false, CanConfirmPhysicalHandover: true, CanUploadSignedAttachment: false, CanConfirmComplete: false, CanPrintHandoverSheet: true });

        var upload = ApprovalWorkflowButtonSupport.Resolve(
            ApprovalWorkflowButtonSupport.Phase.PendingSignedUpload, isOperatorAllowed: true);
        Assert("待上传·可上传+打印", upload is { CanUploadSignedAttachment: true, CanConfirmComplete: false, CanPrintHandoverSheet: true });

        var complete = ApprovalWorkflowButtonSupport.Resolve(
            ApprovalWorkflowButtonSupport.Phase.PendingComplete, isOperatorAllowed: true);
        Assert("待办结·可上传+办结+打印", complete is { CanUploadSignedAttachment: true, CanConfirmComplete: true, CanPrintHandoverSheet: true });

        var done = ApprovalWorkflowButtonSupport.Resolve(
            ApprovalWorkflowButtonSupport.Phase.Completed, isOperatorAllowed: true);
        Assert("已办结·仅打印", done is { CanUploadSignedAttachment: false, CanConfirmComplete: false, CanPrintHandoverSheet: true });

        var denied = ApprovalWorkflowButtonSupport.Resolve(
            ApprovalWorkflowButtonSupport.Phase.PendingApproval, isOperatorAllowed: false);
        Assert("无权限·全关", denied is { CanApprovePass: false, CanConfirmPhysicalHandover: false, CanPrintHandoverSheet: false });

        Assert(
            "ResolvePhase·Submitted→PendingApproval",
            ApprovalWorkflowButtonSupport.ResolvePhase(ApplicationWorkflowStatus.Submitted, false)
            == ApprovalWorkflowButtonSupport.Phase.PendingApproval);
        Assert(
            "ResolvePhase·Approved→PendingPhysicalHandover",
            ApprovalWorkflowButtonSupport.ResolvePhase(ApplicationWorkflowStatus.Approved, false)
            == ApprovalWorkflowButtonSupport.Phase.PendingPhysicalHandover);
        Assert(
            "ResolvePhase·SignedUploaded无签批→PendingSignedUpload",
            ApprovalWorkflowButtonSupport.ResolvePhase(ApplicationWorkflowStatus.SignedUploaded, false)
            == ApprovalWorkflowButtonSupport.Phase.PendingSignedUpload);
        Assert(
            "ResolvePhase·SignedUploaded有签批→PendingComplete",
            ApprovalWorkflowButtonSupport.ResolvePhase(ApplicationWorkflowStatus.SignedUploaded, true)
            == ApprovalWorkflowButtonSupport.Phase.PendingComplete);
        Assert(
            "ResolvePhase·Completed→Completed",
            ApprovalWorkflowButtonSupport.ResolvePhase(ApplicationWorkflowStatus.Completed, true)
            == ApprovalWorkflowButtonSupport.Phase.Completed);

        Assert(
            "办结后其他附件·资料管理员可补",
            ApprovalWorkflowButtonSupport.CanSupplementOtherAttachments(
                isCompleted: true, isArchiveAdmin: true));
        Assert(
            "办结后其他附件·非管理员不可",
            !ApprovalWorkflowButtonSupport.CanSupplementOtherAttachments(
                isCompleted: true, isArchiveAdmin: false));

        Console.WriteLine();
    }

    private static void VerifyShellCopy()
    {
        Console.WriteLine("[壳文案 ApprovalWorkflowShellCopySupport]");

        var a = ApprovalWorkflowShellKind.ApplicationHandover;
        var b = ApprovalWorkflowShellKind.DisposalUnlockUpload;

        Assert("A·中间按钮=确认实物交接",
            ApprovalWorkflowShellCopySupport.GetConfirmMidStepButtonText(a)
            == ApprovalWorkflowShellCopySupport.ConfirmPhysicalHandoverButtonText);
        Assert("B·中间按钮=确认可上传",
            ApprovalWorkflowShellCopySupport.GetConfirmMidStepButtonText(b)
            == ApprovalWorkflowShellCopySupport.ConfirmUploadButtonText);
        Assert("A·打印=打印交接单",
            ApprovalWorkflowShellCopySupport.GetPrintButtonText(a)
            == ApprovalWorkflowShellCopySupport.PrintHandoverSheetButtonText);
        Assert("B·打印=打印签批单",
            ApprovalWorkflowShellCopySupport.GetPrintButtonText(b)
            == ApprovalWorkflowShellCopySupport.PrintApprovalFormButtonText);
        Assert("A·签批区=签批交接单",
            ApprovalWorkflowShellCopySupport.GetSignedZoneTitle(a)
            == ApprovalWorkflowShellCopySupport.SignedHandoverZoneTitle);
        Assert("B·签批区=签批单",
            ApprovalWorkflowShellCopySupport.GetSignedZoneTitle(b)
            == ApprovalWorkflowShellCopySupport.SignedFormZoneTitle);
        Assert("A·不显示草稿底栏", !ApprovalWorkflowShellCopySupport.ShowsDraftSubmitWithdraw(a));
        Assert("B·显示草稿底栏", ApprovalWorkflowShellCopySupport.ShowsDraftSubmitWithdraw(b));
        Assert(
            "A·横幅含实物交接",
            ApprovalWorkflowShellCopySupport.GetWorkspaceBannerText(a).Contains("确认实物交接", StringComparison.Ordinal));
        Assert(
            "B·横幅含确认可上传",
            ApprovalWorkflowShellCopySupport.GetWorkspaceBannerText(b).Contains("确认可上传", StringComparison.Ordinal));

        Console.WriteLine();
    }

    private static void VerifySignatureDateSupport()
    {
        Console.WriteLine("[签字日期 ApprovalSignatureDateSupport]");

        var first = new DateTime(2026, 3, 1);
        var last = new DateTime(2026, 3, 10);
        Assert("下限·优先 FirstPrintedAt",
            ApprovalSignatureDateSupport.ResolveMinDate(first, last, 2) == first.Date);
        Assert("下限·无 First 时用 LastPrintedAt",
            ApprovalSignatureDateSupport.ResolveMinDate(null, last, 1) == last.Date);
        Assert("下限·未打印为空",
            ApprovalSignatureDateSupport.ResolveMinDate(null, last, 0) is null);

        Assert("Clamp·早于下限抬升",
            ApprovalSignatureDateSupport.Clamp(new DateTime(2026, 2, 1), first) == first.Date);
        Assert("Clamp·合法保留",
            ApprovalSignatureDateSupport.Clamp(new DateTime(2026, 3, 5), first) == new DateTime(2026, 3, 5));
        Assert(
            "Validate·早于下限有错",
            ApprovalSignatureDateSupport.ValidateNotBeforePrint(new DateTime(2026, 2, 1), first, "部门审核日期") is not null);
        Assert(
            "Validate·合法无错",
            ApprovalSignatureDateSupport.ValidateNotBeforePrint(new DateTime(2026, 3, 5), first, "部门审核日期") is null);

        Console.WriteLine();
    }

    private static void VerifyShellContracts()
    {
        Console.WriteLine("[VM 壳契约 · A 流]");

        string[] required =
        [
            "ApproveCommand", "ApproveHintText",
            "ConfirmMidStepCommand", "ConfirmMidStepHintText",
            "UploadHintText", "CompleteHintText", "PrintHintText",
            "WindowTitle", "WorkspaceBannerText",
            "SignedAttachments", "PhotoAttachments", "ProofAttachments", "OtherAttachments",
            "CanUploadSignedAttachment", "CanUploadPhotoAttachment", "CanUploadProofAttachment", "CanUploadOtherAttachment",
            "RequiresPhotoAttachment", "RequiresProofAttachment",
            "PhotoZoneTitle", "PhotoZoneHint", "ProofZoneHint",
            "UploadSignedCommand", "CaptureSignedCommand",
            "UploadPhotoCommand", "CapturePhotoCommand",
            "UploadProofCommand", "CaptureProofCommand",
            "UploadOtherCommand", "CaptureOtherCommand",
            "ViewAttachmentCommand", "DeleteAttachmentCommand",
            "PrintCommand", "CompleteCommand", "CloseCommand",
            "SaveDraftCommand", "SubmitCommand", "WithdrawCommand",
            "CanEditHeader", "CanSubmit", "CanWithdraw",
            "CanEditSigners", "SignatureDateMin"
        ];

        (string Code, Type Type)[] aFlows =
        [
            ("HD-OB-APV", typeof(HardDiskMediaApprovalEditDialogViewModel)),
            ("NT-IB", typeof(NetworkInboundEditDialogViewModel)),
            ("NT-OB", typeof(NetworkOutboundEditDialogViewModel)),
            ("YA-REG", typeof(ArchiveRegisterViewModel)),
            ("YA-OB", typeof(ArchiveOutboundViewModel)),
            ("YA-RTN", typeof(ArchiveReturnWorkbenchViewModel)),
            ("HD-RTN", typeof(HardDiskMediaReturnRegistrationPageViewModel))
        ];

        foreach (var (code, type) in aFlows)
        {
            AssertShellContract($"{code} · {type.Name}", type, required);
        }

        Console.WriteLine();
        Console.WriteLine("[VM 壳契约 · B 流]");

        string[] bRequired =
        [
            "ApproveCommand", "ConfirmMidStepCommand",
            "SignedAttachments", "PhotoAttachments", "ProofAttachments", "OtherAttachments",
            "CanUploadSignedAttachment", "UploadSignedCommand", "CompleteCommand", "PrintCommand", "CloseCommand",
            "CanEditSigners", "SignatureDateMin"
        ];

        (string Code, Type Type)[] bFlows =
        [
            ("HD-DSP", typeof(HardDiskDisposalEditDialogViewModel)),
            ("YA-DSP", typeof(ArchiveDisposalEditDialogViewModel)),
            ("NT-DSP", typeof(NetworkOnNetDisposalEditDialogViewModel)),
            ("HA-DSP", typeof(HistoryArchiveDisposalEditDialogViewModel))
        ];

        foreach (var (code, type) in bFlows)
        {
            AssertShellContract($"{code} · {type.Name}", type, bRequired);
        }

        Console.WriteLine();
    }

    private static void AssertShellContract(string label, Type type, IReadOnlyList<string> required)
    {
        var missing = new List<string>();
        foreach (var name in required)
        {
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (prop is null)
            {
                missing.Add(name);
                continue;
            }

            if (name.EndsWith("Command", StringComparison.Ordinal)
                && !typeof(ICommand).IsAssignableFrom(prop.PropertyType)
                && prop.PropertyType != typeof(ICommand))
            {
                // RelayCommand / ICommand? 均可
                if (!typeof(ICommand).IsAssignableFrom(Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType)
                    && prop.PropertyType.Name.IndexOf("Command", StringComparison.Ordinal) < 0)
                {
                    missing.Add($"{name}(非Command类型:{prop.PropertyType.Name})");
                }
            }
        }

        Assert(
            label,
            missing.Count == 0,
            missing.Count == 0 ? "ok" : "缺少 " + string.Join(", ", missing));
    }

    private static void VerifyXamlStructure()
    {
        Console.WriteLine("[XAML 结构 · 依赖 DI 的 Page]");

        var root = FindRepoRoot();
        (string Code, string RelPath, bool ExpectHideTitle)[] pages =
        [
            ("YA-RTN", @"Views\YearlyArchive\ArchiveReturnWorkbenchPage.xaml", true),
            ("HD-RTN", @"Views\HardDiskMedia\HardDiskMediaReturnRegistrationPage.xaml", true)
        ];

        foreach (var (code, rel, hideTitle) in pages)
        {
            var path = Path.Combine(root, rel);
            if (!File.Exists(path))
            {
                Assert($"{code} · 文件存在", false, path);
                continue;
            }

            var text = File.ReadAllText(path);
            Assert($"{code} · 含 ApprovalHandoverWorkspaceShell",
                text.Contains("ApprovalHandoverWorkspaceShell", StringComparison.Ordinal));
            Assert($"{code} · ApplicationHandover",
                text.Contains("ApplicationHandover", StringComparison.Ordinal));
            if (hideTitle)
            {
                Assert($"{code} · ShowTitleChrome=False",
                    text.Contains("ShowTitleChrome=\"False\"", StringComparison.Ordinal)
                    || text.Contains("ShowTitleChrome='False'", StringComparison.Ordinal));
            }
        }

        (string Code, string RelPath)[] dualMode =
        [
            ("NT-IB", @"Views\NetworkTransfer\NetworkInboundApprovalSectionsView.xaml"),
            ("NT-OB", @"Views\NetworkTransfer\NetworkOutboundApprovalSectionsView.xaml"),
            ("YA-REG", @"Views\YearlyArchive\ArchiveRegisterApprovalSectionsView.xaml")
        ];

        foreach (var (code, rel) in dualMode)
        {
            var path = Path.Combine(root, rel);
            var text = File.ReadAllText(path);
            Assert($"{code} · 双模 ShowTitleChrome=False",
                text.Contains("ShowTitleChrome=\"False\"", StringComparison.Ordinal));
            Assert($"{code} · ApplicationHandover",
                text.Contains("ApplicationHandover", StringComparison.Ordinal));
            Assert($"{code} · ApprovalSignatureCardsControl",
                text.Contains("ApprovalSignatureCardsControl", StringComparison.Ordinal));
        }

        Console.WriteLine();
    }

    private static void VerifyUiInstantiation()
    {
        Console.WriteLine("[UI 实例化 · STA]");

        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        try
        {
            EnsureAppStyles(app);

            SmokeShellControl();
            SmokeView("HD-OB-APV", () => new HardDiskMediaApprovalEditDialog(), expectHideTitle: false, expectA: true);
            SmokeView("NT-IB", () => new NetworkInboundApprovalSectionsView(), expectHideTitle: true, expectA: true);
            SmokeView("NT-OB", () => new NetworkOutboundApprovalSectionsView(), expectHideTitle: true, expectA: true);
            SmokeView("YA-REG", () => new ArchiveRegisterApprovalSectionsView(), expectHideTitle: true, expectA: true);
            SmokeView("YA-OB", () => new ArchiveOutboundApprovalPanel(), expectHideTitle: false, expectA: true);
            SmokeView("HD-DSP", () => new HardDiskDisposalEditDialog(), expectHideTitle: false, expectA: false);
            SmokeView("YA-DSP", () => new ArchiveDisposalEditDialog(), expectHideTitle: false, expectA: false);
            SmokeView("NT-DSP", () => new NetworkOnNetDisposalEditDialog(), expectHideTitle: false, expectA: false);
            SmokeView("HA-DSP", () => new HistoryArchiveDisposalEditDialog(), expectHideTitle: false, expectA: false);

            try
            {
                var cards = new ApprovalSignatureCardsControl();
                Assert("签字卡控件可构造", cards is not null);
            }
            catch (Exception ex)
            {
                Assert("签字卡控件可构造", false, ex.Message);
            }
        }
        finally
        {
            try { app.Shutdown(); } catch { /* ignore */ }
        }

        Console.WriteLine();
    }

    private static void EnsureAppStyles(Application app)
    {
        try
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/DocMgr;component/Views/Shared/FormResources.xaml", UriKind.Absolute)
            });
            Assert("加载 FormResources", true);
        }
        catch (Exception ex)
        {
            Assert("加载 FormResources", false, ex.Message);
            return;
        }

        // App.xaml 按钮样式链（壳 BasedOn FormInlineButtonStyle / DialogPrimary*）
        if (app.Resources.Contains("FormInlineButtonStyle"))
        {
            return;
        }

        var baseStyle = new Style(typeof(Button));
        app.Resources["ButtonBaseStyle"] = baseStyle;

        var normal = new Style(typeof(Button)) { BasedOn = baseStyle };
        normal.Setters.Add(new Setter(Control.BackgroundProperty, System.Windows.Media.Brushes.SteelBlue));
        normal.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        app.Resources["ButtonNormalStyle"] = normal;

        var caution = new Style(typeof(Button)) { BasedOn = baseStyle };
        caution.Setters.Add(new Setter(Control.BackgroundProperty, System.Windows.Media.Brushes.IndianRed));
        caution.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        app.Resources["ButtonCautionStyle"] = caution;

        var secondary = new Style(typeof(Button)) { BasedOn = baseStyle };
        secondary.Setters.Add(new Setter(Control.BackgroundProperty, System.Windows.Media.Brushes.SlateGray));
        secondary.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        app.Resources["ButtonSecondaryStyle"] = secondary;

        app.Resources["FormInlineButtonStyle"] = new Style(typeof(Button)) { BasedOn = normal };
        app.Resources["DialogPrimaryActionButtonStyle"] = new Style(typeof(Button)) { BasedOn = normal };
        app.Resources["DialogDangerActionButtonStyle"] = new Style(typeof(Button)) { BasedOn = caution };
        app.Resources["DialogSecondaryActionButtonStyle"] = new Style(typeof(Button)) { BasedOn = secondary };
        app.Resources["DialogCloseButtonStyle"] = new Style(typeof(Button)) { BasedOn = secondary };
        app.Resources["PageCloseButtonStyle"] = new Style(typeof(Button)) { BasedOn = secondary };
        app.Resources["ToolbarActionButtonStyle"] = new Style(typeof(Button)) { BasedOn = normal };
    }

    private static void SmokeShellControl()
    {
        try
        {
            var shell = new ApprovalHandoverWorkspaceShell
            {
                ShellKind = ApprovalWorkflowShellKind.ApplicationHandover
            };
            Assert("壳控件可构造(A)", shell.ShellKind == ApprovalWorkflowShellKind.ApplicationHandover);
            Assert("壳·A 中间文案",
                shell.ConfirmMidStepButtonText == ApprovalWorkflowShellCopySupport.ConfirmPhysicalHandoverButtonText);
            Assert("壳·A 打印文案",
                shell.PrintButtonText == ApprovalWorkflowShellCopySupport.PrintHandoverSheetButtonText);

            shell.ShellKind = ApprovalWorkflowShellKind.DisposalUnlockUpload;
            Assert("壳·切换 B 中间文案",
                shell.ConfirmMidStepButtonText == ApprovalWorkflowShellCopySupport.ConfirmUploadButtonText);
            Assert("壳·切换 B 打印文案",
                shell.PrintButtonText == ApprovalWorkflowShellCopySupport.PrintApprovalFormButtonText);
        }
        catch (Exception ex)
        {
            Assert("壳控件可构造(A)", false, ex.ToString());
        }
    }

    private static void SmokeView(string code, Func<FrameworkElement> factory, bool expectHideTitle, bool expectA)
    {
        try
        {
            var view = factory();
            var shell = FindShell(view);
            if (shell is null)
            {
                Assert($"{code} · 视觉树含壳", false, view.GetType().Name);
                return;
            }

            Assert($"{code} · 视觉树含壳", true);
            var expectedKind = expectA
                ? ApprovalWorkflowShellKind.ApplicationHandover
                : ApprovalWorkflowShellKind.DisposalUnlockUpload;
            Assert($"{code} · ShellKind={expectedKind}", shell.ShellKind == expectedKind, shell.ShellKind.ToString());
            if (expectHideTitle)
            {
                Assert($"{code} · ShowTitleChrome=False", !shell.ShowTitleChrome);
            }

            if (view is Window window)
            {
                window.Close();
            }
        }
        catch (Exception ex)
        {
            Assert($"{code} · 视觉树含壳", false, Truncate(ex.ToString(), 240));
        }
    }

    private static ApprovalHandoverWorkspaceShell? FindShell(DependencyObject root)
    {
        if (root is ApprovalHandoverWorkspaceShell shell)
        {
            return shell;
        }

        if (root is ContentControl { Content: DependencyObject content })
        {
            var nested = FindShell(content);
            if (nested is not null) return nested;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var nested = FindShell(child);
            if (nested is not null) return nested;
        }

        // 未实现视觉树时，LogicalTree 兜底
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject d)
            {
                var nested = FindShell(d);
                if (nested is not null) return nested;
            }
        }

        return null;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DocMgr.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";

    private static void Assert(string name, bool condition, string? detail = null)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"  PASS  {name}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  FAIL  {name}" + (string.IsNullOrWhiteSpace(detail) ? string.Empty : $" — {detail}"));
        }
    }
}
