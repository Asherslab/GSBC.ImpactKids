using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSBC.ImpactKids.Grpc.Data.Migrations
{
    /// <inheritdoc />
    public partial class _1765604141 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(""""DROP VIEW public."VirtualMemorisationEntries";"""");

            migrationBuilder.AddColumn<DateTimeOffset>(
                "StartDateTmp",
                table: "Terms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()"
            );
            migrationBuilder.AddColumn<DateTimeOffset>(
                "EndDateTmp",
                table: "Terms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()"
            );
            migrationBuilder.Sql(
                """
                UPDATE public."Terms"
                SET "StartDateTmp" =
                    "StartDate" - INTERVAL '10 hours',
                    "EndDateTmp" =
                    "EndDate" - INTERVAL '10 hours'
                """
            );
            migrationBuilder.DropColumn(
                "StartDate",
                table: "Terms"
            );
            migrationBuilder.DropColumn(
                "EndDate",
                table: "Terms"
            );
            migrationBuilder.RenameColumn(
                "StartDateTmp",
                table: "Terms",
                newName: "StartDate"
            );
            migrationBuilder.RenameColumn(
                "EndDateTmp",
                table: "Terms",
                newName: "EndDate"
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                "DateTmp",
                table: "Services",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()"
            );
            migrationBuilder.Sql(
                """
                UPDATE public."Services"
                SET "DateTmp" =
                    "Date" - INTERVAL '10 hours'
                """
            );
            migrationBuilder.DropColumn(
                "Date",
                table: "Services"
            );
            migrationBuilder.RenameColumn(
                "DateTmp",
                table: "Services",
                newName: "Date"
            );
            
            migrationBuilder.AddColumn<DateTimeOffset>(
                "FirstTimeTmp",
                table: "People",
                type: "timestamp with time zone",
                nullable: true
            );
            migrationBuilder.AddColumn<DateTimeOffset>(
                "DateOfBirthTmp",
                table: "People",
                type: "timestamp with time zone",
                nullable: true
            );
            migrationBuilder.Sql(
                """
                UPDATE public."People"
                SET "FirstTimeTmp" =
                    "FirstTime" - INTERVAL '10 hours',
                    "DateOfBirthTmp" =
                    "DateOfBirth" - INTERVAL '10 hours'
                """
            );
            migrationBuilder.DropColumn(
                "FirstTime",
                table: "People"
            );
            migrationBuilder.DropColumn(
                "DateOfBirth",
                table: "People"
            );
            migrationBuilder.RenameColumn(
                "FirstTimeTmp",
                table: "People",
                newName: "FirstTime"
            );
            migrationBuilder.RenameColumn(
                "DateOfBirthTmp",
                table: "People",
                newName: "DateOfBirth"
            );
            
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
            
             migrationBuilder.AddColumn<DateTime>(
                "StartDateTmp",
                table: "Terms",
                type: "date",
                nullable: false,
                defaultValueSql: "now()"
            );
            migrationBuilder.AddColumn<DateTime>(
                "EndDateTmp",
                table: "Terms",
                type: "date",
                nullable: false,
                defaultValueSql: "now()"
            );
            migrationBuilder.Sql(
                """
                UPDATE public."Terms"
                SET "StartDateTmp" =
                    "StartDate" + INTERVAL '10 hours',
                    "EndDateTmp" =
                    "EndDate" + INTERVAL '10 hours'
                """
            );
            migrationBuilder.DropColumn(
                "StartDate",
                table: "Terms"
            );
            migrationBuilder.DropColumn(
                "EndDate",
                table: "Terms"
            );
            migrationBuilder.RenameColumn(
                "StartDateTmp",
                table: "Terms",
                newName: "StartDate"
            );
            migrationBuilder.RenameColumn(
                "EndDateTmp",
                table: "Terms",
                newName: "EndDate"
            );

            migrationBuilder.AddColumn<DateTime>(
                "DateTmp",
                table: "Services",
                type: "date",
                nullable: false,
                defaultValueSql: "now()"
            );
            migrationBuilder.Sql(
                """
                UPDATE public."Services"
                SET "DateTmp" =
                    "Date" + INTERVAL '10 hours'
                """
            );
            migrationBuilder.DropColumn(
                "Date",
                table: "Services"
            );
            migrationBuilder.RenameColumn(
                "DateTmp",
                table: "Services",
                newName: "Date"
            );
            
            migrationBuilder.AddColumn<DateTime>(
                "FirstTimeTmp",
                table: "People",
                type: "date",
                nullable: true
            );
            migrationBuilder.AddColumn<DateTime>(
                "DateOfBirthTmp",
                table: "People",
                type: "date",
                nullable: true
            );
            migrationBuilder.Sql(
                """
                UPDATE public."People"
                SET "FirstTimeTmp" =
                    "FirstTime" + INTERVAL '10 hours',
                    "DateOfBirthTmp" =
                    "DateOfBirth" + INTERVAL '10 hours'
                """
            );
            migrationBuilder.DropColumn(
                "FirstTime",
                table: "People"
            );
            migrationBuilder.DropColumn(
                "DateOfBirth",
                table: "People"
            );
            migrationBuilder.RenameColumn(
                "FirstTimeTmp",
                table: "People",
                newName: "FirstTime"
            );
            migrationBuilder.RenameColumn(
                "DateOfBirthTmp",
                table: "People",
                newName: "DateOfBirth"
            );
            
            /*migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "Terms",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "Terms",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "Services",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FirstTime",
                table: "People",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "People",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);*/
            
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
    }
}
