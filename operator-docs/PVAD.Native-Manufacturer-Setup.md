# PVAD Native Manufacturer Setup

Restricted operator preparation, 2026-11-26. Manufacturer display name: **PVAD**.
Website administrators manage account/license policy. Private-key custody and
backup/recovery responsibilities must be assigned explicitly; website Admin
access alone does not grant secret access. Responsibilities can be assigned to
operational roles; no person's name is needed in a customer license.

This prepares the selected free launch. It does not enable production, generate
production keys, choose a Cloud provider, accept third-party terms, publish
packages or apply database migrations.

## Selected Start Policy

| Setting | Selected behavior |
| --- | --- |
| Initial issuance | After verified registration, through the manufacturer worker |
| Model and validity | Subscription, three calendar months, required renewal |
| Expiry | Signed zero grace; new processing stops at the exact expiry time |
| Free renewal | Human owner requests it in Account at expiry; three months from the successful request |
| Company owner | Organization; requester needs live membership and license-read permission |
| Private owner | Individual user, without organization ownership |
| Products | PDF, Word, Spreadsheet, Importer, Converter, restricted by signed account scope |
| Migration | Separate manufacturer Admin approval; never added by automatic issuance/renewal |
| Redistribution | Not granted automatically |
| Customer offline use | Public-key verification; no private signing key or signing token supplied to users |
| Paid transition / multi-user activation checks | Deferred; these must be implemented before the later paid launch |

Three months uses calendar arithmetic, not 90 days. Existing signed files keep
their own terms. Current consumers must support the signed zero-grace field.
Operations already admitted retain the established completion behavior. Renewal
requires Account connectivity; after explicit replacement-file loading, normal
license checking works offline. The separate 30-day Cloud trial is not extended
by native renewal, and its expiry alone does not force payment for native renewal.

The implemented account policy is
`src/Licensing/PVAD.Licensing/PvadAccountLicensePolicy.cs`, not an editable grant
list in this setup file. Website membership roles cannot override a signed
denial. The start-policy review confirms:

| Capability group | Private | Company |
| --- | --- | --- |
| Documents, printing, templates, own code, API and service-account management | Allowed | Allowed |
| PDF, image and SVG import; image analysis/OCR | Denied | Allowed |
| DOC/DOCX, XLSX, CSV/TSV, ODT, PPTX and Markdown import | Allowed | Allowed |
| HTML/XML import | Policy allows it; importers are not implemented | Same availability limitation |
| PDF, DOCX, XLSX and ODT export | Allowed | Allowed |
| PPTX, HTML/XML, Markdown, CSV/TSV, SVG and raster-image export | Denied | Allowed |
| JSON export; other undecided PDF inspection/read/attachment capabilities | Undecided, therefore no positive grant | Allowed |
| PDF signing, redaction, archival conversion and e-invoice creation/validation | Denied | Allowed |
| Migration | Separate Admin approval | Separate Admin approval |
| XLS import/export | Deferred | Deferred |

An allowance is not evidence that a format is implemented or distributable.
In particular, retaining the XLS decision does not close NPOI/distribution
gates. This setup does not add import/export functionality or change roles.

## Files And Static Preflight

- `PVAD.WebApi/configuration/native-manufacturer-free-start.example.json`:
  complete **native-only** configuration projection, with issuer and automation
  disabled and no usable key/operator. The existing three-month automation
  example has identical selected terms.
- `operator-docs/native-manufacturer-readiness.example.json`: preparation record
  containing system/role names and evidence references only. Null means open.
  It is not application configuration or proof of approval.
- `tools/native-licensing/manufacturer-preflight.mjs`: read-only static checker.

From the repository root:

```bash
node tools/native-licensing/manufacturer-preflight.mjs --mode prepare
```

The checked-in examples return exit 0 and `staticChecksPassed: true`, while
`pending` lists the unfulfilled prerequisites. Both issuance flags must remain
false in preparation mode. `productionReady` is **always false**; this tool
cannot establish production acceptance.

Use deployment mode against the examples to see the expected blocking result:

```bash
node tools/native-licensing/manufacturer-preflight.mjs --mode deployment
```

It returns exit 1. Do not fill missing evidence with guessed values to make it
green. Once actual deployment arrangements have been reviewed, use private
metadata/configuration copies outside the repository:

```bash
node tools/native-licensing/manufacturer-preflight.mjs --mode deployment \
  --config /protected/pvad/native-config.json \
  --readiness /protected/pvad/native-readiness.json \
  --trust src/Licensing/PVAD.Licensing/TrustedKeys.json
```

The checker accepts only the two native sections, not a whole appsettings file
with connection strings or mail credentials. It requires JSON booleans/numbers,
exact known fields, the three-month/free-renewal policy, a nonempty operator GUID
matching the allowlist, an absolute key-mount path, a matching public P-256 trust
entry and complete readiness metadata. Known ephemeral/synthetic key IDs are
rejected; other IDs and evidence strings still need human provenance review.
Errors contain fixed codes, not input values, paths or exception details.

It opens the supplied JSON files, including the **public** trust register, but
never opens `PrivateKeyPath`, generates keys, signs a license, contacts a service,
changes application configuration or applies a migration. Paths are checked
syntactically for Unix/Windows; actual host path semantics and permissions are
live checks. The tool does not merge environment variables or validate an entire
deployment. All statically complete deployment inputs give only
`readyForLiveChecks: true`, with `liveVerificationRequired: true`.

## Manufacturer Server Installation Order

1. **Choose hosting and custody.** Record the Cloud provider, a dedicated secret
   store, key custodian, backup custodian and recovery reviewer. Keep private
   signing material out of the application database. The database retains
   owners, signed artifacts, request deduplication, queue history and audit.
2. **Review the exact release candidate.** Retain build/version evidence,
   distribution/notices approval, account-policy tests and the current local
   issuance/renewal acceptance results. Production/platform/package gates stay
   open until their actual checks and approvals are complete.
3. **Prepare the database deployment separately.** Review migration SQL and
   backup/recovery first. The candidate needs
   `20261120054204_AddNativeLibraryLicenses`,
   `20261126180911_AddPersonalNativeLicenseOwnership`, and
   `20261126184951_AddNativeRegistrationLicenseQueue`. Use the approved migration
   process; this guide/tool does not migrate a live database. Preserve the
   append-only ledger and queue history. Existing verified accounts are not
   automatically backfilled.
4. **Create the manufacturer operator account.** Use its actual Identity GUID,
   active/verified/unlocked global System Administrator role and native issuer
   allowlist. If `AdminSecurity.RequireExplicitSystemOperators` is enabled,
   configure the actual `SystemOperatorEmails` entry as well. Manual issuance
   needs `licenses.manage` and recent browser authentication; automatic signing
   checks the stored global role/operator authorization without impersonating
   a customer browser. Company customers need current membership and
   `account.licenses.read`; personal customers remain user-owned.
5. **Provision approved trust and key storage**, using the sequence below. A
   public-key source change requires a reviewed build and consumer/worker checks.
   Updating server configuration alone does not update embedded client trust.
6. **Set native settings on the manufacturer workload only.** Examples are not
   loaded automatically. Supply these sections through the deployment's normal
   application configuration. Environment equivalents use `NativeLicenseIssuer__`
   and `NativeLicenseAutomation__` prefixes. Reconcile the native projection with
   the actual effective configuration, including environment overrides. Keep
   customer servers, CodeWorker and OCR workers from receiving issuer secrets.
   Existing server-license `Licensing` keys are a different profile.
   Keep the recorded `PolicyVersion` stable unless an explicit policy transition
   is planned: changed policy versions send pending registration jobs to review
   and make older free chains ineligible for automatic renewal.
7. **Run static preflight and actual startup validation.** Verify the mounted
   key under the dedicated workload identity, real operator/database state,
   mail/verification routes, session/CSRF behavior and audit access. Startup
   option validation alone does not verify private/public key correspondence;
   the signer verifies issued signatures against embedded trust. Run an
   approved isolated acceptance check before customer enablement.
8. **Enable only after release approval.** Test initial registration, both owner
   kinds, download, offline admission, expiry and free renewal against the exact
   intended candidate. Confirm one artifact/audit after retries and failures.
   Start without silently enqueuing older accounts. Monitor stable failure codes
   and pending/dead-letter work; never log keys or signed license payloads.

## Key Provisioning, Backup And Recovery Procedure

This is a future operator procedure, not authorization to generate production
material now. Cloud/key custody is still undecided.

### Provision

1. Authorize the manufacturer workload and record approved key ID, custodian,
   secure storage reference and public-key provenance. The current signer needs
   a P-256 private PEM supplied as a protected runtime file. A non-exportable
   KMS/HSM adapter is not implemented; selecting such a service requires that
   separate integration before use.
2. Generate/import production material only inside the approved custody process.
   Record the public SPKI SHA-256 fingerprint without exporting private material
   into issue trackers, chat, source control, build contexts or support bundles.
3. Provision the public key through the reviewed native trust build. Check exact
   key ID, P-256 curve, old-key compatibility and intended consumer set.
4. Mount the private PEM read-only at the configured absolute path. Restrict
   Unix permissions to the workload identity (for example `0400`); the current
   signer rejects every group/other permission. On Windows, review equivalent
   identity-specific ACLs. Read-only mounting alone does not prove exclusive
   access. Confirm the private key corresponds to the reviewed public key.

### Back Up

1. Retain the approved recoverable key version in the chosen protected secret
   service and/or an independently encrypted backup under its documented
   recovery mechanism. Decide the actual mechanism after selecting the provider;
   no custom backup encryption is implemented here.
2. Keep recovery credentials/access separate from the backup, and restrict/audit
   retrieval. Record key ID, public fingerprint, secret version, backup date,
   storage reference, custodian and reviewer. Store references, not credentials,
   in the readiness record.
3. Back up the issuance database through its established protected process.
   Include ownership, deduplication and registration jobs/audits; a private-key
   backup does not reconstruct the license ledger, and a database backup does
   not contain the private key. Define acceptable recovery points before launch.

### Restore And Rehearse

1. Run a rehearsal with temporary test keys first. Keep production signing and
   customer traffic disabled in the isolated recovery environment. Restore the
   approved backup/version using the selected secret service's recovery process.
2. Verify the restored public fingerprint and key ID independently against the
   approved record. Check workload permissions and that no private bytes appear
   in output. Verify signing/verification only through an explicitly approved
   isolated probe; do not issue a customer license as a recovery experiment.
3. Recover the database separately when needed. Verify retained immutable
   artifacts, request IDs, predecessor uniqueness and queue leases before
   resuming jobs. Do not delete history or edit signed records to resolve a
   replay. Restoring only a key must not reset or extend customer validity.
4. Have the recovery reviewer retain the outcome, evidence reference and cleanup
   confirmation. A metadata entry is not a completed drill. Destroy isolated
   recovered material and remove temporary mounts/permissions after the check.
5. Resume the manufacturer workload only after operational approval. Rotation or
   suspected compromise needs its own reviewed trust/revocation plan; do not
   silently substitute a new key with an old ID. Retain required old public trust
   for compatible verification of existing valid artifacts.

## Remaining Inputs Before Production

The concrete open inputs are the provider, secret-storage system, operational
custody/backup/review roles and authentic evidence for trust provisioning,
mount permissions, recovery, migration, acceptance, distribution and release
approval. Public publisher/legal and XLS/NPOI decisions remain separate gates.
The display name PVAD and website Admin responsibility do not close those gates.
See [the issuance runbook](PVAD.Native-Library-License-Issuance.md) for restricted
routes and [the closure matrix](../checklists/PVAD.Native-Licensing-Completion-Handoff.md#closure-matrix-all-six-remaining-primary-items)
for the six original completion requirements.
