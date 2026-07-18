# PXA Database Checklist

## Goal

Provide one secure, tenant-aware, and operationally reliable persistence platform for PXA identity, administration, subscriptions, licensing, usage, audit, mail, templates, and document metadata across Cloud and On-Premise deployments.

## Priorities

- [ ] P0: Establish PostgreSQL, EF Core persistence, schema ownership, and migrations.
- [ ] P0: Persist identity, organizations, authorization, subscriptions, licenses, audit, and mail outbox data.
- [ ] P0: Enforce tenant isolation and production security boundaries.
- [ ] P1: Add template metadata, usage aggregation, retention, backup, restore, and operational monitoring.
- [ ] P2: Add scale-out read models, archival, and advanced disaster-recovery options when required.

## Dependencies

- [ ] Align identity and administration entities with `PXA.Admin.md`.
- [ ] Align editions, entitlements, usage, and offline licenses with `PXA.Subscription-Licensing.md`.
- [ ] Align outbox, delivery, consent, and suppression entities with `PXA.Mail-Service.md`.
- [ ] Align container health, volumes, secrets, and startup behavior with `PXA.Api-Docker.md`.
- [x] Confirm a PostgreSQL EF Core provider version compatible with the repository's .NET 10 target before implementation.

## Technology Baseline

- [x] Use PostgreSQL as the primary relational database for Cloud and On-Premise.
- [x] Use EF Core for mappings, transactions, migrations, and application persistence.
- [x] Run PostgreSQL as a dedicated service in the local and On-Premise Docker Compose bundles.
- [ ] Prefer a managed PostgreSQL service for Cloud production.
- [x] Use production-like PostgreSQL containers for integration tests instead of relying on the EF in-memory provider.
- [x] Allow SQLite only for isolated unit tests that do not validate relational behavior.
- [x] Keep database-provider-specific behavior behind the infrastructure boundary.

## Storage Boundaries

- [ ] Store users, organizations, subscriptions, entitlements, audit events, jobs, mail state, and document metadata in PostgreSQL.
- [ ] Store large PDF, DOCX, spreadsheet, image, export, and temporary processing files outside PostgreSQL.
- [ ] Define an object-storage abstraction for Cloud object storage and On-Premise filesystem or S3-compatible storage.
- [ ] Store immutable object keys, content type, size, checksum, tenant, ownership, retention, and lifecycle state in the database.
- [ ] Prevent database transactions from depending on uncommitted object-storage writes without compensation or outbox handling.
- [ ] Define cleanup and reconciliation for orphaned database records and storage objects.

## Schema Ownership

- [ ] Define bounded persistence areas for Identity, Administration, Subscription, Licensing, Usage, Audit, Mail, Templates, Jobs, and Storage Metadata.
- [ ] Use explicit table, column, index, constraint, and foreign-key naming conventions.
- [x] Use stable opaque identifiers and UTC timestamps.
- [ ] Add optimistic concurrency tokens to administrator-editable records.
- [ ] Add creation, update, actor, tenant, and soft-delete metadata where required.
- [ ] Avoid generic key-value storage for domain data that requires validation, filtering, or constraints.
- [ ] Document ownership of every table and prohibit direct cross-boundary writes outside application services.

## Identity And Administration Data

- [x] Persist ASP.NET Core Identity users, credentials, claims, roles, external logins, and security tokens.
- [ ] Persist organizations, memberships, teams, role assignments, and permission grants.
- [ ] Persist invitations, email verification state, active sessions, and session revocation.
- [ ] Persist service accounts, hashed API-key material, scopes, expiry, rotation, and last-used metadata.
- [ ] Preserve identity references needed by immutable audit events after user deactivation or soft deletion.
- [ ] Apply normalized unique constraints for usernames and email addresses according to tenant policy.

## Subscription And Licensing Data

- [ ] Persist account type, edition, billing period, lifecycle state, dates, and organization ownership.
- [ ] Persist edition defaults, negotiated entitlement overrides, seats, assignments, quotas, and current effective grants.
- [ ] Persist subscription state transitions as immutable events.
- [ ] Persist offline-license metadata, public verification data, issuance, replacement, revocation, and expiry without storing private signing keys.
- [ ] Persist idempotent usage events with tenant, product, operation, quantity, request ID, and timestamp.
- [ ] Add aggregation tables or materialized read models only after correctness of raw usage events is established.
- [ ] Protect subscription, license, and usage mutations with transactions and concurrency checks.

## Mail And Outbox Data

- [ ] Persist an application outbox in the same transaction as identity or subscription state changes.
- [ ] Persist mail queue state, template version, recipient reference, delivery attempts, provider message ID, and sanitized failure reason.
- [ ] Persist marketing consent, confirmation, withdrawal, preferences, bounce, complaint, and suppression state separately from transactional mail.
- [ ] Enforce unique idempotency keys for queued messages and provider events.
- [ ] Define retention and deletion rules that preserve required audit evidence without retaining full message bodies unnecessarily.
- [ ] Prevent mail workers from reading or updating another tenant's records.

## Templates, Documents, And Jobs

- [ ] Replace the current in-memory template repository with a persistent implementation.
- [ ] Persist template identity, version, owner, tenant, locale, status, timestamps, and storage reference.
- [ ] Define immutable template versions and controlled publication state transitions.
- [ ] Persist asynchronous job state, progress, cancellation, result reference, diagnostics, and expiry.
- [ ] Keep large source and result files in object storage and store only metadata and references in PostgreSQL.
- [ ] Define cleanup behavior for expired jobs, temporary files, abandoned uploads, and deleted templates.

## Tenant Isolation

- [ ] Require an organization or tenant identifier on all tenant-owned records.
- [ ] Resolve the active tenant from authenticated server context rather than trusting arbitrary request values.
- [ ] Apply tenant filters in repositories and application services.
- [ ] Add database constraints and composite unique indexes that include tenant identity where appropriate.
- [ ] Define the limited set of global System Administrator records explicitly.
- [ ] Prevent cross-tenant joins, exports, background jobs, cache keys, and object-storage references.
- [ ] Add automated cross-tenant isolation tests for every repository and privileged query path.
- [ ] Evaluate PostgreSQL row-level security as an additional defense after application-level isolation is correct.

## Migrations And Initialization

- [x] Store versioned EF Core migrations in source control.
- [ ] Generate migration scripts in CI and review destructive or data-rewriting operations.
- [ ] Separate schema migration from normal API startup in production.
- [ ] Provide an explicit migration command or deployment job for Cloud and On-Premise.
- [ ] Make migrations safe for rolling deployment where supported.
- [ ] Define rollback or forward-fix procedures for failed migrations.
- [ ] Seed only required system roles, permissions, edition definitions, and a controlled initial administrator flow.
- [x] Never seed production with default passwords or demo credentials.
- [ ] Record applied application and schema versions for support diagnostics.

## Security And Privacy

- [ ] Use TLS for database connections outside isolated local development.
- [ ] Store credentials in secret management or Docker secrets, never in source control or images.
- [ ] Use least-privilege database roles for runtime, migration, backup, and administrative access.
- [ ] Encrypt sensitive fields at the application layer when database-level encryption is insufficient.
- [ ] Store passwords, API keys, reset tokens, invitation tokens, and comparable secrets only through approved one-way hashing mechanisms.
- [ ] Avoid storing document content, access tokens, credentials, and private license-signing keys in logs or audit payloads.
- [ ] Define data export, correction, retention, anonymization, and deletion workflows.
- [ ] Audit privileged database-backed changes without allowing audit records to be modified through normal application APIs.

## Reliability And Performance

- [ ] Configure bounded connection pooling, command timeouts, cancellation, and transient-failure handling.
- [ ] Add indexes based on documented Admin, API, worker, audit, and usage query patterns.
- [ ] Require server-side pagination for unbounded lists.
- [ ] Avoid N+1 queries and loading large binary data through EF entities.
- [x] Add database readiness checks without exposing credentials or schema details.
- [ ] Monitor connections, query latency, locks, deadlocks, storage growth, replication lag, and migration state.
- [ ] Define capacity thresholds and alerts before storage or connection exhaustion.
- [ ] Establish a query-performance review process before adding caches or replicas.

## Backup And Disaster Recovery

- [ ] Define automated full and incremental backup schedules for Cloud and On-Premise.
- [ ] Encrypt backups and restrict backup credentials independently from runtime credentials.
- [ ] Define retention, off-site copy, restore-point, and legal-deletion behavior.
- [ ] Document recovery point and recovery time objectives by subscription edition.
- [ ] Test complete restore into an isolated environment on a recurring schedule.
- [ ] Reconcile restored database references with object-storage versions and offline licenses.
- [ ] Document customer-owned backup and restore responsibilities for On-Premise deployments.

## Docker And Local Development

- [ ] Add a version-pinned PostgreSQL service to Docker Compose with a named data volume.
- [ ] Add a health check and make API readiness depend on database availability and schema compatibility.
- [ ] Provide safe local-development credentials through ignored environment files or secret tooling.
- [ ] Provide commands for migration, reset of disposable local data, backup, and restore.
- [ ] Keep destructive development reset commands explicitly scoped and unavailable in production images.
- [ ] Ensure container replacement preserves data while an intentional volume removal remains explicit.

## Tests

Current verification status:

- [x] Verify that the EF Core model matches the checked-in migration snapshot.
- [x] Verify persistence mappings and constraints with fast model tests.
- [x] Provide a Testcontainers test that creates PostgreSQL, applies all migrations, and persists an identity user with an organization membership.
- [x] Execute the Testcontainers PostgreSQL test locally through Rancher Desktop.
- [x] Add tenant-scoped organization-role assignments with relational uniqueness and foreign-key constraints.
- [x] Add organization-owned audit events for privileged user-status and role changes.
- [x] Apply the tenant-role and audit migration to the local PostgreSQL development database.
- [x] Verify user administration tenant isolation and mutation auditing against Testcontainers PostgreSQL.
- [x] Verify organization administration, cross-tenant denial, and switched System Administrator context against Testcontainers PostgreSQL.

- [ ] Unit-test domain and application rules independently from EF Core where appropriate.
- [x] Run repository and migration tests against a real version-pinned PostgreSQL container.
- [ ] Apply every migration from an empty database and from the latest supported previous release.
- [ ] Test constraints, concurrency, transactions, idempotency, pagination, and cancellation.
- [ ] Test tenant isolation for Identity, Admin, Subscription, Licensing, Usage, Mail, Templates, Jobs, and Storage Metadata.
- [ ] Test outbox atomicity and worker recovery after interruption.
- [ ] Test backup and restore with database and object-storage reconciliation.
- [ ] Test expired retention records, soft deletion, anonymization, and hard-deletion workflows.
- [ ] Load-test representative Admin queries, usage ingestion, audit search, and queued job updates.
- [ ] Verify that test fixtures contain no production credentials or customer data.

## Acceptance Criteria

- [ ] Cloud and On-Premise use the same logical PostgreSQL schema and migration history.
- [ ] No production identity, subscription, license, audit, or mail state depends on in-memory storage.
- [ ] Large customer documents are stored outside PostgreSQL with tenant-safe metadata references.
- [ ] Every tenant-owned query and mutation enforces organization isolation.
- [ ] Schema changes are versioned, reviewable, repeatable, and recoverable.
- [ ] Backups can be restored and reconciled through a tested documented procedure.
- [ ] Database outages and incompatible schemas produce safe readiness failures instead of partial application startup.
