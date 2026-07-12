# Word Fidelity V2 Beta Runbook

Last updated: 2026-05-19

## Objective

Run a controlled internal beta for `word_fidelity_v2`, collect mismatch artifacts, and decide default/finalization gates.

## Preconditions

- `word_fidelity_v2` flag available via export options (`ExportOptions.WordFidelityV2`).
- Baseline sample pack and regression/fidelity suites are green.
- Artifact output path is available:
  - `tests/PXA.Export.Tests/Fidelity/artifacts/latest/`

## Beta Scope

- Internal users only.
- Start with template categories:
  - invoice/report
  - table-heavy
  - image-heavy
  - form/utility
  - positioned/overlap

## Execution Steps

1. Run preflight:
   - `./checklists/word-fidelity-preflight.sh`
2. Enable `word_fidelity_v2` for beta tenants/users.
3. Export candidate templates in both modes:
   - `WordFidelityV2=true`
   - `WordFidelityV2=false` (fallback baseline)
4. Run fidelity harness:
   - `dotnet test tests/PXA.Export.Tests/PXA.Export.Tests.csproj --filter "WordPdfFidelityTests"`
5. Collect artifacts from:
   - `tests/PXA.Export.Tests/Fidelity/artifacts/latest/`
6. Collect user-reported mismatches:
   - template id
   - page number
   - element id/type
   - screenshot or generated file
   - whether fallback mode resolved issue

## Triage Rules

- Critical: export crash, missing core content, unreadable document.
- High: major layout drift, broken table/image placement.
- Medium: typography/style drift with readable content.
- Low: minor spacing/pixel-level drift.

## Promotion Gates

- No export-blocking exceptions in beta sample set.
- Regression suite remains green.
- Fidelity report generated for current sample pack.
- Open Critical issues: 0.
- Open High issues: <= agreed team threshold.

## Default Finalization Plan

1. Keep `WordFidelityV2=true` as default for new exports.
2. Maintain fallback (`WordFidelityV2=false`) during stabilization window.
3. After stabilization gates are met:
   - remove fallback path usage from runtime configs
   - keep tests for migration safety
   - update docs/changelog

## Rollback Plan

- Toggle `WordFidelityV2=false` for impacted scopes.
- Re-run regression and affected sample exports.
- Attach new artifacts to incident thread.
