# PVAD GitHub Protection and Environment Cutover

This is an operator preparation plan, not permission to change GitHub settings,
publish artifacts or deploy. Technical identity is PVAD; public brand is PVADE.
Historical `pxa-*` names below identify existing remote resources, not new defaults.
The [rename checklist](../checklists/Rename-PXA-To-PVAD.md) remains authoritative.

## Verified Baseline

Seven GET requests on 2026-10-26 observed `silentdevcore/Canvas` between
`2026-10-26T13:29:53.890Z` and `2026-10-26T13:29:58.972Z`. Ignored local evidence:
`artifacts/rename/github-cutover-2026-10-26/report.json`, SHA-256
`23729522fdb00e841ad1792612f006d30d227539537d6ffe8343289aae5a5e81`.
CI sources were fetched at the observed branch commit, verified against their Git
blob hashes, and compared with the local candidate. The snapshot is non-atomic.

| Control | `main` | `develop` |
| --- | --- | --- |
| Observed commit | `30de5ea7d55d009dfc32a378e5bcc08d1f3ae8d1` | `79ff91b65a40d806ce436b59051056d4cce27097` |
| Required contexts | `version-policy`, `dotnet`, `frontend` | Same |
| Required check source | App ID `15368` for each context | Same |
| Up-to-date branch / strict checks | Required | Required |
| Admin enforcement | Enabled | Enabled |
| PR approval | One; dismiss stale; require last-push approval | Same |
| Linear history | Not required | Required |
| Force push / deletion | Disabled / disabled | Disabled / disabled |

The source-level comparison resolves all three contexts on both remote sources
and on the local candidate. It does not establish successful PR execution,
effective organization/repository rulesets, merge enforcement, or deployment.
Rulesets and recent emitted check-run identities were not queried in this audit.

## Required Check Decision

The repeatable offline reviewer is
[`reviewCiChecks`](../tools/configuration/rename-ci-check-review.mjs). It accepts
parsed CI YAML, the observed `requiredStatusChecks` metadata, and an explicit
`main` or `develop` target. It preserves check-source App IDs and returns separate
required/proposed rows, dependencies and blockers. It makes no network calls,
executes no workflow code and never authorizes a protection change. The evidence
collector and `review.mjs` under the ignored directory above record exact inputs
and the remote/local comparison in `ci-review.json`.

| Context | Current remote CI sources | Local candidate | Decision |
| --- | --- | --- | --- |
| `version-policy`, `dotnet`, `frontend` | Present and required | Present | Retain names, strict mode and App ID bindings |
| `dependency-security` | Present, not required | Present | Recommend adding after verified PR runs and owner approval |
| `supply-chain` | Present, not required | Present | Recommend adding after verified PR runs and owner approval |
| `mcp-beta` | Absent | Present | Do not require before source rollout and real emitted-check verification |
| `visual-parity` | Absent | Present | Do not require before source rollout and real emitted-check verification |
| `snapshot` | Conditional push-only artifact job | Same role | Do not use as a PR validation gate |

Recommendation, not approved policy: ultimately require all seven validation
contexts above, retaining the existing three. Adding them is separate from
renaming the displayed workflow or an environment. Do not add `snapshot` as a
substitute for validation, or drop old required checks to unblock a merge.

The reviewer flags missing/ambiguous names, dynamic matrix names, conditional or
tolerated-error jobs, unknown/cyclic dependencies and PR filters. It intentionally
requires manual review even for a condition such as `always()`. Conditions inside
individual steps and test quality require separate review. Static resolution is
not proof that a failure blocks a PR. GitHub treats skipped jobs differently from
workflows skipped by filters; dependent jobs may be skipped after failure. Check
actual positive and negative PR cases before accepting a gate. See
[GitHub required-check guidance](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks).

## Environment Mapping and Policy

| Current remote resource | Proposed resource | Observed protection |
| --- | --- | --- |
| `pxa-staging` | `pvad-staging` | Protected branches only; no required environment reviewer; admin bypass allowed |
| `pxa-production` | `pvad-production` | Protected branches only; one reviewer; self-review allowed; admin bypass allowed |

The complete current listing contains only the two historical names. Target
resources have not been created. These observations are not approval of bypass
or self-review. Reviewer IDs are retained only in the local metadata evidence.

For the rename, preserve exact reviewer IDs/types, timers, branch/tag patterns,
secret/variable inventories and check-source bindings. Do not silently substitute
a reviewer, broaden allowed branches or auto-create an unprotected environment.
The existing [environment comparator](../tools/deployment/README.md#offline-protected-environment-comparison)
rejects any source/target control difference, including a stricter target.

Separately recommend preventing production self-review and disabling production
admin bypass. This is a policy decision, not an incidental rename. If approved,
record and apply the policy deliberately, establish a reviewed matching baseline
for source and target, then rerun the comparator. Do not weaken its equality
checks or claim a mismatched snapshot passed. Staging reviewer/bypass policy and
an independent qualified production reviewer also require explicit decisions.

## Authorized Activation Sequence

Every step below remains pending authorization or real-environment evidence.
The local source changes and this review alone complete none of these steps.

1. **Approve scope.** Name the repository/release operator, reviewer and test
   window. Review effective rulesets, current PR requirements and environment
   policy. Keep package publishing and real deployment permissions separate.
2. **Preserve recovery evidence.** Export the complete protection/lock/workflow
   metadata with commit and source hashes. Record secret and variable names only;
   obtain and verify values through the approved secure channel. A secret name
   or an empty environment list does not establish repository/organization inputs.
3. **Roll out CI source normally.** Use protected, reviewed PRs, first to
   `develop`, then the normal release PR to `main`. Preserve required checks and
   branch history rules; do not push directly or create a release merely for this
   preparation. Run real PR validation at the final candidate revision.
4. **Approve additional checks separately.** Verify exact emitted names, App IDs,
   supported PR events and recent successful runs. Then activate only the
   approved additions, retaining existing checks and strictness. Demonstrate
   that a deliberately failed required job blocks merging and a successful PR
   passes without bypass. Do not assume a manually dispatched run satisfies a PR.
5. **Prepare target environments before references.** Create the two approved
   canonical environments with reviewed controls before a workflow can refer to
   them. Transfer and verify configuration securely. Retain historical resources
   and do not delete secrets. Run the existing comparator on fresh, complete,
   provenance-reviewed source/target metadata; its pass is still not approval.
6. **Drain and coordinate writers.** Stop new release/preparation/deployment
   submissions during the approved window. Inspect all workflow generations and
   refs, including queued/waiting runs, immediately before changing bindings.
   Complete or deliberately cancel old runs; maintain one writer across old/new
   concurrency groups. The earlier drain observation is not a current certificate.
7. **Activate the reviewed candidate.** Bind only validated environments on the
   authorized control branches. Preserve Action pins, permissions, approval
   behavior, serialization and cancellation settings. Hash the exact candidate
   and retain the prior sources. Do not launch both old/new writers together.
8. **Validate staging and recovery.** Verify approval/denial behavior and negative
   cases for unauthorized branches, missing configuration and rejected reviewers.
   Test exact-version artifact selection, failed validation and a rollback with
   original historical images. The current deployment-validation workflow records
   validation evidence; it is not a target deployment adapter or proof of rollout.
9. **Approve production separately.** Require successful real staging, protected
   material recovery and explicit release/deployment approval. Do not overwrite
   old image versions, rename retained evidence, or use `latest` for rollback.

## Abort and Rollback

Abort before activation for missing/unknown metadata, policy drift, insufficient
reviewer access, configuration mismatch, active old-generation runs, absent
immutable artifacts or failing PR checks. Leave existing bindings intact.

After activation, freeze submissions and drain the canonical generation before
restoring the previously hashed workflow/environment references through the
approved protected change path. Keep the historical environment controls and
artifacts intact; do not restore an obsolete weaker branch policy. Revalidate
approvals, exact-version selection and the single-writer invariant. A workflow
reference rollback is not a database or application rollback; those require the
target's separately approved backup/recovery procedure and actual deployment
adapter. Retain both audit trails and record the abort reason and operator.

## Exit Evidence and Remaining Blockers

- Fresh complete, owner-reviewed environment/configuration metadata, matching
  controls, secure value verification and effective-ruleset review.
- Reviewed policy decisions for self-review, bypass, extra checks and reviewers.
- Successful and deliberately failing real PR checks with verified check sources.
- Repeatable fresh drain, protected-branch rollout and tested single-writer rollback.
- Actual staging/deployment/recovery evidence and separate publication approval.
- NuGet/npm publisher selection and canonical package publication permissions.

No environments, branch rules, workflows, tags or published packages were changed
by this preparation. The eight primary rename gates stay open: **90/98 (91.8%)
complete; 8/98 (8.2%) open**. Supporting static checks do not increase that score.
