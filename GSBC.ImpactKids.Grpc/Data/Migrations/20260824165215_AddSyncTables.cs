using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "People",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ElvantoFieldSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "text", nullable: false),
                    LastSeenHash = table.Column<string>(type: "text", nullable: false),
                    LastSeenValue = table.Column<string>(type: "text", nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElvantoFieldSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldChangeLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "text", nullable: false),
                    ValueHash = table.Column<string>(type: "text", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldChangeLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElvantoId = table.Column<string>(type: "text", nullable: false),
                    MatchConfidence = table.Column<int>(type: "integer", nullable: false),
                    MatchStrategy = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PersonName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingReviews_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SyncFieldConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    FieldName = table.Column<string>(type: "text", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
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
                    MatchConfidence = table.Column<int>(type: "integer", nullable: false),
                    MatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MatchStrategy = table.Column<string>(type: "text", nullable: true),
                    ManualReviewReason = table.Column<string>(type: "text", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "SyncOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncOperations_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SyncAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    FieldName = table.Column<string>(type: "text", nullable: true),
                    FromValue = table.Column<string>(type: "text", nullable: true),
                    ToValue = table.Column<string>(type: "text", nullable: true),
                    Direction = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncAuditLogs_SyncOperations_SyncOperationId",
                        column: x => x.SyncOperationId,
                        principalTable: "SyncOperations",
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
                    { new Guid("34b7454b-74ac-4a48-c0b2-89e8ef1d5698"), "Bidirectional", "Person", "SchoolGradeId", "Elvanto" },
                    { new Guid("3ba3eb59-3a5d-dfc0-5434-696367590a7a"), "InboundOnly", "Person", "FamilyGuardian", "Elvanto" },
                    { new Guid("612124ce-e604-b8af-b118-4d61e47dfb7e"), "OutboundOnly", "Person", "Allergies", "App" },
                    { new Guid("859e2ffb-98c7-7799-8f6c-7316b37bde5e"), "OutboundOnly", "Person", "MedicalNotes", "App" },
                    { new Guid("a8b0fa9c-7d2f-c571-2dbb-575b6409e07d"), "Bidirectional", "Person", "FirstName", "Elvanto" },
                    { new Guid("b6fbd7e5-74d7-9cb4-5a3d-d3543f4468bb"), "InboundOnly", "Person", "FamilyId", "Elvanto" },
                    { new Guid("bfcd1d0d-4275-70af-c5ba-038189177e39"), "Bidirectional", "Person", "Email", "Elvanto" },
                    { new Guid("d6544c23-e195-93c9-62e1-c47a23089c82"), "Bidirectional", "Person", "FirstTime", "Elvanto" },
                    { new Guid("d91dbee9-ebe0-c13f-47f4-cac9d901344f"), "Bidirectional", "Person", "DateOfBirth", "Elvanto" },
                    { new Guid("e2ef7ea7-a11a-3123-dbf8-26fd3e8f826a"), "Bidirectional", "Person", "LastName", "Elvanto" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElvantoFieldSnapshots_EntityType_EntityId_FieldName",
                table: "ElvantoFieldSnapshots",
                columns: new[] { "EntityType", "EntityId", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldChangeLogs_EntityType_EntityId_FieldName_ChangedAt",
                table: "FieldChangeLogs",
                columns: new[] { "EntityType", "EntityId", "FieldName", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingReviews_PersonId",
                table: "PendingReviews",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingReviews_PersonId_ElvantoId",
                table: "PendingReviews",
                columns: new[] { "PersonId", "ElvantoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncAuditLogs_PersonId_OccurredAt",
                table: "SyncAuditLogs",
                columns: new[] { "PersonId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncAuditLogs_SyncOperationId_OccurredAt",
                table: "SyncAuditLogs",
                columns: new[] { "SyncOperationId", "OccurredAt" });

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

            migrationBuilder.CreateIndex(
                name: "IX_SyncOperations_PersonId",
                table: "SyncOperations",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncOperations_Scope_StartedAt",
                table: "SyncOperations",
                columns: new[] { "Scope", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElvantoFieldSnapshots");

            migrationBuilder.DropTable(
                name: "FieldChangeLogs");

            migrationBuilder.DropTable(
                name: "PendingReviews");

            migrationBuilder.DropTable(
                name: "SyncAuditLogs");

            migrationBuilder.DropTable(
                name: "SyncFieldConfigs");

            migrationBuilder.DropTable(
                name: "SyncMetadata");

            migrationBuilder.DropTable(
                name: "SyncOperations");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "People");
        }
    }
}
