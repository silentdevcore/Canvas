# PVAD Rename: Internal Major Release Dry Run

## Decision And Scope

Recorded on 2026-10-30 on `chore/pvad-release-readiness`, based on
`d43abbf465a6c04268944589afa6a733bc47b65a`, plus the local documentation,
regression-test and pending-fragment corrections described below.

**The technical rename phase is closed. The internal Major release rehearsal
passed. This is not authorization or evidence of publication or deployment.**
The [primary checklist](../checklists/PVAD.Rename-Plan.md) remains
**90/98 complete (91.8%), with eight release/operational gates open (8.2%)**.
Do not create another cosmetic rename batch merely to increase this score.

[PR 213](https://github.com/silentdevcore/Canvas/pull/213) merged normally to
`develop` as the source commit above after all seven required checks succeeded.
Its [complete PR workflow](https://github.com/silentdevcore/Canvas/actions/runs/41031642865)
passed; the PR-only snapshot skip is intentional. These are PR results, not a
claim about a later post-merge workflow or a deployed environment.

## Rehearsal Results

| Check | Observed result |
| --- | --- |
| Current repository version | 1.1.0; unchanged |
| Proposed release | 2.0.0; Major impact, explicit confirmation required |
| Pending fragments | 183, including this internal-only rehearsal fragment |
| Public changes | 139 fragments; 44 internal-only fragments excluded from public changes |
| Public categories | 25 added, 26 improved, 66 fixed, 13 reviewed security, 9 breaking |
| Affected components | All 14 registered components |
| Frontend/MCP versions | Seven package manifests and three lockfiles match 1.1.0 |
| .NET version evaluation | All 116 tracked projects report Version, PackageVersion, AssemblyVersion, FileVersion and InformationalVersion 1.1.0 |
| Deterministic preview | Repeated CLI JSON/Markdown previews match; CLI and library preview agree |
| Actual preparation rehearsal | Passed only in a temporary metadata copy; package/lock/container defaults synchronize to 2.0.0 and fragments are consumed there |
| Release confirmation | Temporary preparation without explicit Major confirmation is rejected |
| Release PR contract | Temporary 2.0.0 candidate accepts develop plus release:major; four missing/multiple/wrong-label or wrong-source cases are rejected |
| References | 45 static file/directory targets resolve; linked guide downloads exist in the Vite output; three focus routes are retained separately |
| Preservation | SHA-256 before/after checks match for all 316 monitored release/project files; HEAD and local Git tags are unchanged by the rehearsal |

The date in the preview is a deterministic sample input, not a promised release
date. The preview's Stable channel and Released label do not mean it was shipped.
The internal preview includes repository references; it is not a public-site
deployment. Fragment counts are this local checkpoint, not permanent contracts.

MSBuild evaluation is not a new compile, NuGet pack, installation or runtime test.
Container version synchronization was rehearsed on copied metadata, not built or
published as a 2.0.0 image. No NuGet/npm publisher ownership or availability is
inferred from package names. Existing security-review attestations were validated,
not newly granted by this rehearsal.

## Corrected Guidance And Pending Link

- Fresh API storage uses logical `pvad-api-data`. Existing installations require
  the explicit legacy overlay and verified `PVAD_LEGACY_API_VOLUME`; a new logical
  name must not silently select empty replacement storage.
- Canonical licensing mounts use `pvad_license_private_key` and
  `pvad_license_public_key`, preserving configured source files and signing bytes.
- Stable diagnostics and cryptographic compatibility purposes remain intentional.
  There is no rename-specific retained-job/object Data Protection envelope to
  migrate. Encryption at rest must not be inferred or invented as rename work.
- The first source-only reference assertion rejected an HTML guide target in an
  unpublished fragment. DocFX has a matching Markdown source, but the Vite build
  packages only the Markdown download. The pending fragment now links to that
  actual download. Published release history remains untouched.

See the corrected [migration guide](pvad-rename-migration-notes.md),
[license/job boundaries](pvad-license-and-job-compatibility.md), and
[infrastructure binding guide](pvad-infrastructure-mapping.md).

## Evidence And Reproduction

Generated, ignored local evidence is in
`artifacts/rename/release-dry-run-20261030/`:

- `release.json`: structured candidate preview; SHA-256
  `e035446f671b87611cf4fe7b5376971b080fc4fa4f661803a725ef998cc88702`.
- `RELEASE_NOTES.md`: readable preview; SHA-256
  `088d221ae1b2d6afb79fba1e8843301f76ff355b6234a9b3c18edb6e2d71688c`.
- `evidence.json`: source commit, temporary preparation location, checksums,
  references and package inventory.
- `dotnet-versions.json`: all 116 property evaluations.
- `preliminary-link-review.json`: initial rejected reference and its correction.

The preview can be reproduced without preparing a release:

```bash
node tools/versioning/pvad-version.mjs check
node tools/versioning/pvad-version.mjs fragments
node tools/versioning/pvad-version.mjs prepare-fragments --dry-run \
  --title "PVADE 2.0.0" --date 2026-10-30 \
  --summary "PVADE introduces PVAD libraries and editable project formats, with expanded document processing, migration workflows and compatibility guidance." \
  --format markdown
```

Local validation: 47 version/release-author/build-metadata/documentation contracts
and 59 Documentation tests passed; the Documentation production build passed.
Its existing Vite native-config-loader advisory remains visible and was not
suppressed. A focused regression covers fresh/retained storage guidance and
portable pending release-guide links. Whole-Solution compilation, runtime
acceptance, DocFX generation, browser checks and image builds were not repeated
for this documentation-only follow-up; prior CI evidence retains its own scope.

## Remaining Release And Operational Gates

| Primary gate | Evidence still required |
| --- | --- |
| Public identities | NuGet/npm publishers, publication rights, real domain/mail verification; confirmed names are not requested again |
| Protected material | Actual keyring access, signing-key recovery, retained protected consumers and paired storage/rollback evidence |
| Infrastructure identity activation | Protected environment cutover, coordinated workflow generations and live monitoring migration |
| Host and callback alignment | Real HTTPS hosts, proxy, CORS/CSP, authentication callbacks, sender and webhooks |
| Authorized external cutover | Explicit target-environment and operator authorization before changing live resources |
| Deployed staging | Representative multi-host acceptance; localhost results remain separate |
| Major release | Approved release preparation, migration guide, develop-to-main PR, verified main checks, immutable tags and authorized publication |
| Owners and rollback | Named production operators, genuinely independent reviewer, tested cutover/restore/rollback evidence |

These are the existing eight gates, not eight new tasks or a new score.
Use the [approval register](pvad-rename-release-approvals.md) and
[GitHub cutover plan](pvad-github-cutover-plan.md) for their dependencies.
The next phase is release/deployment preparation when its owners, access and
environment are available; source rename work resumes only for a concrete defect.
`VERSION`, published manifests, tags, registries, remote controls and deployments
were not changed. No commit or push is included in this local rehearsal.
