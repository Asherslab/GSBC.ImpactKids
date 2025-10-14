using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1760437408 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BibleVerses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Verse = table.Column<string>(type: "text", nullable: false),
                    VerseNumber = table.Column<int>(type: "integer", nullable: false),
                    ChapterNumber = table.Column<int>(type: "integer", nullable: false),
                    BookNumber = table.Column<int>(type: "integer", nullable: false),
                    BookName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleVerses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Terms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemoryVerseLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SchoolTermId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryVerseLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoryVerseLists_Terms_SchoolTermId",
                        column: x => x.SchoolTermId,
                        principalTable: "Terms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    SchoolTermId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_Terms_SchoolTermId",
                        column: x => x.SchoolTermId,
                        principalTable: "Terms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemoryVerses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceName = table.Column<string>(type: "text", nullable: false),
                    Verse = table.Column<string>(type: "text", nullable: false),
                    MemoryVerseListId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryVerses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoryVerses_MemoryVerseLists_MemoryVerseListId",
                        column: x => x.MemoryVerseListId,
                        principalTable: "MemoryVerseLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DbMemoryVerseBibleVerseRelationship",
                columns: table => new
                {
                    MemoryVerseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BibleVerseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BibleVersesId = table.Column<Guid>(type: "uuid", nullable: false),
                    DbMemoryVerseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbMemoryVerseBibleVerseRelationship", x => new { x.MemoryVerseId, x.BibleVerseId });
                    table.ForeignKey(
                        name: "FK_DbMemoryVerseBibleVerseRelationship_BibleVerses_BibleVerses~",
                        column: x => x.BibleVersesId,
                        principalTable: "BibleVerses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DbMemoryVerseBibleVerseRelationship_MemoryVerses_DbMemoryVe~",
                        column: x => x.DbMemoryVerseId,
                        principalTable: "MemoryVerses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DbMemoryVerseServiceRelationship",
                columns: table => new
                {
                    MemoryVerseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryVersesId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicesId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbMemoryVerseServiceRelationship", x => new { x.MemoryVerseId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_DbMemoryVerseServiceRelationship_MemoryVerses_MemoryVersesId",
                        column: x => x.MemoryVersesId,
                        principalTable: "MemoryVerses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DbMemoryVerseServiceRelationship_Services_ServicesId",
                        column: x => x.ServicesId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DbMemoryVerseBibleVerseRelationship_BibleVersesId",
                table: "DbMemoryVerseBibleVerseRelationship",
                column: "BibleVersesId");

            migrationBuilder.CreateIndex(
                name: "IX_DbMemoryVerseBibleVerseRelationship_DbMemoryVerseId",
                table: "DbMemoryVerseBibleVerseRelationship",
                column: "DbMemoryVerseId");

            migrationBuilder.CreateIndex(
                name: "IX_DbMemoryVerseServiceRelationship_MemoryVersesId",
                table: "DbMemoryVerseServiceRelationship",
                column: "MemoryVersesId");

            migrationBuilder.CreateIndex(
                name: "IX_DbMemoryVerseServiceRelationship_ServicesId",
                table: "DbMemoryVerseServiceRelationship",
                column: "ServicesId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryVerseLists_SchoolTermId",
                table: "MemoryVerseLists",
                column: "SchoolTermId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryVerses_MemoryVerseListId",
                table: "MemoryVerses",
                column: "MemoryVerseListId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_SchoolTermId",
                table: "Services",
                column: "SchoolTermId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DbMemoryVerseBibleVerseRelationship");

            migrationBuilder.DropTable(
                name: "DbMemoryVerseServiceRelationship");

            migrationBuilder.DropTable(
                name: "BibleVerses");

            migrationBuilder.DropTable(
                name: "MemoryVerses");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "MemoryVerseLists");

            migrationBuilder.DropTable(
                name: "Terms");
        }
    }
}
