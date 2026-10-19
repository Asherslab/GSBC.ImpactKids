using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class ServiceSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleItemTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<TimeSpan>(type: "interval", nullable: true),
                    NeedsFillIn = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleItemTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultForServiceTypeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schedules_ServiceTypes_DefaultForServiceTypeId",
                        column: x => x.DefaultForServiceTypeId,
                        principalTable: "ServiceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Schedules_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleBlocks_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleGroups_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleLanes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    GroupIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleLanes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleLanes_ScheduleBlocks_BlockId",
                        column: x => x.BlockId,
                        principalTable: "ScheduleBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FillIn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NeedsFillIn = table.Column<bool>(type: "boolean", nullable: false),
                    ItemTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedMinutes = table.Column<int>(type: "integer", nullable: false),
                    AdjustmentMinutes = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleItems_ScheduleItemTemplates_ItemTemplateId",
                        column: x => x.ItemTemplateId,
                        principalTable: "ScheduleItemTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduleItems_ScheduleLanes_LaneId",
                        column: x => x.LaneId,
                        principalTable: "ScheduleLanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleItemAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleItemAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleItemAdjustments_ScheduleItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "ScheduleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleItemAdjustments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleBlocks_ScheduleId",
                table: "ScheduleBlocks",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleGroups_ScheduleId",
                table: "ScheduleGroups",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemAdjustments_ItemId",
                table: "ScheduleItemAdjustments",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemAdjustments_UserId",
                table: "ScheduleItemAdjustments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_ItemTemplateId",
                table: "ScheduleItems",
                column: "ItemTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_LaneId",
                table: "ScheduleItems",
                column: "LaneId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleLanes_BlockId",
                table: "ScheduleLanes",
                column: "BlockId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_DefaultForServiceTypeId",
                table: "Schedules",
                column: "DefaultForServiceTypeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_ServiceId",
                table: "Schedules",
                column: "ServiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleGroups");

            migrationBuilder.DropTable(
                name: "ScheduleItemAdjustments");

            migrationBuilder.DropTable(
                name: "ScheduleItems");

            migrationBuilder.DropTable(
                name: "ScheduleItemTemplates");

            migrationBuilder.DropTable(
                name: "ScheduleLanes");

            migrationBuilder.DropTable(
                name: "ScheduleBlocks");

            migrationBuilder.DropTable(
                name: "Schedules");
        }
    }
}
