using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifyFiveNodeApprovalSigners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VicePresidentDate",
                table: "YearlyArchiveReturnRecords",
                newName: "ProductionVicePresidentDate");

            migrationBuilder.RenameColumn(
                name: "VicePresident",
                table: "YearlyArchiveReturnRecords",
                newName: "ProductionVicePresident");

            migrationBuilder.RenameColumn(
                name: "ReviewerName",
                table: "YearlyArchiveReturnRecords",
                newName: "DeptHead");

            migrationBuilder.RenameColumn(
                name: "ReviewerDate",
                table: "YearlyArchiveReturnRecords",
                newName: "DeptHeadDate");

            migrationBuilder.RenameColumn(
                name: "ApprovedBy",
                table: "YearlyArchiveReturnRecords",
                newName: "ArchiveRoomHead");

            migrationBuilder.RenameColumn(
                name: "RndLeader",
                table: "YearlyArchiveRegisterRecords",
                newName: "ProductionVicePresident");

            migrationBuilder.RenameColumn(
                name: "RndDate",
                table: "YearlyArchiveRegisterRecords",
                newName: "ProductionVicePresidentDate");

            migrationBuilder.RenameColumn(
                name: "ProdLeader",
                table: "YearlyArchiveRegisterRecords",
                newName: "ProductionHead");

            migrationBuilder.RenameColumn(
                name: "ProdDate",
                table: "YearlyArchiveRegisterRecords",
                newName: "ProductionHeadDate");

            migrationBuilder.RenameColumn(
                name: "DeputyLeader",
                table: "YearlyArchiveRegisterRecords",
                newName: "DeptHead");

            migrationBuilder.RenameColumn(
                name: "DeputyDate",
                table: "YearlyArchiveRegisterRecords",
                newName: "DeptHeadDate");

            migrationBuilder.RenameColumn(
                name: "DeptLeader",
                table: "YearlyArchiveRegisterRecords",
                newName: "ArchiveRoomHead");

            migrationBuilder.RenameColumn(
                name: "DeptDate",
                table: "YearlyArchiveRegisterRecords",
                newName: "ArchiveRoomHeadDate");

            migrationBuilder.RenameColumn(
                name: "VicePresidentDate",
                table: "YearlyArchiveOutboundRecords",
                newName: "ProductionVicePresidentDate");

            migrationBuilder.RenameColumn(
                name: "VicePresident",
                table: "YearlyArchiveOutboundRecords",
                newName: "ProductionVicePresident");

            migrationBuilder.RenameColumn(
                name: "DeptAuditor",
                table: "YearlyArchiveOutboundRecords",
                newName: "DeptHeadOpinion");

            migrationBuilder.RenameColumn(
                name: "DeptAuditOpinion",
                table: "YearlyArchiveOutboundRecords",
                newName: "DeptHead");

            migrationBuilder.RenameColumn(
                name: "DeptAuditDate",
                table: "YearlyArchiveOutboundRecords",
                newName: "DeptHeadDate");

            migrationBuilder.RenameColumn(
                name: "RndLeader",
                table: "NetworkOutboundRecords",
                newName: "ProductionVicePresident");

            migrationBuilder.RenameColumn(
                name: "RndDate",
                table: "NetworkOutboundRecords",
                newName: "ProductionVicePresidentDate");

            migrationBuilder.RenameColumn(
                name: "ProdLeader",
                table: "NetworkOutboundRecords",
                newName: "ProductionHead");

            migrationBuilder.RenameColumn(
                name: "ProdDate",
                table: "NetworkOutboundRecords",
                newName: "ProductionHeadDate");

            migrationBuilder.RenameColumn(
                name: "DeputyLeader",
                table: "NetworkOutboundRecords",
                newName: "DeptHead");

            migrationBuilder.RenameColumn(
                name: "DeputyDate",
                table: "NetworkOutboundRecords",
                newName: "DeptHeadDate");

            migrationBuilder.RenameColumn(
                name: "DeptLeader",
                table: "NetworkOutboundRecords",
                newName: "ArchiveRoomHead");

            migrationBuilder.RenameColumn(
                name: "DeptDate",
                table: "NetworkOutboundRecords",
                newName: "ArchiveRoomHeadDate");

            migrationBuilder.RenameColumn(
                name: "RndLeader",
                table: "NetworkInboundRecords",
                newName: "ProductionVicePresident");

            migrationBuilder.RenameColumn(
                name: "RndDate",
                table: "NetworkInboundRecords",
                newName: "ProductionVicePresidentDate");

            migrationBuilder.RenameColumn(
                name: "ProdLeader",
                table: "NetworkInboundRecords",
                newName: "ProductionHead");

            migrationBuilder.RenameColumn(
                name: "ProdDate",
                table: "NetworkInboundRecords",
                newName: "ProductionHeadDate");

            migrationBuilder.RenameColumn(
                name: "DeputyLeader",
                table: "NetworkInboundRecords",
                newName: "DeptHead");

            migrationBuilder.RenameColumn(
                name: "DeputyDate",
                table: "NetworkInboundRecords",
                newName: "DeptHeadDate");

            migrationBuilder.RenameColumn(
                name: "DeptLeader",
                table: "NetworkInboundRecords",
                newName: "ArchiveRoomHead");

            migrationBuilder.RenameColumn(
                name: "DeptDate",
                table: "NetworkInboundRecords",
                newName: "ArchiveRoomHeadDate");

            migrationBuilder.RenameColumn(
                name: "ReviewerName",
                table: "HardDiskMediaApplications",
                newName: "ProductionVicePresident");

            migrationBuilder.RenameColumn(
                name: "ReviewerDate",
                table: "HardDiskMediaApplications",
                newName: "ProductionVicePresidentDate");

            migrationBuilder.RenameColumn(
                name: "ApprovedTime",
                table: "HardDiskMediaApplications",
                newName: "ProductionHeadDate");

            migrationBuilder.RenameColumn(
                name: "ApprovedBy",
                table: "HardDiskMediaApplications",
                newName: "ProductionHead");

            migrationBuilder.AddColumn<string>(
                name: "ArchiveDeputyPresident",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveDeputyPresidentDate",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveRoomHeadDate",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveDeputyPresident",
                table: "YearlyArchiveRegisterRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveDeputyPresidentDate",
                table: "YearlyArchiveRegisterRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveDeputyPresident",
                table: "YearlyArchiveOutboundRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveDeputyPresidentDate",
                table: "YearlyArchiveOutboundRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveDeputyPresident",
                table: "YearlyArchiveDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveDeputyPresidentDate",
                table: "YearlyArchiveDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveRoomHead",
                table: "YearlyArchiveDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveRoomHeadDate",
                table: "YearlyArchiveDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeptHead",
                table: "YearlyArchiveDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeptHeadDate",
                table: "YearlyArchiveDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionHead",
                table: "YearlyArchiveDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionHeadDate",
                table: "YearlyArchiveDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionVicePresident",
                table: "YearlyArchiveDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionVicePresidentDate",
                table: "YearlyArchiveDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveDeputyPresident",
                table: "NetworkOutboundRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveDeputyPresidentDate",
                table: "NetworkOutboundRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeptHead",
                table: "NetworkOnNetDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeptHeadDate",
                table: "NetworkOnNetDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionHead",
                table: "NetworkOnNetDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionHeadDate",
                table: "NetworkOnNetDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionVicePresident",
                table: "NetworkOnNetDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionVicePresidentDate",
                table: "NetworkOnNetDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveDeputyPresident",
                table: "NetworkInboundRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveDeputyPresidentDate",
                table: "NetworkInboundRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeptHead",
                table: "HistoryArchiveDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeptHeadDate",
                table: "HistoryArchiveDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionHead",
                table: "HistoryArchiveDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionHeadDate",
                table: "HistoryArchiveDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionVicePresident",
                table: "HistoryArchiveDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionVicePresidentDate",
                table: "HistoryArchiveDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveRoomHead",
                table: "HardDiskMediaApplications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveRoomHeadDate",
                table: "HardDiskMediaApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeptHead",
                table: "HardDiskMediaApplications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeptHeadDate",
                table: "HardDiskMediaApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveDeputyPresident",
                table: "HardDiskDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveDeputyPresidentDate",
                table: "HardDiskDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveRoomHead",
                table: "HardDiskDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveRoomHeadDate",
                table: "HardDiskDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeptHead",
                table: "HardDiskDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeptHeadDate",
                table: "HardDiskDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionHead",
                table: "HardDiskDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionHeadDate",
                table: "HardDiskDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionVicePresident",
                table: "HardDiskDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionVicePresidentDate",
                table: "HardDiskDisposalRecords",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresident",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresidentDate",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveRoomHeadDate",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresident",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresidentDate",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresident",
                table: "YearlyArchiveOutboundRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresidentDate",
                table: "YearlyArchiveOutboundRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresident",
                table: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresidentDate",
                table: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveRoomHead",
                table: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveRoomHeadDate",
                table: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "DeptHead",
                table: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "DeptHeadDate",
                table: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionHead",
                table: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionHeadDate",
                table: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionVicePresident",
                table: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionVicePresidentDate",
                table: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresident",
                table: "NetworkOutboundRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresidentDate",
                table: "NetworkOutboundRecords");

            migrationBuilder.DropColumn(
                name: "DeptHead",
                table: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropColumn(
                name: "DeptHeadDate",
                table: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionHead",
                table: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionHeadDate",
                table: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionVicePresident",
                table: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionVicePresidentDate",
                table: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresident",
                table: "NetworkInboundRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresidentDate",
                table: "NetworkInboundRecords");

            migrationBuilder.DropColumn(
                name: "DeptHead",
                table: "HistoryArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "DeptHeadDate",
                table: "HistoryArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionHead",
                table: "HistoryArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionHeadDate",
                table: "HistoryArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionVicePresident",
                table: "HistoryArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionVicePresidentDate",
                table: "HistoryArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveRoomHead",
                table: "HardDiskMediaApplications");

            migrationBuilder.DropColumn(
                name: "ArchiveRoomHeadDate",
                table: "HardDiskMediaApplications");

            migrationBuilder.DropColumn(
                name: "DeptHead",
                table: "HardDiskMediaApplications");

            migrationBuilder.DropColumn(
                name: "DeptHeadDate",
                table: "HardDiskMediaApplications");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresident",
                table: "HardDiskDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresidentDate",
                table: "HardDiskDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveRoomHead",
                table: "HardDiskDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveRoomHeadDate",
                table: "HardDiskDisposalRecords");

            migrationBuilder.DropColumn(
                name: "DeptHead",
                table: "HardDiskDisposalRecords");

            migrationBuilder.DropColumn(
                name: "DeptHeadDate",
                table: "HardDiskDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionHead",
                table: "HardDiskDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionHeadDate",
                table: "HardDiskDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionVicePresident",
                table: "HardDiskDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ProductionVicePresidentDate",
                table: "HardDiskDisposalRecords");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresidentDate",
                table: "YearlyArchiveReturnRecords",
                newName: "VicePresidentDate");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresident",
                table: "YearlyArchiveReturnRecords",
                newName: "VicePresident");

            migrationBuilder.RenameColumn(
                name: "DeptHeadDate",
                table: "YearlyArchiveReturnRecords",
                newName: "ReviewerDate");

            migrationBuilder.RenameColumn(
                name: "DeptHead",
                table: "YearlyArchiveReturnRecords",
                newName: "ReviewerName");

            migrationBuilder.RenameColumn(
                name: "ArchiveRoomHead",
                table: "YearlyArchiveReturnRecords",
                newName: "ApprovedBy");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresidentDate",
                table: "YearlyArchiveRegisterRecords",
                newName: "RndDate");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresident",
                table: "YearlyArchiveRegisterRecords",
                newName: "RndLeader");

            migrationBuilder.RenameColumn(
                name: "ProductionHeadDate",
                table: "YearlyArchiveRegisterRecords",
                newName: "ProdDate");

            migrationBuilder.RenameColumn(
                name: "ProductionHead",
                table: "YearlyArchiveRegisterRecords",
                newName: "ProdLeader");

            migrationBuilder.RenameColumn(
                name: "DeptHeadDate",
                table: "YearlyArchiveRegisterRecords",
                newName: "DeputyDate");

            migrationBuilder.RenameColumn(
                name: "DeptHead",
                table: "YearlyArchiveRegisterRecords",
                newName: "DeputyLeader");

            migrationBuilder.RenameColumn(
                name: "ArchiveRoomHeadDate",
                table: "YearlyArchiveRegisterRecords",
                newName: "DeptDate");

            migrationBuilder.RenameColumn(
                name: "ArchiveRoomHead",
                table: "YearlyArchiveRegisterRecords",
                newName: "DeptLeader");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresidentDate",
                table: "YearlyArchiveOutboundRecords",
                newName: "VicePresidentDate");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresident",
                table: "YearlyArchiveOutboundRecords",
                newName: "VicePresident");

            migrationBuilder.RenameColumn(
                name: "DeptHeadOpinion",
                table: "YearlyArchiveOutboundRecords",
                newName: "DeptAuditor");

            migrationBuilder.RenameColumn(
                name: "DeptHeadDate",
                table: "YearlyArchiveOutboundRecords",
                newName: "DeptAuditDate");

            migrationBuilder.RenameColumn(
                name: "DeptHead",
                table: "YearlyArchiveOutboundRecords",
                newName: "DeptAuditOpinion");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresidentDate",
                table: "NetworkOutboundRecords",
                newName: "RndDate");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresident",
                table: "NetworkOutboundRecords",
                newName: "RndLeader");

            migrationBuilder.RenameColumn(
                name: "ProductionHeadDate",
                table: "NetworkOutboundRecords",
                newName: "ProdDate");

            migrationBuilder.RenameColumn(
                name: "ProductionHead",
                table: "NetworkOutboundRecords",
                newName: "ProdLeader");

            migrationBuilder.RenameColumn(
                name: "DeptHeadDate",
                table: "NetworkOutboundRecords",
                newName: "DeputyDate");

            migrationBuilder.RenameColumn(
                name: "DeptHead",
                table: "NetworkOutboundRecords",
                newName: "DeputyLeader");

            migrationBuilder.RenameColumn(
                name: "ArchiveRoomHeadDate",
                table: "NetworkOutboundRecords",
                newName: "DeptDate");

            migrationBuilder.RenameColumn(
                name: "ArchiveRoomHead",
                table: "NetworkOutboundRecords",
                newName: "DeptLeader");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresidentDate",
                table: "NetworkInboundRecords",
                newName: "RndDate");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresident",
                table: "NetworkInboundRecords",
                newName: "RndLeader");

            migrationBuilder.RenameColumn(
                name: "ProductionHeadDate",
                table: "NetworkInboundRecords",
                newName: "ProdDate");

            migrationBuilder.RenameColumn(
                name: "ProductionHead",
                table: "NetworkInboundRecords",
                newName: "ProdLeader");

            migrationBuilder.RenameColumn(
                name: "DeptHeadDate",
                table: "NetworkInboundRecords",
                newName: "DeputyDate");

            migrationBuilder.RenameColumn(
                name: "DeptHead",
                table: "NetworkInboundRecords",
                newName: "DeputyLeader");

            migrationBuilder.RenameColumn(
                name: "ArchiveRoomHeadDate",
                table: "NetworkInboundRecords",
                newName: "DeptDate");

            migrationBuilder.RenameColumn(
                name: "ArchiveRoomHead",
                table: "NetworkInboundRecords",
                newName: "DeptLeader");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresidentDate",
                table: "HardDiskMediaApplications",
                newName: "ReviewerDate");

            migrationBuilder.RenameColumn(
                name: "ProductionVicePresident",
                table: "HardDiskMediaApplications",
                newName: "ReviewerName");

            migrationBuilder.RenameColumn(
                name: "ProductionHeadDate",
                table: "HardDiskMediaApplications",
                newName: "ApprovedTime");

            migrationBuilder.RenameColumn(
                name: "ProductionHead",
                table: "HardDiskMediaApplications",
                newName: "ApprovedBy");
        }
    }
}
