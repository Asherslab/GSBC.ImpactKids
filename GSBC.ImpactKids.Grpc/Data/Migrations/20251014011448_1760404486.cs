using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1760404486 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BibleVerses",
                table: "BibleVerses");

            migrationBuilder.DropIndex(
                name: "IX_BibleVerses_BookId_ChapterId_Id",
                table: "BibleVerses");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "BibleVerses",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_BibleVerses",
                table: "BibleVerses",
                columns: new[] { "BookId", "ChapterId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BibleVerses",
                table: "BibleVerses");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "BibleVerses",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_BibleVerses",
                table: "BibleVerses",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_BibleVerses_BookId_ChapterId_Id",
                table: "BibleVerses",
                columns: new[] { "BookId", "ChapterId", "Id" },
                unique: true);
        }
    }
}
