# PXA Designer Template Persistence Checklist

## Goal

Replace volatile Designer template storage with PostgreSQL-backed, organization-owned drafts and immutable versions. The Designer must autosave safely, enforce tenant isolation, detect concurrent edits, and preserve an auditable version history.

## Priorities

- [x] P0: Replace `InMemoryTemplateRepository` with tenant-aware PostgreSQL persistence.
- [x] P0: Persist mutable drafts with optimistic concurrency and autosave.
- [x] P0: Protect every template query and mutation by active organization.
- [x] P1: Add immutable named versions, publication, archive, and restore.
- [ ] P1: Add scalable previews and asset storage through an object-storage abstraction.
- [ ] P2: Add collaboration, branching, review, and advanced retention.

## Dependencies

- [x] Use `PXA.Designer-Authentication.md` for user, session, active organization, and entitlement enforcement.
- [x] Use PostgreSQL and EF Core through `PXA.Infrastructure.Persistence`.
- [x] Align schema and migration operations with `PXA.Database.md`.
- [x] Keep gallery examples as static product assets rather than customer database records.
- [ ] Define object-storage integration before storing large previews, imports, or attachments.

## Ownership Model

- [x] Make every saved template belong to exactly one organization.
- [x] Use the personal organization created for an Individual Developer.
- [x] Resolve organization ownership only from authenticated server context.
- [x] Store creator and last-updater user references.
- [x] Permit access through explicit organization permissions and Designer entitlements.
- [x] Prevent ownership transfer through normal create or update payloads.
- [ ] Define controlled organization-transfer behavior as deferred administrative work.

## Database Model

- [x] Add a `designer_templates` table for current draft metadata and current draft JSON.
- [x] Add a `designer_template_versions` table for immutable snapshots.
- [x] Use opaque UUID primary keys.
- [x] Store organization ID, creator ID, updater ID, name, description, tags, status, revision, checksum, timestamps, published-version reference, and soft-deletion state.
- [x] Store the complete current design document in PostgreSQL `jsonb`.
- [x] Store each immutable version design document in PostgreSQL `jsonb`.
- [x] Use a monotonic numeric revision for draft concurrency.
- [x] Use a per-template monotonic version number and optional version label.
- [x] Store schema version and Designer application version with every draft and version.
- [x] Add foreign keys to organizations and users while preserving required audit identity after user deactivation.
- [x] Add tenant-scoped indexes for name search, status, update time, tags, and soft deletion.
- [x] Add tenant-scoped uniqueness for version numbers.
- [x] Add a concurrency token to administrator-editable metadata.
- [x] Add an EF Core migration and update the checked-in model snapshot.

## Draft And Version Rules

- [x] Keep one mutable current draft per template.
- [x] Increment the draft revision after each successful save.
- [x] Treat named and published versions as immutable.
- [x] Create versions only through explicit Create version or Publish actions.
- [x] Calculate a stable checksum for every persisted design snapshot.
- [x] Avoid creating a new version when the content checksum has not changed.
- [x] Allow a published version to be selected without mutating its stored JSON.
- [x] Keep publication state separate from draft state.
- [x] Archive through soft deletion and preserve versions.
- [x] Restore archived templates only within the owning organization.
- [ ] Define permanent deletion and retention as a privileged deferred operation.

## Repository And Application Boundary

- [x] Replace `InMemoryTemplateRepository` with a PostgreSQL implementation.
- [x] Make repository operations tenant-aware and cancellation-aware.
- [x] Remove the in-memory sample invoice.
- [x] Stop using client-supplied `CreatedBy`, `UpdatedBy`, or organization values.
- [x] Populate actor and organization metadata from authenticated services.
- [x] Return not found for inaccessible cross-tenant template identifiers.
- [x] Map persistence records to domain and API contracts without exposing EF entities.
- [x] Return standard Problem Details instead of internal exception messages.
- [x] Add audit events for create, update, version, publish, archive, restore, and rejected conflicts.

## API Contract

- [x] Add tenant-scoped routes under `/api/pxa/v1/designer/templates`.
- [x] Add paginated list with search, tags, status, updated-time ordering, and archived filtering.
- [x] Add create, read, update metadata, update draft, archive, and restore operations.
- [x] Add version list, version create, version read, and publish operations.
- [x] Return an ETag or equivalent revision token with draft reads.
- [x] Require `If-Match` or the equivalent revision token for draft updates.
- [x] Return HTTP 409 with current revision metadata for stale updates.
- [x] Return HTTP 413 for design documents above the configured size limit.
- [x] Set the default uncompressed design-JSON limit to 10 MiB.
- [x] Apply bounded pagination and request cancellation.
- [x] Keep compatibility aliases for existing template routes only while current callers migrate.
- [x] Protect rendering by template ID with the same tenant and entitlement checks as template reads.

## Designer Autosave

- [x] Load the current draft and revision when a saved template opens.
- [x] Autosave after two seconds without document changes.
- [x] Allow only one save request in flight per open template.
- [x] Coalesce newer edits while a save is in flight.
- [x] Retry transient failures with bounded exponential backoff.
- [x] Do not retry authorization, validation, conflict, or payload-size failures automatically.
- [x] Display idle, changed, saving, saved, retrying, conflict, offline, and failed states.
- [x] Keep unsaved changes in memory when the API is temporarily unavailable.
- [x] Warn before navigation, reload, or close while unsaved changes remain.
- [x] Do not add persistent offline editing in P0.
- [x] Stop autosave after access, organization, or entitlement changes.
- [x] Clear tenant-specific cached lists when the active organization changes.

## Conflict Handling

- [x] Detect stale revisions server-side in one transaction.
- [x] Return current revision, updater, and update timestamp without leaking document content.
- [x] Offer Reload server version, Save as new template, and Download local JSON actions.
- [x] Do not silently overwrite a newer server draft.
- [ ] Keep automatic field-level or element-level merge as deferred collaboration work.

## Asset Boundary

- [ ] Keep large previews, source documents, images, and attachments outside PostgreSQL.
- [ ] Store only tenant-safe object keys, content type, size, checksum, timestamps, and lifecycle state in PostgreSQL.
- [ ] Use Cloud object storage or customer-configured filesystem/S3-compatible storage through one abstraction.
- [ ] Validate object ownership on every access.
- [ ] Define orphan cleanup and database/object-store reconciliation before enabling assets.

## Tests

- [x] Unit-test draft, immutable version, publication, archive, restore, no-op, and checksum rules.
- [x] Test EF mappings, constraints, indexes, and migration snapshot.
- [x] Apply migrations to an empty PostgreSQL database.
- [x] Test create, read, update, list, search, archive, restore, version, and publish operations.
- [x] Test optimistic concurrency and simultaneous draft updates.
- [x] Test no-op saves and checksum equality.
- [x] Test the exact 10 MiB boundary, one-byte overflow, missing/null documents, and malformed HTTP JSON.
- [x] Test server-derived ownership and ignored client tenant identifiers.
- [x] Test cross-tenant read, list, update, render, version, and archive attempts.
- [x] Test removed memberships, revoked sessions, and expired entitlements against the real template endpoint.
- [x] Test pagination, cancellation, and stable ordering, including equal-timestamp page boundaries.
- [x] Test autosave debounce, request coalescing, transient retry, document switching, authorization loss, and unload warning.
- [x] Test conflict recovery without silent data loss.
- [x] Run Designer template-library and editor end-to-end smoke tests.

## Acceptance Criteria

- [x] Restarting the API does not lose saved templates.
- [x] Every template and version belongs to one organization.
- [x] Individual Developer templates use the user's personal organization workspace.
- [x] Cross-tenant identifiers never expose template existence or content.
- [x] Autosave preserves changes without generating immutable versions.
- [x] Concurrent edits cannot silently overwrite newer drafts.
- [x] Explicit versions remain immutable and auditable.
- [ ] Large binary assets are not stored in PostgreSQL template JSON.

## Deferred Work

- [ ] Add persistent offline editing.
- [ ] Add real-time multi-user collaboration and presence.
- [ ] Add branches, reviews, approvals, and merge operations.
- [ ] Add privileged ownership transfer and permanent deletion.
