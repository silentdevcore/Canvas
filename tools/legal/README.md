# PXA Legal Operations

## English Swiss-law candidate documents

`product-metadata/legal-documents/en/` contains the seven initial authoritative
English Legal candidates for a Swiss PXA operator serving international markets.
They preserve mandatory local rights and use controlled placeholders for operator
details that have not yet been verified.

Validate the catalog and content contract with:

```bash
node tools/legal/validate-legal-content.mjs
```

System Administrators with `legal.author` can import the catalog from PXA.Admin.
Import creates missing documents and Draft versions only. It never overwrites a
different version, submits, approves, schedules, or publishes Legal content. A
second authorized user and qualified Swiss counsel must complete review before
publication.

## Browser storage inventory

`product-metadata/browser-storage.json` is the reviewed technical inventory for every PXA-owned cookie, Local Storage key, and Session Storage key. Each entry records its applications, owner, purpose, data, lifetime, category, and implementation sources. Optional analytics and marketing storage remain disabled.

Run the inventory contract before changing browser storage:

```bash
node tools/legal/validate-browser-storage.mjs
```

The validator rejects unregistered source files and literal keys, legacy Canvas keys, missing implementation sources, and optional storage while the launch policy disables it. CI runs the validator before frontend tests. A new storage access must therefore update the inventory and the public Cookie and Storage Policy in the same change.

## Data processing inventory

`product-metadata/data-processing-inventory.json` maps every current processing activity to its purposes, data subjects, data categories, systems, persisted entities, providers, region model, transfer condition, retention state, and implementation sources. It deliberately keeps `productionApproved` false while the operator identity, Cloud providers and regions, international-transfer safeguards, and category-specific retention periods remain undecided.

Run the contract whenever a database entity, external provider, document-processing path, telemetry export, or retention rule changes:

```bash
node tools/legal/validate-data-processing-inventory.mjs
```

The validator requires coverage of every Legal inventory area and every `PxaDbContext` entity. It also rejects unknown providers, missing source evidence, duplicate entity ownership, and conditional transfers without an explicit review gate.

## Legal launch readiness

`checklists/PXA.Legal-Benchmark-And-Decision-Register.md` records the researched
market patterns and PXA decision proposals for operator identity, document set,
retention, consumer checkout, optional tracking, and final publication review.
It is a decision aid, not approved Legal wording.

Run the development report while preparing Legal content:

```bash
node tools/legal/validate-legal-launch-readiness.mjs
```

Development reports known blockers without preventing local work. Production
validation fails closed until those blockers are resolved:

```bash
node tools/legal/validate-legal-launch-readiness.mjs --production
```

The report checks the verified operator identity, processing and retention
approval, draft Company Legal copy, Imprint placeholders, obsolete EU ODR URLs,
optional browser storage, global commerce approval, Country Readiness, and
explicit Consumer-checkout activation. A passing report supports but never
replaces Legal and company-owner sign-off.

## Retention governance

The WebApi embeds the processing inventory as its runtime retention catalog. Production startup fails
closed while `productionApproved` is false or any processing category has an `approvalStatus` other
than `approved`. Development and Testing start with a warning so incomplete policies remain testable.

System Administrators can inspect the effective catalog and run a non-destructive evaluation under
`/api/pxa/v1/admin/system/retention`. The dry run reports candidates, held records, and allowed actions;
it never changes data. PXA.Admin deliberately has no manual cleanup execution operation.

Legal Holds are persisted in `administration.retention_legal_holds`. They can cover one inventory
category globally or for one organization, and their creation and release are audited. The active job
and transactional-mail retention services fail closed for matching holds. A release requires a
documented reason; deleting or editing hold history is unsupported.

## Published snapshot export

PXA.Company can continue serving the last verified published legal text while
the Legal API is unavailable. Registration and checkout never use this static
copy to authorize a transaction.

Before a production Company deployment, export the effective public documents
from the production API and build the site:

```bash
PXA_LEGAL_API_BASE=https://api.powerdoxautomation.com \
PXA_LEGAL_LOCALE=en \
PXA_LEGAL_AUDIENCE=All \
npm --prefix websites/PXA.Company run build:deployment
```

The exporter validates the schema, content hashes, effective dates, unique
document keys, and non-empty published-document collection. It writes
`public/legal-snapshots/<locale>.json` atomically, and Vite copies that file
into the Company deployment. Generated JSON snapshots are deployment artifacts
and are intentionally ignored by Git.

Generate English snapshots for the authoritative site. Generate localized
snapshots separately only when a target market requires an approved translation.
A failed export must stop the deployment; do not reuse an
unverified file from a build workspace.

For a one-off export, the script also accepts `--api`, `--locale`, `--audience`,
and `--output` arguments through `npm run snapshot:legal -- ...`.

## Deployment smoke test

Run the complete Legal deployment contract from the repository root while a
Docker-compatible runtime is available:

```bash
tools/legal/run-deployment-smoke-tests.sh
```

The smoke test applies the current EF Core migrations to an isolated PostgreSQL
container, seeds synthetic published Terms and Privacy versions, verifies the
public snapshot response, and confirms that registration fails closed after
those versions are retired. It then checks snapshot validation and exercises
the Company Legal page against live, unavailable, stale, and corrupt sources in
desktop and mobile Chromium. CI runs the PostgreSQL and browser portions in
their respective jobs.

## PostgreSQL backup and recovery

Legal governance data is relationally connected to identity, organizations,
subscriptions, and audit evidence. Use the full-database tools instead of
exporting individual Legal tables:

```bash
PXA_DATABASE_URL_FILE=/run/secrets/pxa_database_url \
PGPASSFILE=/run/secrets/pxa_pgpass \
  tools/legal/backup-postgres.sh /protected/pxa-backups

PXA_DATABASE_URL_FILE=/run/secrets/pxa_recovery_database_url \
PGPASSFILE=/run/secrets/pxa_recovery_pgpass \
PXA_RESTORE_CONFIRM='RESTORE PXA DATABASE' \
  tools/legal/restore-postgres.sh /protected/pxa-backups/pxa-UTC.dump

PXA_DATABASE_URL_FILE=/run/secrets/pxa_recovery_database_url \
PGPASSFILE=/run/secrets/pxa_recovery_pgpass \
  tools/legal/verify-legal-recovery.sh
```

Production connection URLs should omit passwords. Mount the password separately
through the standard libpq `PGPASSFILE`; inline credentials are suitable only
for the isolated synthetic drill.

Restore accepts only a checksum-verified custom-format backup and an empty
target database. The detailed incident, encryption, validation, rollback, and
return-to-service procedure is restricted to
`operator-docs/PXA.Legal-Backup-Restore-And-Recovery.md` and must not be
published with public Documentation.

The isolated recovery drill uses synthetic records only:

```bash
tools/legal/run-backup-restore-smoke-test.sh
```

The drill waits for a successful query against the configured `pxa` database,
not only for the PostgreSQL server socket. Readiness is bounded by
`PXA_POSTGRES_READY_ATTEMPTS` and `PXA_POSTGRES_READY_DELAY_SECONDS`; a timeout
prints container state and recent PostgreSQL logs. CI sets
`PXA_POSTGRES_STARTUP_DELAY_SECONDS` to exercise delayed database creation.
