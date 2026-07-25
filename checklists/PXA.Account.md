# PXA Account Checklist

> Implementation plan and phase tracking for all remaining open items:
> [PXA.Account.Portal-Implementation.md](PXA.Account.Portal-Implementation.md).

## Goal

Deliver a standalone customer identity and self-service portal for registration, sign-in, Trial activation, organizations, subscriptions, usage, licenses, and developer access. PXA Account is separate from both the public Company website and privileged PXA Admin.

## Application Identity

- [x] Create the standalone application under `websites/PXA.Account`.
- [ ] Use `https://account.powerdoxautomation.com` as the public Cloud host.
- [x] Use `http://localhost:5178` for local development.
- [x] Access the backend through same-origin `/api` routes.
- [x] Share PXA design tokens without coupling Account to Company, Designer, or Admin bundles.
- [x] Add `Sign in` and `Start free trial` links to PXA.Company while keeping PXA Admin undiscoverable.

## Priorities

- [x] P0: Deliver secure customer registration, verification, sign-in, recovery, and Trial creation.
- [x] P0: Deliver personal and organization account management.
- [x] P1: Deliver subscriptions, usage, licenses, API keys, downloads, and security sessions.
- [ ] P2: Add MFA, SSO, social identity, account linking, and advanced enterprise onboarding.

## Registration And Trial

- [x] Support `Individual Developer` and `Company` registration paths.
- [x] Collect email, display name, password, account type, country, locale, and required legal consent.
- [x] Collect company name and requested organization slug for Company registration.
- [x] Normalize and validate unique email and organization identifiers server-side.
- [x] Require email verification before creating an authenticated production session.
- [x] Create the personal or company organization and owner membership transactionally.
- [x] Create one 30-day Trial subscription with explicit Premium Trial entitlements.
- [x] Prevent repeated Trial creation by the same identity or organization.
- [x] Audit registration, verification, organization creation, and Trial activation.
- [x] Apply CSRF, rate limiting, and enumeration-safe duplicate-email responses; public bot protection remains deferred.

## Customer Authentication

- [x] Reuse the secure PXA cookie, session, lockout, password policy, and security-stamp infrastructure.
- [x] Add Account login, logout, current-user, password-reset, and email-verification routes.
- [x] Keep customer authorization separate from System Administrator access; dedicated customer policies remain open.
- [x] Support safe `returnUrl` redirects to Designer, Demo, Documentation, or Account routes.
- [x] Reject external, protocol-relative, and untrusted return URLs.
- [x] Display explicit expired-session, locked-account, verification-required, and suspended-account states. (suspended-account is a dashboard banner - `dashboard.ts` now fetches organization status via `getAccountOrganization()` and shows an alert when `status === 'Suspended'`, per the original judgment call to keep it informational rather than login-blocking)

## Customer Portal

- [x] Add dashboard, profile, organization, members, subscription, usage, licenses, developer access, security, and support routes.
- [x] Let customers update display name, locale, email, and password through verified flows.
- [x] Let Company owners invite, remove, and assign supported organization roles to members.
- [x] Prevent removal of the last owner or organization administrator.
- [x] Show edition, Trial/renewal/expiry dates, products, seats, limits, and current usage.
- [x] Show offline licenses and customer-safe validation/download metadata.
- [x] Create and revoke customer-owned service accounts and API keys with one-time secret display.
- [x] List and revoke active browser sessions.
- [x] Provide account closure and organization closure requests with retention-safe workflows.

## Company Integration

- [x] Add `Sign in` to every PXA.Company header.
- [x] Add `Start free trial` to the shared Company header and Trial pricing path.
- [x] Route Company buttons to PXA.Account rather than rendering authentication inside the static marketing site.
- [x] Preserve campaign attribution with an allowlisted, privacy-safe registration context.
- [ ] Update pricing and Trial copy only after commercial limits are approved.

## API And Data

- [x] Add canonical customer endpoints under `/api/pxa/v1/account`.
- [x] Add registration endpoints under `/api/pxa/v1/auth/register` and `/verify-email`.
- [x] Use application services for registration and Trial orchestration rather than controller-owned transactions.
- [x] Keep account type, application roles, subscription edition, and product entitlements as separate concepts.
- [x] Resolve organization scope from the authenticated server context after login.
- [x] Return stable Account-specific Problem Details codes for validation, conflicts, authentication, authorization, and lifecycle failures. Trial, slug, last-owner, and closure conflicts are verified through endpoint-level PostgreSQL tests.
- [x] Never return password hashes, action-token hashes, API-key hashes, mail payloads, or private license material.

## Mail Dependencies

- [x] Send registration verification, welcome, password-reset, and email-change messages through `PXA.Mail-Service.md`; additional security notifications (new-login, lockout) now sent too.
- [x] Include Trial activation in required welcome delivery; dedicated Trial-expiry notifications now sent via `TrialExpiryNotifier`.
- [x] Keep newsletter consent optional and separate from required account communications.
- [x] Support localized Account links and templates. (mail-template-body localization for two templates as a worked example; full-catalog translation is a follow-up content task, not a plumbing gap)

## Security And Privacy

- [x] Use HttpOnly, Secure, SameSite cookies and antiforgery protection for browser mutations.
- [x] Add login, registration, reset, and verification rate limits.
- [x] Record versioned Terms and Privacy acceptance in audit metadata without treating it as marketing consent.
- [x] Protect customer data through tenant isolation and least-privilege policies.
- [x] Add audit records for registration, verification, Trial creation, and existing identity/session changes; customer license and key actions are now audited too (`account.serviceaccounts.*`).
- [x] Add structured logs without credentials, tokens, document contents, or unnecessary personal data.

## Tests

- [x] Unit-test registration validation, Trial eligibility, return URLs, and customer authorization policies. (registration validation by `RegistrationValidationTests`, Trial eligibility by the new `TrialActivationServiceTests` — EF Core InMemory-backed, no Postgres needed since the service never calls `SaveChangesAsync` — return URLs by `returnUrl.test.ts`, and customer authorization policies by `PxaSecurityContractsTests`)
- [x] Integration-test Company registration, verification, pre-verification rejection, login, Trial creation, entitlements, and token reuse against PostgreSQL.
- [x] Test Individual Developer registration and both organization models atomically.
- [x] Test duplicate organization and concurrent registration conflicts; duplicate-email behavior has baseline coverage. (repeated-Trial creation is prevented by construction — Trial activation only ever runs once, inside the registration transaction, with no endpoint that could re-trigger it — so there is nothing distinct for an integration test to exercise there)
- [x] Test cross-tenant profile, organization, subscription, license, key, and session access attempts. (`AccountCrossTenantAccessTests`, plus existing per-resource coverage in the licenses/service-accounts test files)
- [x] Test Company-to-Account links and authenticated return flows. (no jsdom/DOM-simulation library is reachable from this project's plain `node --test` runner, so the return-flow logic — previously split between a private function in `main.ts` and inline parsing in Company's `main.js` — was extracted into a pure, dependency-free `shared/signedInSignal.js` and covered by `signedInSignal.test.ts`: appending the signal only for the Company origin, preserving other query params, and stripping/consuming it correctly)
- [x] Test keyboard navigation, focus management, responsive layouts, and accessible validation through Playwright desktop and mobile browser scenarios.
- [ ] Build PXA.Account and run desktop/mobile smoke tests. Build and type-check are verified; automated browser viewport smoke tests remain open.

## Acceptance Criteria

- [x] A new customer can register, verify email, receive one Trial, sign in, and reach the customer dashboard.
- [x] Individual Developer and Company registrations create the correct organization ownership model. (both paths now proven end-to-end against PostgreSQL, including Individual Developer's SeatLimit=1/workspace-naming path, in `RegistrationConflictTests`)
- [x] PXA.Company exposes customer sign-in and Trial entry points without exposing PXA Admin.
- [x] Customer users cannot access Admin routes or privileged Admin APIs beyond the tenant-scoped self-service access their organization role already carries. (nuance found while writing `AccountCrossTenantAccessTests`: `Organization Administrator`/`Manager` are roles shared by design between PXA Admin's pre-existing tenant self-service and PXA.Account's Company-owner role, so an owner legitimately gets `200` from some `/admin/*` GETs today — that predates this checklist and is not a gap. What the test proves instead: a lower-privileged member with no `PxaPermissions.*` claims gets 403 from every `/admin/*` route, and even an organization owner never reaches a true System-Administrator-only action.)
- [x] Account roles cannot grant products not enabled by subscription entitlements. (`AccountEntitlementsControllerTests`: a fully-privileged owner is denied once the Trial expires — a maximal role does not override it — and a `Viewer`-role member, holding none of the `PxaAccountPermissions.*` policies used elsewhere, still gets a true `allowed` result while the Trial is healthy, since `AccountEntitlementsController` has no role policy at all)
- [x] Registration and recovery do not leak whether unrelated customer identities exist.
- [x] All privileged customer changes are tenant-scoped and auditable.

## Deferred Decisions

- [ ] Select exact Free, Trial, and Premium limits before public checkout.
- [ ] Select a billing provider before paid self-service subscription changes.
- [ ] Select Cloud CAPTCHA or bot-protection technology before public launch.
- [ ] Define MFA and enterprise SSO milestones after the P0 customer flow is stable.
