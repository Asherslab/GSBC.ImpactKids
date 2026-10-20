using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class SongsLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LibrariesJson",
                table: "ProPresenterMachines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SongLibraryUuidsJson",
                table: "ProPresenterMachines",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Songs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProPresenterUuid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Library = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "text", nullable: false),
                    FirstSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SyncedByMachineId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Songs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Songs_ProPresenterMachines_SyncedByMachineId",
                        column: x => x.SyncedByMachineId,
                        principalTable: "ProPresenterMachines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Songs_ProPresenterUuid",
                table: "Songs",
                column: "ProPresenterUuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Songs_SyncedByMachineId",
                table: "Songs",
                column: "SyncedByMachineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Songs");

            migrationBuilder.DropColumn(
                name: "LibrariesJson",
                table: "ProPresenterMachines");

            migrationBuilder.DropColumn(
                name: "SongLibraryUuidsJson",
                table: "ProPresenterMachines");
        }
    }
}
