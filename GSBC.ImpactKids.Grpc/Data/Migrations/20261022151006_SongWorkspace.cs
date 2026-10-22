using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class SongWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveWorkspaceUuid",
                table: "ProPresenterMachines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SongWorkspaceUuid",
                table: "ProPresenterMachines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkspacesJson",
                table: "ProPresenterMachines",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveWorkspaceUuid",
                table: "ProPresenterMachines");

            migrationBuilder.DropColumn(
                name: "SongWorkspaceUuid",
                table: "ProPresenterMachines");

            migrationBuilder.DropColumn(
                name: "WorkspacesJson",
                table: "ProPresenterMachines");
        }
    }
}
