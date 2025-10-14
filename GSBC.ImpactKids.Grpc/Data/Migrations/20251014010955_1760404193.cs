using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1760404193 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BibleVerses_BookId",
                table: "BibleVerses");

            migrationBuilder.CreateIndex(
                name: "IX_BibleVerses_BookId_ChapterId_Id",
                table: "BibleVerses",
                columns: new[] { "BookId", "ChapterId", "Id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BibleVerses_BookId_ChapterId_Id",
                table: "BibleVerses");

            migrationBuilder.CreateIndex(
                name: "IX_BibleVerses_BookId",
                table: "BibleVerses",
                column: "BookId");
        }
    }
}
