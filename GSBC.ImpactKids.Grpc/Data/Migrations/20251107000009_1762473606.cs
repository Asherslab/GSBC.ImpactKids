using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1762473606 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_MemorisationEntries",
                table: "MemorisationEntries");

            migrationBuilder.DropIndex(
                name: "IX_MemorisationEntries_PersonId_ServiceId_MemoryVerseId",
                table: "MemorisationEntries");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "MemorisationEntries");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MemorisationEntries",
                table: "MemorisationEntries",
                columns: new[] { "PersonId", "ServiceId", "MemoryVerseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_MemorisationEntries",
                table: "MemorisationEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "MemorisationEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_MemorisationEntries",
                table: "MemorisationEntries",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_MemorisationEntries_PersonId_ServiceId_MemoryVerseId",
                table: "MemorisationEntries",
                columns: new[] { "PersonId", "ServiceId", "MemoryVerseId" },
                unique: true);
        }
    }
}
