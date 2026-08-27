using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class PickupDisplayKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PickupDisplayKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyHash = table.Column<string>(type: "text", nullable: false),
                    RotatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RotatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickupDisplayKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickupDisplayKeys_Users_RotatedByUserId",
                        column: x => x.RotatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PickupDisplayKeys_RotatedByUserId",
                table: "PickupDisplayKeys",
                column: "RotatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickupDisplayKeys");
        }
    }
}
