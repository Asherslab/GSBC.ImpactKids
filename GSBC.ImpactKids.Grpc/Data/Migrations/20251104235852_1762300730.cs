using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1762300730 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredName",
                table: "People");

            migrationBuilder.AddColumn<bool>(
                name: "FamilyGuardian",
                table: "People",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                table: "People",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SchoolGradeId",
                table: "People",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SchoolGrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    ElvantoId = table.Column<string>(type: "text", nullable: true),
                    NextGradeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousGrade = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolGrades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_People_SchoolGradeId",
                table: "People",
                column: "SchoolGradeId");

            migrationBuilder.AddForeignKey(
                name: "FK_People_SchoolGrades_SchoolGradeId",
                table: "People",
                column: "SchoolGradeId",
                principalTable: "SchoolGrades",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_People_SchoolGrades_SchoolGradeId",
                table: "People");

            migrationBuilder.DropTable(
                name: "SchoolGrades");

            migrationBuilder.DropIndex(
                name: "IX_People_SchoolGradeId",
                table: "People");

            migrationBuilder.DropColumn(
                name: "FamilyGuardian",
                table: "People");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "People");

            migrationBuilder.DropColumn(
                name: "SchoolGradeId",
                table: "People");

            migrationBuilder.AddColumn<string>(
                name: "PreferredName",
                table: "People",
                type: "text",
                nullable: true);
        }
    }
}
