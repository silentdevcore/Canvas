# PVAD Rename Remaining Items

## Current Source Review

Updated 2026-11-08 against baseline `d909de2b`. Technical naming is PVAD;
the public product and Designer brand is PVADE. This is the consolidated
decision list, not a replacement for historical audit evidence or approvals.

| Reviewed area | Result and evidence |
| --- | --- |
| JavaScript and XML | All 68 JavaScript and 47 XML files reviewed; general test inputs and a format label corrected. The 27 matching XML files are original validator evidence. |
| Frontend TypeScript | All 330 frontend files reviewed; current test defaults and comments corrected with explicit compatibility cases retained. |
| Backend C# | 840 production and 322 test sources reviewed, including null-containing source. General OAuth/generated-code inputs and explanatory comments corrected. |
| Build, scripts, project files and templates | 444 tracked regular files reviewed. Workflow/test display labels corrected; runtime bindings, historical binaries and test contracts retained. |
| Remaining tool TypeScript and C# matches | MCP compatibility readers, resource/schema aliases and stable diagnostics retained. The cutover harness, native fixture checksums and parser/Trust warning codes remain exact. |

See the [dated review records](PVAD.Remaining-Code-Naming-Cleanup.md) for actual
commands, results and limits. The new inventory tool itself is untracked until
the changes are committed and is tested independently of the tracked scan.

## Freely Renameable Findings

- [x] Replace the current binary-cutover workflow comment and step label with neutral descriptions of the pinned historical binary.
- [x] Replace the deployment test's free-form historical-release title without changing its original artifact inputs or expected contracts.
- [x] Make short-name detection case-insensitive for every casing and for occurrences embedded in identifiers.
- [x] Add a repeatable tracked-file inventory covering filenames, text, null-containing source and separate binary offsets without copying source lines or matching suffix values.
- [x] Verify that the inventory neither scans private/untracked/ignored paths nor follows file or parent-directory symlinks.

No additional freely renameable current name was identified in this reviewed
build/configuration scope. This is a scoped result, not a claim that every
repository match is harmless or that all wider source review is complete.

## Retained Contracts

| Remaining area | Concrete boundary | Future change required to remove it |
| --- | --- | --- |
| Diagnostics and translations | Exact diagnostic and compiler-warning keys, six-language mappings, unknown-code rejection and explicit raw/display tests. | A separately versioned diagnostic transition; never a text replacement of stored results. |
| APIs, resources and formats | Old routes, fields, resource URIs, schema resources, obsolete members and file readers; current producers already use canonical names. | End the agreed compatibility major with an explicit breaking-change decision and migration tests. |
| Authentication and configuration | Old scopes, claims, cookies, ticket-protection purposes, environment aliases and credential-header readers. Conflicting old/new values remain rejected. | A coordinated session/configuration transition, not changing protected values or dropping readers cosmetically. |
| Persisted and cryptographic identity | Stored audit keys, format IDs, license key IDs, signatures and applied retention migration. | Preserve original records; introduce a separate future contract only if needed. |
| Monitoring and installed infrastructure | Grafana UIDs, existing provider/folder identity, dual-family metric selectors, protected environments and explicit old storage/database bindings. | An authorized target-specific cutover with backup, permissions and rollback evidence. |
| Paired-binary proof | The historical project and namespace in the job-cutover harness identify the actual original executable. | Keep the original executable identity while this proof is required. |

References: [compatibility boundaries](../docs/pvad-rename-exceptions.md),
[public identifier register](../docs/reference/compatibility-identifiers.v1.json),
[protected identifier register](../operator-docs/compatibility-identifiers.v1.json)
and [source acceptance](../docs/pvad-rename-source-acceptance.md).
Do not expose the protected register through public documentation output.

## Protected Original Evidence

Historical JSON/Markdown inventories, original TSV audits, approved baselines,
published release notes, signed PDFs, certificates, independent validator
reports, reference project archives and hash-bound recovery HTML stay unchanged.
Some original filenames also contain the former identity. Their exact paths and
hashes belong in the existing registers and generated inventories, not renamed
copies that would invalidate provenance. Vendor terms and incidental encoded
substrings are not current first-party naming.

## Repeatable Inventory

Run from the repository root:

```bash
node tools/configuration/repository-naming-inventory.mjs
node tools/configuration/repository-naming-inventory.mjs --write-report
```

The first command prints counts without writing a report. The second refreshes
the ignored local artifact
`artifacts/rename/repository-name-inventory-current.json`. It records exact
tracked paths, content hashes, line hashes/positions or binary byte offsets.
It does not classify or approve findings and is not an exception allowlist.
Neither command changes source files, the index or a database.

This scope excludes paths matching ignore rules even when tracked: 228 such
paths in the current inventory. It also excludes private IDE paths, untracked
files and symlinks. PDFs and invalid UTF-8 are reported as binary evidence;
known text sources with null bytes remain text-scanned. Counts therefore must
not be compared directly with earlier inventories that used different scopes
or binary detection. Audit and exception files participate in the counts; a
large count does not represent the same number of unfinished defects.

## Current Output Follow-up (2026-11-08)

Baseline `13ec82ed`: the remaining-file inventory did not expose these display
gaps because the components receive old diagnostic references dynamically.
The [application-output review](PVAD.Remaining-Code-Naming-Cleanup.md#current-application-output-review---2026-11-08)
corrects project-import messages, workspace banners, the three C# error previews,
PDF signing status and the Trust mutation-blocking warning using the existing
display-only helper. Stable codes, original errors, API responses, editor drafts
and customer filenames remain exact. No new alias or wire contract is created.

- [x] Reproduce the visible failures, correct their rendering boundaries and test both projected output and untouched original data.
- [x] Add independent guards that reject regressions even if an exception hash is refreshed.
- [x] Pass all 827 Designer tests, 92 suites, the snapshot, typecheck and production build.
- [x] Add a customer-facing Patch fragment without changing VERSION or published history.

All 443 final configuration/deployment/versioning tests also passed. The existing
generic-example naming guard remains unchanged; dedicated failure fixtures are
checked separately with exact expected display output. All 816 original review
notes/reasons and 91 protected evidence files remain intact. The added review
record covers only the exact new workspace diagnostic fixture. Fresh inventory
counts include the explicit regression inputs and are not completion percentages.

The API still exposes explicit compatibility fields and the Company page keeps
the agreed single naming-history note. Diagnostic raw IDs, cryptographic and
persisted identities, historical evidence and old readers are retained for the
specific reasons above. Raw code/document previews are not branding surfaces:
altering customer content to reduce search counts is prohibited. This follow-up
does not certify a browser deployment or make the repository a zero-match tree.

## Account And Admin Follow-up (2026-11-08)

The [portal-output review](PVAD.Remaining-Code-Naming-Cleanup.md#account-and-admin-output-review---2026-11-08)
corrects dynamic error presentation in both entry points and nine Account pages.
Admin also accepts structured JSON Problem Details instead of discarding its
diagnostic fields. Raw response bodies, machine codes and support traces remain
unchanged. Error displays are projected and escaped; stored customer names,
license/key identifiers, audit actions and Legal content are not renamed.

- [x] Correct Account/Admin dynamic diagnostic displays and retain original error/state objects.
- [x] Verify license/subscription failures without rewriting signed license identifiers or customer organization data.
- [x] Preserve structured Problem Details in the Admin transport and cover JSON media-type variants.
- [x] Pass Account/Admin tests and builds, Account typecheck and all 846 Designer tests.
- [x] Inspect four desktop/mobile browser scenarios using synthetic responses and stop the isolated preview servers.
- [x] Add a Patch fragment while keeping the product version and published history intact.

Release presentation was already projected; historical manifests and persisted
notifications were not rewritten. The browser checks prove the mocked failure
workflow only, not a real backend authorization/licensing deployment. Remaining
raw contract identifiers and protected evidence still have the boundaries listed
above; this pass does not make a repository-wide zero-match claim.

Final verification passed 447 configuration/deployment/versioning tests. AST
comparison limits the 11 UI source changes to presentation and confirms that
the transport change only reads additional JSON error media types. All 817
original review notes/reasons and 91 protected evidence files are preserved;
the version, published releases, original inventories and compatibility
registers are unchanged. No additional diagnostic alias was created.

## Public Websites Follow-up (2026-11-08)

The [public website review](PVAD.Remaining-Code-Naming-Cleanup.md#public-websites-output-review---2026-11-08)
checks Company, Documentation and Demo as one scope. Two public PAdES catalog
wrappers needed editorial naming corrections. Their download targets, signed
documents, validator reports, manifests and recorded classifications are kept
exact. Current app sources, Company metadata, release presentation and all eight
Demo example functions are covered without rewriting raw customer content.

The earlier blanket retention of these two HTML wrappers is narrowed to their
artifact references and original evidence. Current catalog headings are product
presentation, not a historical validation result. All other corpus artifacts
retain their existing protection. The source checklist records actual tests,
browser limits and any outstanding verification; this is not a zero-match claim
for binary downloads or compatibility data.

## CLI And MCP Follow-up (2026-11-08)

The [tool-output review](PVAD.Remaining-Code-Naming-Cleanup.md#cli-mcp-and-diagnostic-tool-review---2026-11-08)
corrects one standards error explanation and the MCP smoke summaries. Resource
addresses, stable diagnostics, structured payloads and original schema contents
remain exact. Tool tests cover all catalog examples, customer-data preservation,
both resource generations and retained standards-policy rejection behavior.
Parser/fidelity/corpus evidence checks continue to enforce original hashes and
thresholds. This is internal tooling work, not production protocol migration or
new validation approval. The source checklist records actual test outcomes and
the initial sandbox socket restriction separately.

## Generated Document Follow-up (2026-11-08)

The [fresh-output review](PVAD.Remaining-Code-Naming-Cleanup.md#generated-document-output-review---2026-11-08)
tests six output formats and native PDF/Spreadsheet facades with current sample
data and explicit customer metadata. All 15 new cases pass in the existing API
branding suite. Current writer behavior needed no additional rename; production
code and the strict general-example guard are unchanged. Broader selections
passed 50 API, 64 export and five filename tests. Fourteen synthetic artifacts
were inspected separately through PDF metadata and Office/XML package readers.

The source checklist records interrupted build attempts, test-harness fixes,
existing warnings and the precise validation limits. Source inspection is not
substituted for artifact checks, and retained customer content is not counted
as outdated product naming. No original document, signature, schema contract,
licence approval, deployment or published release changes in this pass.

## Verification Still Outstanding

- [ ] Build the genuine Release outputs for the collector test hosts and relay before rerunning the two broad coverage-host checks. The broad run passed 557 of 559 tests; those two failed on missing output files, not on naming assertions. No placeholder outputs or relaxed assertions are allowed.
- [x] Review the combined pending JavaScript, TypeScript, C#, build-tooling and documentation diff. This source review does not authorize a commit or declare overall repository semantic closure.

Final verification passed all 439 configuration, deployment and versioning
tests across 56 files, with no failures or skips. All 32 shell scripts passed
syntax checks and all 119 project/build XML files parsed successfully. The
workflow's executable configuration is identical to the baseline after ignoring
its display title. All 135 local links and section references in the four
review documents resolve. The 816 original review notes/reason records and 91
protected evidence files are unchanged, as are both compatibility registers,
original JSON/Markdown inventories, VERSION and published releases. The new
documents and fragments also pass the naming check before being tracked.

The historical eight rollout/release gates remain separate in the
[rename plan](PVAD.Rename-Plan.md). Publishers, staging, independent approval,
deployment, live resource migration and publication are not part of the current
repository-only rename request. Raw occurrence counts and the old 90/98
operational-gate total are not a repository completion percentage.

The separate stored-template rendering limitation remains recorded in the
[code-cleanup checklist](PVAD.Remaining-Code-Naming-Cleanup.md); naming work does
not assert document-rendering fidelity. VERSION and published releases remain
unchanged. No commit, push or deployment is authorized by this document.
