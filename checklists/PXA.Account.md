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

- [ ] P0: Deliver secure customer registration, verification, sign-in, recovery, and Trial creation.
- [ ] P0: Deliver personal and organization account management.
- [ ] P1: Deliver subscriptions, usage, licenses, API keys, downloads, and security sessions.
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
- [ ] Display explicit expired-session, locked-account, verification-required, and suspended-account states. (expired-session/locked-account/verification-required done; suspended-account deferred to the dashboard work in Phase 3/6 of [PXA.Account.Portal-Implementation.md](PXA.Account.Portal-Implementation.md))

## Customer Portal

- [ ] Add dashboard, profile, organization, members, subscription, usage, licenses, developer access, security, and support routes.
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
- [ ] Preserve campaign attribution with an allowlisted, privacy-safe registration context.
- [ ] Update pricing and Trial copy only after commercial limits are approved.

## API And Data

- [ ] Add canonical customer endpoints under `/api/pxa/v1/account`.
- [x] Add registration endpoints under `/api/pxa/v1/auth/register` and `/verify-email`.
- [x] Use application services for registration and Trial orchestration rather than controller-owned transactions.
- [x] Keep account type, application roles, subscription edition, and product entitlements as separate concepts.
- [ ] Resolve organization scope from the authenticated server context after login.
- [ ] Return stable Account-specific Problem Details codes for validation, conflicts, authentication, authorization, and lifecycle failures.
- [x] Never return password hashes, action-token hashes, API-key hashes, mail payloads, or private license material.

## Mail Dependencies

- [x] Send registration verification, welcome, password-reset, and email-change messages through `PXA.Mail-Service.md`; additional security notifications remain open.
- [x] Include Trial activation in required welcome delivery; dedicated Trial-expiry notifications remain open.
- [ ] Keep newsletter consent optional and separate from required account communications.
- [ ] Support localized Account links and templates.

## Security And Privacy

- [x] Use HttpOnly, Secure, SameSite cookies and antiforgery protection for browser mutations.
- [x] Add login, registration, reset, and verification rate limits.
- [x] Record versioned Terms and Privacy acceptance in audit metadata without treating it as marketing consent.
- [ ] Protect customer data through tenant isolation and least-privilege policies.
- [x] Add audit records for registration, verification, Trial creation, and existing identity/session changes; customer license and key actions remain open.
- [ ] Add structured logs without credentials, tokens, document contents, or unnecessary personal data.

## Tests

- [ ] Unit-test registration validation, Trial eligibility, return URLs, and customer authorization policies.
- [x] Integration-test Company registration, verification, pre-verification rejection, login, Trial creation, entitlements, and token reuse against PostgreSQL.
- [ ] Test Individual Developer registration and both organization models atomically.
- [ ] Test duplicate organization, repeated Trial, and concurrent registration conflicts; duplicate-email behavior has baseline coverage.
- [ ] Test cross-tenant profile, organization, subscription, license, key, and session access attempts.
- [ ] Test Company-to-Account links and authenticated return flows.
- [ ] Test keyboard navigation, focus management, responsive layouts, and accessible validation.
- [ ] Build PXA.Account and run desktop/mobile smoke tests.

## Acceptance Criteria

- [ ] A new customer can register, verify email, receive one Trial, sign in, and reach the customer dashboard.
- [ ] Individual Developer and Company registrations create the correct organization ownership model.
- [ ] PXA.Company exposes customer sign-in and Trial entry points without exposing PXA Admin.
- [ ] Customer users cannot access Admin routes or privileged Admin APIs.
- [ ] Account roles cannot grant products not enabled by subscription entitlements.
- [ ] Registration and recovery do not leak whether unrelated customer identities exist.
- [ ] All privileged customer changes are tenant-scoped and auditable.

## Deferred Decisions

- [ ] Select exact Free, Trial, and Premium limits before public checkout.
- [ ] Select a billing provider before paid self-service subscription changes.
- [ ] Select Cloud CAPTCHA or bot-protection technology before public launch.
- [ ] Define MFA and enterprise SSO milestones after the P0 customer flow is stable.
