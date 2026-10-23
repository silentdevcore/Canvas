# PVAD Rename Release Approvals

The decision register below distinguishes confirmed scope from pending release
approval. Local tests are not authorization to publish,
change production infrastructure or certify an existing customer installation.
The [rename tracker](../checklists/Rename-PXA-To-PVAD.md) remains authoritative.

| Area | Required decision or evidence | Responsible party | Status |
| --- | --- | --- | --- |
| Installed product inventory | Owner reports no publication/customer installations; current scope is local development/testing. | Product owner | Confirmed in conversation, 2026-10-21 |
| Public identities | Owner selects pvade.com and support@pvade.com. Package publishers/registries explicitly unregistered; mail sender and infrastructure ownership not approved. | Business owner and operations | Domain/support names confirmed 2026-10-21; publisher and deployment gates remain open |
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
| `release.yml` | `pxa-stable-release` concurrency group | Drain earlier runs and switch every applicable branch/workflow consistently |
| `prepare-release.yml` | `pxa-release-preparation` concurrency group | Drain preparation runs and retain single-writer release preparation |
| `release-dry-run.yml` | `pxa-release-dry-run` concurrency group | Coordinate old/new run cancellation and lock identity |
| `deployment-validation.yml` | `pxa-deployment-*` concurrency groups | Preserve per-environment serialization during the transition |
| `job-binary-cutover.yml` | PXA in the historical worker step name | Keep identifying the real pinned old binary; it is not a PVAD build renamed cosmetically |

These entries describe source bindings, not proof that a remote environment or
package exists. No GitHub workflow, registry publication or remote configuration
was triggered during review. Source comments identify the retained boundaries;
their parsed workflow definitions remain unchanged. Local checks inspect YAML,
job dependencies, action pins, filesystem references and Bash syntax only.

Before a future image-name switch, explicitly decide how historical versions map
to image repositories. Archive identity and container identity are distinct in
the current transition: deployment accepts both complete archive generations,
but the current publisher and container evidence verifier still use the retained
image names. Never silently relabel, overwrite or invent an immutable historical
container digest. This gate remains open until authorized and tested.

## Concrete Answers Needed

Owner confirmation on 2026-10-21 selects `pvade.com` as the target domain.
This records the intended name, not registrar/DNS/TLS ownership evidence.
Owner confirmation also selects `support@pvade.com` as the support address.
Package publishers and registry organizations are explicitly not registered or
chosen yet. Selecting a support address does not prove a configured mailbox.
Publisher ownership, mail sender configuration and actual domain control remain
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

The repository [phase review](pvad-rename-phase-review.md) separates completed
local checks from pending exhaustive semantic/staged review. Neither document
turns external owner or production evidence gates into completed code tasks.
