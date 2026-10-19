using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProPresenterMachines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProPresenterMachines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SecretHash = table.Column<string>(type: "text", nullable: true),
                    TokenSigningKey = table.Column<string>(type: "text", nullable: false),
                    EnrolmentCodeHash = table.Column<string>(type: "text", nullable: true),
                    EnrolmentChallenge = table.Column<string>(type: "text", nullable: true),
                    EnrolmentRedirectUri = table.Column<string>(type: "text", nullable: true),
                    EnrolmentExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EnrolledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProPresenterHost = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProPresenterPort = table.Column<int>(type: "integer", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AgentVersion = table.Column<string>(type: "text", nullable: true),
                    ProPresenterReachable = table.Column<bool>(type: "boolean", nullable: true),
                    ProPresenterVersion = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProPresenterMachines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProPresenterMachines_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProPresenterMachines_CreatedByUserId",
                table: "ProPresenterMachines",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProPresenterMachines_EnrolmentCodeHash",
                table: "ProPresenterMachines",
                column: "EnrolmentCodeHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProPresenterMachines");
        }
    }
}
