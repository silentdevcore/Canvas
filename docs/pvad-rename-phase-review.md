# PVAD Rename Phase Review

This records the repository rename review, not publication, legal approval or
authorization to cut over external infrastructure. See the
[tracker](../checklists/Rename-PXA-To-PVAD.md).

## Current Audit: 2026-10-24

The rename is not complete. The tracker counts 86/98 primary acceptance gates
complete (87.8%) and 12/98 open (12.2%). Dated follow-up checkboxes are supporting
evidence and are not added a second time. This is task completion, not an effort
estimate or production readiness percentage.

The repository now contains committed rename follow-ups through `f2a90213`.
The migration artifact writer follow-up is still uncommitted in the working tree.
The old statements about an entirely staged, uncommitted rename below refer to
their historical checkpoints, not current Git state.

Concrete remaining source work includes OAuth/MCP default resources, built-in and
service-account clients, scope discovery/writers and coordinated stored-client
compatibility. Known scope aliases and opt-in grant migration already exist.
Final-source full API/PostgreSQL, browser/CSP and DocFX validation are not closed
by earlier successful or selectively filtered runs. Infrastructure activation,
publisher ownership, artwork/installed associations and Major release approvals
remain separate gates. The tracker's R01-R12 breakdown lists the exact tasks.

## Historical Source-only Closeout

This subsection records an earlier reviewed scope, not the current completion
claim. The current audit above supersedes it where later findings differ.

The reviewed source rename is provisionally closed. The follow-up Observability
file/template changes and Java/Maven/JAR/image renames are staged, but their builds
and tests were explicitly deferred by the owner. Remaining legacy compatibility,
persisted identities, signed evidence and neutral drawing terminology are not
unresolved cosmetic renames. No further blanket replacement is authorized by
this closeout.

Everything below records earlier review evidence. In particular, its file counts,
hash snapshots and successful tests must not be treated as a new review of the
latest source revision. Resume with final-source validation and the outstanding
deployment/release approvals; there is no new 95% acceptance claim.

Additional concrete source finding: Compose defaults now use PVAD for services,
resource keys, secret mounts and development credentials. Existing data is selected
only through explicit compatibility overlays with required verified external IDs.
51 focused checks pass, including actual Compose config resolution and missing-
binding rejection. No containers or existing resources were changed; application
builds and runtime cutover remain deferred. See [the explicit mapping](pvad-infrastructure-mapping.md).

## Earlier Repository Review (2026-10-22)

The three repository review gates now have separate evidence:

- Context audit: reviewed active naming across API/security, Core, Execution,
  Generation, Importing, Infrastructure, Migrations, Designer, websites, tools,
  operational configuration and documentation. Corrected remaining active
  metadata, captions, sample output, descriptions, guides and temporary-image
  prefixes. A whole-file legacy replacement is not used for customer content,
  signed/accepted material, wire protocols or applied database migrations.
- Retained exceptions: the exact path/hash/decision snapshot is validated by
  `rename-residual-review.mjs`. Every recorded entry has an explicit review
  decision and boundary guide; changes invalidate the decision. The final context
  table explains diagnostics, format/read aliases, crypto/persistence identities,
  old infrastructure bindings, historical evidence, vendor APIs and incidental
  encoded substrings separately. CI runs this validator. This is a naming review,
  not general approval of all historical functionality described by these files.
- Actual index: `rename-index-review.mjs` checks staged blob bytes against working
  files, all original rename targets, untracked files, file modes and newly added
  runtime/secret paths. Including the later cutover-preparation follow-up, the
  checked index contains 2,918 files, zero unpaired
  deletions, zero unexpected mode changes and zero structural problems. All
  1,984 source deletions have targets; 36 retained external-signature basenames
  preserve their bytes. Git's similarity-based display may show some of these
  as delete/add rather than rename; that is not file loss.

Review covered the identity-only, substantive-text, new-file and binary queues,
using normalized diffs only as reading aids and raw files for protocol/security
decisions. New tests and compatibility adapters were checked with their callers,
not inferred correct from mechanical substitution. The 22 changed binary-queue
items are the documented UI recaptures and one NUL-bearing C# test source; that
source retains its intentional NUL and differs only in technical identity.

The first full staged whitespace check found CRLF differences, empty final TSV
cells and two extra EOF blank lines missed by unstaged-only checks. Normalized
line endings in 83 affected text files without changing their program content;
the original inventory's empty last field is represented by `-` (no match), with
all four columns retained. No signed, legal or vendor fixture bytes were changed
by that formatting pass. The final staged check must pass before a commit.

The earlier snapshots below describe the incremental review chronology. Their
pending-review statements are historical and are superseded by this section;
external production/publisher/rollback gates remain open.

## Content Review Queue And First Staged Batch

`node tools/configuration/rename-content-review.mjs` compares HEAD blobs with
current working files, including renamed, staged and untracked files. Git blob
payloads are read in one length-delimited batch; binary/invalid-UTF8 data never
passes through text replacement. Reports contain paths/hashes/classification,
not source text or secrets. Symlink targets are read as link text, not followed.

The first snapshot separates 929 identity-substitution-only candidates, 890 files
with additional text changes, 815 byte-identical files, 22 changed binary files
and 200 new files. There are no missing target entries. These are triage counts,
not semantic approvals: even pure string substitutions can break wire contracts.
The output `artifacts/rename/content-review.json` is ignored and must be refreshed
as the working tree changes. Content comparison is not a file-mode review.

The staged review-tool/ignore batch was read in full with `git diff --cached`;
its whitespace check passes. Core's joined-identifier contract and exception
documentation remain a separate unstaged follow-up. The full staged-diff gate
is still open. No commit was created.

## File Completeness

### 2026-10-21 Follow-up Snapshot

The refreshed content triage reports 1,058 identity-substitution candidates,
779 files with additional text changes, 797 byte-identical entries, 22 changed
binary entries and 213 new files. These numbers precede this review-note update
and will change with new documentation/fragments. None is automatic approval.
In particular, a complete final staged review has not happened. The checked-in
remaining-name inventory contains 1,058 matching files and remains a review queue.

The source follow-up bounds all old identifiers in Generation, Importing and
Infrastructure with explicit contracts in the exception document. Newly generated
OCR worker activity names now use PVAD. New Company return links use the canonical
cosmetic signal; old links remain recognized. Canonical values take precedence,
both marker generations are removed when consumed, and external origins remain
unchanged. This signal never authenticates a user or changes authorization.

Reviewed the existing limited staged test/tool batch again with `git diff --cached`,
including API-key denial states, retained file-store/annotation bytes, keyring
restart/tampering, model assertions and historical migration contracts. Its
working-file tooling follow-up also has an isolated staging regression test.
The existing index is deliberately not expanded to all application files. A
passing full Solution build (zero errors, 73 warnings), frontend builds and scoped
tests do not replace the remaining whole-diff review. No application commit exists
for this batch, and no publication/deployment approval is implied.

The Designer font-registration follow-up has real Chromium desktop/mobile
evidence under `artifacts/rename/designer-fonts/`. Four local font faces load
under the name requested by the current text renderer. This is an isolated
browser check, not approval of full Designer/backend visual parity. Visual
parity command producers now use canonical PVAD inputs; legacy alias readers
remain unchanged. Detailed results are recorded in the rename test report.

`node tools/configuration/rename-file-pairs.mjs` compared 1,984 tracked deletions
with current files: every file has a target, with zero unresolved pairs.
Thirty-six external-signature PDF/XML evidence files retain their original
basenames under the renamed Documentation directory and are byte-identical to
HEAD. Other paired files have before/after hashes but are not automatically
semantically approved. The ignored report is `artifacts/rename/file-pairs.json`.
The report now covers HEAD deletions whether staged or unstaged, compared with
the working files. An isolated temporary Git repository verifies identical
pairing before/after staging and no deletions after its synthetic rename commit.
This does not certify index contents, arbitrary external files or semantic
equivalence. Rerun before staging/committing.

The check found and corrected two missed owned basenames: the WebApi HTTP request
example and the Designer workbook-schema test. The HTTP example uses the canonical
API route; the test still reads the legacy schema for compatibility comparison.

## Reviewed Boundaries

### Stored Objects, Annotations And Worker Paths

Reviewed FileSystemPvadObjectStorage, PvadStoredObjectService,
PdfViewerAnnotationStore, PvadCodeWorkerClient, licensing key-path resolution,
API worker-copy targets and Compose mount settings. Annotation persistence is
unchanged from HEAD except its namespace. Object reads continue using stored
tenant/object keys, not rewritten product names.

New tests preserve a synthetic retained object and its bytes across changed
application roots with an explicit shared mount, reject overwrite, and verify
temporary-write cleanup. A separate case demonstrates why an unchanged relative
default under a different content root is not a storage migration. Annotation
restart tests retain IDs, source names, timestamps, JSON bytes and hashed filename.

The YAML-parsed container contract checks that all four persistent store paths
stay under the retained data mount and that worker executables/copy targets use
the canonical packages. It does not build an image, execute a worker or authorize
replacement of a live volume. Deployment notes cover the required explicit paths.

### Persistence And Protected Payload Follow-up

Historical migrations are checked against the pinned pre-rename commit: all 62
migration source/designer files match after C# namespace/type substitutions.
Migration IDs, SQL and the retained `pxa.e_invoice_retention_purge` session key
are unchanged. The mutable model snapshot is deliberately excluded from the
historical immutability contract so future schema migrations remain possible.

Persistence registration tests preserve explicitly configured legacy, canonical
and customer-owned database/login names, resolve migration metadata and leave
connections closed. These are local model/configuration tests, not PostgreSQL
upgrade evidence. The retained `pxa-database` data-source label is not a physical
database name. No database, login or volume was renamed during this review.

Mail and revocation-evidence protection now have separate real-file-keyring
restart cases for both old reads and new writes, plus old/new tamper rejection.
Production mounts and keys are not inspected or moved; migration notes warn
that a changed content root must not select an empty default key directory.

The content classifier was corrected to preserve UTF-8 BOMs during comparison.
Its corrected snapshot has 1,035 identity-only candidates and 784 additional
text changes (previously 929 and 890); the difference is classification only,
not 106 additional code fixes or approvals.

### Authentication Follow-up

Reviewed current security headers, API-key secret generation and handler,
authentication scheme identities, cookie validation, claim normalization,
permission expansion and OAuth scope/client registration code. Compared the
cookie and API-key handlers with HEAD's implementations: user/security-stamp,
session revocation/expiry and organization checks remain; header normalization
precedes authentication. Scheme names and registered OAuth client IDs remain
retained contracts, not unattended external identity migrations.

Expanded the real-handler/InMemory API-key matrix from six active cases to 36
cases spanning both secret prefixes, all three transports and active/revoked/
expired/disabled-service/suspended-organization/closed-organization states.
Rejected keys never acquire LastUsedAt. The focused API/cookie/claims/scope
selection passes 62/62. Two additional scope-migration tests pass, including
the real OpenIddict InMemory store preserving credentials and grants.

This is not production keyring validation, an OAuth network-client cutover,
PostgreSQL revalidation or a global security certification. No runtime code was
changed in this follow-up; only tests and review evidence were added.

| Phase | Evidence | Still pending |
| --- | --- | --- |
| Projects and build identity | File pairing, renamed project paths, shared version source and prior full Solution build | Final semantic review and staged diff |
| Core, importers and API | Scoped identity contracts, native archive tests, HTTP alias tests and full API evidence in the test report | Exhaustive joined/lowercase identifier review |
| Authentication and retained data | Explicit cookie, scheme, Data Protection, diagnostic and stored-evidence exceptions | Production key access and customer-instance validation |
| Public surfaces | Company subpage metadata, illustrative demo descriptors and their tests; prior screenshots remain separate evidence | Complete remaining-document review and approved artwork |
| Operations | Actual Compose equivalence, rule validation and template rendering | Production hosts, credentials, registry ownership and rollout |
| Release | Validated fragments and no-write major-release dry run | Approval, publication and actual immutable major release |

## Release/Workflow Review

Reviewed release/deployment workflow diffs and `Directory.Build.props`: internal
script/project paths, build metadata and new archive names use PVAD. Published
historical artifact generations remain supported by tooling. Existing GHCR image
repositories still use `pxa-webapi` and `pxa-observability-webhook-relay`; these are
external publisher decisions, not silently renamed destinations. Existing
workflow concurrency/environment identities remain operational compatibility
boundaries until their cutover is approved. No workflow was dispatched.

### Workflow and Historical Checkout Follow-up

Release validation previously read only the renamed manifest filename from a
checked-out tag. Historical tags still contain `product-metadata/pxa-releases.json`.
The CLI now accepts exactly one of the historical or canonical manifest names;
missing, malformed or ambiguous checkouts fail rather than silently selecting
another manifest. Integration tests create isolated temporary Git repositories,
commits and tags for both generations and run the actual verification CLI. They
do not modify this repository's history, contact GitHub or execute a deployment.

Short-lived CI upload/download artifact names, scratch PR-body files and the
local SBOM image now use PVAD. Both preflight artifact handoff steps were changed
together. Registry repository names, workflow concurrency locks and protected
GitHub environment names remain unchanged pending owner approval. Physical
Prometheus rule paths and pinned PDF fixture names remain compatibility evidence.

The CI SBOM container build now receives an explicit build timestamp and checks
version, full commit and timestamp through the existing strict metadata validator.
Unit contracts cover those arguments; all nine workflow files parse as YAML.
These local tests do not claim that GitHub Actions ran or that an image was
published or rebuilt during this review.

## Retention Configuration Finding

The current processing inventory and Admin status service still referred to old
retention environment keys. They now advertise canonical `PVAD_*` keys and resolve
old input through the existing conflict-checking environment helper. Tests cover
canonical, legacy, identical dual inputs, defaults and conflicting values without
disclosing those values. Retention durations, holds, policy approval status and
Production readiness remain unchanged; this is not a legal retention approval.

## Active Documentation and Packaged Guide Review

Reviewed the current docs overview, chart guide, code-workspace guide, asset and
existing-PDF architecture guides, Nutrient Java mapping inventory and API deployment
README against the current project/registry names. Updated first-party prose only;
vendor API names, source snapshot dates, planned/unsupported statuses and stable
diagnostic identifiers are not revalidated or changed by this naming review.

Chart instructions now name the actual Tables and Charts group. Code-workspace
instructions distinguish newly generated `pvad-element-id` markers from accepted
historical `pxa-element-id` input. Asset guidance distinguishes native output
extensions from legacy readers. Operator-defined artifact mount examples are
explicitly conventions, not invented automatic API configuration settings.

The Chart and Code Workspace guides were referenced by release fragments but not
included in the public download allowlist. Both are now packaged explicitly, and
the production build's copies were compared byte-for-byte with their sources.
The public DocFX config now includes the native-format and architecture files
already linked by its TOC. The dangling Operations navigation is removed; operator
mail guidance is not added to public build inputs. Static contract checks verify
this boundary. DocFX itself is not installed locally, so its generated HTML build
was not executed or claimed to pass.
