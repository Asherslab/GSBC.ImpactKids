using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class FamilyIdBidirectional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SyncFieldConfigs",
                keyColumn: "Id",
                keyValue: new Guid("b6fbd7e5-74d7-9cb4-5a3d-d3543f4468bb"),
                column: "Direction",
                value: "Bidirectional");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SyncFieldConfigs",
                keyColumn: "Id",
                keyValue: new Guid("b6fbd7e5-74d7-9cb4-5a3d-d3543f4468bb"),
                column: "Direction",
                value: "InboundOnly");
        }
    }
}
