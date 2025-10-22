using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1761136412 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemorisationEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryVerseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerseRecited = table.Column<bool>(type: "boolean", nullable: false),
                    FiveDollaryDoosGiven = table.Column<bool>(type: "boolean", nullable: false),
                    OneDollaryDooGiven = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemorisationEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemorisationEntries_MemoryVerses_MemoryVerseId",
                        column: x => x.MemoryVerseId,
                        principalTable: "MemoryVerses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemorisationEntries_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemorisationEntries_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemorisationEntries_MemoryVerseId",
                table: "MemorisationEntries",
                column: "MemoryVerseId");

            migrationBuilder.CreateIndex(
                name: "IX_MemorisationEntries_PersonId_ServiceId_MemoryVerseId",
                table: "MemorisationEntries",
                columns: new[] { "PersonId", "ServiceId", "MemoryVerseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemorisationEntries_ServiceId",
                table: "MemorisationEntries",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemorisationEntries");
        }
    }
}
