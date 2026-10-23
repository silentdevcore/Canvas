# License And Background Job Compatibility

## Signed License Inventory

Schema versions 1 and 2 remain supported by `PvadOfflineLicenseValidator`.
The signature is ECDSA P-256/SHA-256 over the UTF-8 bytes of `EnvelopeJson`.
Whitespace, property ordering, spelling and string values are signature-covered.
Do not deserialize, rename and reserialize an issued envelope in the database.

| Signed property | Preservation rule |
| --- | --- |
| `schemaVersion` | Retain 1/2; new issuance uses schema 2 |
| `licenseId`, `licenseNumber` | Retain original identifiers, including PXA numbers |
| `organizationId`, `organizationName` | Retain original signed values |
| `edition`, `accountType`, `deploymentMode` | Retain original contractual values |
| `validFrom`, `validUntil`, `issuedAt` | Preserve exact serialized timestamps |
| `instanceLimit`, `entitlements` | Never increase or rename through a brand rewrite |
| `productVersion`, `deploymentId` | Retain version/deployment binding; schema 2 fields |

Each entitlement contains `capability`, `enabled`, `limit`, `unit`, `expiresAt`.
These names are neutral. No signed field contains a CLR namespace/type name.
The wrapper holds `signature`, `keyId`, `algorithm` and the presentation `envelope`.
These metadata identify the verifier; changing a key ID is not a cosmetic rename.

## New Issuance And Downloads

New issuance allocates `PVAD-*` numbers. It does not update the number, payload
or signature of an existing license.
PVAD-numbered downloads use `application/vnd.pvad.license+json` and
`.pvad-license.json`. Historical/custom numbers retain the old MIME type and
`.pxa-license.json` filename so existing downloads remain compatible.

New-format wrappers add `envelopeJson`, the exact signed text. Verify its UTF-8
bytes against `signature` with the trusted key selected by `keyId` and the allowed
algorithm before parsing or trusting its contents. The presentation `envelope`
is not an independently trusted source. Do not reconstruct signed bytes from it.
No existing database row is rewritten by downloading either format.

The issue/revoke workflows remain explicit and auditable. The protected
`POST /api/pvad/v1/admin/licenses/{licenseId}/replace` operation now replaces a
valid active license inside one serializable transaction, including the old
`Replaced` status, new row, linked audit event and transactional mail outbox.
No signed bytes on the old row are modified.
There is no mass replacement during startup and no automatic license extension.
Existing `PXA_LICENSE_*` diagnostics remain compatibility identifiers pending the
separate diagnostic-family migration. This stage does not silently rename them.

### Explicit Replacement

Only System Administrators with `licenses.manage` and a valid CSRF token may call
replacement. Supply a reason of 1–500 characters. For schema 2, existing product
version and deployment binding cannot be changed. For schema 1, explicitly supply
`productVersion` and `deploymentId` to create a schema-2 successor.

```json
{
  "reason": "Replace the legacy format during the PVAD transition",
  "productVersion": "1.1.0",
  "deploymentId": "example-production"
}
```

The target organization and subscription are resolved from the old license, never
from request tenant IDs. Suspended organizations and inactive subscriptions are
rejected. The validator must accept the original license before replacement.
Validity, instance limit and signed entitlements are copied without extensions.
Changed version/deployment bindings return 400. Already replaced/revoked/expired
licenses and concurrent replacement conflicts return 409. A missing ID returns 404.
Unknown signed fields are rejected instead of silently dropping possible future
constraints; a reviewed migration is required for such envelopes.
Repeated requests do not create additional successors. Cancellation/signing/outbox
or storage failures must not commit a partial replacement.

Offline installations cannot learn a replacement status without an update. Retain
the revocation-distribution and customer migration procedure; the server transaction
alone does not remotely invalidate already distributed offline artifacts.

## Signing Key Cutover

- Retain configured `Licensing:KeyId`, private/public key paths and mounted files.
- The historical default `pxa-development-1` is deliberately unchanged. Do not
  substitute a PVAD key ID for an existing verifier without a rotation design.
- A complete existing pair is reused. An incomplete pair fails even in Testing
  and Development, rather than overwriting the surviving key.
- Only an entirely absent pair may be generated in Development/Testing.
- Production still requires mounted keys; this work does not generate or rotate them.
- The current validator has one configured verifier, not a historical key ring.
  Multi-key rotation and production backup/restore must be verified separately.
- Never commit real keys, licensed customer payloads or usable licenses as fixtures.

Synthetic tests use real ECDSA signatures to verify restart continuity, byte-level
tampering rejection, new wrapper integrity and incomplete-pair failure.

## Persisted Job And Storage Inventory

Job type strings remain unchanged and neutral:

- `designer.template-render`
- `document.import`
- `document.export`
- `migration.code`
- `designer.code-execution`
- `pdf.trust-inspection`

Jobs retain IDs, tenant/user IDs, type, payload JSON, retention mode, attempt counts,
status, lease ID/expiry, cancellation state and input/result object references.
Dispatch does not use CLR assembly-qualified type names. Type-specific records
are selected by stable type strings, not C# class names. Caller-provided payloads
may contain historical text; do not perform substring replacement inside them.

Storage retains `Storage:RootPath` (default `App_Data/objects`) and keys of the form
`<organization-id-N>/<object-id-N>`. Do not move or re-key existing blobs merely
because a class/namespace changed. Metadata filenames/checksums and stored bytes
stay unchanged. New temporary buffers use `pvad-object-*`; new import-job output
filenames use `.pvad.json`. These do not rename already stored objects.

Job/storage metrics and activities use canonical `pvad.*` identities. Historical
metric data is handled by the observability compatibility rules, not rewritten.

Filesystem storage writes supplied bytes; this inventory is not an encryption-at-rest
guarantee. Retained object encryption, representative backup/restore and a real
cross-binary in-flight job cutover remain open acceptance gates. Never claim that
normal processor tests alone prove a production rolling upgrade.

### Restart, Leases And Cancellation

- Restarting with an unexpired processing lease leaves that claim intact.
- Expired claims are retried only below MaximumAttempts; exhausted pending/expired
  claims become DeadLetter without starting another attempt. Retention is reapplied.
- Worker shutdown propagates cancellation and leaves the persisted claim available
  for recovery after expiry. It does not mark work completed merely because the
  worker stopped. Recovery is at-least-once, not exactly-once execution.
- Final status/result publication checks the current processing state, matching
  lease ID and expiry atomically. A worker that lost its claim cannot overwrite a
  successor or a Cancelled state; its newly generated result is discarded.
- The cancel API cancels Pending jobs only, with an atomic state predicate and
  tenant filter. A concurrent claim wins instead of being overwritten; the response
  reports the actual state. This is not interrupting cancellation of active work.
- There is no lease heartbeat in this milestone. Operations must fit the configured
  LeaseMinutes; long-running jobs need a separately designed extension mechanism.

Source-level tests use persisted legacy-shaped payloads, fresh DbContexts/processors
and reopened filesystem storage. They simulate shutdown, claim loss, cancellation
and retry exhaustion against PostgreSQL. A separate paired-binary test now also
kills a real PXA worker process and resumes its job with PVAD; see below.
An abrupt process death between storing a
result and publishing it can still leave an unreferenced object; these tests do
not guarantee orphan-free storage or universal side-effect deduplication.

### Coordinated Worker Cutover

1. Back up database/object storage and retain the matching old configuration.
2. Stop submissions and gracefully stop all old PXA workers; confirm they exited.
3. Keep database rows, object roots, keys and type strings unchanged. Do not reset
   attempt counters or manually delete active leases as a rename shortcut.
4. Start the compatible PVAD workers. Wait for unexpired leases to expire naturally
   before interrupted work is eligible for recovery.
5. Check status, attempts, result access and tenant isolation before reopening traffic.

Do not run old PXA and new PVAD workers concurrently as a rolling upgrade: old
binaries do not enforce conditional publication. The new guard cannot prevent an
old binary from writing directly. Production cutover/rollback still requires a
representative customer-like backup and deployment verification, beyond the
synthetic paired-binary test.

### Representative Paired-Binary Test

The [job cutover harness](../tools/job-cutover/README.md) builds real PXA application
assemblies from immutable commit `fce423cf8c5f5e71eff6178f712fe4e5eb5af69a` and
the current PVAD assemblies. The common test driver is compiled separately against
each generation, with an assembly-identity assertion; production baseline sources
are not renamed or patched to make the test pass.

PXA applies its migrations and creates synthetic users, organization, role links,
subscription/entitlement, template, input object and jobs. After the PXA worker
claims a document-import job, the parent terminates the OS process and waits for
exit. PVAD then cancels the other Pending job, recovers and completes the first,
and both generations independently verify the retained data and result.

The fixture checks migration IDs, no pending migrations, stable physical column/
table metadata, user/organization/template IDs, role and subscription links,
template checksum/revision, original object key/checksum and foreign-tenant denial.
Lease expiry is advanced explicitly in this test after the old process exits;
production must use the normal coordinated lease procedure.

This proves one deterministic document-import boundary, not every importer,
OCR/code worker, auth/session flow or safe whole-application rollback. It uses a
controlled test importer rather than claiming PDF/OCR fidelity coverage. The
manual `PVAD Paired Binary Job Cutover` workflow reproduces it on a CI runner;
adding the workflow does not imply it has already run on GitHub.

## Related Guides

- [Identity compatibility](pvad-identity-compatibility.md)
- [Rename roadmap](../checklists/Rename-PXA-To-PVAD.md)
- [Rename test report](../checklists/PVAD.Rename-Test-Report.md)
