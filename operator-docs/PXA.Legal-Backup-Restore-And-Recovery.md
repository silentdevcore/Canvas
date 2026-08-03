# PXA Legal Backup, Restore, and Disaster Recovery

> Restricted operator runbook. Exclude this file from PXA.Documentation, public website builds, and the PXA.Admin handbook payload.

## Purpose And Boundary

Legal documents, immutable versions, publication approvals, acceptance evidence,
users, organizations, subscriptions, and audit events form one relational
consistency boundary. Back up and restore the complete PXA PostgreSQL database;
never attempt to recover only the Legal tables with ad hoc SQL exports.

The public last-known-good Legal snapshot is an availability artifact. It keeps
published pages readable during an outage, but it cannot authorize registration,
checkout, reacceptance, or publication and it is not a database backup.

Deployment owners must define and approve the recovery point objective, recovery
time objective, retention schedule, encryption keys, storage region, and restore
roles. This repository deliberately supplies no production credentials or fixed
commercial retention period.

## Required Controls

- Store the password-free database connection URL in a mounted file and set
  `PXA_DATABASE_URL_FILE`. Supply the password separately through a protected
  libpq password file referenced by `PGPASSFILE`; use an inline password only for
  isolated synthetic local drills.
- Encrypt backup files at rest with deployment-owned key management before they
  leave the protected backup host.
- Keep the backup, its `.sha256` file, encryption metadata, and access audit in
  immutable or versioned storage with independently tested retention.
- Separate backup operators from Legal authors and approvers where staffing
  permits. A restore never constitutes publication approval.
- Restore into a new isolated database. The repository restore script rejects a
  non-empty target and has no in-place overwrite mode.
- Coordinate PostgreSQL and external object-storage recovery to the same approved
  recovery point when documents or attachments are introduced there.
- Never include connection strings, SQL dumps, document bodies, acceptance rows,
  tokens, or customer identifiers in tickets, chat, logs, or screenshots.

## Scheduled Backup

1. Confirm database readiness and record the deployment, UTC time, application
   version, database migration level, and approved recovery-point identifier.
2. Mount the read-only password-free connection file, libpq password file, and a
   protected output volume.
3. Run:

   ```bash
   PXA_DATABASE_URL_FILE=/run/secrets/pxa_database_url \
   PGPASSFILE=/run/secrets/pxa_pgpass \
     tools/legal/backup-postgres.sh /protected/pxa-backups
   ```

4. Confirm that both the custom-format `.dump` and adjacent `.sha256` files exist.
   The script validates the archive catalog before publishing either file.
5. Encrypt and transfer the pair through the deployment backup service. Do not
   reuse the local staging directory as retention storage.
6. Export current German and English public Legal snapshots from the healthy API
   as documented in `tools/legal/README.md`. Store them with the matching release
   artifact, not inside the database backup.
7. Record success or failure in the protected operational change record without
   recording secrets or Legal content.

## Incident Response

1. Declare the incident and freeze Legal publication, registration, checkout,
   reacceptance, and other mutations that require current Legal versions.
2. Keep public Legal pages on the validated last-known-good snapshot. Confirm the
   UI visibly identifies archived or stale content.
3. Select the newest approved backup that predates the corruption or loss. Verify
   storage metadata and decrypt it only on an isolated recovery host.
4. Provision a new empty PostgreSQL database with a supported server version.
   Do not restore over the affected database.
5. Keep WebApi and workers disconnected from the recovery target until every
   validation below succeeds.

## Restore

1. Place the `.dump` and its original `.sha256` file in a protected local
   directory and mount the new target connection secret.
2. Execute the explicit restore:

   ```bash
   PXA_DATABASE_URL_FILE=/run/secrets/pxa_recovery_database_url \
   PGPASSFILE=/run/secrets/pxa_recovery_pgpass \
   PXA_RESTORE_CONFIRM='RESTORE PXA DATABASE' \
     tools/legal/restore-postgres.sh /protected/pxa-backups/pxa-UTC.dump
   ```

3. The script verifies SHA-256, validates the archive catalog, confirms that the
   target has no user tables, and restores with owner and privilege metadata
   removed. Any failure stops the procedure.
4. Run the domain verifier before starting PXA:

   ```bash
   PXA_DATABASE_URL_FILE=/run/secrets/pxa_recovery_database_url \
   PGPASSFILE=/run/secrets/pxa_recovery_pgpass \
     tools/legal/verify-legal-recovery.sh
   ```

5. Run `dotnet ef database update` only when the restored application version
   requires a newer migration. Never migrate backward. Repeat domain verification
   after every forward migration.

## Verification And Return To Service

- Confirm EF migration history and all four Legal governance relations exist.
- Confirm every Legal content hash is a lowercase SHA-256 value.
- Confirm acceptance hashes equal the immutable version hashes they reference.
- Confirm no Legal version is orphaned and current Terms and Privacy versions are
  effective and not retired.
- Start one isolated WebApi instance against the recovery target. Verify readiness,
  the anonymous Legal snapshot endpoint, and registration-policy availability.
- Generate fresh deployment snapshots from the recovered API and validate Company
  live, fallback, stale, and corrupt-snapshot behavior.
- Compare approved row-count and integrity summaries with the incident record.
  Do not export row contents for comparison.
- Reconnect workers and object storage only after consistency checks pass.
- Switch traffic through the normal deployment mechanism, monitor errors and Legal
  endpoints, then retire the affected database according to incident policy.

If current Terms or Privacy cannot be verified, keep registration and checkout
disabled. Never bypass the fail-closed policy with configured fallback versions in
production.

## Rollback And Evidence

- If validation or migration fails, disconnect the candidate database and create a
  new empty target for the next attempt. Do not repeatedly mutate the failed target.
- Preserve the original backup, checksum, tool output, incident timestamps,
  selected recovery point, approvers, and pass/fail summaries according to the
  approved incident-retention policy.
- Record the traffic switch and any privileged corrective action in the protected
  operational audit. Do not manufacture application audit events for offline SQL.
- Complete a post-incident review covering root cause, actual RPO/RTO, missing
  telemetry, snapshot age, customer communication, and prevention actions.

## Recovery Drill

Run at least on the deployment-approved cadence and after changing PostgreSQL
versions, migrations, backup tooling, encryption, storage, or Legal persistence:

```bash
tools/legal/run-backup-restore-smoke-test.sh
```

The drill creates isolated source and target PostgreSQL containers, seeds only
synthetic Legal records, creates and verifies a backup, restores into the empty
target, validates Legal relationships and hashes, and removes both containers.
It must never be pointed at a production database.
