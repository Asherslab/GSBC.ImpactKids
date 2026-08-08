using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1786163364 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RevealStep",
                table: "GameBoards",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RevealStep",
                table: "GameBoards");
        }
    }
}
