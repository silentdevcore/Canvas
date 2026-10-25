# PXA Dependency Security and Compliance

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Historical PXA/Canvas product references, implementation notes and compatibility identifiers are retained; task statuses are unchanged.

## Purpose

Track the controls that keep third-party dependencies secure, traceable, and legally approved across the PXA monorepo. Generated SBOMs are the complete dependency inventory; the curated compliance catalog records exceptional approval decisions and production blockers.

## P0 - Enforced Supply-Chain Controls

- [x] Add a deterministic repository compliance catalog and validator.
- [x] Scan direct and transitive NuGet dependencies for known vulnerabilities in CI.
- [x] Audit production npm dependencies for Designer, Admin, and MCP packages in CI.
- [x] Configure Dependabot for NuGet, npm, Docker, and GitHub Actions against `develop`.
- [x] Upgrade GitHub and Docker workflow actions to Node.js 24-compatible releases and pin every external action to an immutable commit SHA.
- [x] Generate SPDX JSON SBOMs for the WebApi publish output, Designer build, and WebApi container image.
- [x] Upload generated SBOMs as immutable CI artifacts.
- [x] Expose a sanitized dependency-compliance status to System Administrators.
- [x] Add API and Admin UI contract tests for the protected status.
- [x] Inventory NPOI production usage, remove its unused converter dependency, and document the legal and technical decision options.
- [ ] Obtain written legal approval for the NPOI 2.8.0 OSMF EULA and explicitly accept it in the build, or replace NPOI.
- [ ] Keep production release approval blocked while any required license decision is pending.
- [ ] Migrate Designer routing to React Router 7 after compatibility testing to remove the remaining moderate advisories without a forced major update.

## P1 - Release and Operations Integration

- [ ] Sign release SBOMs and publish checksums with release artifacts.
- [ ] Attach an SBOM attestation to each published container image.
- [ ] Add license-policy evaluation for forbidden, restricted, and notice-required licenses.
- [ ] Generate the distributable third-party notices file from resolved release dependencies.
- [ ] Define vulnerability remediation deadlines by severity and deployment exposure.
- [ ] Add an audited exception workflow with owner, rationale, expiry, and compensating controls.
- [ ] Surface dependency age and unresolved update risk without exposing internal package paths publicly.
- [ ] Document customer access to SBOMs and security advisories for Enterprise deployments.

## Cryptographic Dependency Remediation - 2026-10-25

- [x] Record the online baseline: 111 projects, 103 repeated finding instances, and five distinct Microsoft advisories.
- [x] Update both direct PKCS references and all three direct XML references to the patched .NET 10 package version `10.0.13`.
- [x] Correct the compliance validator's obsolete XML pin and add enforcement for both PKCS references.
- [x] Add a regression contract for all five direct pins and verify red/green behavior.
- [x] Restore the entire solution and verify all retained crypto package entries resolve to `10.0.13`.
- [x] Pass the online direct/transitive NuGet scan and existing CI vulnerability gate for all 111 projects with zero known findings.
- [x] Verify an isolated SDK `10.0.113` archive against the official SHA-512 and build the full solution with patched runtimes `10.0.13`.
- [x] Run and record the full patched-runtime x64 .NET suite: 3,428 passed, 82 skipped, three failed baseline checks; do not claim a green full suite.
- [x] Pass all 646 importer/signing tests, 1,168 API tests, 81 infrastructure tests, and 220 export tests; remove the disposable API test database.
- [x] Normalize the synthetic OCSP fixture clock to whole seconds without relaxing production validation or test assertions.
- [x] Verify that the loaded PKCS and XML shared-framework assemblies are both `10.0.13`.
- [x] Add the owner-reviewed customer-facing Patch security fragment without changing the product version or published manifest.
- [x] Review the two runtime-dependent compressed-archive byte baselines while preserving original fixtures, exact uncompressed content, metadata, order and same-runtime byte determinism; reject content/metadata/entry mutations.
- [x] Independently validate all seven patched-runtime invoice PDFs before approving replacement PDF/A validation-input hashes; preserve the previous manifest byte-for-byte, retain raw evidence, and compare all 11 raster pages at 144 DPI, extracted text and attachments.
- [x] Prevent fixture tests from rewriting approved manifests and bind current/previous hashes to the recorded validation outcomes.
- [x] Obtain a green full regression run on the patched target runtime after those explicit baseline decisions: 53 completed project reports, 3,437 passed, 82 skipped, zero failed, process exit code 0; disposable database removed. Keep the initial timeout/failed reports as evidence.
- [ ] Update the system-wide developer runtime; the existing `10.0.5` installation is not covered by the isolated test SDK.
- [ ] Rebuild, inventory, and scan final runtime/container artifacts before production release; a clean package audit does not validate shared-framework, OS, or native-library patch levels.
- [ ] Re-run deployment smoke tests on those exact release artifacts.

See [the evidence and runtime boundary](../docs/pvad-cryptography-security-update.md).
NPOI approval and the remaining rename gates are not completed by this maintenance.

## Dependencies and Ownership

- [ ] Security owns vulnerability policy and exception approval.
- [ ] Legal owns license interpretation and approval, including the NPOI OSMF EULA.
- [x] Keep third-party license approvals separate from customer Terms, Privacy acknowledgements, and optional consent evidence defined in `PVAD.Legal-Terms-Privacy-And-Consent.md`.
- [ ] Engineering owns dependency updates, SBOM generation, and deterministic validation.
- [ ] Release Management verifies that required gates pass before publishing.
- [ ] Operations retains SBOM and scan artifacts according to the approved retention policy.

## Testing

- [x] Reject malformed or internally inconsistent compliance metadata.
- [x] Reject an undeclared NPOI dependency or an unapproved acceptance flag.
- [x] Reject known vulnerable direct or transitive NuGet packages.
- [x] Reject high or critical production npm vulnerabilities.
- [x] Verify Dependabot coverage for every managed package ecosystem and directory.
- [x] Reject workflow changes that introduce a moving external Action tag or an unapproved Node.js 20 Action release.
- [x] Verify API authorization, no-store caching, and sanitized output.
- [x] Verify Admin loading, failure, refresh, and production-blocker presentation.
- [x] Exercise SBOM generation in CI for API, Designer, and container outputs.
- [ ] Verify that published SBOMs match the exact release version and commit.

## Acceptance Criteria

- [ ] No stable production release can bypass vulnerability, SBOM, or required license gates.
- [ ] Every shipped API, Designer, and container artifact has a matching SPDX SBOM.
- [ ] System Administrators can identify pending compliance decisions without seeing secrets or raw scanner output.
- [ ] Every exceptional license decision has a recorded owner, decision, evidence, and review date.
- [x] Every external GitHub Action is reproducibly pinned and the core workflow actions use Node.js 24 runtimes.
- [ ] The NPOI OSMF EULA is either explicitly approved or NPOI is absent from production artifacts.

## Deferred Decisions

- [ ] Choose the long-term license scanning and policy engine.
- [ ] Decide whether signed SBOM attestations are public or customer-authenticated.
- [ ] Define the final vulnerability exception SLA and emergency release procedure.

## Related Decision Record

- [x] Record the NPOI usage inventory, replacement matrix, recommendation, and approval evidence requirements in `PVAD.NPOI-License-Decision.md`.
- [x] Record the boundary to customer-facing Legal documents and consent evidence in `PVAD.Legal-Terms-Privacy-And-Consent.md`.
