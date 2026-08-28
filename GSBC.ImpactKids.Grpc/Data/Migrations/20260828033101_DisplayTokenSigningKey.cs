using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class DisplayTokenSigningKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenSigningKey",
                table: "PickupDisplayKeys",
                type: "text",
                nullable: false,
                defaultValue: "");

            // An existing key row would carry an empty signing key, which cannot sign
            // anything - the walls enrolled on it would keep their cookies at the proxy and
            // then be turned away by the gRPC service, with nothing on screen explaining why.
            // Clearing the row instead makes the state honest: the admin page reads "no key
            // has ever been set up" and offers the button, and the screens are re-enrolled
            // from the new link. Any display is offline until somebody presses rotate.
            migrationBuilder.Sql("""DELETE FROM "PickupDisplayKeys";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TokenSigningKey",
                table: "PickupDisplayKeys");
        }
    }
}
