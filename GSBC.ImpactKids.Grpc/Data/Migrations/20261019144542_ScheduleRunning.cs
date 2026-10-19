using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleRunning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WaitingItemId",
                table: "Schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OnScreen",
                table: "ScheduleLanes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualEndedAt",
                table: "ScheduleItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualStartedAt",
                table: "ScheduleItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleTimerUuid",
                table: "ProPresenterMachines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimersJson",
                table: "ProPresenterMachines",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScheduleRunSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaneId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Before = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRunSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleRunSteps_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRunSteps_ScheduleId_LaneId_At",
                table: "ScheduleRunSteps",
                columns: new[] { "ScheduleId", "LaneId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleRunSteps");

            migrationBuilder.DropColumn(
                name: "WaitingItemId",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "OnScreen",
                table: "ScheduleLanes");

            migrationBuilder.DropColumn(
                name: "ActualEndedAt",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "ActualStartedAt",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "ScheduleTimerUuid",
                table: "ProPresenterMachines");

            migrationBuilder.DropColumn(
                name: "TimersJson",
                table: "ProPresenterMachines");
        }
    }
}
