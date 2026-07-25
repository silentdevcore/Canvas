# PXA Designer Template Persistence Checklist

## Goal

Replace volatile Designer template storage with PostgreSQL-backed, organization-owned drafts and immutable versions. The Designer must autosave safely, enforce tenant isolation, detect concurrent edits, and preserve an auditable version history.

## Priorities

- [ ] P0: Replace `InMemoryTemplateRepository` with tenant-aware PostgreSQL persistence.
- [ ] P0: Persist mutable drafts with optimistic concurrency and autosave.
- [ ] P0: Protect every template query and mutation by active organization.
- [ ] P1: Add immutable named versions, publication, archive, and restore.
- [ ] P1: Add scalable previews and asset storage through an object-storage abstraction.
- [ ] P2: Add collaboration, branching, review, and advanced retention.

## Dependencies

- [ ] Use `PXA.Designer-Authentication.md` for user, session, active organization, and entitlement enforcement.
- [ ] Use PostgreSQL and EF Core through `PXA.Infrastructure.Persistence`.
- [ ] Align schema and migration operations with `PXA.Database.md`.
- [ ] Keep gallery examples as static product assets rather than customer database records.
- [ ] Define object-storage integration before storing large previews, imports, or attachments.

## Ownership Model

- [ ] Make every saved template belong to exactly one organization.
- [ ] Use the personal organization created for an Individual Developer.
- [ ] Resolve organization ownership only from authenticated server context.
- [ ] Store creator and last-updater user references.
- [ ] Permit access through explicit organization permissions and Designer entitlements.
- [ ] Prevent ownership transfer through normal create or update payloads.
- [ ] Define controlled organization-transfer behavior as deferred administrative work.

## Database Model

- [ ] Add a `designer_templates` table for current draft metadata and current draft JSON.
- [ ] Add a `designer_template_versions` table for immutable snapshots.
- [ ] Use opaque UUID primary keys.
- [ ] Store organization ID, creator ID, updater ID, name, description, tags, status, revision, checksum, timestamps, published-version reference, and soft-deletion state.
- [ ] Store the complete current design document in PostgreSQL `jsonb`.
- [ ] Store each immutable version design document in PostgreSQL `jsonb`.
- [ ] Use a monotonic numeric revision for draft concurrency.
- [ ] Use a per-template monotonic version number and optional version label.
- [ ] Store schema version and Designer application version with every draft and version.
- [ ] Add foreign keys to organizations and users while preserving required audit identity after user deactivation.
- [ ] Add tenant-scoped indexes for name search, status, update time, tags, and soft deletion.
- [ ] Add tenant-scoped uniqueness for version numbers.
- [ ] Add a concurrency token to administrator-editable metadata.
- [ ] Add an EF Core migration and update the checked-in model snapshot.

## Draft And Version Rules

- [ ] Keep one mutable current draft per template.
- [ ] Increment the draft revision after each successful save.
- [ ] Treat named and published versions as immutable.
- [ ] Create versions only through explicit Create version or Publish actions.
- [ ] Calculate a stable checksum for every persisted design snapshot.
- [ ] Avoid creating a new version when the content checksum has not changed.
- [ ] Allow a published version to be selected without mutating its stored JSON.
- [ ] Keep publication state separate from draft state.
- [ ] Archive through soft deletion and preserve versions.
- [ ] Restore archived templates only within the owning organization.
- [ ] Define permanent deletion and retention as a privileged deferred operation.

## Repository And Application Boundary

- [ ] Replace `InMemoryTemplateRepository` with a PostgreSQL implementation.
- [ ] Make repository operations tenant-aware and cancellation-aware.
- [ ] Remove the in-memory sample invoice.
- [ ] Stop using client-supplied `CreatedBy`, `UpdatedBy`, or organization values.
- [ ] Populate actor and organization metadata from authenticated services.
- [ ] Return not found for inaccessible cross-tenant template identifiers.
- [ ] Map persistence records to domain and API contracts without exposing EF entities.
- [ ] Return standard Problem Details instead of internal exception messages.
- [ ] Add audit events for create, update, version, publish, archive, restore, and rejected conflicts.

## API Contract

- [ ] Add tenant-scoped routes under `/api/pxa/v1/designer/templates`.
- [ ] Add paginated list with search, tags, status, updated-time ordering, and archived filtering.
- [ ] Add create, read, update metadata, update draft, archive, and restore operations.
- [ ] Add version list, version create, version read, and publish operations.
- [ ] Return an ETag or equivalent revision token with draft reads.
- [ ] Require `If-Match` or the equivalent revision token for draft updates.
- [ ] Return HTTP 409 with current revision metadata for stale updates.
- [ ] Return HTTP 413 for design documents above the configured size limit.
- [ ] Set the default uncompressed design-JSON limit to 10 MiB.
- [ ] Apply bounded pagination and request cancellation.
- [ ] Keep compatibility aliases for existing template routes only while current callers migrate.
- [ ] Protect rendering by template ID with the same tenant and entitlement checks as template reads.

## Designer Autosave

- [ ] Load the current draft and revision when a saved template opens.
- [ ] Autosave after two seconds without document changes.
- [ ] Allow only one save request in flight per open template.
- [ ] Coalesce newer edits while a save is in flight.
- [ ] Retry transient failures with bounded exponential backoff.
- [ ] Do not retry authorization, validation, conflict, or payload-size failures automatically.
- [ ] Display idle, changed, saving, saved, retrying, conflict, offline, and failed states.
- [ ] Keep unsaved changes in memory when the API is temporarily unavailable.
- [ ] Warn before navigation, reload, or close while unsaved changes remain.
- [ ] Do not add persistent offline editing in P0.
- [ ] Stop autosave after access, organization, or entitlement changes.
- [ ] Clear tenant-specific cached lists when the active organization changes.

## Conflict Handling

- [ ] Detect stale revisions server-side in one transaction.
- [ ] Return current revision, updater, and update timestamp without leaking document content.
- [ ] Offer Reload server version, Save as new template, and Download local JSON actions.
- [ ] Do not silently overwrite a newer server draft.
- [ ] Keep automatic field-level or element-level merge as deferred collaboration work.

## Asset Boundary

- [ ] Keep large previews, source documents, images, and attachments outside PostgreSQL.
- [ ] Store only tenant-safe object keys, content type, size, checksum, timestamps, and lifecycle state in PostgreSQL.
- [ ] Use Cloud object storage or customer-configured filesystem/S3-compatible storage through one abstraction.
- [ ] Validate object ownership on every access.
- [ ] Define orphan cleanup and database/object-store reconciliation before enabling assets.

## Tests

- [ ] Unit-test draft, version, publication, archive, and checksum rules.
- [ ] Test EF mappings, constraints, indexes, and migration snapshot.
- [ ] Apply migrations to an empty PostgreSQL database.
- [ ] Test create, read, update, list, search, archive, restore, version, and publish operations.
- [ ] Test optimistic concurrency and simultaneous draft updates.
- [ ] Test no-op saves and checksum equality.
- [ ] Test 10 MiB request limits and malformed JSON.
- [ ] Test server-derived ownership and ignored client tenant identifiers.
- [ ] Test cross-tenant read, list, update, render, version, and archive attempts.
- [ ] Test removed memberships, revoked sessions, and expired entitlements.
- [ ] Test pagination, cancellation, and stable ordering.
- [ ] Test autosave debounce, request coalescing, transient retry, and unload warning.
- [ ] Test conflict recovery without silent data loss.
- [ ] Run Designer template-library and editor end-to-end smoke tests.

## Acceptance Criteria

- [ ] Restarting the API does not lose saved templates.
- [ ] Every template and version belongs to one organization.
- [ ] Individual Developer templates use the user's personal organization workspace.
- [ ] Cross-tenant identifiers never expose template existence or content.
- [ ] Autosave preserves changes without generating immutable versions.
- [ ] Concurrent edits cannot silently overwrite newer drafts.
- [ ] Explicit versions remain immutable and auditable.
- [ ] Large binary assets are not stored in PostgreSQL template JSON.

## Deferred Work

- [ ] Add persistent offline editing.
- [ ] Add real-time multi-user collaboration and presence.
- [ ] Add branches, reviews, approvals, and merge operations.
- [ ] Add privileged ownership transfer and permanent deletion.
