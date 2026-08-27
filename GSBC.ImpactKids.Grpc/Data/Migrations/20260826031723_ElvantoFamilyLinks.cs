using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class ElvantoFamilyLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElvantoFamilyLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalFamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElvantoFamilyId = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElvantoFamilyLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElvantoFamilyLinks_ElvantoFamilyId",
                table: "ElvantoFamilyLinks",
                column: "ElvantoFamilyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElvantoFamilyLinks_LocalFamilyId",
                table: "ElvantoFamilyLinks",
                column: "LocalFamilyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElvantoFamilyLinks");
        }
    }
}
