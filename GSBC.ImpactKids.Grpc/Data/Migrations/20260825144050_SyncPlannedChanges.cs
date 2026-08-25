using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncPlannedChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PlanExpiresAt",
                table: "SyncOperations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncOperationId",
                table: "PendingReviews",
                type: "uuid",
                nullable: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_PendingReviews_SyncOperations_SyncOperationId",
                table: "PendingReviews",
                column: "SyncOperationId",
                principalTable: "SyncOperations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PendingReviews_SyncOperations_SyncOperationId",
                table: "PendingReviews");

            migrationBuilder.DropTable(
                name: "PlannedChanges");

            migrationBuilder.DropIndex(
                name: "IX_PendingReviews_SyncOperationId",
                table: "PendingReviews");

            migrationBuilder.DropColumn(
                name: "PlanExpiresAt",
                table: "SyncOperations");

            migrationBuilder.DropColumn(
                name: "SyncOperationId",
                table: "PendingReviews");
        }
    }
}
