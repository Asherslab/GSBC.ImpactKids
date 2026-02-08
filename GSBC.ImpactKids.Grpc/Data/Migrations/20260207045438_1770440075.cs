using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1770440075 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceItemTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Reward = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceItemTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SignedIn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SignedOut = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignedInUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignedOutUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_User_SignedInUserId",
                        column: x => x.SignedInUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_User_SignedOutUserId",
                        column: x => x.SignedOutUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AttendanceItemRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemBrought = table.Column<bool>(type: "boolean", nullable: false),
                    RewardGiven = table.Column<bool>(type: "boolean", nullable: false),
                    AttendanceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceItemTypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceItemRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceItemRecords_AttendanceItemTypes_AttendanceItemTyp~",
                        column: x => x.AttendanceItemTypeId,
                        principalTable: "AttendanceItemTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceItemRecords_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceItemRecords_AttendanceItemTypeId",
                table: "AttendanceItemRecords",
                column: "AttendanceItemTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceItemRecords_AttendanceRecordId",
                table: "AttendanceItemRecords",
                column: "AttendanceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_PersonId",
                table: "AttendanceRecords",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_SignedInUserId",
                table: "AttendanceRecords",
                column: "SignedInUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_SignedOutUserId",
                table: "AttendanceRecords",
                column: "SignedOutUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceItemRecords");

            migrationBuilder.DropTable(
                name: "AttendanceItemTypes");

            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
