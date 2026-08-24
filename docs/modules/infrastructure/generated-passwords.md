---
title: Generated passwords and persistent volumes
kind: reference
status: current
module: infrastructure
verified: 2026-08-24
code:
  - GSBC.ImpactKids.AppHost/AppHost.cs
---

# Generated passwords and persistent volumes

Why a local run can suddenly fail to authenticate against its own database or broker, and how to
recover without losing data. Read this before deleting a volume.

**The containers hold credentials that outlive any single run.** `sql` and `rabbitmq` are
`ContainerLifetime.Persistent` with data volumes, and both seed their password **only when their data
directory is empty**. Postgres ignores `POSTGRES_PASSWORD` on an existing cluster; RabbitMQ ignores
`RABBITMQ_DEFAULT_PASS` once Mnesia exists. Change the password the AppHost holds and the volumes do not
follow — every connection is then refused with credentials both sides believe are correct.

## How the passwords go out of sync

Aspire generates each parameter password once and stores it in the AppHost's user secrets
(`Parameters:sql-password`, `Parameters:rabbitmq-password`, and so on). The host adds user secrets **only
in the Development environment**. So a Production profile starts with no value, generates a fresh one,
and persists it back over the original — which is not a rotation but a loss, because the volumes still
want the old one and nothing else has a copy.

`AppHost.cs` now calls `AddUserSecrets` unconditionally so every profile resolves the same parameters.
Verified 2026-08-24: a PROD run after that change reused the stored passwords and did not recreate either
container.

## Recognising it

The symptom is not an auth error in the app — it is the stack never starting. `migrations` waits for the
database, `grpc` waits for migrations, `yarp` waits for `grpc`, so nothing serves and no service logs a
cause.

The Aspire dashboard's MCP `list_resources` names it immediately, in the resource health report:

- Postgres — `Npgsql.PostgresException (0x80004005): 28P01: password authentication failed for user "postgres"`
- RabbitMQ — `PLAIN login refused: user 'guest' - invalid credentials`, repeating every five seconds

Confirm by hashing rather than eyeballing: compare `docker exec <container> printenv POSTGRES_PASSWORD`
against `Parameters:sql-password` in
`~/.microsoft/usersecrets/88705c3c-8603-4e41-afd3-b947d2a3ac4e/secrets.json`. **Matching hashes do not
mean healthy** — they mean the AppHost and the container agree, while the volume disagrees with both.
That is exactly the failure.

## Recovering without losing data

The data is untouched in both cases. Reset the stored credential to the one the AppHost now holds.

RabbitMQ is easy, because `rabbitmqctl` authenticates with the Erlang cookie rather than a password:

```bash
r=$(docker ps --format '{{.Names}}' | grep '^rabbitmq-')
docker exec $r rabbitmqctl change_password guest "$(docker exec $r printenv RABBITMQ_DEFAULT_PASS)"
docker exec $r rabbitmqctl authenticate_user guest "$(docker exec $r printenv RABBITMQ_DEFAULT_PASS)"
```

Postgres has no password-free path — the image's `pg_hba.conf` is `scram-sha-256` on every line,
including `local`. But `pg_hba.conf` lives *inside the volume*, so it can be edited without
authenticating. Back it up, allow local trust, reload, set the password, put the file back:

```bash
c=$(docker ps --format '{{.Names}}' | grep '^sql-')
d=/var/lib/postgresql/data
docker exec -u root $c cp $d/pg_hba.conf $d/pg_hba.conf.bak
docker exec -u root $c sed -i 's/^local\( *\)all\( *\)all\( *\)scram-sha-256/local\1all\2all\3trust/' $d/pg_hba.conf
docker exec -u root $c chown postgres:postgres $d/pg_hba.conf     # sed -i rewrites the file as root
docker exec -u postgres $c pg_ctl reload -D $d                    # pg_ctl refuses to run as root
docker exec -u postgres $c psql -U postgres -c "ALTER USER postgres WITH PASSWORD '<the env value>'"
docker exec -u root $c cp $d/pg_hba.conf.bak $d/pg_hba.conf
docker exec -u root $c chown postgres:postgres $d/pg_hba.conf
docker exec -u postgres $c pg_ctl reload -D $d
```

Two traps in that sequence, both of which bite silently: `pg_ctl` refuses to run as root, and `sed -i`
replaces the file so it comes back owned by root — which the server cannot read, so the reload appears to
do nothing.

Afterwards, check that trust is closed again (a password-free `psql` should be refused) and that the
password works over TCP.

**Deleting the volume also fixes it, and costs the local database.** That is roughly 1700 people, their
attendance, and every game record — an Elvanto sync repopulates people but not the rest. Treat it as the
last resort, not the quick fix.
