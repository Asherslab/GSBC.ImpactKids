using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class PickupRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PickupRequested",
                table: "AttendanceRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PickupRequestedUserId",
                table: "AttendanceRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_PickupRequestedUserId",
                table: "AttendanceRecords",
                column: "PickupRequestedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_Users_PickupRequestedUserId",
                table: "AttendanceRecords",
                column: "PickupRequestedUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_Users_PickupRequestedUserId",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_PickupRequestedUserId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "PickupRequested",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "PickupRequestedUserId",
                table: "AttendanceRecords");
        }
    }
}
