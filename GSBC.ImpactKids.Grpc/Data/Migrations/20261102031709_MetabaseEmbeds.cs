using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class MetabaseEmbeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetabaseEmbeds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MetabaseId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetabaseEmbeds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetabaseEmbeds_Label",
                table: "MetabaseEmbeds",
                column: "Label",
                unique: true);

            // The embeds that were in the WASM appsettings.json until this table replaced it, so the
            // Analytics menu is the same the moment this deploys.
            migrationBuilder.InsertData(
                table: "MetabaseEmbeds",
                columns: ["Id", "Label", "MetabaseId", "Type"],
                values: new object[,]
                {
                    { new Guid("6f1c2a52-3d0e-4b8a-9a61-0b5d2f7e1a01"), "Memorisation Graphs", 3, "Dashboard" },
                    { new Guid("6f1c2a52-3d0e-4b8a-9a61-0b5d2f7e1a02"), "Memorisation Table", 49, "Question" },
                    { new Guid("6f1c2a52-3d0e-4b8a-9a61-0b5d2f7e1a03"), "Attendance - Today", 4, "Dashboard" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetabaseEmbeds");
        }
    }
}
