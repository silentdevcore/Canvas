# PXA Admin Documentation Checklist

## Goal

Deliver complete English-language administration documentation for customer administrators, application integrators, and separately authorized platform operators.

## Protected Admin Handbook

- [x] Keep all administration documentation inside the authenticated `PXA.Admin` application.
- [x] Add `/documentation` to the protected PXA.Admin navigation.
- [x] Require an authenticated Administrator session before rendering any handbook content.
- [x] Serve handbook JSON and screenshots through an authorized, no-store Admin API instead of public static assets.
- [x] Require `System Administrator` or `Organization Administrator` for handbook and image endpoints.
- [x] Exclude the handbook, its data module, screenshots, and navigation from public `PXA.Documentation`.
- [x] Document Admin access, login, organization context, and dashboard behavior.
- [x] Document users, invitations, profile changes, status, recovery, sessions, seats, roles, and bulk actions.
- [x] Document organizations, memberships, tenant switching, and last-administrator protections.
- [x] Document roles and permissions separately from subscriptions, seats, and product entitlements.
- [x] Document subscription lifecycle, capabilities, usage, and seat management.
- [x] Document offline-license issuance, validation, download, revocation, replacement expectations, and expiry.
- [x] Document service accounts, one-time API-key secrets, expiry, and revocation.
- [x] Document mail delivery, retries, cancellation, failure states, and protected metadata.
- [x] Document audit search, detail, filtering, export limits, and tenant boundaries.
- [x] Document common UI states, diagnostics, troubleshooting, and safe support escalation.
- [x] Document retention approval status, safe dry runs, global and organization Legal Holds, audit effects, and the absence of manual cleanup execution.

## Technical Reference

- [x] Document the dedicated Admin host and same-origin API architecture.
- [x] Document session cookies, CSRF, API-key boundaries, authorization, and tenant isolation.
- [x] Document `/api/pxa/v1/admin`, pagination expectations, Problem Details, and `PXAAPI001` through `PXAAPI008`.
- [x] Provide route-to-documentation coverage for every implemented Admin workspace.
- [x] Use synthetic examples and prohibit credentials, tokens, signing material, and customer data.

## Restricted Operator Guidance

- [x] Classify the customer-facing Admin handbook as protected product documentation.
- [x] Classify the operator guide as a restricted operational guideline/runbook.
- [x] Create a separate operator guide outside both public Documentation and the PXA.Admin bundle.
- [x] Cover bootstrap boundaries, operator allowlist, hosting, reverse proxy, migrations, readiness, recovery, and emergency access principles.
- [x] Add a restricted Legal backup, restore, verification, rollback, and disaster-recovery runbook outside the Admin and public Documentation bundles.
- [x] Add an authenticated operator-documentation deployment with gateway and API enforcement for allowlisted System Administrators.
- [ ] Add deployment-specific runbooks after Cloud and Docker hosting are implemented.

## Presentation And Validation

- [x] Integrate Admin topics with sidebar search, active state, and focus mode.
- [x] Add permissions, prerequisites, steps, result, failures, audit effect, endpoint, and related guidance to every workflow.
- [x] Capture and maintain sanitized desktop/mobile screenshots for all major Admin workspaces.
- [x] Add a reproducible Playwright capture script with intercepted synthetic API fixtures.
- [x] Add contract tests for navigation, coverage, links, restricted-content exclusion, and assets.
- [x] Add API tests proving anonymous handbook and screenshot downloads return HTTP 401.
- [x] Run the PXA.Admin production build and branding/secret scans.

## Acceptance Criteria

- [x] Authenticated Organization Administrators can follow normal administration workflows from the protected Admin handbook.
- [x] System-Administrator-only actions are explicitly identified.
- [x] Roles, permissions, seats, subscriptions, and entitlements are distinct concepts.
- [x] Every documented privileged workflow explains its audit effect.
- [x] Restricted operator guidance is absent from public navigation and output.
- [x] Admin handbook content and screenshots are absent from the public PXA.Documentation build.
- [x] PXA.Company receives no Admin navigation link.
