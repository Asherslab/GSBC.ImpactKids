using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1761022854 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MemoryVersesId",
                table: "DbMemoryVerseBibleVerseRelationship",
                newName: "MemoryVerseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MemoryVerseId",
                table: "DbMemoryVerseBibleVerseRelationship",
                newName: "MemoryVersesId");
        }
    }
}
