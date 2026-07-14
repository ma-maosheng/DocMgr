# -*- coding: utf-8 -*-
"""Batch fill SchemaDictionary.yaml review field Chinese names."""
from __future__ import annotations

import re
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
YAML_PATH = REPO / ".cursor" / "schema" / "SchemaDictionary.yaml"
CS_PATH = REPO / "Infrastructure" / "Seeding" / "FieldDomainSeedService.ReviewFieldAliasMaps.cs"

ALIASES: dict[str, str] = {
    "ArchiveContainerProjection.ArchivedBy": "立档人",
    "ArchiveContainerProjection.ContainerCode": "容器编号",
    "ArchiveContainerProjection.Remarks": "备注",
    "ArchiveContainerProjection.Year": "所属年度",
    "BusinessLogicSettings.ApplicationOverdueSetting": "申请单逾期设置",
    "BusinessLogicSettings.UpdatedAt": "更新时间",
    "CabinetArchiveBoxPlacement.BoxCode": "档案盒编号",
    "CabinetArchiveBoxPlacement.BoxSpecification": "档案盒规格",
    "CabinetArchiveBoxPlacement.CabinetName": "柜体名称",
    "CabinetArchiveBoxPlacement.FaceCode": "面别代码",
    "CabinetArchiveBoxPlacement.PlacementMode": "放置方式",
    "CabinetArchiveBoxPlacement.SlotCode": "档口编号",
    "CabinetArchiveBoxPlacement.SourceRecordKey": "来源记录标识",
    "CabinetArchiveBoxPlacement.UpdatedAt": "更新时间",
    "CabinetArchiveBoxPlacement.UpdatedBy": "最后更新人",
    "CabinetHardDiskSlotCategoryAssignment.CabinetId": "柜体ID",
    "CabinetHardDiskSlotCategoryAssignment.FaceCode": "门别编码",
    "CabinetHardDiskSlotCategoryAssignment.SlotCode": "档口编号",
    "CabinetHardDiskSlotCategoryAssignment.UpdatedTime": "更新时间",
    "Cabinet.DamagedDiskSlotCode": "损坏硬盘档口编号",
    "Cabinet.DamagedDiskSlotFaceCode": "损坏硬盘档口面别",
    "Cabinet.Depth": "深度",
    "DbOperationLog.ChangedColumns": "变更字段",
    "DbOperationLog.EntityKey": "实体主键",
    "DbOperationLog.EntityType": "实体类型",
    "DbOperationLog.Operation": "操作类型",
    "DbOperationLog.OperationTime": "操作时间",
    "DbOperationLog.SessionId": "会话ID",
    "DbOperationLog.SourceButton": "来源按钮",
    "DbOperationLog.SourcePage": "来源页面",
    "DbOperationLog.Summary": "操作摘要",
    "DbOperationLog.TableName": "数据库表名",
    "DbOperationLog.UserId": "用户ID",
    "DbOperationLog.UserName": "用户名称",
    "HardDiskLedger.DiskCode": "硬盘编号",
    "HardDiskLedger.HolderOrOrganization": "持有人/保管单位",
    "HardDiskLedger.MediumId": "介质ID",
    "HardDiskLedger.NeedReturn": "是否需归还",
    "HardDiskLedger.RegisterDate": "登记日期",
    "HardDiskLedger.RegisterPerson": "登记人",
    "HardDiskLedger.StorageLocation": "存放位置",
    "HardDiskLedger.UpdatedTime": "更新时间",
    "HardDiskMediaApplication.FormatConfirmation": "格式化确认",
    "HardDiskMediaApplication.InspectionResult": "查验结果",
    "HardDiskMediaApplication.ReviewerDate": "审核时间",
    "HardDiskMediaApplication.ReviewerName": "审核人",
    "HardDiskMediaApplication.SourceApplicationId": "来源申请单ID",
    "OpticalDiscLedger.DiscCode": "光盘编号",
    "OpticalDiscLedger.HolderOrOrganization": "持有人/保管单位",
    "OpticalDiscLedger.MediumId": "介质ID",
    "OpticalDiscLedger.NeedReturn": "是否需归还",
    "OpticalDiscLedger.RegisterDate": "登记日期",
    "OpticalDiscLedger.RegisterPerson": "登记人",
    "OpticalDiscLedger.StorageLocation": "存放位置",
    "OpticalDiscLedger.UpdatedTime": "更新时间",
    "OpticalDiscMedium.Capacity": "容量",
    "OpticalDiscMedium.DiscCode": "光盘编号",
    "OpticalDiscMedium.DiscType": "光盘类型",
    "OpticalDiscMedium.IsDeleted": "逻辑删除",
    "OpticalDiscMedium.RegisterDate": "登记日期",
    "OpticalDiscMedium.RegisterPerson": "登记人",
    "OpticalDiscMedium.RegistrationMethod": "登记方式",
    "OpticalDiscMedium.Remarks": "备注",
    "OpticalDiscMedium.SourceRecordKey": "来源记录键",
    "OpticalDiscMedium.UpdatedTime": "更新时间",
    "OpticalDiscMediaTransaction.AfterLocation": "流转后位置",
    "OpticalDiscMediaTransaction.AfterStatus": "流转后状态",
    "OpticalDiscMediaTransaction.ApplicationId": "申请单ID",
    "OpticalDiscMediaTransaction.BeforeLocation": "流转前位置",
    "OpticalDiscMediaTransaction.BeforeStatus": "流转前状态",
    "OpticalDiscMediaTransaction.ExpectedReturnDate": "预计归还日期",
    "OpticalDiscMediaTransaction.MediumId": "介质ID",
    "OpticalDiscMediaTransaction.NeedReturn": "是否要求归还",
    "OpticalDiscMediaTransaction.OperateTime": "办理时间",
    "OpticalDiscMediaTransaction.OperatorName": "经办人",
    "OpticalDiscMediaTransaction.RelatedArchiveTitle": "相关资料标题",
    "OpticalDiscMediaTransaction.RelatedBatch": "相关批次",
    "OpticalDiscMediaTransaction.RelatedPerson": "相关人员",
    "OpticalDiscMediaTransaction.TargetOrganization": "目标单位",
    "OpticalDiscMediaTransaction.TransactionType": "流转类型",
    "UserSession.IsActive": "是否活跃",
    "UserSession.LastHeartbeatTime": "最后心跳时间",
    "UserSession.LogoutTime": "登出时间",
    "UserSession.SessionId": "会话ID",
    "UserSession.TerminalName": "终端名称",
    "UserSession.UserId": "用户ID",
    "YearlyArchiveBoxMediaItemLink.YearlyArchiveBoxId": "年度档案盒ID",
    "YearlyArchiveBoxMediaItemLink.YearlyArchiveRegisterMediaItemId": "登记介质明细ID",
    "YearlyArchiveBox.ContainerLifecycleStatus": "容器生命周期状态",
    "YearlyArchiveBox.LastStorageLocation": "最后存储位置",
    "YearlyArchiveBox.PlacementMode": "放置方式",
    "YearlyArchiveBox.RetiredAt": "退役时间",
    "YearlyArchiveBox.RetiredBy": "退役操作人",
    "YearlyArchiveFilingFact.ArchiveCopyRole": "归档副本角色",
    "YearlyArchiveFilingFact.BorrowHintLevel": "借出提示级别",
    "YearlyArchiveFilingFact.BorrowHintText": "借出提示文本",
    "YearlyArchiveFilingFact.BorrowHintUpdatedAt": "借出提示更新时间",
    "YearlyArchiveFilingFact.BoxLocationCode": "盒位编码",
    "YearlyArchiveFilingFact.BoxSpecs": "盒规格",
    "YearlyArchiveFilingFact.CabinetName": "柜体名称",
    "YearlyArchiveFilingFact.ContainerCode": "容器编号",
    "YearlyArchiveFilingFact.ContainerId": "容器ID",
    "YearlyArchiveFilingFact.ContainerKind": "容器类别",
    "YearlyArchiveFilingFact.CurrentContainerCode": "当前容器编号",
    "YearlyArchiveFilingFact.CurrentStorageLocation": "当前存储位置",
    "YearlyArchiveFilingFact.DataSizeMb": "数据大小(MB)",
    "YearlyArchiveFilingFact.FiledAt": "立档时间",
    "YearlyArchiveFilingFact.FiledBy": "立档人",
    "YearlyArchiveFilingFact.FilingFactNo": "立档事实编号",
    "YearlyArchiveFilingFact.FilingStoragePath": "立档存储路径",
    "YearlyArchiveFilingFact.LifecycleRemark": "生命周期备注",
    "YearlyArchiveFilingFact.LifecycleStatus": "生命周期状态",
    "YearlyArchiveFilingFact.LifecycleUpdatedAt": "生命周期更新时间",
    "YearlyArchiveFilingFact.MediumCode": "介质编号",
    "YearlyArchiveFilingFact.PrimaryFilingFactId": "原件立档事实ID",
    "YearlyArchiveFilingFact.RegisterMediaId": "登记介质ID",
    "YearlyArchiveFilingFact.RegisterRecordId": "登记记录ID",
    "YearlyArchiveFilingFact.SourceLinkId": "来源关联ID",
    "YearlyArchiveFilingFact.SourceLinkType": "来源关联类型",
    "YearlyArchiveFilingFact.StorageCarrierType": "存储载体类型",
    "YearlyArchiveFilingFact.StorageLocation": "存储位置",
    "YearlyArchiveMaterialTransaction.AfterContainerCode": "流转后容器编号",
    "YearlyArchiveMaterialTransaction.AfterLifecycleStatus": "流转后生命周期状态",
    "YearlyArchiveMaterialTransaction.AfterStorageLocation": "流转后存储位置",
    "YearlyArchiveMaterialTransaction.BeforeContainerCode": "流转前容器编号",
    "YearlyArchiveMaterialTransaction.BeforeLifecycleStatus": "流转前生命周期状态",
    "YearlyArchiveMaterialTransaction.BeforeStorageLocation": "流转前存储位置",
    "YearlyArchiveMaterialTransaction.DedupKey": "去重键",
    "YearlyArchiveMaterialTransaction.FilingFactId": "立档事实ID",
    "YearlyArchiveMaterialTransaction.OperatedAt": "办理时间",
    "YearlyArchiveMaterialTransaction.OperatorName": "经办人",
    "YearlyArchiveMaterialTransaction.Summary": "流转摘要",
    "YearlyArchiveMaterialTransaction.TransactionType": "流转类型",
    "YearlyArchiveOutboundItem.ArchiveCopyRole": "归档副本角色",
    "YearlyArchiveOutboundItem.ContainerCode": "容器编号",
    "YearlyArchiveOutboundItem.ContainerDisposition": "容器处置方式",
    "YearlyArchiveOutboundItem.ContentEntryId": "内容条目ID",
    "YearlyArchiveOutboundItem.ContentEntryKind": "内容条目类别",
    "YearlyArchiveOutboundItem.ContentEntryName": "内容条目名称",
    "YearlyArchiveOutboundItem.ContentEntryRelativePath": "内容条目相对路径",
    "YearlyArchiveOutboundItem.CopyCount": "拟领用份数",
    "YearlyArchiveOutboundItem.CurrentStorageLocation": "当前存储位置",
    "YearlyArchiveOutboundItem.DataSizeMb": "数据大小(MB)",
    "YearlyArchiveOutboundItem.ElectronicMediaSource": "电子介质来源",
    "YearlyArchiveOutboundItem.ElectronicMediumType": "电子介质类型",
    "YearlyArchiveOutboundItem.FilingFactId": "立档事实ID",
    "YearlyArchiveOutboundItem.IsSelfDiskRegistered": "是否自带盘登记",
    "YearlyArchiveOutboundItem.ItemArchiveYear": "明细归档年度",
    "YearlyArchiveOutboundItem.NeedReturn": "是否需归还",
    "YearlyArchiveOutboundItem.OutboundRecordId": "出库单ID",
    "YearlyArchiveOutboundItem.PrimaryFilingFactId": "原件立档事实ID",
    "YearlyArchiveOutboundItem.RequisitionedDiskCode": "领用硬盘编号",
    "YearlyArchiveOutboundItem.RequisitionedDiskNeedReturn": "领用硬盘是否需归还",
    "YearlyArchiveOutboundItem.RequisitionedMediumId": "领用介质ID",
    "YearlyArchiveOutboundItem.ReservationStatus": "预订状态",
    "YearlyArchiveOutboundItem.SelectionScopeKind": "选取范围类别",
    "YearlyArchiveOutboundItem.SelfDiskCapacity": "自带盘容量",
    "YearlyArchiveOutboundItem.SelfDiskCodesJson": "自带盘编号JSON",
    "YearlyArchiveOutboundItem.SelfDiskSerialNo": "自带盘序列号",
    "YearlyArchiveOutboundItem.SelfDiskSerialNumbersJson": "自带盘序列号JSON",
    "YearlyArchiveOutboundItem.SortOrder": "排序序号",
    "YearlyArchiveOutboundItem.SourceResultSetId": "来源检索集ID",
    "YearlyArchiveOutboundItem.SourceResultSetItemId": "来源检索集明细ID",
    "YearlyArchiveOutboundItem.StockCopyCount": "登记库存份数",
    "YearlyArchiveOutboundItem.StorageCarrierType": "存储载体类型",
    "YearlyArchiveOutboundItem.StorageLocation": "存储位置",
    "YearlyArchiveOutboundItem.UsageMode": "领用方式",
    "YearlyArchiveOutboundRecord.ApplicantUserId": "申请人用户ID",
    "YearlyArchiveOutboundRecord.ApprovalDeadline": "审批截止日期",
    "YearlyArchiveOutboundRecord.ApprovedAt": "审批时间",
    "YearlyArchiveOutboundRecord.ArchiveRoomHead": "资料室负责人",
    "YearlyArchiveOutboundRecord.ArchiveRoomHeadDate": "资料室负责人日期",
    "YearlyArchiveOutboundRecord.ArchiveRoomHeadOpinion": "资料室负责人意见",
    "YearlyArchiveOutboundRecord.ArchiveYear": "归档年度",
    "YearlyArchiveOutboundRecord.CompletedAt": "办结时间",
    "YearlyArchiveOutboundRecord.DeptAuditDate": "部门审核日期",
    "YearlyArchiveOutboundRecord.DeptAuditOpinion": "部门审核意见",
    "YearlyArchiveOutboundRecord.DeptAuditor": "部门审核人",
    "YearlyArchiveOutboundRecord.DestinationKind": "去向类别",
    "YearlyArchiveOutboundRecord.ExpectedReturnDate": "预计归还日期",
    "YearlyArchiveOutboundRecord.ExternalUnit": "外部单位",
    "YearlyArchiveOutboundRecord.ForceVoidKind": "强制作废类别",
    "YearlyArchiveOutboundRecord.ForceVoidReason": "强制作废原因",
    "YearlyArchiveOutboundRecord.ForceVoidedAt": "强制作废时间",
    "YearlyArchiveOutboundRecord.HandoverRemark": "交接备注",
    "YearlyArchiveOutboundRecord.LastPrintedAt": "最后打印时间",
    "YearlyArchiveOutboundRecord.MaterialSummary": "资料摘要",
    "YearlyArchiveOutboundRecord.OutboundNo": "出库单编号",
    "YearlyArchiveOutboundRecord.OverdueRemindedAt": "逾期提醒时间",
    "YearlyArchiveOutboundRecord.PhysicallyCompletedBy": "实物出库办理人",
    "YearlyArchiveOutboundRecord.ProductionHead": "生产管理科负责人",
    "YearlyArchiveOutboundRecord.ProductionHeadDate": "生产管理科负责人日期",
    "YearlyArchiveOutboundRecord.ProductionHeadOpinion": "生产管理科负责人意见",
    "YearlyArchiveOutboundRecord.ProofMaterialNote": "证明材料备注",
    "YearlyArchiveOutboundRecord.Reason": "申请原因",
    "YearlyArchiveOutboundRecord.SelfRetainDisposition": "自留处置方式",
    "YearlyArchiveOutboundRecord.SignedUploadedAt": "签字件上传时间",
    "YearlyArchiveOutboundRecord.SourceResultSetId": "来源检索集ID",
    "YearlyArchiveOutboundRecord.SourceResultSetNo": "来源检索集编号",
    "YearlyArchiveOutboundRecord.SubmittedAt": "提交时间",
    "YearlyArchiveOutboundRecord.UpdatedAt": "更新时间",
    "YearlyArchiveOutboundRecord.VicePresident": "分管领导",
    "YearlyArchiveOutboundRecord.VicePresidentDate": "分管领导日期",
    "YearlyArchiveOutboundRecord.VicePresidentOpinion": "分管领导意见",
    "YearlyArchiveOutboundRecord.WithdrawReason": "撤回原因",
    "YearlyArchiveOutboundRecord.WithdrawnAt": "撤回时间",
    "YearlyArchiveOutboundSyncEntry.EntryKind": "同步条目类别",
    "YearlyArchiveOutboundSyncEntry.FilingFactId": "立档事实ID",
    "YearlyArchiveOutboundSyncEntry.OperatedBy": "操作人",
    "YearlyArchiveOutboundSyncEntry.OutboundItemId": "出库明细ID",
    "YearlyArchiveOutboundSyncEntry.OutboundRecordId": "出库单ID",
    "YearlyArchiveOutboundSyncEntry.Phase": "同步阶段",
    "YearlyArchiveOutboundSyncEntry.UpdatedAt": "更新时间",
    "YearlyArchiveRegisterMedia.BorrowedHardDiskCode": "借用硬盘编号",
    "YearlyArchiveRegisterMedia.IsBorrowedHardDisk": "是否借用硬盘",
    "YearlyArchiveRegisterRecord.ElectronicArchiveStatus": "电子归档状态",
    "YearlyArchiveRegisterRecord.SimulatedArchiveStatus": "模拟归档状态",
    "YearlyArchiveRelocationItem.AfterContainerCode": "移库后容器编号",
    "YearlyArchiveRelocationItem.AfterStorageLocation": "移库后存储位置",
    "YearlyArchiveRelocationItem.BeforeContainerCode": "移库前容器编号",
    "YearlyArchiveRelocationItem.BeforeStorageLocation": "移库前存储位置",
    "YearlyArchiveRelocationItem.FilingFactId": "立档事实ID",
    "YearlyArchiveRelocationItem.RelocationRecordId": "移库单ID",
    "YearlyArchiveRelocationItem.SourceLinkId": "来源关联ID",
    "YearlyArchiveRelocationItem.SourceLinkType": "来源关联类型",
    "YearlyArchiveRelocationRecord.OperatedAt": "操作时间",
    "YearlyArchiveRelocationRecord.OperatedBy": "操作人",
    "YearlyArchiveRelocationRecord.PreviewReport": "预览报告",
    "YearlyArchiveRelocationRecord.RelocationMode": "移库方式",
    "YearlyArchiveRelocationRecord.RelocationNo": "移库单编号",
    "YearlyArchiveRelocationRecord.Remarks": "备注",
    "YearlyArchiveRelocationRecord.SourceContainerCode": "来源容器编号",
    "YearlyArchiveRelocationRecord.SourceContainerId": "来源容器ID",
    "YearlyArchiveRelocationRecord.SourceMediumDisposition": "来源介质处置",
    "YearlyArchiveRelocationRecord.SourceStorageLocation": "来源存储位置",
    "YearlyArchiveRelocationRecord.TargetContainerCode": "目标容器编号",
    "YearlyArchiveRelocationRecord.TargetContainerId": "目标容器ID",
    "YearlyArchiveRelocationRecord.TargetStorageLocation": "目标存储位置",
    "YearlyArchiveReturnItem.ContainerCode": "容器编号",
    "YearlyArchiveReturnItem.FilingFactId": "立档事实ID",
    "YearlyArchiveReturnItem.ItemCondition": "归还物状态",
    "YearlyArchiveReturnItem.RegisterMediaId": "登记介质ID",
    "YearlyArchiveReturnItem.ReturnCopyCount": "归还份数",
    "YearlyArchiveReturnItem.ReturnRecordId": "归还单ID",
    "YearlyArchiveReturnItem.SortOrder": "排序序号",
    "YearlyArchiveReturnItem.SourceOutboundItemId": "源出库明细ID",
    "YearlyArchiveReturnItem.StorageLocation": "存储位置",
    "YearlyArchiveReturnItem.UsageMode": "领用方式",
    "YearlyArchiveReturnRecord.ArchiveYear": "归档年度",
    "YearlyArchiveReturnRecord.BorrowerDept": "借出部门",
    "YearlyArchiveReturnRecord.BorrowerName": "借出人",
    "YearlyArchiveReturnRecord.CompletedAt": "办结时间",
    "YearlyArchiveReturnRecord.HandlerName": "办结管理员",
    "YearlyArchiveReturnRecord.LastPrintedAt": "最后打印时间",
    "YearlyArchiveReturnRecord.Reason": "归还原因",
    "YearlyArchiveReturnRecord.RegisteredAt": "登记时间",
    "YearlyArchiveReturnRecord.RegisteredByDept": "登记人部门",
    "YearlyArchiveReturnRecord.RegisteredByName": "登记人",
    "YearlyArchiveReturnRecord.RegisteredByUserId": "登记人用户ID",
    "YearlyArchiveReturnRecord.SourceOutboundNo": "源出库单编号",
    "YearlyArchiveReturnRecord.SourceOutboundRecordId": "源出库单ID",
    "YearlyArchiveReturnRecord.UpdatedAt": "更新时间",
    "YearlyArchiveReturnRecord.VoidReason": "作废原因",
    "YearlyArchiveReturnRecord.VoidedAt": "作废时间",
    "YearlyArchiveSearchResultSetItem.AddedAt": "加入时间",
    "YearlyArchiveSearchResultSetItem.BorrowHintLevel": "借出提示级别",
    "YearlyArchiveSearchResultSetItem.BorrowHintText": "借出提示文本",
    "YearlyArchiveSearchResultSetItem.ContainerCode": "容器编号",
    "YearlyArchiveSearchResultSetItem.ContentEntryId": "内容条目ID",
    "YearlyArchiveSearchResultSetItem.ContentEntryKind": "内容条目类别",
    "YearlyArchiveSearchResultSetItem.ContentEntryName": "内容条目名称",
    "YearlyArchiveSearchResultSetItem.ContentEntryRelativePath": "内容条目相对路径",
    "YearlyArchiveSearchResultSetItem.FilingFactId": "立档事实ID",
    "YearlyArchiveSearchResultSetItem.LifecycleStatus": "生命周期状态",
    "YearlyArchiveSearchResultSetItem.RequestedCopyCount": "申请份数",
    "YearlyArchiveSearchResultSetItem.ResultSetId": "检索集ID",
    "YearlyArchiveSearchResultSetItem.SelectionScopeKind": "选取范围类别",
    "YearlyArchiveSearchResultSetItem.SortOrder": "排序序号",
    "YearlyArchiveSearchResultSetItem.StorageLocation": "存储位置",
    "YearlyArchiveSearchResultSet.CreatedByName": "创建人",
    "YearlyArchiveSearchResultSet.CreatedByUserId": "创建人用户ID",
    "YearlyArchiveSearchResultSet.Remarks": "备注",
    "YearlyArchiveSearchResultSet.ResultSetNo": "检索集编号",
    "YearlyArchiveSearchResultSet.SearchCriteriaJson": "检索条件JSON",
    "YearlyArchiveSearchResultSet.UpdatedAt": "更新时间",
    "YearlyElectronicArchiveUnitDiscLink.OpticalDiscMediumId": "光盘介质ID",
    "YearlyElectronicArchiveUnitDiscLink.YearlyElectronicArchiveUnitId": "电子立档单元ID",
    "YearlyElectronicArchiveUnitMediaItemLink.DataSizeMb": "数据大小(MB)",
    "YearlyElectronicArchiveUnitMediaItemLink.FilingStoragePath": "立档存储路径",
    "YearlyElectronicArchiveUnitMediaItemLink.MediumCode": "介质编号",
    "YearlyElectronicArchiveUnitMediaItemLink.YearlyArchiveRegisterMediaItemId": "登记介质明细ID",
    "YearlyElectronicArchiveUnitMediaItemLink.YearlyElectronicArchiveUnitId": "电子立档单元ID",
    "YearlyElectronicArchiveUnitMediaLink.YearlyArchiveRegisterMediaId": "登记介质ID",
    "YearlyElectronicArchiveUnitMediaLink.YearlyElectronicArchiveUnitId": "电子立档单元ID",
    "YearlyElectronicArchiveUnitMediumLink.HardDiskMediumId": "硬盘介质ID",
    "YearlyElectronicArchiveUnitMediumLink.YearlyElectronicArchiveUnitId": "电子立档单元ID",
    "YearlyElectronicArchiveUnit.ArchivedBy": "立档人",
    "YearlyElectronicArchiveUnit.ContentSummary": "资料摘要",
    "YearlyElectronicArchiveUnit.ElectronicArchiveNo": "电子立档编号",
    "YearlyElectronicArchiveUnit.LinkedMediumCodes": "关联介质编号",
    "YearlyElectronicArchiveUnit.Remarks": "备注",
    "YearlyElectronicArchiveUnit.SourceRecordKey": "来源记录键",
    "YearlyElectronicArchiveUnit.StorageCarrierType": "存储载体类型",
    "YearlyElectronicArchiveUnit.StorageLocation": "存放位置",
    "YearlyElectronicArchiveUnit.UnitLifecycleStatus": "单元生命周期状态",
    "YearlyElectronicArchiveUnit.Year": "所属年度",
}


def apply_yaml() -> tuple[int, int, list[str]]:
    text = YAML_PATH.read_text(encoding="utf-8")
    entity: str | None = None
    field: str | None = None
    updated = 0
    missing: list[str] = []
    lines = text.splitlines()
    out: list[str] = []

    for line in lines:
        m = re.match(r"^  ([A-Za-z][A-Za-z0-9]*):\s*$", line)
        if m:
            entity = m.group(1)
            out.append(line)
            continue

        m = re.match(r"^      ([A-Za-z][A-Za-z0-9]*):\s*$", line)
        if m:
            field = m.group(1)
            out.append(line)
            continue

        if field and entity and line.strip().startswith("chineseName:"):
            key = f"{entity}.{field}"
            if key in ALIASES:
                out.append(f"        chineseName: {ALIASES[key]}")
                updated += 1
                continue

        if field and entity and "needsReview: true" in line:
            key = f"{entity}.{field}"
            if key in ALIASES:
                out.append("        needsReview: false")
                field = None
                continue
            missing.append(key)
            out.append(line)
            field = None
            continue

        if field and entity and line.strip().startswith("needsReview:"):
            out.append(line)
            field = None
            continue

        out.append(line)

    YAML_PATH.write_text("\n".join(out) + "\n", encoding="utf-8")
    return updated, len(ALIASES), missing


def write_cs_map() -> None:
    lines = [
        "namespace DocMgr.Infrastructure.Seeding;",
        "",
        "public static partial class FieldDomainSeedService",
        "{",
        "    private static readonly Dictionary<string, string> ReviewFieldAliasMap = new(StringComparer.OrdinalIgnoreCase)",
        "    {",
    ]
    for key in sorted(ALIASES.keys()):
        entity, field = key.split(".", 1)
        lines.append(f'        ["{entity}.{field}"] = "{ALIASES[key]}",')
    lines.extend(
        [
            "    };",
            "}",
        ]
    )
    CS_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8-sig")


def main() -> None:
    review_keys = []
    entity = None
    field = None
    for line in YAML_PATH.read_text(encoding="utf-8").splitlines():
        m = re.match(r"^  ([A-Za-z][A-Za-z0-9]*):\s*$", line)
        if m:
            entity = m.group(1)
            continue
        m = re.match(r"^      ([A-Za-z][A-Za-z0-9]*):\s*$", line)
        if m:
            field = m.group(1)
            continue
        if field and entity and "needsReview: true" in line:
            review_keys.append(f"{entity}.{field}")
            field = None

    missing_keys = [k for k in review_keys if k not in ALIASES]
    if missing_keys:
        raise SystemExit(f"Missing aliases for {len(missing_keys)} review fields: {missing_keys[:10]}")

    updated, total, missing = apply_yaml()
    write_cs_map()
    print(f"Review fields: {len(review_keys)}")
    print(f"Updated {updated} field chineseName entries in YAML")
    print(f"Alias map entries: {total}")
    if missing:
        print("Missing during apply:", missing)
        raise SystemExit(1)


if __name__ == "__main__":
    main()
