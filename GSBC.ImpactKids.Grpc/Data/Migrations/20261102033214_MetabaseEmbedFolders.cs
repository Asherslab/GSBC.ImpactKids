using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class MetabaseEmbedFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MetabaseId",
                table: "MetabaseEmbeds",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "MetabaseEmbeds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "MetabaseEmbeds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MetabaseEmbeds_ParentId",
                table: "MetabaseEmbeds",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_MetabaseEmbeds_MetabaseEmbeds_ParentId",
                table: "MetabaseEmbeds",
                column: "ParentId",
                principalTable: "MetabaseEmbeds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // The order the seeded embeds had in appsettings.json; Memorisation Graphs keeps 0.
            migrationBuilder.UpdateData(
                table: "MetabaseEmbeds",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a52-3d0e-4b8a-9a61-0b5d2f7e1a02"),
                column: "SortOrder",
                value: 1);

            migrationBuilder.UpdateData(
                table: "MetabaseEmbeds",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a52-3d0e-4b8a-9a61-0b5d2f7e1a03"),
                column: "SortOrder",
                value: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetabaseEmbeds_MetabaseEmbeds_ParentId",
                table: "MetabaseEmbeds");

            migrationBuilder.DropIndex(
                name: "IX_MetabaseEmbeds_ParentId",
                table: "MetabaseEmbeds");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "MetabaseEmbeds");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "MetabaseEmbeds");

            migrationBuilder.AlterColumn<int>(
                name: "MetabaseId",
                table: "MetabaseEmbeds",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
