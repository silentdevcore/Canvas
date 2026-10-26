# PVAD Deployment Control

PVAD deployment validation consumes an existing immutable stable release. It
never calculates a version, changes `VERSION`, creates a tag, or rebuilds a
release artifact.

The `PVADE Deployment Validation` workflow verifies:

- the requested stable `vX.Y.Z` tag and exact checked-out commit;
- the shared stable release-manifest entry;
- all seven required GitHub Release archives and SHA-256 digests;
- published WebApi and Observability Relay container tags and digests;
- environment, operation, actor, workflow run, and optional source run.

The protected environment job creates a machine-readable deployment evidence
artifact with status `validated`. No Cloud or On-Premise target adapter is
configured yet, so the workflow must not be interpreted as a successful target
deployment. A later adapter will consume the same verified release and update
the evidence to `succeeded` or `failed` after target health checks.

Retry and rollback are new workflow runs against an existing stable version.
They require the source workflow run ID and never change version metadata or
release notes.

New archive sets use `pvad-*` names. Complete historical `pxa-*` archive sets
remain valid inputs for immutable release verification; names and digests are
not rewritten. Mixed, duplicate or incomplete sets are rejected. New evidence
writes `product: PVAD` and records the artifact identity separately. The old
`pxa-deployment.mjs` command delegates to `pvad-deployment.mjs` for compatibility.
New release workflow sources now target `ghcr.io/silentdevcore/pvad-webapi` and
`ghcr.io/silentdevcore/pvad-observability-webhook-relay`, selected by the owner
on 2026-10-26. The two workflows reject a repository owner outside the selected
namespace before registry operations. No image was published by this source
change, and registry permissions or package availability are not certified.
Protected GitHub environment names remain unchanged pending their separate
authorized infrastructure cutover.
The local evidence verifier additionally accepts a complete PVAD image pair and
records `containerIdentity` separately from archive identity. It requires exact
immutable version tags and the GHCR namespace matching the supplied repository
owner. Mixed generations, duplicate roles, other publishers and mutable tags are
rejected. Existing records, archives, image strings and digests are never relabeled.
The [workflow binding register](../../docs/pvad-rename-release-approvals.md#retained-workflow-bindings)
lists each retained identity and the coordinated changes needed. Source comments
do not certify remote configuration, approve publication or execute a cutover.

Repository environments:

- `pxa-staging` accepts deployment validation only from protected branches.
- `pxa-production` additionally requires approval by the configured release
  operator before the environment job can create evidence.

Both environments are serialized independently by workflow concurrency. The
workflow requires explicit confirmation and fails before release downloads when
the operation, version, environment, or source run is invalid.

## Offline Container Workflow Candidate

Generate a candidate from the current active workflow definitions:

```bash
node tools/configuration/rename-container-workflows.mjs
node --test tools/configuration/rename-container-workflows.test.mjs tools/deployment/pvad-deployment.test.mjs
```

Output under `artifacts/rename/container-workflows/` is ignored local preparation:

- `release.candidate.yml`: both image producers and pull/metadata checks use
  canonical repository leaf names and the configured owner-selected publisher.
  Already migrated sources are preserved; older input models remain supported
  as offline candidates without inventing a confirmed publisher.
- `deployment-validation.candidate.yml`: an explicit image-family choice selects
  the original PXA or new PVAD names for the requested immutable version. It
  defaults to PXA for historical releases; new PVAD images require selecting PVAD.
  Archive names are never used to guess the image family.
- `report.json`: `deploymentAuthorized: false` and outstanding approval gates.

For deployment/retry/rollback, select the exact `container_identity` explicitly:
`PVAD` for new canonical images and `PXA` for historical images. The conservative
default stays `PXA` for historical requests; archive names never select the image
family automatically. Missing or invalid identities fail before inspection.
Do not republish old versions under new image names or use `latest` for rollback.

The generator performs no Docker, GitHub, registry or deployment operation and
does not edit `.github/workflows`. It accepts no apply/publish flags and refuses
unexpected producer/consumer structure. Local tests parse candidate YAML, check
Bash syntax and simulate image selection; they do not verify actual image
availability or registry digests.

The report binds both source workflows and generated candidates to SHA-256
hashes. Missing or invalid evidence cannot leave a previous successful report or
environment candidate as the current result. Source-only generation explicitly
reports `protected-environment-snapshot-required`; it does not manufacture a
matching remote environment.

Before release activation, verify access to the selected publisher/repositories,
record the actual image family and digests per immutable version, and validate
registry access plus a non-production run. The local release/deployment sources
are updated together; this does not activate them on protected branches.
Keep existing concurrency groups and protected environment
bindings until their own coordinated migration is approved. Do not republish,
retag, overwrite or invent historical images to make a rollback pass.

## Offline Protected Environment Comparison

The earlier local-only preparation checkpoint on 2026-10-26 left the publisher
unselected. The subsequent owner decision selects `ghcr.io/silentdevcore` for
local container workflow changes, not publication. Environment bindings remain
unchanged. No account, repository, secret or environment is created or migrated
by the commands below.

An operator can supply a reviewed, metadata-only snapshot later:

```bash
node tools/configuration/rename-container-workflows.mjs \
  --environment-snapshot artifacts/rename/github-environments.snapshot.json
```

The input is a normalized local JSON export, not a credentials file. Keep it in
ignored artifacts. Do not include secret/variable values, tokens, private keys,
reviewer contact details or customer data. The generator makes no network calls;
the operator must establish the provenance and completeness of the export.

Required root fields:

| Field | Contract |
| --- | --- |
| `schemaVersion` | `1` |
| `repository` | The actual `owner/repository`, matched case-insensitively to the local GitHub `origin`; this is not publisher approval |
| `capturedAt` | UTC ISO timestamp ending in `Z`; no future timestamp and at most 24 hours old |
| `paginationComplete` | Boolean declaration that all relevant environment, policy, metadata and workflow-run pages were reviewed; `false` blocks comparison |
| `workflowRuns` | Every relevant run across workflow generations/refs, normalized to `{ "id": 123, "status": "completed" }`; any non-completed or unknown status blocks comparison |
| `environments` | Snapshots for `pxa-staging`, `pvad-staging`, `pxa-production` and `pvad-production`; missing source/target blocks comparison |

Each environment has exactly these fields:

| Field | Contract |
| --- | --- |
| `name` | One of the four names above |
| `canAdminsBypass` | Explicit boolean from the environment's bypass setting |
| `protectionRules` | GitHub-style `required_reviewers`, `wait_timer` and `branch_policy` rules; unsupported rules/fields require manual review |
| `deploymentBranchPolicy` | `null`, or explicit `protected_branches` / `custom_branch_policies` booleans with exactly one enabled |
| `deploymentBranchPolicies` | All custom policies as `{ "name": "develop", "type": "branch" }` or tag policies; must be empty outside custom mode and nonempty in custom mode |
| `secretNames` | Unique names only, never secret values |
| `variableNames` | Unique names only, never variable values |

A reviewer rule retains its `type`, optional rule `id`, `prevent_self_review`
boolean and `reviewers`, for example
`[{ "type": "User", "reviewer": { "id": 123 } }]`. Team reviewers use
`"type": "Team"`. A timer rule retains `type`, optional `id` and `wait_timer`
minutes. A branch-policy rule retains `type` and optional `id`. Strip unrelated
GitHub response metadata before supplying the normalized input; do not silently
drop unfamiliar protection controls. Unknown fields/rules fail preparation.

The comparison preserves exact reviewer IDs/types, self-review protection,
wait time, administrator bypass, protected/custom-branch mode, branch/tag patterns
and secret/variable name inventories. Different array order and per-environment
rule IDs do not count as changed protections. It rejects weakened controls and
also requires review for stronger-but-different controls rather than assuming
that a change was intentional. Reports expose control hashes and blocker codes,
not reviewer IDs, secret names or variable names.

Only a fresh, complete matching snapshot produces the additional
`deployment-validation.environments.candidate.yml`. That file differs from the
ordinary container candidate only in the protected environment binding. The
ordinary candidate always retains the existing binding. The report still says
`deploymentAuthorized: false`, `configurationValuesVerified: false` and
`operatorApprovalRequired: true`, even when `protectionComparisonPassed: true`.

Matching names do not prove matching secret/variable values, actual access,
protection enforcement, an authenticated snapshot or a race-free workflow drain.
An owner must transfer and verify values securely, review branch restrictions
and custom protection apps, repeat the complete drain check immediately before
activation and approve rollback. No candidate can be applied or published by
this tool. Earlier snapshots and local synthetic tests never close a remote
cutover gate.
