using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1786109744 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old enum values were Red/Green/Blue/Yellow at 0-3, which is exactly the
            // order of the default team list, so the stored numbers keep their meaning.
            migrationBuilder.RenameColumn(
                name: "Team",
                table: "GamePointRecords",
                newName: "TeamIndex");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "GamePointRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Games",
                table: "GameBoards",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Teams",
                table: "GameBoards",
                type: "jsonb",
                nullable: true);

            // Turn the old two-or-four count into a real team list before the column goes.
            migrationBuilder.Sql(
                """
                UPDATE "GameBoards"
                SET "Teams" = CASE
                    WHEN "TeamCount" = 2 THEN '[
                        {"Index":0,"Name":"Red","Colour":"#d32f2f"},
                        {"Index":1,"Name":"Green","Colour":"#2e7d32"}
                    ]'::jsonb
                    ELSE '[
                        {"Index":0,"Name":"Red","Colour":"#d32f2f"},
                        {"Index":1,"Name":"Green","Colour":"#2e7d32"},
                        {"Index":2,"Name":"Blue","Colour":"#1565c0"},
                        {"Index":3,"Name":"Yellow","Colour":"#e5a100"}
                    ]'::jsonb
                END;
                """
            );

            migrationBuilder.DropColumn(
                name: "TeamCount",
                table: "GameBoards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "GamePointRecords");

            migrationBuilder.RenameColumn(
                name: "TeamIndex",
                table: "GamePointRecords",
                newName: "Team");

            migrationBuilder.AddColumn<int>(
                name: "TeamCount",
                table: "GameBoards",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            // Anything past four teams cannot be expressed by the old column - clamp it
            // rather than lose the board entirely.
            migrationBuilder.Sql(
                """
                UPDATE "GameBoards"
                SET "TeamCount" = CASE
                    WHEN jsonb_array_length(COALESCE("Teams", '[]'::jsonb)) <= 2 THEN 2
                    ELSE 4
                END;
                """
            );

            migrationBuilder.DropColumn(
                name: "Games",
                table: "GameBoards");

            migrationBuilder.DropColumn(
                name: "Teams",
                table: "GameBoards");
        }
    }
}
