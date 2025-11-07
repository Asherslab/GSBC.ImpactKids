using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1762431952 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """"
                CREATE OR REPLACE VIEW public."VirtualMemorisationEntries" AS
                SELECT
                    p."Id"  AS "PersonId",
                    mv."Id" AS "MemoryVerseId",
                    s."Id"  AS "ServiceId",
                    COALESCE(me."VerseRecited",         FALSE) AS "VerseRecited",
                    COALESCE(me."FiveDollaryDoosGiven", FALSE) AS "FiveDollaryDoosGiven",
                    COALESCE(me."OneDollaryDooGiven",   FALSE) AS "OneDollaryDooGiven",
                
                    -- Has this verse been recited before (for this person) at any earlier service?
                    EXISTS (
                        SELECT 1
                        FROM "MemorisationEntries" AS me_prev
                                 JOIN "Services"            AS s_prev
                                      ON s_prev."Id" = me_prev."ServiceId"
                        WHERE me_prev."PersonId"      = p."Id"
                          AND me_prev."MemoryVerseId" = mv."Id"
                          AND me_prev."VerseRecited"  = TRUE
                          AND s_prev."Date" < s."Date"
                    ) AS "VerseHasBeenRecitedBefore"
                
                FROM "People" AS p
                         CROSS JOIN "MemoryVerses" AS mv
                         JOIN "DbMemoryVerseServiceRelationship" AS mvsr
                              ON mvsr."MemoryVersesId" = mv."Id"
                         JOIN "Services" AS s
                              ON s."Id" = mvsr."ServicesId"
                         LEFT JOIN "MemorisationEntries" AS me
                                   ON me."PersonId"      = p."Id"
                                       AND me."MemoryVerseId" = mv."Id"
                                       AND me."ServiceId"     = s."Id";
                """"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(""""DROP VIEW public."VirtualMemorisationEntries";"""");
        }
    }
}
