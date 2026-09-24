using System;
using DocMgr.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <summary>
    /// 硬盘盘库登记：损坏登记办结前「已完成损坏硬盘迁档」确认字段。
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260924070000_AddHardDiskInventoryDamagedRelocationConfirm")]
    public class AddHardDiskInventoryDamagedRelocationConfirm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DamagedDiskRelocationConfirmed",
                table: "HardDiskInventoryRegisterRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DamagedDiskRelocationConfirmedAt",
                table: "HardDiskInventoryRegisterRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DamagedDiskRelocationConfirmedBy",
                table: "HardDiskInventoryRegisterRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DamagedDiskRelocationConfirmed",
                table: "HardDiskInventoryRegisterRecords");

            migrationBuilder.DropColumn(
                name: "DamagedDiskRelocationConfirmedAt",
                table: "HardDiskInventoryRegisterRecords");

            migrationBuilder.DropColumn(
                name: "DamagedDiskRelocationConfirmedBy",
                table: "HardDiskInventoryRegisterRecords");
        }
    }
}
