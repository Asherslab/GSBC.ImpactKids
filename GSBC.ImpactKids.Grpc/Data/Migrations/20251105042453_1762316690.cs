using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1762316690 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Allergens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allergens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Allergies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllergenId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Severe = table.Column<bool>(type: "boolean", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allergies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Allergies_Allergens_AllergenId",
                        column: x => x.AllergenId,
                        principalTable: "Allergens",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Allergies_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MedicalNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicalTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Severe = table.Column<bool>(type: "boolean", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalNotes_MedicalTypes_MedicalTypeId",
                        column: x => x.MedicalTypeId,
                        principalTable: "MedicalTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MedicalNotes_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allergies_AllergenId",
                table: "Allergies",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_Allergies_PersonId",
                table: "Allergies",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalNotes_MedicalTypeId",
                table: "MedicalNotes",
                column: "MedicalTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalNotes_PersonId",
                table: "MedicalNotes",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Allergies");

            migrationBuilder.DropTable(
                name: "MedicalNotes");

            migrationBuilder.DropTable(
                name: "Allergens");

            migrationBuilder.DropTable(
                name: "MedicalTypes");
        }
    }
}
