using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropSyncFieldConfigsAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncFieldConfigs");

            migrationBuilder.DropTable(
                name: "SyncMetadata");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncFieldConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    FieldName = table.Column<string>(type: "text", nullable: false),
                    PrecedenceOnTie = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncFieldConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElvantoId = table.Column<string>(type: "text", nullable: false),
                    LastSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "text", nullable: true),
                    ManualReviewReason = table.Column<string>(type: "text", nullable: true),
                    MatchConfidence = table.Column<int>(type: "integer", nullable: false),
                    MatchStrategy = table.Column<string>(type: "text", nullable: true),
                    MatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncMetadata_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "SyncFieldConfigs",
                columns: new[] { "Id", "Direction", "EntityType", "FieldName", "PrecedenceOnTie" },
                values: new object[,]
                {
                    { new Guid("0b002705-d332-f598-e7f1-958d18bfbc70"), "Bidirectional", "Person", "PhoneNumber", "Elvanto" },
                    { new Guid("2f2ea444-7b57-9ffa-ac55-37b6f6c7b778"), "Bidirectional", "Person", "MediaConsent", "Elvanto" },
                    { new Guid("34b7454b-74ac-4a48-c0b2-89e8ef1d5698"), "InboundOnly", "Person", "SchoolGradeId", "Elvanto" },
                    { new Guid("3ba3eb59-3a5d-dfc0-5434-696367590a7a"), "InboundOnly", "Person", "FamilyGuardian", "Elvanto" },
                    { new Guid("9c27bc74-3cb5-98c2-901d-3f8b97ca7dc8"), "Bidirectional", "Person", "MedicalAllergyNotes", "App" },
                    { new Guid("a8b0fa9c-7d2f-c571-2dbb-575b6409e07d"), "Bidirectional", "Person", "FirstName", "Elvanto" },
                    { new Guid("b6fbd7e5-74d7-9cb4-5a3d-d3543f4468bb"), "Bidirectional", "Person", "FamilyId", "Elvanto" },
                    { new Guid("bfcd1d0d-4275-70af-c5ba-038189177e39"), "Bidirectional", "Person", "Email", "Elvanto" },
                    { new Guid("d6544c23-e195-93c9-62e1-c47a23089c82"), "Bidirectional", "Person", "FirstTime", "Elvanto" },
                    { new Guid("d91dbee9-ebe0-c13f-47f4-cac9d901344f"), "Bidirectional", "Person", "DateOfBirth", "Elvanto" },
                    { new Guid("e2ef7ea7-a11a-3123-dbf8-26fd3e8f826a"), "Bidirectional", "Person", "LastName", "Elvanto" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncFieldConfigs_EntityType_FieldName",
                table: "SyncFieldConfigs",
                columns: new[] { "EntityType", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncMetadata_ElvantoId",
                table: "SyncMetadata",
                column: "ElvantoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncMetadata_PersonId",
                table: "SyncMetadata",
                column: "PersonId",
                unique: true);
        }
    }
}
