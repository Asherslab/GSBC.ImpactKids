using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSection",
                table: "ScheduleItemTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "LaneId",
                table: "ScheduleItems",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "IsSection",
                table: "ScheduleItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentItemId",
                table: "ScheduleItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_ParentItemId",
                table: "ScheduleItems",
                column: "ParentItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItems_ScheduleItems_ParentItemId",
                table: "ScheduleItems",
                column: "ParentItemId",
                principalTable: "ScheduleItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItems_ScheduleItems_ParentItemId",
                table: "ScheduleItems");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleItems_ParentItemId",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "IsSection",
                table: "ScheduleItemTemplates");

            migrationBuilder.DropColumn(
                name: "IsSection",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "ParentItemId",
                table: "ScheduleItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "LaneId",
                table: "ScheduleItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
