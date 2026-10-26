# PVAD Rename Release Approvals

The decision register below distinguishes confirmed scope from pending release
approval. Local tests are not authorization to publish,
change production infrastructure or certify an existing customer installation.
The [rename tracker](../checklists/Rename-PXA-To-PVAD.md) remains authoritative.

| Area | Required decision or evidence | Responsible party | Status |
| --- | --- | --- | --- |
| Installed product inventory | Earlier owner report covered local development/testing and no customer installations; historical release archives and legacy GHCR images are now observed. | Product owner | Owner statement dated 2026-10-21; artifact evidence updated 2026-10-26; no customer installation independently inspected |
| Public identities | Owner selects pvade.com, support@pvade.com, technical sender no-reply@pvade.com and GHCR target ghcr.io/silentdevcore. Historical container read access and visible package/tag inventory are verified; NuGet/npm publishers, publishing permissions and infrastructure/mail verification remain open. | Business owner and operations | Domain/support names confirmed 2026-10-21; sender selected 2026-10-24; container target, local workflow changes and read-only package inventory confirmed 2026-10-26; no publication approval |
| Compatibility policy | .pvadp/.pvads, stable diagnostic IDs, old API/format readers for one major release. | Product owner | Confirmed in conversation, 2026-10-21 |
| Cutover and rollback | Repository owner accepts responsibility for local development backup, restore and rollout. Production/customer procedures are not approved. | Repository owner (local only) | Local owner confirmed, 2026-10-21; production approval pending |
| Protected material | Verify actual keyring access and retained encrypted payload recovery without exposing or rewriting secrets. | Security and operations | Pending |
| External infrastructure | Verify real DNS, TLS, reverse proxy, CSP, authentication redirects, webhooks and sender configuration. | Infrastructure owners | Pending |
| Native association and artwork | First installation-test scope is macOS/Chrome with the existing generic project icons; final corporate artwork and other platforms are separate. | Product owner and packaging | Owner confirmed scope/icons 2026-10-25; real default association/import/revocation/uninstall tests passed locally. Manual first-run prompts, public deployment and other platforms remain untested. See [platform evidence](pvad-native-project-platform-validation.md). |
| Staging acceptance | Authorize and execute a representative deployed environment with real host boundaries. | Release owner and operations | Owner selected localhost on 2026-10-25; local verification is separate from the still-pending external deployment gate. See [local scope and evidence](pvad-local-staging-validation.md). |
| Release publication | Approve the major version, migration instructions, tags and registry publication. | Release owner | Pending |

Before closing a row, record the approver, UTC date, target environment and a
redacted evidence location. Never place credentials, signing keys, customer
documents or unredacted configuration in this document.

## Retained Workflow Bindings

Initial review checkpoint: 2026-10-23; source status updated 2026-10-26.
The remaining old names in release/deployment
workflows are explicit compatibility or infrastructure bindings, not additional
current product names. The read-only audit below confirms historical published
container artifacts; customer installations remain unverified. NuGet and npm publisher accounts
remain unselected/unregistered; the container target is now owner-selected.
Local source renaming does not establish registry access or permission to publish.

| Source | Retained identity | Required coordinated action |
| --- | --- | --- |
| `release.yml`, `deployment-validation.yml`, deployment evidence verifier | New images use GHCR `pvad-webapi` and `pvad-observability-webhook-relay`; historical `pxa-*` images remain explicit inputs | Source changes and historical read inventory complete for owner-selected ghcr.io/silentdevcore; canonical image availability, publishing permissions and real release approval remain unverified |
| `deployment-validation.yml` | `pxa-staging`, `pxa-production` via its environment expression | Inspect actual GitHub configuration, then verify reviewers, secrets, branch restrictions and approval behavior before changing workflow bindings |
| `release.yml` | Old protected-branch runs may still use `pxa-stable-release`; local source uses `pvad-stable-release` | Drain earlier runs and switch every applicable branch/workflow consistently |
| `prepare-release.yml` | Old runs may still use `pxa-release-preparation`; local source uses `pvad-release-preparation` | Drain preparation runs and retain single-writer release preparation |
| `release-dry-run.yml` | Old runs may still use `pxa-release-dry-run`; local source uses `pvad-release-dry-run` | Coordinate old/new run cancellation and lock identity |
| `deployment-validation.yml` | Old runs may still use `pxa-deployment-*`; local source uses `pvad-deployment-*` | Preserve per-environment serialization during the transition |
| `job-binary-cutover.yml` | PXA in the historical worker step name | Keep identifying the real pinned old binary; it is not a PVAD build renamed cosmetically |

These entries do not prove a published package exists. The read-only follow-up
below confirms actual protected environments, but does not change them or run a
workflow. Prior unchanged-workflow comparisons describe earlier checkpoints;
the four local concurrency group names are now explicitly migrated. Container
producer/consumer sources now use the selected canonical family. Permissions,
action pins and protected environment bindings remain unchanged.

Historical versions retain their exact image repositories. Archive identity and
container identity are distinct in the current transition: deployment accepts
both complete archive generations, and its source now requires an explicit image
family choice. New release sources publish only the canonical image family.
The evidence verifier now supports either complete image family, with strict
publisher/version checks and an explicit `containerIdentity` in new records.
Never silently relabel, overwrite or invent an immutable historical container
digest. This gate remains open until authorized and tested.

Local preparation now exists in `tools/configuration/rename-container-workflows.mjs`.
It generates ignored YAML candidates under `artifacts/rename/container-workflows/`
and never edits active workflows, contacts GitHub or publishes images. The candidate
updates the two producers and their consumers together, preserving permissions,
action pins, concurrency groups and protected-environment bindings. Historical
container family selection is explicit and independent of archive names.
The generator preserves whichever lock names the input workflows currently use;
it does not activate or validate a remote lock cutover.
The generator records the configured publisher separately from unverified
registry access. This is not proof of image availability or a completed remote
cutover. See [deployment control](../tools/deployment/README.md#offline-container-workflow-candidate).

## Container Publisher Source Transition (2026-10-26)

The owner subsequently confirmed `silentdevcore`, the current GitHub repository
owner, as the GHCR target for local workflow changes. This supersedes the earlier
same-day undecided-container-publisher instruction for this source-only step.

New release source targets:

- `ghcr.io/silentdevcore/pvad-webapi`
- `ghcr.io/silentdevcore/pvad-observability-webhook-relay`

Both workflow sources declare the same publisher and reject a different
repository owner before registry operations. API and Relay version/minor/latest
tags, immutable-version pulls and OCI metadata checks change together. Deployment
requests select `PVAD` or historical `PXA` explicitly; the historical default
remains conservative, and archives do not imply an image family. The existing
evidence verifier continues rejecting mixed families, other publishers and
mutable tags without rewriting retained records.

The offline generator now accepts canonical sources idempotently and retains
support for legacy input models. Its report distinguishes a configured namespace
from unverified registry access. Protected environment names, reviewers, secret
values, Action pins, permissions and concurrency settings are not changed.

An initial additional shell-stub test unexpectedly selected the host Docker
client because the shell's startup path prepended the Rancher CLI directory.
Its registry hostname lookup failed; this is neither passing offline evidence
nor verification of registry access. The final test resets and freezes its
isolated PATH inside the shell, verifies the selected Docker stub before running
the inspection block, and exercises both image families without registry access.
The initial failed result remains separately recorded in ignored local artifacts.

No successful registry contact, push, workflow dispatch, image/tag publication,
protected environment edit, merge or product-version change occurred. Actual registry
permissions, image availability, remote workflow drain, staging and release
approval remain open, as do NuGet/npm publisher selection. The combined primary
rename gates are not completed by this source transition.

## Read-Only GitHub and Registry Audit (2026-10-26)

The owner authorized read-only inspection, not publication or configuration
changes. Evidence is recorded under ignored local artifacts at
`artifacts/rename/github-readonly-2026-10-26/report.json`, covering
`2026-10-26T09:00:26.003Z` through `2026-10-26T09:00:58.114Z`. Source baseline is
`47fe1501`. Retain this report before cleaning the workspace. It is a dated,
non-atomic observation, not ongoing authorization or a release attestation.

### Account, Packages and Images

- The authenticated GitHub account and repository owner are both `silentdevcore`.
  Repository metadata reports administrator access and default branch `develop`.
  Repository administration is not proof of package ACLs or publishing rights.
- At this checkpoint, the authenticated container-package list returned HTTP 403 because the
  GitHub credential lacks `read:packages`. The inventory is incomplete, not an
  empty namespace. No authentication scopes or credentials were changed.
- Exact-tag registry inspection read both historical `1.1.0` image manifests:

| Image | Observed immutable registry digest |
| --- | --- |
| `ghcr.io/silentdevcore/pxa-webapi:1.1.0` | `sha256:16ced89a510a01a8ac20e892969261504fd6992e83c1dbed02e5970ae73fbd97` |
| `ghcr.io/silentdevcore/pxa-observability-webhook-relay:1.1.0` | `sha256:24630bcab45727f32caec03ebfd8358bf8fb04d4a4a340e2db8431a26b26e401` |

The canonical API/Relay `pvad-*` manifest requests for `1.1.0` were unavailable
with an authorization/not-public response. This does not prove that the image
repositories are absent or that publishing them would succeed. No image layers
were pulled, run, rewritten, retagged or published. Manifest readability is not
validation of image contents, runtime version, signatures or deployment safety.
Keep these registry digests distinct from local test image IDs and archive hashes.

### Environments and Branch Protection

The complete environment list contains only `pxa-staging` and `pxa-production`;
no canonical counterpart appeared. Both allow only protected branches. Production
requires one environment reviewer but does not prevent self-review. Both
environments permit administrator bypass. These existing settings were observed,
not approved as a future security policy, weakened or changed.

Both environment-level secret metadata lists were successfully read and empty.
This does not imply that repository/organization secrets are absent. Variable
endpoints were deliberately not queried because their responses include values;
the variable-name inventory remains unknown. No secret values were requested,
printed or saved; existing CLI authentication remained opaque and unchanged.
This is not a complete migration snapshot.

Both `main` and `develop` are protected with administrator enforcement, one
approving pull-request review, stale-review dismissal, last-push approval and
strict required checks `version-policy`, `dotnet` and `frontend`. Force pushes
and branch deletion are disabled. Linear history is required on `develop`, not
on `main`. These branch rules are separate from environment self-review/bypass.
Review the required-check set against the current CI jobs before activation;
this audit does not change repository policy.

### Workflows and Remaining Activation

All four pages of the workflow-run listing were read: 372 distinct runs, with
no non-completed run observed. This is not an atomic drain certificate; repeat
the check immediately before any coordinated old/new lock activation.

Remote release sources on both protected branches still use historical image
leaves and the old release concurrency group. The deployment-validation source
on `develop` retains the old environment/lock bindings and has no explicit
image-family input; its source request on `main` returned HTTP 404. The local
canonical workflow changes are therefore not evidence of protected-branch
activation. The two branch tips, workflow blob/source hashes and request outcomes
are preserved in the local report; they are not silently treated as this branch.

At this checkpoint, still required: owner-controlled read-only package access, complete package/tag
inventory, NuGet/npm selection, permission review, provenance-reviewed environment
and variable metadata, deliberate self-review/bypass policy, protected-branch
workflow rollout, a fresh drain check, staging and release authorization. No
remote setting, environment, permission, workflow run, tag or package was changed.
The eight primary rename gates stay open; completion remains 90/98 (91.8%).
The package-scope blocker was subsequently resolved by the approved follow-up below;
the original observation remains unchanged as dated evidence.

## Approved Package Read Access and Inventory (2026-10-26)

The owner authorized adding only `read:packages` to the existing GitHub CLI
account `silentdevcore`. The first browser authorization used a different account
and was rejected; it did not replace the active account. The successful retry
retained `gist`, `read:org`, `repo` and `workflow`, adding `read:packages` without
`write:packages` or `delete:packages`. No tokens or credential-store contents were
read, displayed or persisted in audit evidence. Git credential setup was declined.

The successful GET-only audit is preserved under ignored local evidence at
`artifacts/rename/github-packages-verified-2026-10-26/report.json`, from
`2026-10-26T12:56:42.055Z` through `2026-10-26T12:56:46.365Z`, on baseline
`e62bc33c`. Its SHA-256 is
`97ba8696acd6b4100b6271d113fa8a82932377a81ac20f180ff2a94c0f1b7418`.
All six API requests succeeded. The complete visible owner container list contains
two public packages, with complete active-version pagination and counts verified against
their individual package-detail responses:

| Package | Version records | Tagged record | Untagged records |
| --- | --- | --- | --- |
| `pxa-webapi` | 3 | `1.1.0`, `1.1`, `latest` all reference the same digest | 2 |
| `pxa-observability-webhook-relay` | 3 | `1.1.0`, `1.1`, `latest` all reference the same digest | 2 |

Both `1.1.0` digests match the earlier exact-tag manifest observations above.
Three version records do not mean three product releases. Untagged records are
retained without assuming their contents, platform or provenance. The canonical
`pvad-webapi` and `pvad-observability-webhook-relay` packages are not listed in
this account's visible inventory; global absence and publishing rights are not
established. These are dated, non-atomic observations, not a durable reservation
or proof that future publication will succeed.
Deleted or otherwise unreadable versions were not inventoried.

The earlier local attempt at
`artifacts/rename/github-packages-2026-10-26/report.json` remains incomplete:
the list endpoint omits version counts, so its collector could not verify counts.
The corrected attempt queries package details rather than treating missing counts
as zero or silently changing the failed report.

Only the explicitly authorized CLI scope changed. No Docker authentication,
package/tag mutation, image pull, environment/branch policy change, workflow run,
deployment or publication was performed. NuGet/npm publisher selection,
publishing permission review, protected-environment metadata/policy decisions,
coordinated workflow activation, fresh drain verification and real staging/release
approval remain open. This closes a supporting inventory task, not a primary
acceptance gate: **90/98 (91.8%) complete, 8/98 (8.2%) open**.

## Workflow Lock Cutover

The four active source definitions now use `pvad-*` concurrency groups. Their
existing `cancel-in-progress` values are unchanged: stable release, preparation
and deployment serialize without cancellation; dry-run supersession still
cancels the earlier run. Deployment remains scoped by target environment.

Before activating these sources on protected branches, pause dispatch/release
triggers and drain all legacy queued, requested, pending, waiting and running
jobs. Switch every applicable branch together, including historical manual refs;
do not dispatch old and new lock generations concurrently. GitHub does not alias
different concurrency group names. Repeat the drain check immediately before
rollout; a point-in-time read is not an ongoing cross-generation lock.

Read-only GitHub inspection on 2026-10-24 found the two existing environments
`pxa-staging` and `pxa-production`, both restricted to protected branches.
Production has a required reviewer; staging has no required reviewer in the
returned configuration. Their names are retained to preserve those bindings.
No secret values were requested. The most recent 100 workflow runs contained no
non-completed runs; this limited snapshot is not an exhaustive drain certificate
and does not authorize publishing or deployment.

Before changing an environment name, inventory its secrets, variables, reviewers,
branch restrictions and any custom protection rules. A new environment must not
silently replace an approved old environment with weaker settings. Environment
secrets cannot be recovered from their read API; coordinate the secret owner.
No remote environment, secret, workflow run or repository setting was changed.

Local preparation follow-up, 2026-10-26: the owner explicitly leaves the
container publisher undecided and authorizes local preparation only. The
[offline environment comparison](../tools/deployment/README.md#offline-protected-environment-comparison)
now checks paired protection metadata, complete/fresh snapshots and drained
workflow generations before generating an optional binding candidate. It
compares secret/variable names, not values, and never authorizes activation.
The default report records missing snapshot evidence. No new remote inspection,
environment migration, publisher selection or publication was performed.

GET-only follow-up on 2026-10-25 reconfirmed the `pxa-staging` branch restriction
and an empty environment-variable list, without requesting secret values. GitHub
also contains a historical `v1.1.0` release from 2026-08-03 with seven complete
PXA-named archives and advertised SHA-256 digests. No archive was downloaded,
renamed or independently hashed by that metadata check. This is not a PVAD rename
release, proof of package-publisher ownership or evidence of customer rollout.
The earlier owner report of no publication remains dated information; reconcile
distribution inventory before external cutover rather than claiming no repository
release exists. The owner-selected [localhost validation](pvad-local-staging-validation.md)
does not authorize changes to GitHub environments, public hosts or registries.

## Concrete Answers Needed

Current-route generated API problem types and runtime telemetry identifiers now
follow the [API/runtime migration](pvad-api-problem-and-health-identities.md).
Legacy routes retain their generated type identifiers; explicit custom types are
not rewritten. New health-name and trace writers require matching custom-query
updates. This source change does not prove URI hosting or authorize database,
key, session, remote telemetry or deployment changes.

Monitoring source identities now use PVAD scrape jobs and alert service labels,
with dual-generation query support. The
[monitoring migration](pvad-monitoring-identity-migration.md) requires an explicit
silence/routing and alert-fingerprint review before live activation. Offline
native tool checks do not approve remote reloads or prove notification delivery.
Grafana UIDs, provider/folder names and installed permissions remain retained.

Grafana provider/folder labels have an
[offline candidate and cutover guide](pvad-grafana-provider-cutover.md).
This does not authorize active provisioning changes or prove permission migration.
Folder inventory, consistent state backup, staging checks and rollback rehearsal
remain required before activation.

Owner confirmation on 2026-10-21 selects `pvade.com` as the target domain.
This records the intended name, not registrar/DNS/TLS ownership evidence.
Owner confirmation also selects `support@pvade.com` as the support address.
NuGet/npm publishers remain unregistered or unselected. The subsequent container
selection below does not prove registry access. Selecting a support address does
not prove a configured mailbox.
Owner confirmation on 2026-10-24 selects `no-reply@pvade.com` as the technical
sender, separately from `support@pvade.com`. The application default and offline
candidate now use that sender; delivery remains disabled. SMTP credentials,
mailbox/provider configuration, SPF/DKIM/DMARC and delivery verification are not
approved or configured by this naming decision.
The C# mail-options fallback is aligned with the same approved sender, including
when the address is omitted from configuration. Explicit customer overrides
remain supported. Protected Admin handbook host and reference links now use the
selected Admin/Operator hosts. Existing Grafana dashboard UIDs, CSRF compatibility
headers and diagnostic IDs remain unchanged; these link corrections do not prove
DNS/TLS availability or widen access to the protected handbook.
The earlier owner report of no publication did not identify a container
publisher; at that checkpoint all three selections remained null.
Publisher ownership, actual sender verification and domain control remain
publication gates; no DNS, account registration or mail setup was executed.

These questions prepare approval; writing an answer does not execute a deployment.

1. Which published PXA versions are installed, and in which environments? Provide
   redacted version/environment records, not customer documents or credentials.
2. What are the approved Company, Documentation, Demo, Account, Admin, Designer
   and API hosts, support address and mail sender? Confirm ownership separately
   from a proposed spelling; no domain availability is assumed.
3. New images now target the owner-selected canonical GHCR repositories described
   below; historical versions keep their original repositories, without dual
   publication. Verify actual registry permissions and identify the release
   approver before publishing. NuGet/npm publisher selection remains open.
4. Confirm `.pvadp` and `.pvads`, old `.pxap`/`.pxas` readers, the stable diagnostic
   policy and the exact compatibility-release window. Identify integrations that
   need the explicit legacy XML envelope rather than the new default root.
5. Who owns each environment's backup, restore, key access, cutover and rollback?
   Provide an evidence location for an actual restore drill; never send key values.
6. Is delivery web-only for now, or must a desktop installer register native file
   associations? If required, specify operating systems and approved artwork.
7. Which staging environment may be tested, who approves the window, and who
   authorizes the major release after that evidence is reviewed?

The 2026-10-25 [OAuth client transition](pvad-oauth-client-transition.md) implements
canonical writers and an explicit maintenance procedure. Disposable-store tests
do not approve applying either migration flag to an installed environment.
Retained service-account IDs remain until normal key rotation, and built-in
counterparts require reviewed metadata/permissions and matching credentials.

The repository [phase review](pvad-rename-phase-review.md) separates historical
reviewed checkpoints from the current uncommitted migration follow-up and final
candidate acceptance. The [current audit](../checklists/Rename-PXA-To-PVAD.md#current-audit-and-progress-2026-10-24)
records the earlier writer findings, closed by the dated OAuth follow-up, and
remaining local validation work, not only external approvals. Neither document turns owner decisions or production evidence
gates into completed code tasks.
