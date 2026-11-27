# Native Library License Issuance

For the selected free launch configuration, read-only configuration preflight,
manufacturer installation order and key backup/recovery preparation, use
[Manufacturer Setup](PVAD.Native-Manufacturer-Setup.md). It leaves production
disabled and records the Cloud/custody decisions still required.

## Scope And Access

This is a restricted manufacturer runbook. Do not include it in public website
navigation or documentation output. Native library licenses are a separate
profile from server offline licenses, account editions, user roles, and seats.
Customer deployments must keep native issuance disabled and receive only public
verification keys plus their own issued license artifacts.

The backend workflow, native Admin workspace and tenant-authorized Account
downloads are implemented. Commercial approval, production key provisioning,
durable revocation management and protected-package acceptance remain separate
outstanding milestones. No production key is generated
by the application or this guide.

## Preconditions

1. Obtain manufacturer approval for products, exact migration provider keys,
   model, validity dates, customer reference, and redistribution rights.
2. Provision the approved public key in the native library embedded trust
   register through a reviewed build. A key supplied by the request is never
   trusted. Existing server-license keys do not configure native trust.
3. Mount the corresponding private P-256 PEM from manufacturer-controlled
   secret storage, outside repository, packages and customer images. On Unix,
   remove all group/other permissions. Never include key contents in logs,
   examples, support attachments, or customer-provided C#.
4. Configure `NativeLicenseIssuer:Enabled`, `KeyId`, `PrivateKeyPath` and
   `AllowedUserIds` only on the manufacturer deployment. The path must be
   absolute; the allowlist contains explicit Identity user GUIDs. Disabled is
   the default. Do not configure these settings on customer servers or workers.
5. The operator must have the actual global System Administrator database role,
   `licenses.manage`, explicit native issuer allowlisting, and any required
   platform operator allowlisting. The user must be active, email verified,
   unlocked, and use a live persistent session within the configured recent
   authentication interval. API keys and service accounts cannot issue licenses.
6. Apply `AddNativeLibraryLicenses` and `AddPersonalNativeLicenseOwnership`
   through the reviewed deployment process. The ledger remains append-only.
   The ownership migration preserves existing bytes/owners, adds personal-user
   ownership and protects renewal ownership. Rollback is refused before DDL if
   personal rows exist. The application does not migrate a live database for you.

## Proposed Initial Production Key Storage (Not Provisioned)

Planning proposal recorded on 2026-11-24 at the owner's request. Manufacturer
display name: PVAD. Website administrators manage license policy; this alone
does not grant host access to private keys. Hosting environment and the specific
key/backup custodian still need confirmation. The owner subsequently confirmed
Cloud hosting, with the provider still open. No production key was generated.

Recommend keeping the manufacturer private signing key in the chosen Cloud's
dedicated secret/key service, separate from the application database. The
database holds issued signed license artifacts, ownership, rights and audit
records; it does not need the private signing key. Key IDs and service references
may be stored as configuration without exposing private material.

The current PEM-based signer can use a secret supplied as a protected runtime
file. Alternatively, a future remote-signing adapter can call a non-exportable
P-256 key in a managed key service. That option requires implementation and
signature-format verification; it is not supported by the current file signer.
Only the manufacturer issuer workload should receive secret-read or signing
permission. Website administrator access must not automatically confer secret
retrieval or export rights. Final provider and storage selection are pending.

For a manufacturer-controlled server or container deployment, the existing
signer supports a P-256 private PEM file at an absolute path:

- Keep the original outside source control, build contexts, images and public
  web directories. Supply it to the manufacturer issuer through a protected
  runtime secret/file mount, read-only to the application.
- On Unix, make the mounted file readable only by the dedicated issuer service
  identity (for example mode `0400`); the signer rejects group/other permissions.
  Establish and verify the equivalent restricted ACL on Windows.
- Keep an encrypted backup separately from the server and keep its recovery
  credential separately from the backup. Record an accountable custodian and
  independent recovery reviewer, then demonstrate recovery without exposing
  private contents or issuing a customer license.
- Maintain public key ID/fingerprint, provisioning approval and rotation records.
  Ship only the reviewed public key in customer libraries; customer hosts and
  workers never receive the private key.
- For automatic free issuance, use a narrowly scoped server-side issuance policy
  after verified customer eligibility. Do not give the customer Admin issuance
  rights or accept arbitrary products, owners or Migration grants from a request.

A managed secret store may supply the mounted file if the hosting platform
supports that arrangement. Remote non-exportable KMS/HSM signing is a separate
integration: the current signer reads a PEM file and is not a KMS/HSM client.
Do not generate production material until storage, access, backup, recovery and
public-register provisioning have been reviewed and explicitly authorized.

## Issue A License

1. Reauthenticate as an authorized manufacturer operator and obtain the normal
   same-origin CSRF token. Never paste cookies or tokens into support reports.
2. Submit `POST /api/pvad/v1/admin/native-licenses` with `requestId`,
   `customerReference`, `model`, `validFrom`, `validUntil`, `updatesUntil`,
   `redistributionAllowed`, `grants`, `reasonCode`, and exactly one owner:
   `organizationId` for a company, or `ownerUserId` with `organizationId: null`
   for a personal license. Personal owners must be active, verified and unlocked;
   the server applies individual feature limits even if they belong to a company.
   An individual-scoped license cannot newly target an organization.
3. Use a new request GUID per intended action. Retry the identical request with
   the same GUID after an uncertain response; changing its contents returns 409.
4. Use UTC dates. Trial lasts exactly 30 days from its signed start; subscription
   has an end date; perpetual has an update cutoff and no end date. The existing
   native verifier remains authoritative for every model constraint.
5. Use canonical product/provider keys from the native product and migration
   catalogs. For example, `migration.pdf.code` uses `aspose-pdf`, not `aspose`
   or `auto`. Reason codes are `purchase`, `evaluation`, `renewal`, `correction`.
6. Expect 201 with immutable issuance metadata. Retrieve that metadata at
   `GET /api/pvad/v1/admin/native-licenses/{licenseId}` and the exact signed
   artifact at `GET /api/pvad/v1/admin/native-licenses/{licenseId}/download`.
   These routes require the same manufacturer access and recent session.
7. Deliver the artifact through an approved private channel or let the customer
   download it from Account > Licenses > Native library licenses. The manufacturer
   endpoints are never exposed as customer issuance endpoints.

The ledger row and successful `native-licenses.issue` audit event commit in one
transaction. Audit details contain model, reason code, artifact hash, renewal
link and personal-owner ID where applicable; not the full artifact, customer
reference, email address or signing material.

## Approved Free Launch Terms (2026-11-26)

The owner selected renewable free native licenses valid for **three calendar
months**, with **no grace period**: new processing is denied at the signed
`validUntil` instant. This is not a perpetual free-use or update-cutoff promise,
not 90 days, and not an instruction to introduce a fee after three months. The
Cloud provider/key storage remains open; continue with local synthetic validation.

`PVAD.WebApi/configuration/native-license-free-three-months.example.json` records
that policy as a disabled example (`Subscription`, `ValidityMonths: 3`). It is not
automatically loaded and does not contain an approved issuer identity or key.
Supply the actual allowlisted manufacturer identity and trust/key custody before
reviewed activation; do not simply enable the placeholder example.

New strict subscription files sign `gracePeriodDays: 0`. Their native consumer
libraries must be updated together: older strict V2 readers reject the new field
rather than silently allowing seven days. Files without the field retain the
previous subscription grace behavior; no historical artifact is rewritten.
Renewal retains the signed strict-expiry setting and owner. Already-admitted
operations keep the established completion semantics; expiry prevents new
operations. The optional customer-requested free renewal below issues a new file;
there is no unattended renewal or automatic activation on customer machines.

## Customer-Requested Free Renewal

`NativeLicenseAutomation.FreeRenewalEnabled` defaults to false. The disabled
three-month example sets it to true, but the enclosing automation and issuer
must still be explicitly configured and enabled. This workflow only accepts
the selected subscription policy with exactly three calendar months and zero
grace; it is not a paid-license renewal endpoint.

An authenticated human owner opens **Account → Licenses → Details**, then
**Renew free license** at or after the signed expiry time. Company users need
current organization membership and `account.licenses.read`; personal owners
need no organization. `POST /api/pvad/v1/account/native-licenses/{id}/renew-free`
requires the normal CSRF token and uses the manufacturer issuance rate limit.
It accepts no customer-selected owner, term, grants or signing token. The
allowlisted manufacturer actor must still be active, verified, unlocked and a
global System Administrator authorized by the operator policy.

Only completed automatic registration artifacts under the same policy version,
and their automatic free successors, are eligible. Manual, paid, legacy-grace,
Trial, perpetual and administrator-renewed artifacts are excluded. Account,
organization, membership, cancellation and policy eligibility are rechecked.
Expiry of the independent 30-day Cloud trial does not block this free native
renewal; explicit suspension or cancellation does. This changes no Cloud access
or trial entitlements.

The server starts the new term at the successful request's current time and
sets its end to three calendar months later, with no grace. A late request
does not lose validity to an already elapsed gap. Ownership and baseline product
grants are retained, and feature rights are intersected with current account
policy. No Migration or redistribution rights are added. The original signed
artifact stays immutable. The append-only predecessor constraint and stable
request identity allow only one successor and audit; a completed retry returns
the same successor bytes and term. A simultaneous loser may receive 409: refresh
and retry the same predecessor, never create a new request identity.

The renewal audit records both manufacturer issuer and requesting customer.
Signing failure commits no license or renewal audit. Details link to an existing
successor, including one produced through the manufacturer workflow. Customers
download the new file and explicitly load/deploy it in their offline application;
Account cannot update an offline installation remotely. No schema migration or
new production key is required by this feature.

Offline revocation manifests contain license IDs and are not consulted by this
issuance endpoint. When stopping a customer's future issuance, also suspend the
owner account/organization or disable free renewal; revoking only an old file
does not disable an otherwise eligible account's renewal route.

## Automatic Registration Issuance

Automation is a separate, disabled-by-default manufacturer workload. Apply
`20261126184951_AddNativeRegistrationLicenseQueue` before deploying the updated
registration/API code. Email verification records one durable job per owner in
the same transaction as activation, audit and welcome-mail enqueue. Registration
does not read signing keys or require signing to succeed. Failed/unverified email
confirmation does not enqueue a license; retry/resend cannot create duplicate
owner jobs. Existing already-verified accounts are not silently backfilled.

Configure `NativeLicenseAutomation` only on the manufacturer deployment:

- `Enabled`: defaults to false; native manufacturer issuance must also be enabled.
- `IssuerUserId`: explicitly allowlisted, active, verified, unlocked global System
  Administrator authorized by the operator policy. The worker does not impersonate
  a browser session. Customers have only the constrained free-renewal route above;
  service accounts have no issuance route.
- `PolicyVersion`: a reviewed stable lowercase identifier, for example `free-1`.
- `Model`: explicitly choose `Subscription`, `Trial` or `Perpetual`; there is no
  default free contract. Subscription requires exactly one of `ValidityDays` (1–3650) or `ValidityMonths`
  (1–120), and no update cutoff. The selected free policy uses three calendar
  months and signs zero grace days. Trial requires exactly 30 days and no month
  value. Perpetual requires an explicit UTC
  `UpdatesUntil` and no validity-day value. This is configuration capability, not
  approval of any commercial or perpetual-use terms.

The worker fixes the approved policy, issuer and dates on its first claim. A
configuration change cannot silently change that job's contract; a mismatch is
retained for operator review. It grants only PDF, Word, Spreadsheet, Importer and
Converter under the server account-capability policy, never redistribution or
Migration, even if a subscription entitlement lists Migration. Approved Migration
still uses the separate provider-specific manufacturer workflow.

Company ownership is organization-bound, with current active workspace/account
and membership eligibility rechecked. Private ownership is user-bound and remains
independent of workspace membership; the source registration's account type and
the active verified user are checked. An inactive user/account cannot obtain an
automatic artifact. Lease claims use row locks with `SKIP LOCKED`; a worker whose
lease expired cannot overwrite another worker's completion. A crash after ledger
commit reuses the same request, signed bytes and audit rather than issuing again.
Transient failures retry with the pinned contract, at most five attempts.
Automatic `IssuedAt` records that contract's preparation time (`ValidFrom`), so
queue delays and retries preserve the same term and valid trial ordering. Current
time still rejects an already-expired contract before issuance.
Permanent failures become review items. Failure fields contain stable codes, not
exception messages, paths or signing material. Audit records retain the policy
version, and the job links verification, owner, request and final artifact.

Account shows pending/review status and delivers completed files through the
existing owner-authorized download endpoint. Queue status does not grant product
rights. Operators can inspect retained rows in
`administration.native_license_registration_jobs` using approved operational
access; no public reset/requeue API is added. Fix configuration/account causes
before an explicitly reviewed replay. Do not edit issued artifacts, owner IDs or
pinned terms. The migration refuses rollback if any queue history exists, because
dropping it would lose pending work and deduplication state.

No automation is enabled, no production secret is provisioned and no live
migration is performed by this change. Production activation still requires the
approved contract, trusted public register and secret custody described above.
Offline verification and the later installation/sharing controls are unchanged.

## Renew Without Rewriting History

1. Select an existing native license and confirm its customer and rights.
2. Submit `POST /api/pvad/v1/admin/native-licenses/{licenseId}/renew` with a new
   `requestId`, `reasonCode`, and the strictly extended `validUntil` for a
   subscription or `updatesUntil` for a perpetual license. Leave the other date
   null. Expired new cutoffs are rejected.
3. Expect a new license ID and `previousLicenseId`; verify unchanged customer,
   owner, products, providers, model, start date, and redistribution rights.
4. Download and deliver the new artifact. Explicitly load it in the customer
   process. Failed local replacement preserves a previously valid license.

Trial licenses cannot be renewed through this operation. Changed products,
customer, model, or redistribution terms require a separately approved new
issuance. Each predecessor has at most one successor; concurrent renewals return
one success and one conflict. The prior artifact and its audit remain unchanged.
Renewal does not remotely revoke an offline artifact or alter existing customers.

## Failure Handling

Admin exposes the manufacturer workspace at `/native-licenses`, with catalog-
driven product/provider selection, organization/private-user search, paging, Details, Download
and Renewal. No signing secrets are entered in the browser. A rejected operation
keeps the draft and reuses its request ID only for an identical retry. A fresh
sign-in can be started through the sign-out/sign-in action after a recent-session
failure. Customer verification-only servers display issuance disabled.

Account lists, details and downloads use `/api/pvad/v1/account/native-licenses`.
Owner identity and active organization come from the persistent session, not
query parameters or a body. Each request checks the verified active user and
live browser session. Personal owners need no organization or organizational
permission and retain access after workspace changes or membership removal.
Organization artifacts additionally require active membership, active organization
and license-read permission. Foreign IDs return 404 within an authorized context;
API keys and service accounts cannot use this customer flow. Legacy organization
artifacts retain their ownership, including formerly issued individual-scoped
files. Their renewal does not silently transfer ownership; identical retained
issuance requests stay idempotent.
Issuance need not be enabled and no private key is loaded for customer downloads.
Stored hashes and metadata are checked before delivery; the native runtime still
verifies the signature independently before activation. Expired and superseded
artifacts remain historical downloads, not newly valid processing rights.

Screenshots of the current workspace use only synthetic browser fixtures. This
visual rehearsal is separate from the real PostgreSQL and HTTP authorization tests.

| Result | Operator action |
| --- | --- |
| 400 / invalid request or provider | Correct dates, model, reason, or canonical grant; never edit a signed payload. |
| 401 | Authenticate through Account. |
| 403 / disabled or forbidden | Check manufacturer deployment, allowlists, actual global role, verified user and recent live session. Do not enable customer issuance. |
| 404 | Check the native license ID; a server offline-license ID is a different profile. |
| 409 / conflict | Re-read the existing issuance; retry only the same request ID/body or renew the latest successor after authorization. |
| 409 / unverifiable | Stop delivery and investigate stored artifact integrity and embedded trust. Do not regenerate replacement keys. |
| 429 | Wait for the mutation rate-limit window; do not multiply request IDs. |
| 503 / issuer unavailable | Verify mounted key access, P-256 key type, configured ID and embedded public trust without exposing secret material. |

Request bodies are limited to 64 KiB; issuance mutations are rate-limited per
operator. Database failures roll back license and successful audit together.
Privileged operators must not disable append-only triggers to correct a license.
Create a separately approved artifact instead. Backup, retention, recovery, and
account erasure must retain required issuance evidence under approved policy.

## Explicit Offline Revocation Rollout

The native library now accepts bounded, signed cumulative revocation lists. This
does not add an Admin revoke button or a database-backed list issuer. The offline
preparation CLI remains authorized by manufacturer private-key-file access, not
by customer role or a public API. Keep its key outside customer deployments.

1. Approve the affected native license IDs, including any renewal successors that
   must also stop processing. Revoking a predecessor does not revoke a different ID.
2. Reserve a new positive sequence in the manufacturer's authoritative durable
   operator ledger. The counter is global across key rotations. Do not guess or
   reuse a number. Automated durable reservation/audit is a later implementation.
3. Prepare a version-one payload with purpose `PVAD.NativeLicenseRevocations`,
   `sequence`, UTC `issuedAt`, `nextUpdate`, and the full cumulative
   `revokedLicenseIds` set, retaining all prior IDs. At most 512 IDs fit this
   initial contract; do not split or truncate a list to bypass that bound.
4. Sign exact payload bytes with the existing approved manufacturer key:

   ```bash
   dotnet run --project tools/PVAD.LicenseIssuer -- revocations \
     --payload /secure/approved-revocations.json \
     --private-key /secure/manufacturer-key.pem \
     --key-id approved-manufacturer-key \
     --output /secure/new-signed-revocations.json
   ```

5. Independently verify the artifact/hash and scope before private distribution.
   Back up the latest artifact and reserved sequence; the tool never overwrites
   an existing output, generates a key, distributes a list or mutates a live DB.
6. Configure `NativeLicensing:RevocationListPath` on the intended API deployment,
   or explicitly call `PvadLicense.ImportRevocationsFromFile` in an embedded app.
   Reimport the latest approved list on every restart. API startup imports the
   configured list before its license; missing or invalid configured input fails.
7. Inspect content-free `GetRevocationStatus` metadata and test new processing.
   Revoked license IDs report `PVADLIC014`; rejected list updates report
   `PVADLIC015`. Existing admitted operations may finish; future operations must
   recheck. C# PDF/OCR workers receive the accepted signed snapshot, not the key.

An overdue list remains effective for known IDs; it is not automatically removed
or replaced by an empty list. Rollback prevention and cumulative state are local
to the running process. Customer-controlled clocks, restarts, restored files or
separate installations cannot provide immediate universal offline revocation.
Do not claim that a database status or Account download remotely disables users.
Mistaken revocations require an authorized new license ID. Signing-key revocation,
large-list partitioning and automatic distribution require separate design.

## Local Verification

The explicit `native-issuer` test runner uses an ephemeral key and isolated build
trust, a synthetic operator, and disposable PostgreSQL. It never provisions
manufacturer trust, changes a live database, publishes packages, or enables a
production bypass. Run it only using the documented test tooling. Test keys,
temporary build output and raw license artifacts are not publishable evidence.
