using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncFieldBaseAppLeg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppHash",
                table: "ElvantoFieldSnapshots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppValue",
                table: "ElvantoFieldSnapshots",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppHash",
                table: "ElvantoFieldSnapshots");

            migrationBuilder.DropColumn(
                name: "AppValue",
                table: "ElvantoFieldSnapshots");
        }
    }
}
