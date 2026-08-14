# PXA Legal Terms, Privacy, and Consent

## Goal

Deliver a central, PostgreSQL-backed legal-document system for PXA. Published
documents are immutable, versioned, auditable, and linked to the exact terms
accepted or privacy notice acknowledged by a user.

> Legal review gate: the implementation provides technical controls and evidence.
> Final English wording, retention periods, company details, consumer checkout,
> and production publication require approval by qualified Swiss legal counsel
> experienced in international software and privacy law.

The researched market patterns, source links, PXA recommendations, and remaining
owners for these blockers are tracked in
`PXA.Legal-Benchmark-And-Decision-Register.md`.
Worldwide sales, regional checkout gates, tax ownership, currencies, and the
researched pricing proposal are tracked in `PXA.Global-Commerce-And-Pricing.md`.
The six launch decisions, recommended defaults, accountable owners, and open
approval fields are consolidated in `PXA.Commercial-Launch-Decision-Packet.md`.

## Scope Boundary And Related Compliance

- [x] Keep this workflow limited to legal documents, notices, acknowledgements, and consents governing the relationship between PXA and its customers or users.
- [x] Keep third-party software license decisions in the dependency-compliance workflow rather than publishing them as customer Legal documents.
- [x] Never store an NPOI EULA approval or another supplier-license decision in `legal_acceptance_events`; those events represent customer or user actions only.
- [x] Share qualified Legal ownership and review standards across customer Legal content and third-party license compliance without combining their records, permissions, or approval states.
- [x] Reference `PXA.Dependency-Security-And-Compliance.md` and `PXA.NPOI-License-Decision.md` as separate production-gate records.
- [x] Define a protected Admin navigation relationship between Legal documents and Dependency Compliance while retaining separate APIs, data models, permissions, and audit event types.

## Priorities

- [x] P0: Define legal document types, lifecycle states, audiences, languages, and immutable published versions.
- [x] P0: Persist legal documents, versions, publication approvals, and acceptance evidence in PostgreSQL.
- [x] P0: Provide public current/version APIs and protected Admin authoring APIs.
- [x] P0: Enforce a four-eyes publication rule.
- [x] P0: Add Company legal routes and a necessary-storage notice.
- [x] P0: Add a protected Legal workspace to PXA.Admin.
- [x] P0: Prepare complete English Swiss-law candidate text for all seven initial Legal documents.
- [ ] P0: Replace candidate text with counsel-approved English text before production publication.
- [ ] P0: Add the real operator name, legal form, address, representatives, register, VAT ID, and contact details.
- [ ] P0: Approve category-specific retention periods and legal holds.
- [x] P0: Keep paid B2C checkout technically disabled until pricing, payment, withdrawal, and legal review are complete.

Operator identity is intentionally blocked until verified Swiss company records
are available. The missing values and resume trigger are recorded in
`PXA.Commercial-Launch-Decision-Packet.md`; placeholders must remain visible and
must continue to block Production publication.

## Legal Documents

- [x] Support Terms and Conditions, Privacy Notice, Cookie and Storage Policy, Imprint, Consumer Withdrawal Information, Data Processing Agreement, and License Agreement.
- [x] Reserve Subprocessor List and Service Level Agreement document types.
- [x] Treat English as the sole authoritative language; add localized notices only where a target market requires them.
- [x] Use Switzerland as the governing-law baseline while preserving mandatory consumer, privacy, and forum rights in each approved sales market.
- [x] Support Individual Developer, Company, Consumer, Business, Cloud, and On-Premise audiences.
- [x] Store source Markdown, safe rendered HTML, SHA-256 content hash, version, locale, status, change summary, effective date, and publication actors.
- [x] Keep published, scheduled, and retired document content immutable.
- [x] Require corrections to create a new version.
- [x] Complete a machine-validated technical processing inventory for logs, identity, billing, documents, workers, mail, telemetry, browser storage, providers, regions, transfers, and retention; keep production approval blocked until operator, provider, transfer, and retention decisions receive legal approval.
- [x] Load the machine-readable retention catalog at runtime and fail Production startup while the inventory or any category lacks legal approval.
- [x] Provide a protected System Administrator retention status and non-destructive dry run covering every processing category.
- [x] Persist auditable global and organization-scoped legal holds and enforce them in active job and mail cleanup services.
- [x] Default queued document operations to transient retention, purge content after successful download or within 24 hours, require explicit seven-day retention, scrub payloads with content, and expire minimized terminal metadata after 30 days while honoring Legal Holds.
- [x] Expose Legal Hold creation and release without exposing a manual destructive cleanup operation in PXA.Admin.
- [x] Draft B2B and B2C distinctions without blanket liability or warranty exclusions.
- [x] Prepare withdrawal instructions and a model withdrawal form that explain the Swiss baseline and preserve mandatory foreign consumer rights.
- [ ] Add any electronic withdrawal function required in an enabled consumer market, including a permanently accessible first action, confirmation action, and immediate durable receipt.
- [x] Prepare a Swiss FADP/GDPR-capable DPA candidate with processing, security, subprocessor, transfer, assistance, incident, audit, and deletion terms.
- [ ] Publish the approved DPA, deployment-specific technical and organizational measures, and subprocessor list before production customer document processing.

## Public Legal API

- [x] Add `GET /api/pxa/v1/legal/documents`.
- [x] Add `GET /api/pxa/v1/legal/documents/{type}/current`.
- [x] Add `GET /api/pxa/v1/legal/documents/{type}/versions` with public metadata for every effective published or retired version.
- [x] Add `GET /api/pxa/v1/legal/documents/{type}/versions/{version}`.
- [x] Add `GET /api/pxa/v1/legal/storage-policy`.
- [x] Return ETags derived from the immutable content hash.
- [x] Resolve scheduled versions by effective time without mutating historical content.
- [x] Return only public published or effective scheduled versions.
- [x] Keep retired publications publicly readable by exact version while excluding drafts, reviews, approvals, and future schedules.
- [x] Make production registration fail closed when current Terms and Privacy versions cannot be verified.
- [x] Generate a deployment-time last-known-good static snapshot.
- [x] Add a shared checkout-readiness gate that fails closed unless Consumer Terms, Privacy, and Withdrawal versions are current and the commercial checkout switch is explicitly enabled.
- [x] Provide a shared fail-closed paid-checkout readiness gate combining catalog, price-book, billing-provider, restriction, country, B2B/B2C, and Consumer Legal readiness.
- [ ] Require every future paid checkout mutation to call `PxaPaidCheckoutReadinessGate.RequireAsync` before payment-provider or order work begins.

## Admin Legal Workflow

- [x] Add `legal.read`, `legal.author`, and `legal.approve` permissions.
- [x] Add a protected Legal navigation item and workspace to PXA.Admin.
- [x] List documents and all versions with status, locale, audience, and effective date.
- [x] Create documents and new draft versions.
- [x] Import the seven repository-managed English Swiss-law candidates as non-published Draft versions without overwriting existing content.
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
- [x] Avoid storing full IP addresses in Legal acceptance evidence; enforce the minimized evidence shape with model and export tests unless a documented necessity and retention rule is approved later.

## Legal Update Notifications

- [x] Create one idempotent global Legal notification when an approved version is published or scheduled.
- [x] Keep scheduled notifications hidden until the document becomes effective.
- [x] Remove a pending notification when its scheduled Legal version is retired before taking effect.
- [x] Keep read notifications available for repeated review until the user explicitly dismisses them.
- [x] Link Designer Legal notifications to the trusted PXA.Account origin rather than treating server-provided URLs as arbitrary external links.
- [x] Add a persistent PXA.Account Legal updates page with current status, public change summary, and current and previous publication links.
- [x] Show the same change summary and predecessor link in the blocking Account Legal review.
- [x] Keep Terms acceptance, Privacy acknowledgement, and marketing consent visibly and technically separate.
- [x] Avoid copying document bodies or internal review comments into notification records.

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

- [ ] Gate consumer checkout independently by billing country and never treat one jurisdiction's approval as worldwide approval.
- [ ] Show highlighted pre-contract information immediately before a paid consumer order.
- [ ] Use an unambiguous payment-obligation button.
- [ ] Show total price, tax, term, renewal, cancellation, and payment method.
- [ ] Capture the legally required request for early digital-service performance separately.
- [ ] Send a durable contract confirmation.
- [ ] Implement withdrawal, refund, update, material-change, and termination workflows.
- [ ] Keep the electronic withdrawal function available throughout the withdrawal period and reference its location in the withdrawal information.
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
- [x] Make the isolated recovery drill wait for the `pxa` database, fail with bounded diagnostics, and test delayed PostgreSQL initialization in CI.
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
- [x] Add desktop and mobile Company coverage for current publications, archived versions, version navigation, snapshots, stale snapshots, and unavailable states.
- [x] Test Legal notification creation, scheduled visibility, pre-effective retirement, persistent read state, trusted Account links, and Account update presentation.
- [x] Add automated outage, stale-snapshot, invalid-snapshot, and API-recovery coverage.
- [x] Add recovery-contract coverage for database-level readiness, timeout diagnostics, and delayed startup.
- [x] Run a development safety scan confirming no tracked secrets, customer data, obsolete ODR links, or legacy Canvas branding in the Legal implementation; remaining draft copy and operator details are explicit launch blockers.
- [x] Benchmark the six remaining Legal launch decisions against primary legal sources and supplier-owned Legal, privacy, retention, subscription, and policy-history patterns.
- [x] Validate that the repository catalog contains exactly seven substantive English authoritative documents governed by the Swiss baseline.
- [x] Add a repeatable Legal launch-readiness validator that reports unresolved decisions in Development and fails closed in Production mode.
- [x] Add deployed-environment smoke tests for PostgreSQL migration, published snapshot generation, fail-closed registration, last-known-good fallback, stale snapshots, corrupt snapshots, and Legal API outages.
- [x] Add fail-closed Consumer checkout-readiness coverage for missing documents, disabled commercial rollout, authenticated API access, and complete effective document sets.
- [x] Add protected Admin contract coverage for the separate Dependency Compliance workspace, cross-navigation, and route documentation.
- [x] Add a model contract preventing network-address fields from entering minimized Legal acceptance evidence.
- [ ] Repeat the legal-content scan after counsel-approved copy and operator details are installed and before production launch.

## Acceptance Criteria

- [x] Legal content has one database-backed version source with immutable publication history.
- [x] Public consumers can retrieve only the current effective legal versions.
- [x] Company Legal pages show the active document, authoritative status, effective date, content hash, change summary, related Legal documents, and immutable public version history.
- [x] One administrator cannot author and approve the same version.
- [x] PXA.Admin exposes a protected Legal workflow without linking Admin from PXA.Company.
- [x] Necessary storage is explained without pretending that consent is required.
- [ ] Counsel-approved English documents and complete Swiss operator data replace all launch-blocking placeholders.
- [x] Paid B2C checkout remains unavailable by default and cannot report readiness without every required Consumer document.
- [ ] Enable paid B2C checkout only after every consumer-law task above is approved, implemented, and tested.
- [x] Customer Legal acceptance cannot approve, clear, or otherwise mutate a third-party dependency-license decision.
