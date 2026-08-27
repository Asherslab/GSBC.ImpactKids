#!/usr/bin/env bash
#
# Reset the local dev database from a production dump, then bring it up to the
# current migration head.
#
#   ./db-restore.sh                  # newest *.dump in the repo root
#   ./db-restore.sh path/to.dump     # a specific dump
#   ./db-restore.sh --yes            # skip the confirmation prompt
#   ./db-restore.sh --no-migrate     # restore only, leave migrations pending
#
# Why this exists: testing the Elvanto sync (or anything else) against real data
# means going back to a known prod state repeatedly. Doing that by hand invites
# the failure this script was written after - a half-migrated database whose
# __EFMigrationsHistory lists migrations that no longer exist in the branch, so
# every later `dotnet ef` run dies on "column already exists".
#
# Nothing here is hardcoded except the database name: the container and its
# password are discovered from Docker, so it keeps working when Aspire
# regenerates credentials.
#
# The dumps hold real people's data. *.dump is gitignored - keep it that way,
# and delete local copies when you are done with them.

set -euo pipefail

DB_NAME="impact-kids"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PG_RESTORE="${PG_RESTORE:-/opt/homebrew/bin/pg_restore}"
PSQL="${PSQL:-/opt/homebrew/bin/psql}"

DUMP=""
ASSUME_YES=0
MIGRATE=1

for arg in "$@"; do
  case "$arg" in
    --yes|-y)     ASSUME_YES=1 ;;
    --no-migrate) MIGRATE=0 ;;
    -h|--help)    sed -n '2,20p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*)           echo "unknown flag: $arg" >&2; exit 2 ;;
    *)            DUMP="$arg" ;;
  esac
done

# ---- locate the dump -------------------------------------------------------

if [[ -z "$DUMP" ]]; then
  DUMP="$(find "$REPO_ROOT" -maxdepth 1 -name '*.dump' -print0 \
          | xargs -0 ls -t 2>/dev/null | head -1 || true)"
  [[ -n "$DUMP" ]] || { echo "no *.dump found in $REPO_ROOT - pass one explicitly" >&2; exit 1; }
fi
[[ -f "$DUMP" ]] || { echo "no such dump: $DUMP" >&2; exit 1; }

# A custom-format archive starts with the literal PGDMP. Catching this here beats
# letting pg_restore fail halfway through a drop-and-recreate.
[[ "$(head -c 5 "$DUMP")" == "PGDMP" ]] \
  || { echo "not a custom-format pg_dump archive: $DUMP" >&2; exit 1; }

# ---- discover the Aspire postgres container --------------------------------

CONTAINER="$(docker ps --format '{{.Names}}' | grep '^sql-' | head -1 || true)"
[[ -n "$CONTAINER" ]] || {
  echo "no running sql-* container. Start the Aspire AppHost first." >&2; exit 1; }

PGPASSWORD="$(docker exec "$CONTAINER" printenv POSTGRES_PASSWORD)"
export PGPASSWORD
PORT="$(docker port "$CONTAINER" 5432/tcp | head -1 | sed 's/.*://')"
[[ -n "$PORT" ]] || { echo "could not resolve the published port for $CONTAINER" >&2; exit 1; }

# This script force-drops a database. Refuse to do that anywhere but a local
# dev container, whatever else is misconfigured.
HOST=127.0.0.1

conn() { "$PSQL" -h "$HOST" -p "$PORT" -U postgres -d postgres -tA "$@"; }

# ---- confirm ---------------------------------------------------------------

EXISTING="$(conn -c "SELECT 1 FROM pg_database WHERE datname='$DB_NAME'" || true)"
if [[ "$EXISTING" == "1" ]]; then
  PEOPLE="$("$PSQL" -h "$HOST" -p "$PORT" -U postgres -d "$DB_NAME" -tA \
            -c 'SELECT count(*) FROM "People"' 2>/dev/null || echo '?')"
else
  PEOPLE="(database does not exist)"
fi

cat <<EOF

  Restore target : $DB_NAME on $HOST:$PORT  (container $CONTAINER)
  From dump      : $DUMP
                   $(ls -lh "$DUMP" | awk '{print $5}'), $(date -r "$DUMP" '+%Y-%m-%d %H:%M')
  Current People : $PEOPLE

  This DROPs the database and everything in it.

EOF

if [[ "$ASSUME_YES" -ne 1 ]]; then
  read -r -p "Type the database name to confirm: " reply
  [[ "$reply" == "$DB_NAME" ]] || { echo "aborted."; exit 1; }
fi

# ---- drop, recreate, restore ------------------------------------------------

# WITH (FORCE) terminates live backends. The gRPC service holds a pool open, so
# a plain DROP DATABASE fails while the AppHost is running.
echo "==> dropping $DB_NAME"
conn -c "DROP DATABASE IF EXISTS \"$DB_NAME\" WITH (FORCE)" >/dev/null

echo "==> creating $DB_NAME"
conn -c "CREATE DATABASE \"$DB_NAME\"" >/dev/null

# --no-owner/--no-privileges: prod roles do not exist locally.
# -j: the archive is restored in parallel; safe here because the target is empty.
echo "==> restoring"
"$PG_RESTORE" -h "$HOST" -p "$PORT" -U postgres -d "$DB_NAME" \
  --no-owner --no-privileges -j 4 "$DUMP"

# ---- migrate ----------------------------------------------------------------

if [[ "$MIGRATE" -eq 1 ]]; then
  # Pass the connection explicitly rather than letting GsbcDbContextFactory supply
  # it. That factory hardcodes port 60536, but a persistent container keeps
  # whatever port it was first created with, so the two drift apart and every
  # `dotnet ef` call fails with the useless "Unable to determine which migrations
  # have been applied". The discovered port is the truth.
  echo "==> applying pending migrations (port $PORT)"
  dotnet ef database update --project "$REPO_ROOT/GSBC.ImpactKids.Grpc" \
    --connection "Host=$HOST;Port=$PORT;Database=$DB_NAME;Username=postgres;Password=$PGPASSWORD" \
    | tail -3
fi

# ---- report -----------------------------------------------------------------

echo
echo "==> done"
"$PSQL" -h "$HOST" -p "$PORT" -U postgres -d "$DB_NAME" -tA <<SQL | sed 's/^/    /'
SELECT 'people:      ' || count(*) FROM "People";
SELECT 'migrations:  ' || count(*) FROM "__EFMigrationsHistory";
SELECT 'head:        ' || max("MigrationId") FROM "__EFMigrationsHistory";
SQL

if [[ "$MIGRATE" -eq 1 ]]; then
  echo
  echo "    Restart the AppHost so the gRPC service picks up the new database."
fi
