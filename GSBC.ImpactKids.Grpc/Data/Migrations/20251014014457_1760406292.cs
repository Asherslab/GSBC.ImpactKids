using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1760406292 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BibleVerses_BibleBooks_BookId",
                table: "BibleVerses");

            migrationBuilder.DropForeignKey(
                name: "FK_BibleVerses_BibleChapters_ChapterId",
                table: "BibleVerses");

            migrationBuilder.DropTable(
                name: "BibleBooks");

            migrationBuilder.DropTable(
                name: "BibleChapters");

            migrationBuilder.DropIndex(
                name: "IX_BibleVerses_ChapterId",
                table: "BibleVerses");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "BibleVerses",
                newName: "VerseNumber");

            migrationBuilder.RenameColumn(
                name: "ChapterId",
                table: "BibleVerses",
                newName: "ChapterNumber");

            migrationBuilder.RenameColumn(
                name: "BookId",
                table: "BibleVerses",
                newName: "BookNumber");

            migrationBuilder.AddColumn<string>(
                name: "BookName",
                table: "BibleVerses",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookName",
                table: "BibleVerses");

            migrationBuilder.RenameColumn(
                name: "VerseNumber",
                table: "BibleVerses",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "ChapterNumber",
                table: "BibleVerses",
                newName: "ChapterId");

            migrationBuilder.RenameColumn(
                name: "BookNumber",
                table: "BibleVerses",
                newName: "BookId");

            migrationBuilder.CreateTable(
                name: "BibleBooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleBooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BibleChapters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleChapters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BibleVerses_ChapterId",
                table: "BibleVerses",
                column: "ChapterId");

            migrationBuilder.AddForeignKey(
                name: "FK_BibleVerses_BibleBooks_BookId",
                table: "BibleVerses",
                column: "BookId",
                principalTable: "BibleBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BibleVerses_BibleChapters_ChapterId",
                table: "BibleVerses",
                column: "ChapterId",
                principalTable: "BibleChapters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
