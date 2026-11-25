# Native Library License Issuance

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
6. Apply the additive `AddNativeLibraryLicenses` migration through the normal
   reviewed deployment process. It adds an append-only ledger and database
   mutation trigger; the implementation does not migrate a live database for you.

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
   `organizationId`, `customerReference`, `model`, `validFrom`, `validUntil`,
   `updatesUntil`, `redistributionAllowed`, `grants`, and `reasonCode`.
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
transaction. Audit details contain model, reason code, artifact hash, and renewal
link only, not the full artifact, customer reference, or signing material.

## Renew Without Rewriting History

1. Select an existing native license and confirm its customer and rights.
2. Submit `POST /api/pvad/v1/admin/native-licenses/{licenseId}/renew` with a new
   `requestId`, `reasonCode`, and the strictly extended `validUntil` for a
   subscription or `updatesUntil` for a perpetual license. Leave the other date
   null. Expired new cutoffs are rejected.
3. Expect a new license ID and `previousLicenseId`; verify unchanged customer,
   products, providers, model, start date, and redistribution rights.
4. Download and deliver the new artifact. Explicitly load it in the customer
   process. Failed local replacement preserves a previously valid license.

Trial licenses cannot be renewed through this operation. Changed products,
customer, model, or redistribution terms require a separately approved new
issuance. Each predecessor has at most one successor; concurrent renewals return
one success and one conflict. The prior artifact and its audit remain unchanged.
Renewal does not remotely revoke an offline artifact or alter existing customers.

## Failure Handling

Admin exposes the manufacturer workspace at `/native-licenses`, with catalog-
driven product/provider selection, organization search, paging, Details, Download
and Renewal. No signing secrets are entered in the browser. A rejected operation
keeps the draft and reuses its request ID only for an identical retry. A fresh
sign-in can be started through the sign-out/sign-in action after a recent-session
failure. Customer verification-only servers display issuance disabled.

Account lists, details and downloads use `/api/pvad/v1/account/native-licenses`.
The active organization comes from the persistent session, not a query parameter
or body. Each request checks the verified active user, active membership,
organization and live session. Removed members cannot download; foreign IDs return
404 within an otherwise valid workspace. API keys cannot use this customer flow.
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
