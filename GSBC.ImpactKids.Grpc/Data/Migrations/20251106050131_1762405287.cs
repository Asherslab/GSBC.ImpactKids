using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1762405287 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "People",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstTime",
                table: "People",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaConsent",
                table: "People",
                type: "text",
                nullable: false,
                defaultValue: "NotRequested");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "People");

            migrationBuilder.DropColumn(
                name: "FirstTime",
                table: "People");

            migrationBuilder.DropColumn(
                name: "MediaConsent",
                table: "People");
        }
    }
}
