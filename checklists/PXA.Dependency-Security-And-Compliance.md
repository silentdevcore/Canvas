# PXA Dependency Security and Compliance

## Purpose

Track the controls that keep third-party dependencies secure, traceable, and legally approved across the PXA monorepo. Generated SBOMs are the complete dependency inventory; the curated compliance catalog records exceptional approval decisions and production blockers.

## P0 - Enforced Supply-Chain Controls

- [x] Add a deterministic repository compliance catalog and validator.
- [x] Scan direct and transitive NuGet dependencies for known vulnerabilities in CI.
- [x] Audit production npm dependencies for Designer, Admin, and MCP packages in CI.
- [x] Configure Dependabot for NuGet, npm, Docker, and GitHub Actions against `develop`.
- [x] Generate SPDX JSON SBOMs for the WebApi publish output, Designer build, and WebApi container image.
- [x] Upload generated SBOMs as immutable CI artifacts.
- [x] Expose a sanitized dependency-compliance status to System Administrators.
- [x] Add API and Admin UI contract tests for the protected status.
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

## Dependencies and Ownership

- [ ] Security owns vulnerability policy and exception approval.
- [ ] Legal owns license interpretation and approval, including the NPOI OSMF EULA.
- [ ] Engineering owns dependency updates, SBOM generation, and deterministic validation.
- [ ] Release Management verifies that required gates pass before publishing.
- [ ] Operations retains SBOM and scan artifacts according to the approved retention policy.

## Testing

- [x] Reject malformed or internally inconsistent compliance metadata.
- [x] Reject an undeclared NPOI dependency or an unapproved acceptance flag.
- [x] Reject known vulnerable direct or transitive NuGet packages.
- [x] Reject high or critical production npm vulnerabilities.
- [x] Verify Dependabot coverage for every managed package ecosystem and directory.
- [x] Verify API authorization, no-store caching, and sanitized output.
- [x] Verify Admin loading, failure, refresh, and production-blocker presentation.
- [ ] Exercise SBOM generation in CI for API, Designer, and container outputs.
- [ ] Verify that published SBOMs match the exact release version and commit.

## Acceptance Criteria

- [ ] No stable production release can bypass vulnerability, SBOM, or required license gates.
- [ ] Every shipped API, Designer, and container artifact has a matching SPDX SBOM.
- [ ] System Administrators can identify pending compliance decisions without seeing secrets or raw scanner output.
- [ ] Every exceptional license decision has a recorded owner, decision, evidence, and review date.
- [ ] The NPOI OSMF EULA is either explicitly approved or NPOI is absent from production artifacts.

## Deferred Decisions

- [ ] Choose the long-term license scanning and policy engine.
- [ ] Decide whether signed SBOM attestations are public or customer-authenticated.
- [ ] Define the final vulnerability exception SLA and emergency release procedure.
