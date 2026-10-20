using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class SongsInSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSong",
                table: "ScheduleItemTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArrangementUuid",
                table: "ScheduleItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSong",
                table: "ScheduleItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SongId",
                table: "ScheduleItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_SongId",
                table: "ScheduleItems",
                column: "SongId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItems_Songs_SongId",
                table: "ScheduleItems",
                column: "SongId",
                principalTable: "Songs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItems_Songs_SongId",
                table: "ScheduleItems");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleItems_SongId",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "IsSong",
                table: "ScheduleItemTemplates");

            migrationBuilder.DropColumn(
                name: "ArrangementUuid",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "IsSong",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "SongId",
                table: "ScheduleItems");
        }
    }
}
