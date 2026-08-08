using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1786172458 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointsMultiplier",
                table: "GameBoards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Boards written before the column existed are ordinary nights, so they get
            // the usual multiplier rather than a zero the reader has to interpret.
            migrationBuilder.Sql("""UPDATE "GameBoards" SET "PointsMultiplier" = 1000;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsMultiplier",
                table: "GameBoards");
        }
    }
}
