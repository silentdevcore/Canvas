# PXA NPOI License Decision

## Purpose

Resolve the production blocker created by the NPOI 2.8.0 binary-package EULA while preserving a deliberate product decision for legacy Excel `.xls` support. This checklist records engineering evidence; qualified Legal counsel must approve the final licensing decision.

## Current Usage Inventory

- [x] Confirm that `PXA.Infrastructure.Spreadsheet` is the only project that directly requires NPOI at runtime.
- [x] Confirm that `XlsWorkbookIo` uses NPOI HSSF for BIFF8 `.xls` import and export.
- [x] Confirm that the WebApi exposes this dependency through spreadsheet `.xls` import and export operations.
- [x] Confirm that `PxaWorkbookBuilder.Save` uses it when the requested output extension is `.xls`.
- [x] Confirm that NPOI migration providers analyze source text through Roslyn and do not require the NPOI runtime package.
- [x] Remove the unused NPOI package reference from `PXA.Infrastructure.Converters`.
- [x] Keep `.xlsx`, CSV, and TSV processing independent of NPOI.

## License Evidence

- [x] Record that the NPOI 2.8.0 NuGet package identifies `OSMFEULA.txt` as its package license.
- [x] Record that the packaged EULA applies a maintenance fee to revenue-generating users with annual gross revenue of at least USD 10,000, subject to its stated exemptions.
- [x] Record that the packaged EULA distinguishes the distributed binary release from source code and self-compiled binaries under Apache-2.0.
- [x] Keep `AcceptNPOIOSMFLicense` disabled until a written decision and payment determination exist.
- [ ] Obtain a written interpretation from qualified Legal counsel for PXA Cloud, On-Premise images, SDK distribution, customer redistribution, and CI artifacts.
- [ ] Preserve the reviewed EULA text, package version, counsel decision, decision owner, evidence reference, and review date in the approved compliance record.

Authoritative evidence reviewed on 2026-08-02:

- [NPOI 2.8.0 package metadata and binary-release notice](https://www.nuget.org/packages/NPOI/2.8.0)
- [NPOI upstream source repository](https://github.com/nissl-lab/npoi)
- [ClosedXML supported Excel 2007+ formats](https://closedxml.io/ClosedXML/)
- [ExcelDataReader supported input formats](https://github.com/ExcelDataReader/ExcelDataReader)

## Decision Options

### Option A - Approve the NPOI Binary EULA

- [ ] Confirm whether PXA meets the revenue threshold and whether a support or maintenance exemption applies.
- [ ] Confirm the required maintenance fee, payment channel, renewal cadence, and redistribution obligations with the NPOI maintainer.
- [ ] Obtain written Legal and commercial approval.
- [ ] Record payment or exemption evidence without storing secrets in the repository.
- [ ] Set `AcceptNPOIOSMFLicense` only after approval and update the compliance catalog atomically.
- [ ] Retain full `.xls` import and export behavior and run the existing round-trip tests.

Engineering assessment: lowest implementation risk and the recommended route when full legacy `.xls` support is commercially required.

### Option B - Build NPOI From Reviewed Source

- [ ] Ask Legal to confirm that the selected source revision and self-built binaries may be used and redistributed under Apache-2.0 without the binary-release agreement.
- [ ] Pin the exact upstream source commit corresponding to the reviewed release.
- [ ] Create a reproducible, isolated source-build pipeline with provenance, checksums, vulnerability scanning, SBOM output, and license notices.
- [ ] Publish the internal package under an unambiguous package identity and prohibit accidental fallback to the public binary package.
- [ ] Define ownership for upstream security patches and compatibility testing.
- [ ] Verify functional parity for formulas, typed values, merges, column widths, malformed files, and Linux containers.

Engineering assessment: preserves `.xls` behavior but transfers package maintenance and supply-chain responsibility to PXA.

### Option C - Replace Or Reduce Legacy XLS Support

- [ ] Decide whether `.xls` import-only support is sufficient; ExcelDataReader can be evaluated for BIFF input but does not replace export.
- [ ] Decide whether `.xls` export may be removed in favor of `.xlsx`, CSV, and TSV.
- [ ] If full import and export remain required, evaluate a commercially licensed spreadsheet engine against the PXA deployment and redistribution model.
- [ ] Do not treat ClosedXML as a direct replacement because it targets Excel 2007+ OpenXML formats rather than legacy BIFF `.xls`.
- [ ] Document any removed capability as a breaking product change and provide migration guidance.
- [ ] Remove NPOI from all production dependency graphs, SBOMs, containers, and license records before clearing the blocker.

Engineering assessment: import-only replacement is feasible; a free, maintained, drop-in replacement for both current import and export behavior has not been identified.

## Recommendation And Decision Gate

- [x] Recommend Option A when PXA commits to full `.xls` import and export because it has the smallest engineering and compatibility risk.
- [x] Recommend Option C with `.xls` export retirement when legacy export has low commercial value and minimizing special licensing obligations is more important.
- [ ] Product confirms whether full `.xls` export is a launch requirement.
- [ ] Legal selects and signs off Option A, B, or C.
- [ ] Engineering implements only the approved option.
- [ ] Release Management clears `productionReady` only after evidence and implementation are complete.

## Validation

- [ ] Verify that no project other than the approved `.xls` implementation directly references NPOI.
- [ ] Verify that the compliance validator rejects undeclared NPOI usage and premature EULA acceptance.
- [ ] Verify `.xls` import and export behavior for Option A or B, or the approved compatibility response for Option C.
- [ ] Verify that release SBOMs and third-party notices match the selected implementation.
- [ ] Verify that Docker, Cloud, On-Premise, and SDK distribution paths follow the same approved decision.
- [ ] Run the spreadsheet infrastructure tests, export tests, WebApi tests, dependency validator, and full solution build.

## Acceptance Criteria

- [ ] A named Legal approver, decision date, evidence reference, and review date exist.
- [ ] The repository configuration cannot silently accept or reintroduce the unapproved binary EULA.
- [ ] Product documentation accurately states whether legacy `.xls` import and export are supported.
- [ ] No stable production release proceeds while the license decision remains pending.
- [ ] The selected implementation has matching tests, SBOM data, notices, and deployment guidance.
