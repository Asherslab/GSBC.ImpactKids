using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class ElvantoSyncEngine : Migration
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
                name: "ElvantoFamilyLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalFamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElvantoFamilyId = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElvantoFamilyLinks", x => x.Id);
                });

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
                    AppHash = table.Column<string>(type: "text", nullable: true),
                    AppValue = table.Column<string>(type: "text", nullable: true),
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
                name: "SyncOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    PlanExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOperations", x => x.Id);
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
                    SyncOperationId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.ForeignKey(
                        name: "FK_PendingReviews_SyncOperations_SyncOperationId",
                        column: x => x.SyncOperationId,
                        principalTable: "SyncOperations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlannedChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ElvantoId = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    FieldName = table.Column<string>(type: "text", nullable: true),
                    ObservedAppValue = table.Column<string>(type: "text", nullable: true),
                    ObservedAppHash = table.Column<string>(type: "text", nullable: true),
                    ObservedElvantoValue = table.Column<string>(type: "text", nullable: true),
                    ObservedElvantoHash = table.Column<string>(type: "text", nullable: true),
                    ProposedValue = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StatusReason = table.Column<string>(type: "text", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannedChanges_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlannedChanges_SyncOperations_SyncOperationId",
                        column: x => x.SyncOperationId,
                        principalTable: "SyncOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateIndex(
                name: "IX_ElvantoFamilyLinks_ElvantoFamilyId",
                table: "ElvantoFamilyLinks",
                column: "ElvantoFamilyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElvantoFamilyLinks_LocalFamilyId",
                table: "ElvantoFamilyLinks",
                column: "LocalFamilyId",
                unique: true);

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
                name: "IX_PendingReviews_PersonId_ElvantoId",
                table: "PendingReviews",
                columns: new[] { "PersonId", "ElvantoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingReviews_SyncOperationId",
                table: "PendingReviews",
                column: "SyncOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedChanges_PersonId_DecidedAt",
                table: "PlannedChanges",
                columns: new[] { "PersonId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedChanges_SyncOperationId_Status",
                table: "PlannedChanges",
                columns: new[] { "SyncOperationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncAuditLogs_PersonId_OccurredAt",
                table: "SyncAuditLogs",
                columns: new[] { "PersonId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncAuditLogs_SyncOperationId_OccurredAt",
                table: "SyncAuditLogs",
                columns: new[] { "SyncOperationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncOperations_StartedAt",
                table: "SyncOperations",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElvantoFamilyLinks");

            migrationBuilder.DropTable(
                name: "ElvantoFieldSnapshots");

            migrationBuilder.DropTable(
                name: "FieldChangeLogs");

            migrationBuilder.DropTable(
                name: "PendingReviews");

            migrationBuilder.DropTable(
                name: "PlannedChanges");

            migrationBuilder.DropTable(
                name: "SyncAuditLogs");

            migrationBuilder.DropTable(
                name: "SyncOperations");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "People");
        }
    }
}
