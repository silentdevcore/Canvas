# PVAD Checklist Naming Review

## Current Names

- **PVADE** is the market, platform, and Designer name.
- **PVAD** is the technical identity for projects, namespaces, APIs, clients,
  tooling, and configuration.
- **PVADP** / `.pvadp` and **PVADS** / `.pvads` identify the current native
  PDF and Spreadsheet project formats.

The application has one general naming-history notice on Company
`/about.html#rename-history`. Repository checklists also retain a naming-history
notice because they contain implementation records from before the rename;
these notices are not additional application banners.

## Review Scope (2026-10-30)

This follow-up corrects current checklist titles, goals, and open task wording.
It does not certify the implementation of those tasks. Checkbox states and
completed implementation records are unchanged. Original fenced code examples,
historical assessment bodies, test results, and compatibility inputs remain
original rather than being silently presented as new validation.

| Content | Treatment |
| --- | --- |
| Current headings, descriptions, technical names and open tasks | Use PVAD/PVADE. |
| Future SDK packages, client classes and publication instructions | Use proposed PVAD names; verify publisher ownership and availability before publication. |
| Current native-format introduction | Use `.pvadp` / `.pvads` and link to the current contract. |
| Checked task records and old test commands/counts | Retain original names and completion status as implementation history, not current usage instructions. |
| Original assessment, branch review and rename evidence | Retain its original wording; it does not describe a newly reviewed product state. |
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

- [The earlier rename record](../checklists/Rename-To-PXA.md) documents a previous
  generation. Its filename and body are retained; it is not the current rename plan.
- [The dependency PR review](../checklists/PXA.Dependency-PR-Review-2026-10-26.md)
  contains dated branch names, source paths, diagnostics, and completed CI evidence.
- Competitive-readiness, pricing, and valuation assessments retain the product
  name used at their assessment date; a rename does not revalidate their claims.
- Checked entries in otherwise current checklists describe work actually done
  under the earlier identity. Their names are not instructions to recreate it.
- [Compatibility exceptions](pvad-rename-exceptions.md) and the exact-path/hash
  [review ledger](pvad-rename-reviewed-exceptions.json) explain retained protocol,
  storage, fixture, audit, infrastructure, and historical references.

These are bounded exceptions, not permission to use the previous brand for new
features or open plans. New documentation must use the current names unless it
explicitly documents an existing compatibility contract or original evidence.

## Validation Boundary

The documentation contracts check current headings, open tasks, future SDK
naming, publisher caveats, internal links, and explicit historical notices.
This change does not alter application code, database records, package versions,
published release entries, signed material, or runtime compatibility readers.
No build, deployment, artwork, legal, publisher, or release approval is inferred
from editing a checklist. The broader rename release gates remain separate.

Local validation against commit `69e352e3` covers 113 changed checklists:

- All 8,153 checkbox states, 6,227 checked task records, and 57 fenced examples
  compare unchanged with that baseline.
- All 11 newly added or corrected relative checklist links resolve.
- All 49 naming, residual-review, versioning, and release-author tests pass.
- All 59 Documentation website contract tests pass.
- The Documentation production build passes; its existing Vite configuration
  loader warnings remain visible.
- Release-fragment and synchronized-version checks pass; `VERSION` remains
  `1.1.0`. These metadata checks are not production release readiness.

Historical checklist links inside completed records are preserved even when
their old source paths no longer exist. They are evidence of the original
implementation, not current navigation targets. This change validates current
new/updated links; it does not claim that every historical link is usable.
