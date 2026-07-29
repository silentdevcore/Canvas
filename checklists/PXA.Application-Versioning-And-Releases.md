# PXA Application Versioning and Releases

## Goal

- [x] Use one Semantic Version for the complete PXA monorepo.
- [x] Treat every merge to `main` as one immutable stable release.
- [ ] Use `develop` as the default integration branch.

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
- [ ] Make `develop` the GitHub default branch.
- [ ] Protect `develop` and `main` from direct and force pushes.
- [x] Require feature and fix pull requests to target `develop`.
- [x] Allow `main` pull requests only from `develop` or `hotfix/*`.
- [x] Require exactly one `release:major`, `release:minor`, or `release:patch` label.
- [x] Require the version increase to match the release label.
- [ ] Require hotfixes to be merged back into `develop`.

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
- [x] Generate Documentation release pages from the shared release manifest.
- [x] Add version and source-revision OCI labels to first-party containers.

## Validation

- [x] Test major, minor, patch, invalid, duplicate, and mismatched release cases.
- [x] Verify every package manifest and lockfile matches `VERSION`.
- [x] Verify .NET assembly, package, and informational versions.
- [x] Build and test the WebApi and relevant .NET projects.
- [x] Type-check, test, and build PXA Designer.
- [x] Test and build all PXA websites.
- [ ] Dry-run snapshot and stable-release workflows.
- [ ] Verify branch settings and required GitHub checks.

## Later Work

- [ ] Publish Alpha and Beta releases from `develop`.
- [ ] Add automatic production deployment after stable release approval.
- [ ] Add package-registry publishing for public PXA SDKs.

## Acceptance Criteria

- [x] One file determines the release version of every PXA component.
- [ ] A merge to `main` cannot succeed without exactly one valid version increase.
- [x] A successful `main` merge produces one immutable tag and release.
- [x] `develop` builds are traceable but never presented as stable releases.
- [x] Released versions and API contract versions remain separate concepts.
