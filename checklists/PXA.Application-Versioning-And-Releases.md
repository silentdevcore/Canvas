# PXA Application Versioning and Releases

## Related Roadmap

Automated change fragments, agent-assisted authoring, release preparation, and
deployment controls are tracked in
[PXA.Automated-Release-Notes.md](PXA.Automated-Release-Notes.md).

## Goal

- [x] Use one Semantic Version for the complete PXA monorepo.
- [x] Treat every merge to `main` as one immutable stable release.
- [x] Use `develop` as the default integration branch.

## P0 - Version Source

- [x] Add root `VERSION` as the single version source with baseline `1.0.0`.
- [x] Apply the root version to all .NET projects through `Directory.Build.props`.
- [x] Synchronize all first-party `package.json` and supported lockfiles.
- [x] Add build commit, build time, and snapshot metadata without changing release precedence.
- [x] Replace the Designer-only release manifest with one PXA release manifest.
- [x] Require a complete release entry for every current application version.

## P0 - Branch and Release Policy

- [x] Publish baseline tag `v1.0.0` on commit `f53975c8c797`.
- [x] Create and publish `develop` from `v1.0.0`.
- [x] Make `develop` the GitHub default branch.
- [x] Protect `develop` and `main` from direct and force pushes.
- [x] Require feature and fix pull requests to target `develop`.
- [x] Allow `main` pull requests only from `develop` or `hotfix/*`.
- [x] Require exactly one `release:major`, `release:minor`, or `release:patch` label.
- [x] Require the version increase to match the release label.
- [x] Require hotfixes to be merged back into `develop`.

## P0 - Automation

- [x] Add tested `current`, `sync`, `check`, `prepare`, and `validate-pr` commands.
- [x] Build and test snapshots on `develop` without publishing stable tags.
- [x] Validate source branch, label, version, manifest, and tag uniqueness before `main`.
- [x] Create immutable `vX.Y.Z` tags and GitHub Releases after a successful `main` merge.
- [x] Produce versioned .NET, frontend, and Docker artifacts.
- [x] Apply container tags `X.Y.Z`, `X.Y`, and `latest` only to stable releases.
- [x] Never amend `main`, move a published tag, or reuse a released version.

## P1 - Application Integration

- [x] Add anonymous `GET /api/pxa/v1/version`.
- [x] Keep API contract version independent from the PXA product version.
- [x] Show the shared product version and commit in Designer and administrative applications.
- [x] Show authenticated release notes in PXA Admin from the shared release manifest.
- [x] Generate Documentation release pages from the shared release manifest.
- [x] Add version and source-revision OCI labels to first-party containers.
- [x] Emit and validate machine-readable build information for WebApi and every shipped frontend.

## Validation

- [x] Test major, minor, patch, invalid, duplicate, and mismatched release cases.
- [x] Verify every package manifest and lockfile matches `VERSION`.
- [x] Verify .NET assembly, package, and informational versions.
- [x] Build and test the WebApi and relevant .NET projects.
- [x] Type-check, test, and build PXA Designer.
- [x] Test and build all PXA websites.
- [x] Verify the `develop` snapshot workflow and uploaded artifacts.
- [x] Verify frontend, WebApi, container, release-workflow, and public version-endpoint consistency against `VERSION`.
- [x] Run a non-mutating `develop` release preview for proposed version `1.1.0` and verify its JSON and Markdown artifacts.
- [x] Complete the stable-release workflow with the first prepared version increase.
- [x] Verify branch settings and required GitHub checks.

## PXA 1.1.0 Verification Record

- [x] Prepare `1.1.0` from nine reviewed release fragments through pull request [#52](https://github.com/silentdevcore/Canvas/pull/52).
- [x] Resolve the historical `v1.0.0` bootstrap through the latest stable tag when `main` does not yet contain `VERSION`.
- [x] Validate the bootstrap correction through pull request [#53](https://github.com/silentdevcore/Canvas/pull/53) and all required CI checks.
- [x] Open the stable pull request [#54](https://github.com/silentdevcore/Canvas/pull/54) automatically with `release:minor`.
- [x] Require human approval before merging the stable release to `main`.
- [x] Publish immutable tag [`v1.1.0`](https://github.com/silentdevcore/Canvas/releases/tag/v1.1.0) on merge commit `30de5ea7d55d009dfc32a378e5bcc08d1f3ae8d1`.
- [x] Complete stable release workflow [run `30822928671`](https://github.com/silentdevcore/Canvas/actions/runs/30822928671).
- [x] Publish seven versioned application archives with SHA-256 digests.
- [x] Build and publish the WebApi and Observability Relay containers and verify the WebApi container version.
- [x] Preserve shared `develop` and `main` history through merge commits for stable releases.
- [x] Keep required reviews, status checks, admin enforcement, and force-push and deletion protection on `main`; disable linear-history enforcement only to permit release merge commits.
- [ ] Deploy `v1.1.0` into a protected production environment and record the deployment result.

## Later Work

- [ ] Publish Alpha and Beta releases from `develop`.
- [ ] Add automatic production deployment after stable release approval.
- [ ] Add package-registry publishing for public PXA SDKs.

## Acceptance Criteria

- [x] One file determines the release version of every PXA component.
- [x] A merge to `main` cannot succeed without exactly one valid version increase.
- [x] A successful `main` merge produces one immutable tag and release.
- [x] `develop` builds are traceable but never presented as stable releases.
- [x] Released versions and API contract versions remain separate concepts.
