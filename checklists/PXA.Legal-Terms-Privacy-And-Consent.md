# PXA Legal Terms, Privacy, and Consent

## Goal

Deliver a central, PostgreSQL-backed legal-document system for PXA. Published
documents are immutable, versioned, auditable, and linked to the exact terms
accepted or privacy notice acknowledged by a user.

> Legal review gate: the implementation provides technical controls and evidence.
> Final German wording, retention periods, company details, consumer checkout,
> and production publication require approval by qualified German legal counsel.

## Priorities

- [x] P0: Define legal document types, lifecycle states, audiences, languages, and immutable published versions.
- [x] P0: Persist legal documents, versions, publication approvals, and acceptance evidence in PostgreSQL.
- [x] P0: Provide public current/version APIs and protected Admin authoring APIs.
- [x] P0: Enforce a four-eyes publication rule.
- [x] P0: Add Company legal routes and a necessary-storage notice.
- [x] P0: Add a protected Legal workspace to PXA.Admin.
- [ ] P0: Replace all draft legal copy with counsel-approved German text.
- [ ] P0: Add the real operator name, legal form, address, representatives, register, VAT ID, and contact details.
- [ ] P0: Approve category-specific retention periods and legal holds.
- [ ] P0: Keep paid B2C checkout disabled until pricing, payment, withdrawal, and legal review are complete.

## Legal Documents

- [x] Support Terms and Conditions, Privacy Notice, Cookie and Storage Policy, Imprint, Consumer Withdrawal Information, Data Processing Agreement, and License Agreement.
- [x] Reserve Subprocessor List and Service Level Agreement document types.
- [x] Treat German as the authoritative language and English as a marked convenience translation.
- [x] Support Individual Developer, Company, Consumer, Business, Cloud, and On-Premise audiences.
- [x] Store source Markdown, safe rendered HTML, SHA-256 content hash, version, locale, status, change summary, effective date, and publication actors.
- [x] Keep published, scheduled, and retired document content immutable.
- [x] Require corrections to create a new version.
- [x] Complete a machine-validated technical processing inventory for logs, identity, billing, documents, workers, mail, telemetry, browser storage, providers, regions, transfers, and retention; keep production approval blocked until operator, provider, transfer, and retention decisions receive legal approval.
- [x] Load the machine-readable retention catalog at runtime and fail Production startup while the inventory or any category lacks legal approval.
- [x] Provide a protected System Administrator retention status and non-destructive dry run covering every processing category.
- [x] Persist auditable global and organization-scoped legal holds and enforce them in active job and mail cleanup services.
- [x] Expose Legal Hold creation and release without exposing a manual destructive cleanup operation in PXA.Admin.
- [ ] Draft separate B2B and B2C terms instead of using blanket liability or warranty exclusions.
- [ ] Publish withdrawal instructions and the model withdrawal form before consumer sales.
- [ ] Publish the DPA, technical and organizational measures, and approved subprocessor list before production customer document processing.

## Public Legal API

- [x] Add `GET /api/pxa/v1/legal/documents`.
- [x] Add `GET /api/pxa/v1/legal/documents/{type}/current`.
- [x] Add `GET /api/pxa/v1/legal/documents/{type}/versions/{version}`.
- [x] Add `GET /api/pxa/v1/legal/storage-policy`.
- [x] Return ETags derived from the immutable content hash.
- [x] Resolve scheduled versions by effective time without mutating historical content.
- [x] Return only public published or effective scheduled versions.
- [x] Make production registration fail closed when current Terms and Privacy versions cannot be verified.
- [x] Generate a deployment-time last-known-good static snapshot.
- [ ] Make checkout fail closed when required active versions cannot be verified.

## Admin Legal Workflow

- [x] Add `legal.read`, `legal.author`, and `legal.approve` permissions.
- [x] Add a protected Legal navigation item and workspace to PXA.Admin.
- [x] List documents and all versions with status, locale, audience, and effective date.
- [x] Create documents and new draft versions.
- [x] Preview safe rendered content and content hashes.
- [x] Submit drafts for review.
- [x] Record approval or rejection decisions.
- [x] Prevent the version author from approving or publishing the same version.
- [x] Publish immediately or schedule publication.
- [x] Retire published versions without modifying their content.
- [x] Audit privileged legal mutations without storing document bodies in audit events.
- [x] Add an explicit side-by-side version diff.
- [x] Require successor review and publication to reference the recorded predecessor comparison.
- [x] Add acceptance statistics and reacceptance progress dashboards.
- [x] Add export of minimized acceptance evidence.
- [x] Audit every minimized evidence export with its format, filters, and row count without storing identity data or document content.

## Registration And Evidence

- [x] Keep Terms acceptance, Privacy acknowledgement, and marketing consent as separate concepts.
- [x] Extend legal acceptance evidence with document-version ID, content hash, locale, organization, and source.
- [x] Keep existing string version fields as compatibility caches.
- [x] Update Account registration to fetch the current Terms and Privacy versions and submit their immutable IDs.
- [x] Reject stale or mismatched registration document versions atomically.
- [x] Record exact Terms acceptance and Privacy acknowledgement evidence during registration.
- [x] Keep an explicit non-production compatibility fallback until approved documents are published.
- [x] Require renewed Terms acceptance only for versions explicitly marked as requiring reacceptance.
- [x] Treat Privacy updates as acknowledgement unless a separate consent-based purpose applies.
- [x] Gate authenticated Account and Designer return flows until current legal obligations are completed.
- [x] Reject stale Account acknowledgements with stable `PXAAPI017` diagnostics.
- [x] Make exact-version evidence idempotent under concurrent Account submissions with row serialization and database uniqueness.
- [ ] Define pseudonymized evidence retention and account-deletion behavior with legal counsel.
- [ ] Avoid storing full IP addresses unless a documented necessity and retention rule are approved.

## Necessary Storage Notice

- [x] Add a shared, accessible storage notice to PXA.Company, PXA.Documentation, PXA.Demo, and public PXA.Account pages.
- [x] Explain that only necessary cookies and browser storage are used at launch.
- [x] Provide `Understood` and `Learn more` actions without a misleading consent choice.
- [x] Store anonymous acknowledgement only in a first-party browser cookie.
- [x] Add permanent Cookie and Storage Policy and settings links to the shared footer.
- [x] Inventory the existing Company cosmetic sign-in marker and remove it from persistent local storage.
- [x] Complete the technical cookie, local-storage, and session-storage inventory with owner, purpose, data category, lifetime, source files, and CI enforcement; final legal approval remains a production launch gate.
- [ ] Introduce Accept all, Reject all, and Customize only if optional analytics or marketing technology is added.
- [ ] Block every optional script until consent and support equally easy withdrawal.
- [ ] Persist versioned optional consent evidence separately from Terms and Privacy events.

## Consumer Checkout

- [ ] Show highlighted pre-contract information immediately before a paid consumer order.
- [ ] Use an unambiguous payment-obligation button.
- [ ] Show total price, tax, term, renewal, cancellation, and payment method.
- [ ] Capture the legally required request for early digital-service performance separately.
- [ ] Send a durable contract confirmation.
- [ ] Implement withdrawal, refund, update, material-change, and termination workflows.
- [ ] Add the current consumer-dispute-resolution statement without an obsolete EU ODR link.

## Security And Reliability

- [x] Server-side authorization protects all Admin Legal operations.
- [x] Public APIs never expose drafts, approvals, user evidence, or internal comments.
- [x] Legal acceptance evidence and audit events are append-only.
- [x] Content hashes are computed server-side from normalized content.
- [x] Rendered legal HTML is generated by a restrictive renderer that never passes through raw HTML or executable links.
- [x] Keep published legal pages readable from a validated static snapshot during Legal API outages.
- [x] Mark snapshot content and age visibly without allowing it to authorize registration or checkout.
- [x] Add a legal-document backup, restore, and disaster-recovery runbook with checksum validation, empty-target restore, domain verification, snapshot regeneration, and an isolated recovery drill.
- [x] Protect separately deployed restricted operator legal guidance behind the operator gateway, System Administrator role, explicit production allowlist, no-store delivery, fixed runbook registration, and audited reads.
- [x] Require an explicit, auditable Legal Hold release reason before an approved cleanup can resume for the affected scope.

## Tests

- [x] Add unit coverage for hashing, safe rendering, and effective-version selection.
- [x] Add API coverage for anonymous reads, draft exclusion, lifecycle transitions, four-eyes enforcement, and immutable publication.
- [x] Add Legal Admin comparison coverage for line alignment, cross-document rejection, permissions, and review/publication gates.
- [x] Add model tests for relationships, indexes, lengths, and delete behavior.
- [x] Build PXA.WebApi, PXA.Admin, PXA.Company, PXA.Documentation, PXA.Demo, and PXA.Account.
- [x] Add PostgreSQL integration coverage for stale registration versions and exact acceptance evidence.
- [x] Add Account reacceptance, Admin progress, minimized export, and exact-version mismatch coverage.
- [x] Add Account and Admin accessibility contract tests for legal review, progress, and export states.
- [x] Add Playwright browser accessibility and keyboard tests for the Storage Notice, Account legal review, and Admin Legal workflow on desktop and mobile.
- [x] Add automated outage, stale-snapshot, invalid-snapshot, and API-recovery coverage.
- [x] Run a development safety scan confirming no tracked secrets, customer data, obsolete ODR links, or legacy Canvas branding in the Legal implementation; remaining draft copy and operator details are explicit launch blockers.
- [x] Add deployed-environment smoke tests for PostgreSQL migration, published snapshot generation, fail-closed registration, last-known-good fallback, stale snapshots, corrupt snapshots, and Legal API outages.
- [ ] Repeat the legal-content scan after counsel-approved copy and operator details are installed and before production launch.

## Acceptance Criteria

- [x] Legal content has one database-backed version source with immutable publication history.
- [x] Public consumers can retrieve only the current effective legal versions.
- [x] One administrator cannot author and approve the same version.
- [x] PXA.Admin exposes a protected Legal workflow without linking Admin from PXA.Company.
- [x] Necessary storage is explained without pretending that consent is required.
- [ ] Counsel-approved German documents and complete operator data replace all launch-blocking placeholders.
- [ ] Paid B2C checkout remains unavailable until every consumer-law task above is approved and tested.
