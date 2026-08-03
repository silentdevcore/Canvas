# PXA Automated Release Notes

## Goal

- [x] Generate consistent, customer-facing release notes from structured change records.
- [x] Recommend the correct Semantic Version impact without allowing an AI agent to publish releases autonomously.
- [ ] Keep Designer, Admin, Documentation, GitHub Releases, and deployed artifacts on one PXA version.

## Dependencies and Boundaries

- [x] Keep root `VERSION` as the authoritative PXA product version.
- [x] Keep `product-metadata/pxa-releases.json` as the shared published-release source.
- [x] Preserve the existing `develop` integration and `main` stable-release branch policy.
- [x] Treat deployment as consumption of an immutable release; retries and rollbacks must not increase the version.
- [x] Keep API contract versions independent from the PXA product version.
- [x] Require human approval before any stable release is merged to `main`.

## P0 - Structured Release Fragments

- [x] Add a version-controlled `product-metadata/release-fragments/` directory.
- [x] Define and validate one machine-readable release-fragment schema.
- [x] Require a stable fragment ID, impact, component list, change category, customer-facing summary, and breaking-change flag.
- [x] Support `none`, `patch`, `minor`, and `major` impact values.
- [x] Support Added, Improved, Fixed, Security, Deprecated, and Breaking categories.
- [x] Maintain a controlled component list for Designer, API, Admin, Account, Documentation, Migration, and other first-party applications.
- [x] Require a reason when a pull request uses `none`, such as tests, CI, documentation maintenance, or internal refactoring.
- [x] Allow optional feature IDs and Documentation links.
- [x] Reject duplicate IDs, placeholders, empty summaries, unknown components, and invalid links.
- [x] Require every relevant pull request to `develop` to add or update a release fragment.

## P0 - `pxa-release-author` Skill

- [x] Create a reusable Codex skill named `pxa-release-author`.
- [x] Make the skill compare the working branch with its `develop` merge base.
- [x] Detect affected PXA components from the changed projects and files.
- [x] Inspect public API, database, configuration, UI, and compatibility changes.
- [x] Recommend `none`, `patch`, `minor`, or `major` with a concise explanation.
- [x] Generate or update the structured release fragment in customer-facing English.
- [x] Link known feature IDs and relevant Documentation pages.
- [x] Warn when a change needs migration instructions, documentation, or a breaking-change explanation.
- [x] Prevent the skill from creating tags, merging branches, publishing releases, or starting production deployments.
- [x] Document example prompts for creating, reviewing, and correcting a release fragment.

## P0 - Deterministic Validation and Aggregation

- [x] Extend the existing versioning tool with fragment validation and aggregation commands.
- [x] Determine the release impact from the highest impact among all pending fragments.
- [x] Require explicit confirmation for a Major release.
- [x] Group aggregated changes by category and affected component.
- [x] Generate a complete draft entry for `product-metadata/pxa-releases.json`.
- [x] Generate GitHub Release Markdown from the same aggregated data.
- [x] Consume or archive fragments only through the reviewed release-preparation change.
- [x] Fail validation when pending fragments are not represented by the prepared release.
- [x] Keep aggregation deterministic and independent from AI availability.
- [x] Use the agent only to improve wording and summaries, never to calculate or enforce the final version.

## P1 - Pull Request Integration

- [x] Add release impact, customer-visible summary, and Documentation fields to the pull request template.
- [x] Validate release fragments for every pull request targeting `develop`.
- [x] Report missing or invalid fragments as an actionable CI failure.
- [x] Synchronize exactly one `impact:none`, `impact:patch`, `impact:minor`, or `impact:major` label from validated fragment data without executing pull-request code with a write token.
- [x] Keep `release:patch`, `release:minor`, and `release:major` labels reserved for stable pull requests to `main`.
- [x] Prevent commit messages from becoming the authoritative release-note source.
- [x] Prevent internal ticket IDs, secrets, customer data, and unreviewed security details from entering public release notes.

## P1 - Release Preparation

- [x] Add a manually triggered GitHub workflow named `Prepare PXA Release`.
- [x] Run the workflow from the current protected `develop` branch.
- [x] Validate all pending fragments before preparing the release.
- [x] Calculate the next version from the current stable tag and aggregated impact.
- [x] Run the existing version synchronization for .NET, npm, lockfiles, containers, and product metadata.
- [x] Create a release-preparation pull request targeting `develop`.
- [x] Require a human to review the generated title, summary, categories, components, and Documentation links.
- [x] After preparation is merged, open or update the `develop` to `main` stable-release pull request.
- [x] Apply exactly one matching `release:major`, `release:minor`, or `release:patch` label.
- [x] Block the stable pull request when new unaggregated fragments appear on `develop`.
- [x] Add a non-mutating release dry run that uses the same calculation as release preparation.
- [x] Publish JSON and Markdown dry-run reports as traceable GitHub workflow artifacts.

## P1 - Publishing and Deployment

- [x] Reuse the existing stable-release workflow after the approved merge to `main`.
- [x] Create one immutable `vX.Y.Z` tag and GitHub Release.
- [x] Build all release artifacts and containers from the tagged commit.
- [x] Publish versioned container tags before updating stable aliases.
- [x] Display the same release entry in Designer, authenticated Admin, and public Documentation.
- [x] Keep release announcements available in the Designer notification center after they are read.
- [ ] Deploy only successfully built and validated release artifacts.
- [ ] Allow deployment retries and rollbacks without changing `VERSION`, tags, or release notes.
- [ ] Record the deployed version, environment, commit, workflow run, and deployment result.

## Security and Reliability

- [x] Grant release workflows only the minimum GitHub permissions they require.
- [x] Protect release environments with approval rules and concurrency controls.
- [x] Never expose repository, package, deployment, or signing secrets to the release-authoring agent.
- [x] Sanitize generated Markdown and validate all public links.
- [x] Make published versions and tags immutable.
- [x] Fail closed when fragment validation, version synchronization, build, or release-note generation fails.
- [ ] Keep a complete audit trail for preparation, approval, publication, deployment, retry, and rollback.

## Testing

- [x] Test valid `none`, `patch`, `minor`, and `major` fragments.
- [x] Test invalid schemas, duplicate IDs, unknown components, placeholders, unsafe links, and missing reasons.
- [x] Test internal ticket, credential, customer-data, and security-review rejection across fragments and published releases.
- [x] Test highest-impact aggregation across multiple components and categories.
- [x] Test Major-release confirmation and mismatched stable-release labels.
- [x] Test that new pending fragments block an already prepared stable release.
- [x] Test deterministic output by aggregating the same fragments repeatedly.
- [x] Test the skill against representative fixes, features, breaking API changes, migrations, documentation changes, and internal refactoring.
- [x] Test that Designer, Admin, Documentation, GitHub Release, artifacts, containers, and the version endpoint report the same version.
- [x] Dry-run Patch, Minor, and Major releases without publishing.
- [ ] Test successful deployment, failed deployment, retry, and rollback without an additional version increase.
- [x] Run `git diff --check` and all existing version-tool tests.

## Acceptance Criteria

- [x] Every customer-relevant change has a reviewed structured release fragment.
- [x] CI can calculate the required version impact without interpreting free-form commit messages.
- [x] AI-generated text cannot independently publish, tag, merge, or deploy a release.
- [x] A stable release cannot omit pending customer-relevant changes.
- [x] One approved merge to `main` produces one immutable version and one shared release-note entry.
- [ ] Designer, Admin, Documentation, GitHub Releases, and deployed artifacts show consistent release information.
- [ ] Deployment retries and rollbacks never create artificial product versions.

## Deferred Rollout

- [x] Record pull request `#24` as closed without merge and superseded by the release-automation changes subsequently merged into `develop`.
- [x] Synchronize release validation with `develop` commit `30929c01c963b792db34fc9134c4fc5c35f7aed5`.
- [x] Run the GitHub `PXA Release Dry Run` workflow from `develop` ([run `30802436710`](https://github.com/silentdevcore/Canvas/actions/runs/30802436710)).
- [x] Verify proposed version `1.1.0`, Minor impact, five fragments, ten components, customer summaries, JSON output, and Markdown output.
- [x] Confirm that the GitHub dry run leaves `VERSION` at `1.0.0`, `develop` unchanged, `v1.0.0` as the latest tag, and creates no GitHub Release or deployment.
- [x] Run `Prepare PXA Release` only after explicit approval and complete the reviewed `v1.1.0` stable release.
- [ ] Implement production deployment approvals, audit records, retry behavior, and rollback validation in a later phase.

## PXA 1.1.0 Verification Record

- [x] Aggregate nine pending fragments into one deterministic Minor release.
- [x] Review and merge release preparation pull request [#52](https://github.com/silentdevcore/Canvas/pull/52).
- [x] Correct and verify first-release bootstrap handling through pull request [#53](https://github.com/silentdevcore/Canvas/pull/53).
- [x] Automatically create stable release pull request [#54](https://github.com/silentdevcore/Canvas/pull/54) with the matching `release:minor` label.
- [x] Publish [`v1.1.0`](https://github.com/silentdevcore/Canvas/releases/tag/v1.1.0) from the exact `main` merge commit after human approval.
- [x] Verify stable workflow [run `30822928671`](https://github.com/silentdevcore/Canvas/actions/runs/30822928671), seven release assets, container publication, and immutable tag placement.
- [x] Confirm that release publication does not claim or perform a protected production-environment deployment.
- [ ] Validate deployment approval, audit, retry, and rollback behavior in the later deployment phase.

## Deployment Control Foundation

- [x] Add protected `pxa-staging` and approval-gated `pxa-production` GitHub Environments.
- [x] Serialize deployment validation independently for each environment.
- [x] Require explicit operator confirmation and validate deploy, retry, and rollback inputs before downloading artifacts.
- [x] Resolve only an existing immutable stable `vX.Y.Z` tag without changing `VERSION` or release notes.
- [x] Verify the exact tag commit, shared release metadata, seven release archives, SHA-256 digests, and published container tags.
- [x] Produce machine-readable validation evidence with version, environment, operation, commit, actor, workflow run, source run, artifacts, and container digests.
- [x] Keep evidence status at `validated` and state that no target adapter executed.
- [x] Test successful, failed, retry, and rollback evidence without additional version changes.
- [x] Complete validation-only staging run [`30848453307`](https://github.com/silentdevcore/Canvas/actions/runs/30848453307) for immutable release `v1.1.0`.
- [x] Verify all seven release archives and both versioned container digests in the staging preflight.
- [x] Retain protected staging evidence with status `validated`, adapter `unconfigured`, and no target mutation.
- [ ] Select and implement the first Cloud or On-Premise target adapter.
- [ ] Add target health checks and record final `succeeded` or `failed` deployment status.
- [ ] Validate real deployment retry and rollback against the selected target.

## Later Work

- [ ] Complete target deployment, final health status, retry, rollback, and production audit after selecting the first deployment adapter.
- [ ] Add scheduled release-candidate preparation after the manual workflow is proven.
- [ ] Add Alpha and Beta fragment aggregation for prereleases from `develop`.
- [ ] Add localized release-note content with English fallback.
- [ ] Add PXA Admin authoring and approval tools for release summaries.
- [ ] Add release-note generation for public SDK package registries.
