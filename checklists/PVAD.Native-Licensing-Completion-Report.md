# PVAD Native Licensing Completion Report

Executed against the [complete-to-100 handoff](PVAD.Native-Licensing-Completion-Handoff.md)
on 2026-11-23. Earlier reports are preserved unchanged:
[final work report](PVAD.Native-Licensing-Final-Work-Report.md) and
[remaining-work report](PVAD.Native-Licensing-Remaining-Work-Report.md).

**Result: 70/76 primary items (92.1%). Not 100%.** Workstream A (parser
robustness) is technically finished locally:

- Every fresh finding, including one more found by the third campaign, is
  classified and fixed.
- All 42 saved inputs replay with controlled outcomes.
- A fresh unchanged 300-second campaign on the final candidate found
  **0 crashes and 0 timeouts**. No primary item closes, because each
of the six open rows still needs human or external inputs, remote platform
runs, or a reviewed ImageAnalysis baseline that the implementing agent may
not supply.

## 1. Candidate, Inherited Work And Changed Files

| Item | Value |
| --- | --- |
| Branch | `feature/pvad-native-licensing-and-protection` |
| Handoff starting HEAD | `73a522e0796863c40ed01be295d16462b624cee3` |
| Actual HEAD at start | `445519ca0340f9cd3fcd6c3ec5a3352de390d2e4`. The owner committed the handoff's inherited uncommitted parser, evidence and fragment work as this commit before execution. |
| Inherited dirty files at start | Only owner-owned `PVAD.sln` (51-line probe addition) and untracked `.vscode/`. Neither was edited, staged, reverted, stashed or deleted. |
| Final candidate | HEAD `445519ca` plus the uncommitted diff below. Nothing was committed or pushed. |
| VERSION | `1.1.0`, unchanged. Published releases are unchanged. |

Files changed in this round:

| File | Change |
| --- | --- |
| `src/Importing/PVAD.Importer/Parsing/PdfObjectParser.cs` | Overflow-safe `/Length` bound check. Literal and hexadecimal object strings use the strict `StringContent()`. |
| `src/Importing/PVAD.Importer/Fonts/PdfToUnicodeCMapParser.cs` | 64-bit code arithmetic, 1–4-byte source codes, explicit hex validation, and a 262,144-mapping budget per CMap (`PVADPDFPARSE012`) charged before a range is expanded |
| `src/Importing/PVAD.Importer/Fonts/PdfSimpleFontParser.cs` | 262,144-width budget for CID `/W` ranges (`PVADPDFPARSE013`), charged before expansion. A 64-bit loop counter avoids wrapping at `int.MaxValue`. |
| `src/Importing/PVAD.Importer/Tokenizer/PdfToken.cs` | Internal `StringContent()` rejects unterminated strings with `FormatException`. Internal `AvailableStringContent()` keeps every available byte of an unterminated final string. Both replace the former `[1..^1]` slice, which threw on a lone delimiter and dropped a real byte. |
| `src/Importing/PVAD.Importer/Trust/PdfTrustInspector.cs` | Revision counting no longer dereferences a missing cross-reference table, which previously caused a `NullReferenceException`. |
| `src/Importing/PVAD.Importer/Content/PdfContentStreamParser.cs` | String operands use the lenient `AvailableStringContent()` (see section 3, string handling) |
| `tests/PVAD.Importer.Tests/PdfMalformedContentTests.cs` | 38 new deterministic synthetic regression and control cases (19 → 57) |
| `product-metadata/release-fragments/pdf-malformed-content-reliability.json` | The existing pending Patch summary now also covers the new fix classes. No new fragment was added. |
| `checklists/PVAD.Native-Library-Licensing-And-Protection.md` | Status note and Later Findings update. Primary checkbox states are unchanged. |
| `checklists/PVAD.Native-Licensing-Completion-Report.md` | This report |

No public API, serialization, DI, licensing, protection, ImageAnalysis
algorithm, snapshot, threshold, fixture, fuzz harness, expected-exception list,
corpus seed or runtime-evidence file was changed.

## 2. Six-Row Closure Decisions

None of the six rows closes. Synthetic test evidence never replaces a real key
or approval.

| Primary item | Owner | Decision and evidence this round | Remaining blocker / exact next request |
| --- | --- | --- | --- |
| Manufacturer public verification keys | Owner/manufacturer (not named yet) | None. The embedded register stays empty and `TrustedKeys.json` stays empty. | Approved manufacturer P-256 **public** key, stable key ID, provenance record and provisioning approval, delivered through a reviewed change. Never send private material. |
| Publisher/commercial/legal decisions | Product owner + qualified Legal (not named yet) | None. No ownership was inferred from domains or logins. | Named decision owner and date, confirmed NuGet/npm publisher ownership, commercial/developer/OEM terms and qualified Legal approval references. |
| Third-party distribution gates | Product + Legal | NPOI options A/B/C remain open and `AcceptNPOIOSMFLicense` is untouched. Tesseract component notices remain candidate associations. The liblzma version and vcpkg baseline cannot be derived from the retained upstream instructions (section 5). | NPOI A/B/C decision with any payment or exemption record; the authentic vcpkg baseline/ports or a build record/SBOM bound to the shipped DLL hashes (from the wrapper maintainer or another authentic source); an authenticated Debian keyring for the Liberation Sans signature; and a distribution review. |
| Production private-key custody | Operator (not named yet) | None. No private material exists in the repository, packages, images or this report. | Named custodian, approved secret store, access policy, backup, rotation and recovery owners, support-map retention owner, operator sign-off, and an independent second person for four-eyes controls. |
| Complete general validation | Engineering + owner (remote authorization, baseline review) | Parser findings are fixed with a clean local replay and campaign (section 3). The working-tree Solution builds. Local regressions: see section 3. ImageAnalysis still has the four known snapshot failures; no baseline was produced or approved (section 4). | Owner decision on reference font bytes for ImageAnalysis (section 4) and human review of candidates; authorization to commit, push and dispatch the four prepared workflows for Windows, Linux/x64 and macOS acceptance. |
| Final package documentation/distribution review | Distribution reviewer + Legal | No package or documentation sign-off; none can be self-approved. | Final EULA, corresponding-source and distribution decisions, and inspection of final real packages after the decisions above. |

### Owner Decision Follow-Up (2026-11-24)

The table above records the implementation round. The project owner supplied
these additional inputs in the licensing completion conversation on 2026-11-24:

| Item | Owner / evidence | Decision or verified input | Verification performed / remaining blocker |
| --- | --- | --- | --- |
| Legacy `.xls` scope | Project owner, name not yet supplied; explicit conversation reply | Retain full `.xls` import and export. | Recorded in the [NPOI decision](PVAD.NPOI-License-Decision.md#product-decision-recorded-on-2026-11-24). Option A or B still needs selection, applicable evidence and qualified Legal approval. |
| Production signing keys | Project owner; explicit conversation reply | No production signing keys exist yet. | Owner statement recorded; no key was generated or provisioned. Manufacturer identity, custodian, storage, backup, rotation, recovery and reviewed provisioning remain open. |

The owner subsequently chose **PVAD** as the outward manufacturer name and
assigned license administration to the website administrator. This records a
display identity and an operational role, not verified NuGet/npm account
ownership, a legal entity, or completed production key custody. The existing
issuer still requires explicit manufacturer-side authorization; a customer
administrator does not acquire issuance rights from this decision.

Current automation creates, signs and stores the artifact after an authorized
administrator submits an issuance request. The owner subsequently approved the
proposed self-service trigger: a signed-in user with confirmed email requests a
free license in Account. The account-specific rights and ownership requirements
below must be implemented before enabling that flow. No automatic registration,
download or payment trigger was enabled by this documentation update.

Publisher account ownership remains unconfirmed. After initially selecting
preparation of NPOI Option B, the owner explicitly deferred the entire `.xls`
decision for now. This supersedes the Option B preparation choice: retain the
current implementation/dependency graph and leave NPOI distribution approval
open. Intended secure key storage, the specific key custodian, backup and
recovery arrangements remain open; website license administration alone does
not establish those controls.
No EULA, payment, legal approval, production key creation or deployment is
authorized by these planning inputs. No primary row closes;
status remains **70/76 (92.1%)**.

### Free-Phase Account Policy And Implementation Gaps (2026-11-24)

The owner specified these requirements after reviewing the issuance proposal:

| Area | Confirmed requirement | Current implementation / necessary work |
| --- | --- | --- |
| Issuance trigger | Confirmed email, authenticated Account session, explicit free-license request | Implement a customer request path with server-derived rights, idempotency and audit; do not grant customer sessions manufacturer-issuer privileges. |
| Company | License belongs to the organization; all applicable native products except Migration are eligible by default | Issue `pdf`, `word`, `spreadsheet`, `importer` and `converter` only within actual release/distribution approvals. Preserve member role restrictions; a company license does not make every member an administrator. |
| Migration | Both company and private accounts require separate PVAD manufacturer-admin approval (owner confirmation, 2026-11-24) | Existing default role permissions and Trial entitlements include Migration. Enforce explicit approval independently of role changes and withhold all `migration.*` native grants until approval; provider scope must be explicit. |
| Private / individual developer | Personal license belongs to the user; no organization is required | Current registration creates an organization even for individuals, and the license ledger/download path requires an organization. Introduce genuine user ownership and user-scoped access with a reviewed additive persistence/API migration; do not silently substitute a hidden organization for this requirement. |
| Private import | DOC/DOCX, XLSX, CSV/TSV, ODT, PPTX, Markdown (`.md`, `.markdown`), HTML and XML import are allowed by policy. PDF import is denied by the later owner decision on 2026-11-25, superseding the earlier PDF-import allowance. SVG import and raster-image import/OCR remain denied. PDF creation, editing of own designs, export and printing remain allowed. | No dedicated HTML/XML document importers were found in the current file-import registry or inspected import/API/Designer paths; their policy allowance does not establish implemented format support. Track those missing import paths separately from license enforcement. Current `importer` native grant includes document import, SVG and image analysis/OCR. Introduce separately enforceable format/operation rights and matching server/UI policy before granting restricted offline importer access. The separate `.xls` distribution decision remains deferred. |
| Private document authoring | Create and edit documents: allowed (owner confirmation, 2026-11-24) | Preserve the applicable document/PDF/spreadsheet authoring rights within the personal account boundary. |
| Private output | PDF, DOCX, XLSX and ODT export and printing are allowed. PPTX export is explicitly denied, including if a PPTX exporter is added later. Raster-image export (PNG, JPEG, TIFF), SVG export and text/data export (HTML, XML, Markdown, CSV, TSV) are denied. The later format-specific decisions supersede the earlier blanket export allowance (owner confirmations, 2026-11-24). | Enforce format-specific rights for direct exporters, API, UI and code/worker paths, including spreadsheet CSV/TSV output. The current `converter` grant combines allowed ODT with denied formats, so withholding or granting that whole product is insufficient. Preserve internal rendering needed for allowed PDF/printing while denying standalone image export. No denied signing, redaction, archive-conversion or E-Invoice operation is granted by an allowed output format. `.xls` remains deferred. |
| Private templates | Create, version and publish templates: allowed (owner confirmation, 2026-11-24) | Support these rights for user-owned templates without requiring organization membership. Publishing does not imply cross-account data access or an unrestricted global publishing audience. |
| Private code | Edit and execute own code: allowed (owner confirmation, 2026-11-24) | Preserve isolated worker execution and apply the same account/native-operation restrictions to code and worker paths. Code execution must not bypass blocked features. |
| Private advanced PDF operations | Sign PDFs, apply redactions and convert to archival conformance: denied (owner confirmation, 2026-11-24) | Deny `PdfSignaturesSign`, `PdfRedactionsApply` and `PdfComplianceConvert` with matching API/native enforcement. Signature validation, redaction inspection and compliance validation are distinct permissions; do not infer new decisions for those read/check operations. |
| Private E-Invoices | Create and validate E-Invoices: denied (owner confirmation, 2026-11-24) | Cover both create/generate paths and validation paths; basic PDF/Word/Excel authoring grants must not enable these operations through an alternative API or worker. |
| Private API/service accounts | API access and service accounts: allowed (owner confirmation, 2026-11-24) | Scope credentials and resource access to the individual owner; no organization required. Enforce the same account limits for every credential. Service accounts do not gain manufacturer issuance or Migration approval powers. |
| Signing key | Cloud hosting is intended; provider is not selected. Owner requested storage advice; no production key exists. | See the restricted issuer runbook: recommend a cloud secret/key service separate from the application database. Cloud provider, storage implementation and specific custodian remain unconfirmed. |

Roles, commercial/account capabilities and signed native rights remain separate:
a member must pass the applicable server policies, and offline libraries must
enforce the rights in the signed artifact without trusting website role claims.
An ordinary role change must not approve Migration or widen account eligibility.
Conversely, changing a website role does not remotely rewrite an already issued
offline license; existing artifacts need an explicit replacement/revocation
policy under the existing offline limitations.

Inspection references: `PvadRoles.cs`, `PvadDesignerPermissions.cs`,
`CustomerRegistrationService.cs`, `TrialActivationService.cs`,
`PvadEntitlementService.cs`, `AccountNativeLicensesController.cs`,
`NativeLibraryLicense.cs`, `NativeLibraryLicenseConfiguration.cs`,
`PvadLicenseModels.cs` and `PvadLicenseExecution.cs`.
This section records requirements and gaps; it does not claim they are
implemented. The private rights listed above are confirmed; unlisted granular
rights, template publication audience and free version/update coverage remain
to be finalized during policy implementation. The `.xls` decision remains
deferred; company eligibility is not approval to ship unapproved dependencies.

### Commercial Model To Revisit Later (2026-11-24)

The owner stated that PVAD will initially be offered free of charge while still
requiring signed licenses. A paid offering is intended later, but no transition
date is set. The owner asked to retain the following recommendation for later
consideration, without implementing it now:

- For installed software/native libraries, grant ongoing use of the versions
  covered by the free license. Subsequent versions or update rights may become
  paid when the owner decides to introduce commercial pricing.
- Existing free license holders keep the right to run their covered versions;
  this does not promise lifetime free updates or support.
- Define hosted Cloud access separately, including ongoing operating costs;
  do not infer perpetual free hosted access from an installed-software license.

Before implementation, confirm the exact covered releases/update cutoff,
issuance and renewal rules, paid transition terms and customer wording. The
existing perpetual/update-cutoff contract is a candidate basis, not an approved
new license configuration. This note changes no runtime behavior, license
payload, Trial duration, pricing, EULA, production keys or release approvals.

### Mandatory Activation Controls Before Paid Launch (2026-11-24)

The owner explicitly deferred installation activation and license-sharing
controls during the initial free offering, but requires them to be implemented
and validated **before introducing paid licenses**. This is a paid-launch gate,
not an optional later enhancement. It does not change the fixed 76-item primary
denominator or authorize implementation in the current free-phase work.

The current signed offline license checks remain in place during the free
phase; they do not reliably detect copying a license between installations.
Before paid launch, specify and implement:

- The licensed unit (organization, installation or named user), allowed limits,
  and the scope of permitted sharing.
- Server-side activation accounting and signed installation-bound activation
  artifacts, including safe concurrent activation and limit enforcement.
- Deactivation, machine replacement and recovery, with explicitly defined
  treatment of cloned/virtual installations.
- The agreed offline-use policy: initial activation or manual offline exchange,
  any periodic revalidation, and behavior during connectivity/server failures.
  Installation counts must not be represented as verified human-user counts.
- Customer/Admin flows, necessary data/privacy review and end-to-end tests of
  activation, copying, exceeded limits, transfers, renewal and offline behavior.

No activation service, device binding, periodic network requirement or new data
collection was added by this planning note. Existing free-license rights must
be addressed explicitly when designing the paid transition.

## 3. Findings, Fixes, Regression Cases And Runs

### Classification Of The Fresh Campaign Findings

Input hashes were verified against the handoff and the retained campaign
directory `artifacts/native-validation-hardening/linux-fuzz-1795461140739-75fd9e83/`.

| Finding | SHA-256 | macOS/x64 (signed session, Debug and Release) | Linux/arm64, pre-fix HEAD `445519ca` | Root cause |
| --- | --- | --- | --- | --- |
| Crash `id:000000` | `61b16317…ee979f` | `OverflowException` in `PdfObjectParser.ParseDictionaryOrStream` (stream bound check) | Abort (exit 134) under `--afl`; `--child` JSON `rejected/OverflowException` with exit 0 | `/Length 123455555555555555555555555556` is read as a real number, saturates to `long.MaxValue`, and `streamStart + length` wraps negative, so the slice cast overflows |
| Crash `id:000001` | `8d47ecbb…38311e` | Completes under the 512 MiB cap (631 MiB allocated in total); `OutOfMemoryException` in `PdfSimpleFontParser.ReadCidWidths` at 128 and 64 MiB caps | Runtime `Out of memory.`, core dump (exit 134) in both `--afl` and `--child` | Unbounded CID `/W` range expansion; a range ending at `int.MaxValue` can also wrap the loop counter |
| Timeout `id:000000` | `fc2b4290…8270` | About 3.7–4.1 s, then a late `FormatException`; at the 512 MiB cap an `OutOfMemoryException` in `ParseBfRange`. `dotnet-stack` samples were in `PdfToUnicodeCMapParser.ParseBfRange` → `DecodeUnicode` | Runtime `Out of memory.`, core dump (exit 134) | Unbounded ToUnicode `bfrange` expansion |

The first post-fix candidate replay changed crash `id:000000` to an
`ArgumentOutOfRangeException`, which is an allowed type in the harness but not a
deliberate rejection. The cause is an unterminated hexadecimal string at end of
input, sliced with `[1..^1]`. That defect existed independently in the object
and content-stream parsers.

String handling went through one corrected iteration:

1. **First attempt: strict in both parsers.** All Importer tests passed once.
2. **The first full regression run failed** (`artifacts/native-licensing-completion/1795464356-regressions/`,
   step evidence `artifacts/native-test-sessions/5ef2c244-9d67-4d5f-93fd-f3bcbb6a582f/`).
   `PdfEInvoiceAttacherTests.AttachAsync_RejectsAnEncryptedSource` received
   `FormatException` from `PdfContentStreamParser`, called by
   `PdfA2bConformanceValidator`, which scans the **still-encrypted** content
   bytes. Random ciphertext sometimes ends in an open `<` or `(`, so strict
   content parsing made the outcome nondeterministic. The candidate-2 replay
   also showed three old fuzz inputs changing from "completed" to rejected for
   the same reason.
3. **Final design.** The object parser stays strict: an object that ends inside
   a string is malformed and is rejected. The content parser, which is lenient
   by design and is also used by analysis callers, now keeps every available
   byte of an unterminated final string. It no longer drops one byte or throws
   on a lone delimiter.
4. **Recheck.** After the change, `PdfEInvoiceAttacherTests` passed 30 of 30
   repeated runs in one signed session.

Temporary probes were built outside the repository and run only in isolated
signed test sessions. A scratch `dotnet-stack` 10.0.750501 was used for timeout
sampling. None of these are repository tools.

### Fix Design

- Oversized values are not clamped. An out-of-range `/Length` takes the
  parser's existing `endstream` recovery path, which already handled lengths
  past the end of input.
- Font mapping expansion is budgeted **before** any allocation or loop. A
  budget breach throws the existing `PdfParseLimitExceededException` contract
  with new stable codes `PVADPDFPARSE012` (ToUnicode) and `PVADPDFPARSE013`
  (CID widths).
- Both budgets are 262,144 entries, four times a complete two-byte code space
  (CIDs stop at 65,535). Worst case just below the budget, Release macOS/x64
  with a 512 MiB cap: ToUnicode 39–86 ms and CID widths 8–36 ms, far below the
  2,000 ms case limit.
- Malformed codes, invalid hex and unterminated strings throw `FormatException`.
  Native license checks still run before input checks. No broad catch was added.

### Regression Tests

`PdfMalformedContentTests` now has 57 cases (19 inherited + 38 new).
The new cases cover:

- Three `/Length` overflow cases fall back to `endstream` without overflowing.
- Three single-range and one cumulative ToUnicode range case hit the budget.
- Five invalid-code cases cover over-long, empty, out-of-range and non-hex codes.
- A control case maps the full two-byte range plus high four-byte codes.
- Three CID-width cases hit the budget; one control range terminates at
  `int.MaxValue`; one control case expands the full CID range plus explicit widths.
- Six unterminated end-of-object strings are rejected by the object parser.
- Six unterminated final content strings keep every available byte.
- One control case keeps terminated content strings unchanged, and five
  terminated end-of-input strings still parse.
- Two Trust-inspector cases report invalid structure when the cross-reference
  table cannot be read, without a null dereference.

| Run | Result | Evidence |
| --- | --- | --- |
| Full Importer suite, final candidate, signed isolated session, macOS/x64 SDK 10.0.113 | **734/734**, 0 skipped, TRX counters audited (732/732 before the Trust fix: `…1795464603097-eab62492`) | `artifacts/native-engine-tests/1795497907571-4618db10/` |
| Intermediate attempts, retained | 714/714 before the string fix (`…1795463232505-45c9538b`, TRX counters audited, SHA-256 `0c01e307…5be775`). Two runs with one failure each from test-authoring errors (`…1795464023981-6c86ede2`, `…1795464128979-3dfa9b4d`), corrected in the tests. 725/725 with strict content strings (`…1795464222020-efa7895c`), superseded by the regression failure above. | `artifacts/native-engine-tests/` |

The new unit cases were not run against the unfixed code. They would hang or
exhaust memory before failing. The red evidence is the replay of the three
original inputs against unmodified HEAD on both platforms (above).

### Replay Of All Saved Inputs (Linux/arm64)

Environment: `debian@sha256:268fb24c39bca92d326dd83ea3abebe4a3f7751c804ffef23bb6d857dd343ff8`,
2 CPUs, 3 GiB, 512 PIDs, SDK 10.0.113 linux-arm64, SharpFuzz 2.2.0, AFL++ 4.04c.
Each input runs through the instrumented `--afl` entry (single stdin run, exit
code checked) and through `--child`, whose **JSON outcome** is audited rather
than its exit code alone. Both use the 512 MiB managed-heap cap.

| Run | Source | Result |
| --- | --- | --- |
| Pre-fix | `git archive` of HEAD `445519ca`, SHA-256 `eed8715e…66cee9b` | 37 old cases safe (32 `FormatException`, 5 completed) and the valid seed completes. All three new inputs abort (exit 134). `artifacts/native-validation-hardening/linux-replay-head-1795462887-4587749c/` |
| Candidate 1 (before string fix) | working-tree archive `e16c7ff4…94649` | 41/41 exit 0. New crash 1 and the timeout: `PdfParseLimitExceededException`; crash 0: `ArgumentOutOfRangeException`, which led to the string fix. `artifacts/native-validation-hardening/linux-candidate-fuzz-1795463346-69eb81e1/` |
| Candidate 2 (strict content strings) | `9870e8ab…0968ff` | 41/41 exit 0. All three new inputs rejected in a controlled way (crash 0: `FormatException`, crash 1 and timeout: `PdfParseLimitExceededException`); the seed completes. Three old inputs changed from completed to `FormatException`, which led to the content-parser correction. `artifacts/native-validation-hardening/linux-candidate-fuzz-1795464330-113d1325/` |
| Candidate 3 | `ea454c77…635be7` | 41/41 exit 0 with controlled outcomes (33 `FormatException`, 2 `PdfParseLimitExceededException`, 6 completed including the seed). All five previously completing old inputs complete again. `artifacts/native-validation-hardening/linux-candidate-fuzz-1795464929-7daa5023/` |
| Candidate 4 (final) | `85ffd749…e19c060c`, 3,854 tracked files, HEAD `445519ca` plus this diff (per-file hashes in `context.txt`) | **42/42** exit 0 with controlled outcomes: 34 `FormatException`, 2 `PdfParseLimitExceededException`, 6 completed including the valid seed. Every new input is rejected in a controlled way, and the five previously completing old inputs still complete. `artifacts/native-validation-hardening/linux-candidate-fuzz-1795465608-c902ad04/` |

Input SHA-256 values: `cases.sha256` in each run directory. Raw inputs stay in
ignored `artifacts/` and were not uploaded.

### Fresh Coverage-Guided Campaigns

All four campaigns used the unchanged `tools/pdf-parser-fuzzer/run-coverage.sh`
and harness: 300 s, 2,000 ms per case, 512 MiB managed heap, unchanged
corpus seed, a fresh output directory, and the same image and toolchain as above.

| Campaign | Source | Crashes | Timeouts | Corpus / coverage |
| --- | --- | --- | --- | --- |
| Candidate 1 | `e16c7ff4…` | 0 | 0 | 1,334 new items, 5.42% |
| Candidate 2 | `9870e8ab…` | 0 | 0 | 1,165 new items, 5.55% |
| Candidate 3 | `ea454c77…` | **1** | 0 | 1,191 new items, 5.52%. **Failed**; see the next subsection. |
| **Candidate 4 (final)** | `85ffd749…` | **0** | **0** | 1,375 new items, 5.59%. **Passed** (`campaign-exit=0`). |

### Campaign 3 Finding: Trust Inspector Null Dereference

Crash `id:000000,sig:02,src:000388,…`, SHA-256 `cdcbdd89…25f8ae72c`, reproduces
on macOS/x64 as a `NullReferenceException` in
`PdfTrustInspector.InspectAdmittedAsync`, at both 512 MiB and 128 MiB caps.

- **Cause.** When cross-reference parsing throws, the inspector records
  `PVADPDFTRUST002` and leaves `xref` null. The revision count then evaluated
  `xref?.Entries.Count == 0`, which is false for null, and dereferenced
  `xref!.Entries`.
- **Origin.** This is pre-existing code, unchanged since the rename commit
  `fdc9ac41`. The object parser's new strict rejection of unterminated strings
  can make this path easier to reach, for example through a trailer string.
- **Fix.** A missing table now counts as zero revisions.
- **Tests.** Two Trust-inspector cases were added. Against the unfixed inspector,
  one fails with `NullReferenceException` (red), and the other is a passing
  control. Both pass after the fix.
- **Replay.** The input is added to the candidate-4 replay set (42 cases).

### Other Local Runs On The Candidate

| Check | Result | Evidence |
| --- | --- | --- |
| Complete working-tree Solution, `PvadOriginalBaseline=true`, with all three owner probes, signed session | **Final candidate: build succeeded, 0 errors, 178 warnings**, macOS/x64 SDK 10.0.113, 549 s. An earlier build, before the string, Trust and test changes, had the same result (`artifacts/native-licensing-completion/1795463354-solution-build/`). | `artifacts/native-licensing-completion/1795465149-remaining-suites/solution-build.log` |
| `node tools/native-licensing/regressions.mjs`, run 1 | **Failed** at `PVAD.Importer.Tests` (one e-invoice failure from strict content strings; see section 3, string handling) | `artifacts/native-licensing-completion/1795464356-regressions/` |
| `regressions.mjs`, run 2 (before the Trust fix) | Licensing 118, Generator 141, Infrastructure.Pdf 76, Importer 732 and FileImporter 29 all pass with no skips. The runner then **stops at the four known ImageAnalysis snapshot failures** (177/181: `DebugOverlay_SavedVisualSnapshot_MatchesDigest` for invoice-table and clean-screenshot, and `RecognitionQuality_InvoiceTable_MatchesSnapshot` / `_CleanScreenshot_MatchesSnapshot`), so its later steps did not run in this invocation. | `artifacts/native-licensing-completion/1795464928-regressions/`, step evidence `artifacts/native-test-sessions/7fb3700b-5dc9-4bb8-938f-824a8463cf50/` |
| Remaining runner projects and other Importer consumers, run individually | Converters 20/20, Word 6/6 and Spreadsheet 13/13 (no Importer dependency). FileImporter.Image 8/8 and Nutrient migration 26/26 (built after the Trust fix). Export 221/221 ran **before** the Trust fix; the final rerun is below. | `artifacts/native-licensing-completion/1795465149-remaining-suites/summary.txt` |
| ImageOcr, first attempt | **Aborted, not test evidence.** `dotnet test` hung during the build: no child process, 0% CPU, and no log progress for 40 minutes, with no TRX. Only that owned PID was terminated (SIGTERM and SIGINT were ignored; SIGKILL worked). The wrapper reported failure and removed the isolated build, leaving only `run.json`. | same directory |
| Api.Tests with the runner's class filter, without PostgreSQL | 447 passed, 0 failed, **2 not executed**: the PostgreSQL container tests, which need `PVAD_RUN_POSTGRES_TESTS=1`. The wrapper correctly failed the run. | `artifacts/native-engine-tests/1795468387989-f55ba70f/` |
| **Final candidate reruns** (after the Trust fix), TRX counters audited | **Api.Tests (runner class filter) with `PVAD_RUN_POSTGRES_TESTS=1`: 449/449**, using a real temporary PostgreSQL test container. **Export 221/221. ImageOcr 104/104.** For all three, total = executed = passed, with 0 failed, error, timeout, aborted or not executed. | `artifacts/native-licensing-completion/1795496573-final-reruns/`; TRX `artifacts/native-engine-tests/1795496573685-ddc1c976/`, `…1795497129743-555a27b3/`, `…1795497604565-b2658683/` |
| **Final Importer suite** | **734/734**, counters audited | `artifacts/native-engine-tests/1795497907571-4618db10/` |
| Tooling: native check, runtime-source evidence, runtime assets, 250 node tests (native, version, release-author) | All pass, 0 skipped | console |
| `pvad-version check` / `fragments`, `git diff --check` | Pass | console |
| Default package-mode Solution build | Not run: needs the protected local feed | — |
| Complete unfiltered API/PostgreSQL audit, issuer, revocation, Designer, Documentation | Not rerun. Only the runner's PDF, licensing and migration API classes ran (above). Earlier results remain historical and do not bind this candidate. | earlier reports |
| Current-candidate original/protected package and platform checks | Not rerun during the original implementation round. The 2026-11-24 local macOS/x64 follow-up below now passes for the current source. Windows/Linux candidate acceptance remains outstanding and remote runs require owner authorization. | Follow-up below; [Workstream E](PVAD.Native-Licensing-Completion-Handoff.md#workstream-e-current-candidate-validation-and-package-acceptance) |
| Remote workflows (`native-library-licensing`, `visual-parity-platforms`, `pdf-trust-fuzz`, `ci`) | **Not run.** Not authorized. | — |

### Local Protected-Package Follow-Up (2026-11-24)

Executed `node tools/native-licensing/platform-package-check.mjs` against HEAD
`445519ca0340f9cd3fcd6c3ec5a3352de390d2e4` plus the current uncommitted parser
changes. The runner completed successfully on macOS with .NET SDK 10.0.113,
runtime 10.0.13, **x64**; the orchestrating Node process is arm64.

- Jointly protected 54 first-party assemblies and validated 52 independent
  package references through a local-feed consumer without network package
  sources. Public API, package documentation and unchanged third-party assembly
  checks passed.
- Original/protected PDF pixel and JSON/Word/Spreadsheet comparisons passed,
  including the mandatory repeat of the unprotected reference.
- Actual native OCR and CodeWorker execution passed, as did migration provider
  comparisons, 67 single-product checks, 62 Converter call cases and 12
  CodeWorker request cases.
- Both cleanup records confirm success. Ephemeral artifacts/native links were
  removed; embedded source trust and regular product build outputs are unchanged.
- `review-source-context.json` records HEAD and SHA-256 hashes for 1,291 tracked
  source/tooling/metadata/asset/build files, captured during the run. All hashes
  were rechecked after completion and still match. Documentation decisions were
  updated separately during execution.

Evidence:
`artifacts/native-platform-packages/1795542812412-c66577d0/report.json`,
`cleanup.json` and `review-source-context.json` in the same directory;
joint evidence in `artifacts/native-joint-packages/1795542814982-75540c4d/`.

This closes the missing local protected-package replay for this candidate, not
Windows/Linux acceptance, the full application audit, ImageAnalysis baseline
review, NPOI distribution approval or production trust/custody. The report keeps
`productWideAcceptance`, `productionTrustProvisioned` and `publicationPerformed`
false. No primary item closes; status remains **70/76 (92.1%)**.

## 4. ImageAnalysis Reference Context

No candidate baseline was generated, reviewed, approved or promoted. The suite
is unchanged and keeps its four known macOS snapshot failures and the recorded
Windows and Linux failures.

The investigation found a prerequisite decision for the owner. Every synthetic
fixture renders with system family names: `SKTypeface.FromFamilyName("Courier New")`
16 times, `"Arial"` twice and `"Times New Roman"` twice. The **production**
character atlas in `src/Importing/PVAD.FileImporter.ImageAnalysis/Templates/CharacterTemplates.cs`
resolves `fontSpec.Family` the same way. A pinned reference environment
therefore cannot identify its font bytes without deciding:

1. which font bytes define the reference, for example metric-compatible
   Liberation Mono/Sans/Serif, whose source and notices are partly already
   tracked, or separately licensed Microsoft core fonts; and
2. whether the production atlas gets an explicit font source, which is a
   shipped-behavior change, or only the test fixtures do.

Either option changes rendered pixels and therefore requires the reviewed
candidate process (handoff Workstream B steps 4–8). Installing fonts in a
container without that decision would not prove which bytes the renderer uses.

## 5. NPOI, Notices And Provenance

NPOI is not selected. The graph, notices, SBOM and `AcceptNPOIOSMFLicense` are
unchanged. The retained Tesseract wrapper build instructions request static
`giflib`, `libjpeg-turbo`, `liblzma`, `libpng`, `tiff` and `zlib` through vcpkg,
without a vcpkg commit, baseline or port versions. No authentic source binding
the four shipped DLL hashes to exact component revisions exists in the retained
evidence. Dates or current ports would be guesses and were not used. The
Liberation Sans signature stays blocked: no authenticated Debian keyring, and
no key was imported. The truncated Ukrainian model stays unresolved. No final
package inspection or distribution sign-off took place.

## 6. Trust, Key And Support Custody

No manufacturer key, custody record, support-map recipient or operator sign-off
was received. Nothing secret was requested, created or stored. Issuer mode was
not deployed, and no production license, live database change or production key
was created.

## 7. Cleanup And Unchanged Inputs

- Each Docker run used a unique `org.pvad.validation` label and `--rm`. After the final run, a Docker check found **0** containers with an `org.pvad.validation` label. The owner's own PostgreSQL, exporter and other containers were not touched. No Docker prune was used.
- Signed test sessions cleaned up through the existing wrappers. Scratch probes,
  scratch tools and source archives live only in the session scratchpad,
  outside the repository.
- `PVAD.sln` and `.vscode/` are untouched. The following are unchanged: VERSION,
  published releases, `TrustedKeys.json`, runtime evidence/assets, original fuzz
  inputs, failed-run directories, ImageAnalysis fixtures, snapshots and
  thresholds, and the fuzz harness and runner.

## 8. Review Focus And Final State

- The object parser now rejects an object that ends inside a string
  (`FormatException`). Previously, it either threw `ArgumentOutOfRangeException`
  or silently dropped the final byte. Content streams stay lenient but no longer
  drop a byte. Review whether strict object rejection is acceptable for
  truncated real-world files.
- New font limit and format rejections propagate like the existing invalid-hex
  `FormatException`: they fail the import instead of dropping only that font's
  mapping. Object-granular recovery (as for other `PdfParseLimitExceededException`
  cases) is a possible follow-up and is recorded, not implemented.
- Fonts and ToUnicode maps are re-parsed per page without a cache. Repeating an
  under-budget mapping across many pages remains a pre-existing amplification
  path. It was not observed by the campaigns and is recorded as a later finding.
- The ToUnicode budget also counts `bfchar` and array-form entries. Duplicate
  entries count toward the budget even though they overwrite.
- Final `git diff --check`: clean. Final `git status --short`:

  ```text
   M PVAD.sln                       (owner-owned, untouched by this work)
   M checklists/PVAD.Native-Library-Licensing-And-Protection.md
   M product-metadata/release-fragments/pdf-malformed-content-reliability.json
   M src/Importing/PVAD.Importer/Content/PdfContentStreamParser.cs
   M src/Importing/PVAD.Importer/Fonts/PdfSimpleFontParser.cs
   M src/Importing/PVAD.Importer/Fonts/PdfToUnicodeCMapParser.cs
   M src/Importing/PVAD.Importer/Parsing/PdfObjectParser.cs
   M src/Importing/PVAD.Importer/Tokenizer/PdfToken.cs
   M src/Importing/PVAD.Importer/Trust/PdfTrustInspector.cs
   M tests/PVAD.Importer.Tests/PdfMalformedContentTests.cs
  ?? .vscode/                       (owner-owned)
  ?? checklists/PVAD.Native-Licensing-Completion-Report.md
  ```
