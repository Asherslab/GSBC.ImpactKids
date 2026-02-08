using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1770530009 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServiceId",
                table: "AttendanceRecords",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "ItemReturned",
                table: "AttendanceItemRecords",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SchoolTermId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DollarStoreEntryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Service", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_ServiceId",
                table: "AttendanceRecords",
                column: "ServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_Service_ServiceId",
                table: "AttendanceRecords",
                column: "ServiceId",
                principalTable: "Service",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_Service_ServiceId",
                table: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "Service");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_ServiceId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ItemReturned",
                table: "AttendanceItemRecords");
        }
    }
}
