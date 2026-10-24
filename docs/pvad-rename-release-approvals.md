# PVAD Rename Release Approvals

The decision register below distinguishes confirmed scope from pending release
approval. Local tests are not authorization to publish,
change production infrastructure or certify an existing customer installation.
The [rename tracker](../checklists/Rename-PXA-To-PVAD.md) remains authoritative.

| Area | Required decision or evidence | Responsible party | Status |
| --- | --- | --- | --- |
| Installed product inventory | Owner reports no publication/customer installations; current scope is local development/testing. | Product owner | Confirmed in conversation, 2026-10-21 |
| Public identities | Owner selects pvade.com, support@pvade.com and technical sender no-reply@pvade.com. Package publishers remain unselected; actual infrastructure/mail ownership and delivery are not verified. | Business owner and operations | Domain/support names confirmed 2026-10-21; sender selected 2026-10-24; publisher and deployment gates remain open |
| Compatibility policy | .pvadp/.pvads, stable diagnostic IDs, old API/format readers for one major release. | Product owner | Confirmed in conversation, 2026-10-21 |
| Cutover and rollback | Repository owner accepts responsibility for local development backup, restore and rollout. Production/customer procedures are not approved. | Repository owner (local only) | Local owner confirmed, 2026-10-21; production approval pending |
| Protected material | Verify actual keyring access and retained encrypted payload recovery without exposing or rewriting secrets. | Security and operations | Pending |
| External infrastructure | Verify real DNS, TLS, reverse proxy, CSP, authentication redirects, webhooks and sender configuration. | Infrastructure owners | Pending |
| Native association and artwork | Approve final artwork, installer file associations and target operating systems. | Product owner and packaging | Pending |
| Staging acceptance | Authorize and execute a representative deployed environment with real host boundaries. | Release owner and operations | Pending |
| Release publication | Approve the major version, migration instructions, tags and registry publication. | Release owner | Pending |

Before closing a row, record the approver, UTC date, target environment and a
redacted evidence location. Never place credentials, signing keys, customer
documents or unredacted configuration in this document.

## Retained Workflow Bindings

Review checkpoint: 2026-10-23. The remaining old names in release/deployment
workflows are explicit compatibility or infrastructure bindings, not additional
current product names. There are no confirmed published packages or customer
installations. Publisher accounts remain unselected/unregistered; local source
renaming does not establish ownership or permission to publish.

| Source | Retained identity | Required coordinated action |
| --- | --- | --- |
| `release.yml`, `deployment-validation.yml`, deployment evidence verifier | GHCR `pxa-webapi`, `pxa-observability-webhook-relay` | Approve publisher and target image repositories; change publisher, pull/inspect steps and verifier together; retain immutable historical records |
| `deployment-validation.yml` | `pxa-staging`, `pxa-production` via its environment expression | Inspect actual GitHub configuration, then verify reviewers, secrets, branch restrictions and approval behavior before changing workflow bindings |
| `release.yml` | Old protected-branch runs may still use `pxa-stable-release`; local source uses `pvad-stable-release` | Drain earlier runs and switch every applicable branch/workflow consistently |
| `prepare-release.yml` | Old runs may still use `pxa-release-preparation`; local source uses `pvad-release-preparation` | Drain preparation runs and retain single-writer release preparation |
| `release-dry-run.yml` | Old runs may still use `pxa-release-dry-run`; local source uses `pvad-release-dry-run` | Coordinate old/new run cancellation and lock identity |
| `deployment-validation.yml` | Old runs may still use `pxa-deployment-*`; local source uses `pvad-deployment-*` | Preserve per-environment serialization during the transition |
| `job-binary-cutover.yml` | PXA in the historical worker step name | Keep identifying the real pinned old binary; it is not a PVAD build renamed cosmetically |

These entries do not prove a published package exists. The read-only follow-up
below confirms actual protected environments, but does not change them or run a
workflow. Prior unchanged-workflow comparisons describe earlier checkpoints;
the four local concurrency group names are now explicitly migrated. Job steps,
permissions, action pins and environment bindings remain unchanged.

Before a future image-name switch, explicitly decide how historical versions map
to image repositories. Archive identity and container identity are distinct in
the current transition: deployment accepts both complete archive generations,
but the active workflow publisher and inspector still use retained image names.
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
This is not approval of a publisher, proof of image availability or a completed
remote cutover. See [deployment control](../tools/deployment/README.md#offline-container-workflow-candidate).

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
Package publishers and registry organizations are explicitly not registered or
chosen yet. Selecting a support address does not prove a configured mailbox.
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
The owner again reports no publication; this does not identify a registered
container publisher. All three publisher selections remain null.
Publisher ownership, actual sender verification and domain control remain
publication gates; no DNS, account registration or mail setup was executed.

These questions prepare approval; writing an answer does not execute a deployment.

1. Which published PXA versions are installed, and in which environments? Provide
   redacted version/environment records, not customer documents or credentials.
2. What are the approved Company, Documentation, Demo, Account, Admin, Designer
   and API hosts, support address and mail sender? Confirm ownership separately
   from a proposed spelling; no domain availability is assumed.
3. Should new images retain the existing GHCR `pxa-webapi` and
   `pxa-observability-webhook-relay` repositories, publish under approved PVAD
   repositories, or be dual-published? Identify the owner and access approver.
   Do not change publication destinations until this is explicitly decided.
4. Confirm `.pvadp` and `.pvads`, old `.pxap`/`.pxas` readers, the stable diagnostic
   policy and the exact compatibility-release window. Identify integrations that
   need the explicit legacy XML envelope rather than the new default root.
5. Who owns each environment's backup, restore, key access, cutover and rollback?
   Provide an evidence location for an actual restore drill; never send key values.
6. Is delivery web-only for now, or must a desktop installer register native file
   associations? If required, specify operating systems and approved artwork.
7. Which staging environment may be tested, who approves the window, and who
   authorizes the major release after that evidence is reviewed?

The repository [phase review](pvad-rename-phase-review.md) separates historical
reviewed checkpoints from the current uncommitted migration follow-up and final
candidate acceptance. The [current audit](../checklists/Rename-PXA-To-PVAD.md#current-audit-and-progress-2026-10-24)
also records remaining OAuth/MCP writers and local validation work, not only
external approvals. Neither document turns owner decisions or production evidence
gates into completed code tasks.
