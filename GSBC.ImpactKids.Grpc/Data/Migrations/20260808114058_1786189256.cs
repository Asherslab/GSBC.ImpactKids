using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1786189256 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BehaviourPointsMultiplier",
                table: "GameBoards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Behaviour points used to follow the night's multiplier, so an existing board
            // keeps showing what it showed before the two came apart.
            migrationBuilder.Sql(
                """UPDATE "GameBoards" SET "BehaviourPointsMultiplier" = "PointsMultiplier";"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BehaviourPointsMultiplier",
                table: "GameBoards");
        }
    }
}
