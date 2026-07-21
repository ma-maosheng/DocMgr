using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    public class YearlyArchiveToDoProvider : IToDoProvider
    {
        private readonly IArchiveRegisterRepository _archiveRegisterRepository;
        private readonly IArchiveOutboundRepository _archiveOutboundRepository;
        private readonly IArchiveReturnRepository _archiveReturnRepository;
        private readonly IArchiveFilingRepository _archiveFilingRepository;

        public YearlyArchiveToDoProvider(
            IArchiveRegisterRepository archiveRegisterRepository,
            IArchiveOutboundRepository archiveOutboundRepository,
            IArchiveReturnRepository archiveReturnRepository,
            IArchiveFilingRepository archiveFilingRepository)
        {
            _archiveRegisterRepository = archiveRegisterRepository;
            _archiveOutboundRepository = archiveOutboundRepository;
            _archiveReturnRepository = archiveReturnRepository;
            _archiveFilingRepository = archiveFilingRepository;
        }

        public async Task<List<ToDoItem>> GetToDosAsync(User currentUser)
        {
            var result = new List<ToDoItem>();
            if (currentUser == null) return result;

            // 关键规则：资料室资料管理员在申请提交后直至确认办结前，始终保留待办
            if (IsArchiveRoomAdmin(currentUser))
            {
                var pendingRegisters = await _archiveRegisterRepository.GetSubmittedRecordsForToDoAsync(200);

                result.AddRange(pendingRegisters.Select(r => new ToDoItem
                {
                    Id = $"YAR-{r.Id}-REGISTER-PENDING",
                    Title = $"【资料申请】待资料室办理：{r.MaterialName}",
                    BizType = "YearlyArchiveRegister",
                    BizId = r.Id,
                    BizNo = r.FormNo,
                    Stage = BuildRegisterPendingStage(r),
                    CreatedTime = r.CreatedDate,
                    Priority = "高"
                }));

                var pendingOutbounds = await _archiveOutboundRepository.GetPendingRecordsForToDoAsync(200);
                result.AddRange(pendingOutbounds.Select(r => new ToDoItem
                {
                    Id = $"YAO-{r.Id}-OUTBOUND-PENDING",
                    Title = $"【资料借出】待办理：{BuildOutboundSummary(r)}",
                    BizType = ResolveOutboundBizType(r.Status),
                    BizId = r.Id,
                    BizNo = r.OutboundNo,
                    Stage = BuildOutboundPendingStage(r),
                    CreatedTime = r.SubmittedAt ?? r.ApplyDate,
                    Priority = "高"
                }));

                var pendingFiling = await _archiveFilingRepository.GetCompletedUnfiledRecordsForToDoAsync(200);
                result.AddRange(pendingFiling.Select(r => new ToDoItem
                {
                    Id = $"YAR-{r.Id}-PENDING-FILING",
                    Title = $"【资料立档】已办结待立档：{r.MaterialName}",
                    BizType = "YearlyArchiveFiling",
                    BizId = r.Id,
                    BizNo = r.FormNo,
                    Stage = BuildPendingFilingStage(r),
                    CreatedTime = r.AdminDate ?? r.DeliverDate ?? r.CreatedDate,
                    Priority = "高"
                }));

                var pendingReturnRecords = await _archiveReturnRepository.GetPendingReturnRecordsForToDoAsync(200);
                result.AddRange(pendingReturnRecords.Select(r => new ToDoItem
                {
                    Id = $"YAR-RECORD-{r.Id}-PENDING",
                    Title = $"【资料归还】{ResolveReturnToDoTitle(r)}：{BuildReturnRecordSummary(r)}",
                    BizType = "YearlyArchiveReturnRecord",
                    BizId = r.Id,
                    BizNo = r.ReturnNo,
                    Stage = ResolveReturnToDoStage(r),
                    CreatedTime = r.SubmittedAt ?? r.RegisteredAt ?? r.ReturnDate,
                    Priority = "高"
                }));
            }

            var overdueReturns = await _archiveReturnRepository.GetOverdueReturnOutboundsAsync(DateTime.Now, 200);
            IEnumerable<YearlyArchiveOutboundRecord> overdueForCurrentUser = overdueReturns;

            if (!IsArchiveRoomAdmin(currentUser))
            {
                overdueForCurrentUser = overdueReturns.Where(record => record.ApplicantUserId == currentUser.Id);
            }

            result.AddRange(overdueForCurrentUser.Select(r => new ToDoItem
            {
                Id = $"YAR-RETURN-OVERDUE-{r.Id}",
                Title = $"【资料归还】已超期未归还：{r.MaterialSummary}",
                BizType = "YearlyArchiveReturn",
                BizId = r.Id,
                BizNo = r.OutboundNo,
                Stage = $"应还日期 {r.ExpectedReturnDate:yyyy-MM-dd}（借出人 {r.ApplicantName}）",
                CreatedTime = r.ExpectedReturnDate ?? r.CompletedAt ?? r.ApplyDate,
                Priority = "高"
            }));

            return result;
        }

        private static bool IsArchiveRoomAdmin(User user)
        {
            var dept = user.Department?.Trim() ?? string.Empty;
            var role = user.Role?.Trim() ?? string.Empty;

            return (string.Equals(dept, "资料室", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(role, "部门资料管理员", StringComparison.OrdinalIgnoreCase))
                   || string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildRegisterPendingStage(YearlyArchiveRegisterRecord record)
        {
            return record.Status switch
            {
                YearlyArchiveRegisterRecord.Submitted => "已提交待资料室处理",
                YearlyArchiveRegisterRecord.Approved => "已审批待上传签字件",
                YearlyArchiveRegisterRecord.SignedUploaded => "已上传签字件待确认办结",
                _ => record.StatusStr
            };
        }

        private static string BuildOutboundPendingStage(YearlyArchiveOutboundRecord record)
        {
            return record.Status switch
            {
                YearlyArchiveOutboundRecord.Submitted => "已提交待审批",
                YearlyArchiveOutboundRecord.Approved => "已审批待实物交接",
                YearlyArchiveOutboundRecord.SignedUploaded => "已实物交接待办结出库",
                _ => record.StatusStr
            };
        }

        private static string ResolveOutboundBizType(int status)
        {
            return "YearlyArchiveOutboundApproval";
        }

        private static string BuildOutboundSummary(YearlyArchiveOutboundRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.MaterialSummary))
            {
                return record.MaterialSummary.Trim();
            }

            return $"{record.ApplicantName} / {record.ProjectName}".Trim(' ', '/');
        }

        private static string BuildReturnRecordSummary(YearlyArchiveReturnRecord record)
        {
            if (record.Items.Count == 0)
            {
                return record.SourceOutboundNo;
            }

            string firstItem = record.Items[0].ItemName?.Trim() ?? string.Empty;
            if (record.Items.Count == 1)
            {
                return string.IsNullOrWhiteSpace(firstItem) ? record.SourceOutboundNo : firstItem;
            }

            return string.IsNullOrWhiteSpace(firstItem)
                ? $"{record.SourceOutboundNo} 等 {record.Items.Count} 项"
                : $"{firstItem} 等 {record.Items.Count} 项";
        }

        private static string ResolveReturnToDoTitle(YearlyArchiveReturnRecord record) =>
            record.Status switch
            {
                YearlyArchiveReturnRecord.Submitted => "待审批",
                YearlyArchiveReturnRecord.Approved => "待实物交接",
                YearlyArchiveReturnRecord.SignedUploaded when !record.SignedAttachmentUploaded => "待上传签批交接单",
                YearlyArchiveReturnRecord.SignedUploaded => "待办结",
                _ => "待办理"
            };

        private static string ResolveReturnToDoStage(YearlyArchiveReturnRecord record) =>
            YearlyArchiveReturnRecord.ResolveWorkflowStatusDisplay(
                record.Status,
                record.SignedAttachmentUploaded);

        private static string BuildPendingFilingStage(YearlyArchiveRegisterRecord record)
        {
            bool hasSimulated = record.HasSimulatedMedia;
            bool hasElectronic = record.HasElectronicMedia;
            bool simulatedPending = hasSimulated && record.SimulatedArchiveStatus != YearlyArchiveRegisterRecord.TrackArchived;
            bool electronicPending = hasElectronic && record.ElectronicArchiveStatus != YearlyArchiveRegisterRecord.TrackArchived;

            if (simulatedPending && electronicPending)
            {
                return "已办结未立档（模拟介质 + 电子介质）";
            }

            if (simulatedPending)
            {
                return "已办结未立档（模拟介质）";
            }

            if (electronicPending)
            {
                return "已办结未立档（电子介质）";
            }

            return "已办结未立档";
        }
    }
}