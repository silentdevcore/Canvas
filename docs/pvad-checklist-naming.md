# PVAD Checklist Naming Review

## Current Names

- **PVADE** is the market, platform, and Designer name.
- **PVAD** is the technical identity for projects, namespaces, APIs, clients,
  tooling, and configuration.
- **PVADP** / `.pvadp` and **PVADS** / `.pvads` identify the current native
  PDF and Spreadsheet project formats.

The application has one general naming-history notice on Company
`/about.html#rename-history`. Repeated naming-history banners have been removed
from repository checklists and retained planning notes.

## Review Scope (2026-10-30)

At the owner's request, editorial product names are normalized in ordinary
checklist prose, completed-task descriptions, assessments, and code examples.
Technical names use PVAD; the market and Designer name is PVADE. The original
wording remains available through Git history. Checkbox states, assessment dates,
scores, limitations, test results, and compatibility values are not reapproved
or changed by this editorial update.

| Content | Treatment |
| --- | --- |
| Current headings, descriptions, technical names and open tasks | Use PVAD/PVADE. |
| Future SDK packages, client classes and publication instructions | Use proposed PVAD names; verify publisher ownership and availability before publication. |
| Current native-format introduction | Use `.pvadp` / `.pvads` and link to the current contract. |
| Ordinary checked-task descriptions and sample output | Normalize names without changing completion status or claiming a new test run. |
| Recorded test commands/counts and execution results | Retain original bytes as evidence of the actual earlier run. |
| Assessments | Normalize editorial names; retain the assessment date, scores, conclusions and limitations. |
| Original branch reviews, rename audit and cutover evidence | Retain original wording; it does not describe a newly reviewed product state. |
| Existing API aliases, diagnostics, cryptographic purposes, schema IDs and stored keys | Keep exact machine values and document their compatibility purpose. |
| Published Legal versions, accepted hashes, signed corpora and release history | Do not rewrite evidence or imply renewed approval. |

Current usage belongs in the main documentation, not in an old completed task.
In particular, [native project guidance](native-designer-project-formats.md),
[code workspace guidance](code-designer-workspace.md), and the
[architecture](../ARCHITECTURE.md) describe current behavior. The
[documentation audit](../checklists/Documentation-Audit.md) indexes the boundary.

## Future Package And Publisher Decisions

The [SDK roadmap](../checklists/PVAD.SDK-Roadmap.md) now targets canonical
`/api/pvad/v1` routes. Existing machine diagnostic identifiers remain stable;
their PVAD display aliases do not rename the API contract.

| SDK | Naming proposal, not a publication claim |
| --- | --- |
| Java | `com.pvade:pvad-java`, `com.pvade.pvad`, `PvadClient` |
| TypeScript / JavaScript | One `@pvade/pvad` package and `PvadClient` |
| Python | `pvade-pvad`, import `pvad`, `PvadClient` / `AsyncPvadClient` |
| C# REST | `PVAD.Client`, distinct from embedded engine libraries |
| Go | `pvad-go`; final publisher-owned module path is still undecided |
| PHP | `pvad` package; final Packagist vendor is still undecided |
| PowerShell | `PVAD.Client` module |

No SDK publisher or package registration is established by the chosen domain
`pvade.com`. Container publishers also require explicit selection and validation.
The future Admin image is proposed as `pvad-admin`, not a claimed registry path.
Account and Admin Cloud host proposals use `account.pvade.com` and
`admin.pvade.com`; these statements do not assert DNS, TLS, or deployment readiness.

## Why A Repository Search Still Finds The Previous Name

- [The earlier rename record](../checklists/PVAD.Legacy-Rename-Archive.md) documents a previous
  generation. Its current filename is neutral; its original body remains a historical record, not the current rename plan.
- [The dependency PR review](../checklists/PVAD.Dependency-PR-Review-2026-10-26.md)
  contains dated branch names, source paths, diagnostics, and completed CI evidence.
- Competitive-readiness, pricing, and valuation assessments retain the product
  capabilities and conclusions recorded at their assessment date; normalizing
  product names does not revalidate their claims.
- Recorded execution evidence and explicitly retained baseline sections describe
  work actually done under the earlier identity. Their names are not instructions
  to recreate it. Ordinary task descriptions outside those records use current names.
- [Compatibility exceptions](pvad-rename-exceptions.md) and the exact-path/hash
  [review ledger](pvad-rename-reviewed-exceptions.json) explain retained protocol,
  storage, fixture, audit, infrastructure, and historical references.

These are bounded exceptions, not permission to use the previous brand for new
features or open plans. New documentation must use the current names unless it
explicitly documents an existing compatibility contract or original evidence.

## Validation Boundary

The documentation contracts check current headings, open tasks, future SDK
naming, publisher caveats, internal links, canonical sample output, preserved
diagnostics, and the absence of repeated naming-history banners. The historical
path-migration map records the scope of its original preservation checkpoint;
it does not require later editorial wording to remain frozen.
This change does not alter application code, database records, package versions,
published release entries, signed material, or runtime compatibility readers.
No build, deployment, artwork, legal, publisher, or release approval is inferred
from editing a checklist. The broader rename release gates remain separate.

### Previous Validation Checkpoint

The preceding, narrower change against commit `69e352e3` covered 113 checklists.
Its results below belong to that checkpoint, before the current authorization to
normalize completed-task prose and ordinary examples:

- All 8,153 checkbox states, 6,227 checked task records, and 57 fenced examples
  compare unchanged with that baseline.
- All 11 newly added or corrected relative checklist links resolve.
- All 49 naming, residual-review, versioning, and release-author tests pass.
- All 59 Documentation website contract tests pass.
- The Documentation production build passes; its existing Vite configuration
  loader warnings remain visible.
- Release-fragment and synchronized-version checks pass; `VERSION` remains
  `1.1.0`. These metadata checks are not production release readiness.

Original links inside retained execution records may point to source paths that
no longer exist. They are evidence of the original implementation, not current
navigation targets. Current source links are updated when an actual canonical
target exists; this does not claim that every archived link is usable.

### Current Editorial Validation

Against commit `73abda72`, 155 repeated banners were removed. The 157 changed
Markdown files preserve all 9,910 checkbox states, 337 explicit recorded-result
lines, and six original audit/review bodies apart from banner removal. Retained
diagnostic and contract tokens remain present. Seven new or corrected relative
links resolve; no existing Markdown links required a changed heading anchor.

All 50 naming, residual-review, versioning and release-author tests and all 59
Documentation tests pass. The Documentation production build passes with its
existing Vite loader warnings. The path-migration helper has an isolated fixture
test proving that it does not add banners or rename untracked local notes.
Version synchronization and fragment validation remain separate from release
approval; `VERSION` is unchanged at `1.1.0`.

## Editorial Follow-Up (2026-10-30)

The three remaining legacy checklist filenames now use PVAD names, with current
Markdown links and the path-migration map updated together. The eleven internal
PDF specialty milestone IDs use `PVAD-PDF-*`; these are planning IDs, not runtime
diagnostic identifiers. Superseded repeated-banner review comments are condensed
in the residual ledger. Historical inventories, original execution records,
Legal versions and runtime compatibility contracts remain unchanged. This work
closes no release or operational acceptance gate.

## Migration Checklist Follow-Up (2026-10-30)

Migration checklists now use the current code-result property, canonical catalog
and provider routes, Viewer artifact ID, test-project references, workbook class,
and product names in ordinary task descriptions. Designer startup instructions
use the configured port from `pvad-designer/vite.config.ts`. These are editorial
corrections, not new provider implementations or renewed migration-test results.
Original test commands, recorded alias-routing coverage, the original repository
PR link and vendor-specific RPX metadata remain evidence or contracts.
Regression checks reject other previous-name occurrences in migration checklists.

## Repository-Wide Markdown Path Audit (2026-10-30)

The scan against `d608df36` covered all 272 tracked Markdown files, not only
migration checklists. It found 494 previous-name path-like occurrences in 68
files, including archives and compatibility resources. Current project paths,
API references, runbook commands, source links and project-format examples were
corrected across 30 Markdown files. Original command results, test counts,
historical architecture assessments and runtime reader values are not rewritten.

Run the inventory again with:

```bash
node tools/configuration/markdown-path-audit.mjs
```

The ignored outputs are `artifacts/rename/markdown-path-audit-current.md` and
`artifacts/rename/markdown-path-audit-current.json`. Each occurrence includes its
Markdown source, line and path value. The lexical search includes prose, links,
inline code and fenced examples; it is not a complete Markdown grammar or an
automatic rename approval. It reads only tracked regular Markdown files, skips
private IDE paths and symlinks, and does not inspect untracked local notes or
ignored build output. After the corrections, 394 occurrences remain in 46 files:
original verification/audit records, compatibility identities, retained fixtures
and explicit infrastructure bindings still require their real historical values.
The inventory is a navigation aid, not a claim that archived paths still exist.

Regression checks resolve corrected source-file references, current API
namespaces, PDF source links and the Word fidelity project/artifact contract.
The new path scanner has isolated coverage for nested and uppercase Markdown,
tracked versus untracked files, private IDE exclusions and symlink safety.
