# PVAD Rename Audit

Historical record: results and task states are retained. Names and rerun references
are editorially normalized; original commands and wording remain in Git at
`30496722`. This is not a new execution or release approval.

Date: 2026-10-17
Scope: repository analysis and planning only.
Baseline branch: `feature/native-designer-formats`; working tree clean at audit start.
Source brief: user-supplied `/Users/mannheimer/Downloads/PVADE_RENAME_HANDOFF.md`.
Authority: the user's clarification distinguishes technical identity from market identity.
**PVAD** replaces predecessor in technical names. **PVADE** is the market name displayed
on websites and in Designer. These are intentional names, not alternate spellings.
PVADE means Platform for Visualization & Automation of Documents - Engine;
the final E represents Engine. The claim is Create. Visualize. Automate.
PDF and Spreadsheet product names remain PVADP and PVADS.

## Inventory And Method

Git-tracked files were enumerated with `git ls-files -z`. UTF-8 text was searched
case-insensitively for predecessor and Power Dox/PowerDox variants; binary files were not
rewritten or searched as text. Counts represent matching lines, not occurrences.

- 2,656 tracked files; 115 C# project files.
- 2,032 text files contain 27,468 matching lines.
- 2,002 tracked paths contain an old brand variant.
- 2,357 tracked files match by path or text, including binary path matches.
- Full per-file inventory: [PVAD.Rename-Inventory.tsv](PVAD.Rename-Inventory.tsv).
  It records paths, counts and the first eight matching line numbers without source
  text, credentials or document contents.
- Text counts by group: src 645 files/6,192 lines; WebApi 225/4,023;
  tests 323/4,916; Designer 193/1,361; websites 180/4,050;
  checklists 152/3,003; docs 27/728; tools 97/787;
  deploy 57/989; product-metadata 67/296.
- Tracked local designer samples also match (17 files/129 lines). Their ownership
  and intended tracking must be reviewed; existing fixture evidence must survive.
- Inventory is a dated baseline. Re-run after implementation and inspect remaining
  matches rather than equating a zero string count with successful migration.

## Architecture Findings

| Area | Evidence | Consequence |
|---|---|---|
| .NET platform | `Directory.Build.props`, `src/`, `PVAD.WebApi/PVAD.WebApi.csproj` | .NET 10; rename projects, assemblies, namespaces, types, MSBuild properties and every project reference together |
| Solutions | `PVAD.sln`, `PVAD.slnx` | SLNX currently lists only six projects; verify membership separately and preserve each solution's intended scope |
| PDF | `src/Generation/PVAD.Pdf`, legacy Generator component, legacy Infrastructure.Pdf component, legacy Importer component | PDF spans multiple layers; product PVADP does not imply merging those layers |
| Spreadsheet | legacy Infrastructure.Spreadsheet component, Generator facade, Designer spreadsheet features | Product PVADS; retain shared API/Designer and architecture boundaries |
| Migrations | `src/Migrations/{Common,PDF,Spreadsheet,Report}` | Preserve domain/kind/provider taxonomy and vendor identifiers |
| Frontends | `pvad-designer/package.json`, predecessor implementation | React/TypeScript Designer; Vite websites; migrate six locales and shared assets |
| Worker execution | WebApi csproj build/copy targets | OCR and C# worker assembly paths are runtime contracts as well as project references |
| MCP | `tools/PVAD.Mcp/package.json` | Both predecessor executables and a canvas-mcp alias exist; inspect tooling, transport identity and clients |
| Release tooling | predecessor implementation | Explicit frontend paths, Docker ARGs and manifest product predecessor are hard-coded |
| Product files | legacy ProjectModels.cs component | Extension, magic, MIME, format ID and legacy minimumVersion component are separate serialized contracts |
| Persistence | `DatabaseSchemas.cs`, configurations, EF migrations | Schemas identity/administration/designer are neutral and need no branding rename |
| Public URLs | `websites/shared/siteLinks.js` | Production defaults contain powerdoxautomation.com and several subdomains |
| Documentation | `ARCHITECTURE.md` | Naming glossary has contradictory duplicate predecessor rows; migration path description is outdated |

## High-Risk Findings

### Authentication And Encryption

`PVAD.WebApi/Program.cs` names Account, Designer and CSRF cookies with predecessor and
sets Data Protection application name to `PowerDoxAutomation`. New names require
a migration strategy for authenticated sessions, cookie schemes, key rings,
protected job inputs/results and token purposes. Changing the application name
alone can prevent decryption even if the same key directory remains.

Use explicit old-reader/new-writer handling or a documented coordinated logout
and reauthentication window. Cookie handling must keep host-only isolation,
Secure, HttpOnly, SameSite and CSRF guarantees. Any old protection context must
be a bounded compatibility reader, not the new canonical identity.

### Persistent Storage And Infrastructure

Compose contains branded services, networks, database names/users and named
volumes. Renaming a volume key can attach fresh storage. Preserve existing data
with explicit physical volume bindings or verified backup/restore migration.
Changing a configured database name does not rename an existing PostgreSQL DB.

Keep EF migration IDs and the existing migration-history table stable.
Historical migrations may need namespace changes to compile, but must not be
reapplied under new IDs. Signed licenses, legal publication hashes, acceptance
events and audit history cannot be changed through text replacement.

### Native Project Formats And Checksums

Current contracts include legacy p component, legacy s component, predecessor implementation,
predecessor implementation, predecessor implementation,
predecessor implementation, format IDs and legacy minimumVersion component.
Proposed new output uses `.pvadp` and `.pvads`, matching product names.
Reader compatibility must recognize the complete old envelope, not merely rename
the extension. Convert explicitly and recalculate hashes only for new output.
Preserve original packages and signed evidence.

### APIs, Generated Code And Diagnostics

Canonical routes contain predecessor implementation. Code converters generate predecessor C#;
the sandbox validates namespaces, references and builder types. Rename these
together with the worker, semantic analyzers, reflection-dependent code,
source maps, SDK examples and migration-provider generated strings.

Diagnostics include legacy API component, legacy CODE component, legacy PROJECT component, legacy LICENSE component and historical
CANMIG families. New diagnostics need old-to-new translation and tests for
support tooling and client behavior. CANMIG is historical; changing it is a
separate contract migration rather than a predecessor token replacement.

### Evidence, Legal And External Systems

Screenshots, PDF fixtures, expected checksums, validation reports and golden
images may contain the old identity. Synthetic assets can be regenerated and
revalidated. Third-party-produced fixtures, signed material, published legal
versions and historical customer evidence retain their original bytes.

No registry publication, domain control, active customer integration, remote
branch protection or production resource was verified during this repository
audit. A source declaration is not proof that an artifact was published.
No domain availability or legal trademark conclusion is made.

## Risk Classification

- Low: current UI copy, editable help text and neutral new branding constants.
- Medium: paths, TypeScript exports, localization, screenshots, build scripts,
  ordinary samples and internal documentation links.
- High: public namespaces/packages, generated code, worker execution, API routes,
  diagnostics, storage keys, formats and deployment resources.
- Critical: signing material, protected payloads, licenses, customer DB state,
  immutable legal/audit records and production identity integrations.

## Recommendation

Perform a complete canonical rename in phased commits with one central mapping:
PVAD for technical identifiers and PVADE for public platform branding. Website
directories and Designer packages use PVAD technical naming while their visible
titles, logos, navigation and marketing copy use PVADE.
Preserve old names only as explicitly inventoried compatibility inputs or
immutable evidence, each with an owner, reason and removal condition. Final
acceptance requires no unexplained old brand in active product surfaces.

Implementation plan: [PVAD.Rename-Plan.md](PVAD.Rename-Plan.md).
