using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1761023124 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DbMemoryVerseBibleVerseRelationship_MemoryVerses_DbMemoryVe~",
                table: "DbMemoryVerseBibleVerseRelationship");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DbMemoryVerseBibleVerseRelationship",
                table: "DbMemoryVerseBibleVerseRelationship");

            migrationBuilder.DropIndex(
                name: "IX_DbMemoryVerseBibleVerseRelationship_DbMemoryVerseId",
                table: "DbMemoryVerseBibleVerseRelationship");

            migrationBuilder.DropColumn(
                name: "MemoryVerseId",
                table: "DbMemoryVerseBibleVerseRelationship");

            migrationBuilder.RenameColumn(
                name: "DbMemoryVerseId",
                table: "DbMemoryVerseBibleVerseRelationship",
                newName: "MemoryVersesId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DbMemoryVerseBibleVerseRelationship",
                table: "DbMemoryVerseBibleVerseRelationship",
                columns: new[] { "MemoryVersesId", "BibleVersesId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DbMemoryVerseBibleVerseRelationship_MemoryVerses_MemoryVers~",
                table: "DbMemoryVerseBibleVerseRelationship",
                column: "MemoryVersesId",
                principalTable: "MemoryVerses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DbMemoryVerseBibleVerseRelationship_MemoryVerses_MemoryVers~",
                table: "DbMemoryVerseBibleVerseRelationship");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DbMemoryVerseBibleVerseRelationship",
                table: "DbMemoryVerseBibleVerseRelationship");

            migrationBuilder.RenameColumn(
                name: "MemoryVersesId",
                table: "DbMemoryVerseBibleVerseRelationship",
                newName: "DbMemoryVerseId");

            migrationBuilder.AddColumn<Guid>(
                name: "MemoryVerseId",
                table: "DbMemoryVerseBibleVerseRelationship",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_DbMemoryVerseBibleVerseRelationship",
                table: "DbMemoryVerseBibleVerseRelationship",
                columns: new[] { "MemoryVerseId", "BibleVersesId" });

            migrationBuilder.CreateIndex(
                name: "IX_DbMemoryVerseBibleVerseRelationship_DbMemoryVerseId",
                table: "DbMemoryVerseBibleVerseRelationship",
                column: "DbMemoryVerseId");

            migrationBuilder.AddForeignKey(
                name: "FK_DbMemoryVerseBibleVerseRelationship_MemoryVerses_DbMemoryVe~",
                table: "DbMemoryVerseBibleVerseRelationship",
                column: "DbMemoryVerseId",
                principalTable: "MemoryVerses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
