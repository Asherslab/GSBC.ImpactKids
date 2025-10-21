using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1761022586 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DbMemoryVerseServiceRelationship",
                table: "DbMemoryVerseServiceRelationship");

            migrationBuilder.DropIndex(
                name: "IX_DbMemoryVerseServiceRelationship_MemoryVersesId",
                table: "DbMemoryVerseServiceRelationship");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DbMemoryVerseBibleVerseRelationship",
                table: "DbMemoryVerseBibleVerseRelationship");

            migrationBuilder.DropColumn(
                name: "MemoryVerseId",
                table: "DbMemoryVerseServiceRelationship");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "DbMemoryVerseServiceRelationship");

            migrationBuilder.DropColumn(
                name: "MemoryVerseId",
                table: "DbMemoryVerseBibleVerseRelationship");

            migrationBuilder.RenameColumn(
                name: "BibleVerseId",
                table: "DbMemoryVerseBibleVerseRelationship",
                newName: "MemoryVersesId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DbMemoryVerseServiceRelationship",
                table: "DbMemoryVerseServiceRelationship",
                columns: new[] { "MemoryVersesId", "ServicesId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_DbMemoryVerseBibleVerseRelationship",
                table: "DbMemoryVerseBibleVerseRelationship",
                columns: new[] { "MemoryVersesId", "BibleVersesId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DbMemoryVerseServiceRelationship",
                table: "DbMemoryVerseServiceRelationship");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DbMemoryVerseBibleVerseRelationship",
                table: "DbMemoryVerseBibleVerseRelationship");

            migrationBuilder.RenameColumn(
                name: "MemoryVersesId",
                table: "DbMemoryVerseBibleVerseRelationship",
                newName: "BibleVerseId");

            migrationBuilder.AddColumn<Guid>(
                name: "MemoryVerseId",
                table: "DbMemoryVerseServiceRelationship",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceId",
                table: "DbMemoryVerseServiceRelationship",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "MemoryVerseId",
                table: "DbMemoryVerseBibleVerseRelationship",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_DbMemoryVerseServiceRelationship",
                table: "DbMemoryVerseServiceRelationship",
                columns: new[] { "MemoryVerseId", "ServiceId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_DbMemoryVerseBibleVerseRelationship",
                table: "DbMemoryVerseBibleVerseRelationship",
                columns: new[] { "MemoryVerseId", "BibleVerseId" });

            migrationBuilder.CreateIndex(
                name: "IX_DbMemoryVerseServiceRelationship_MemoryVersesId",
                table: "DbMemoryVerseServiceRelationship",
                column: "MemoryVersesId");
        }
    }
}
