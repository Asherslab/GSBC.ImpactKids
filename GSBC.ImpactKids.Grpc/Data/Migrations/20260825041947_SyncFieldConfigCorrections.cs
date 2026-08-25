using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncFieldConfigCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SyncFieldConfigs",
                keyColumn: "Id",
                keyValue: new Guid("612124ce-e604-b8af-b118-4d61e47dfb7e"));

            migrationBuilder.DeleteData(
                table: "SyncFieldConfigs",
                keyColumn: "Id",
                keyValue: new Guid("859e2ffb-98c7-7799-8f6c-7316b37bde5e"));

            migrationBuilder.UpdateData(
                table: "SyncFieldConfigs",
                keyColumn: "Id",
                keyValue: new Guid("34b7454b-74ac-4a48-c0b2-89e8ef1d5698"),
                column: "Direction",
                value: "InboundOnly");

            migrationBuilder.InsertData(
                table: "SyncFieldConfigs",
                columns: new[] { "Id", "Direction", "EntityType", "FieldName", "PrecedenceOnTie" },
                values: new object[] { new Guid("9c27bc74-3cb5-98c2-901d-3f8b97ca7dc8"), "Bidirectional", "Person", "MedicalAllergyNotes", "App" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SyncFieldConfigs",
                keyColumn: "Id",
                keyValue: new Guid("9c27bc74-3cb5-98c2-901d-3f8b97ca7dc8"));

            migrationBuilder.UpdateData(
                table: "SyncFieldConfigs",
                keyColumn: "Id",
                keyValue: new Guid("34b7454b-74ac-4a48-c0b2-89e8ef1d5698"),
                column: "Direction",
                value: "Bidirectional");

            migrationBuilder.InsertData(
                table: "SyncFieldConfigs",
                columns: new[] { "Id", "Direction", "EntityType", "FieldName", "PrecedenceOnTie" },
                values: new object[,]
                {
                    { new Guid("612124ce-e604-b8af-b118-4d61e47dfb7e"), "OutboundOnly", "Person", "Allergies", "App" },
                    { new Guid("859e2ffb-98c7-7799-8f6c-7316b37bde5e"), "OutboundOnly", "Person", "MedicalNotes", "App" }
                });
        }
    }
}
