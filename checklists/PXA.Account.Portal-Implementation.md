# PXA Account — Customer Portal & Remaining Checklist Implementation

Tracking checklist for closing every remaining unchecked item in
[PXA.Account.md](PXA.Account.md). That checklist defines the *what*; this one
tracks the *how*, phased so each step is independently shippable with green
tests. Branch: `pxa-account-all-open-points`.

## Goal & approach

`PXA.Account.md` has registration, verification, and Trial creation done and
integration-tested. Everything downstream of login — the Customer Portal
(profile, organization/members, subscription, usage, licenses, developer
access, security sessions, closure), the canonical `/api/pxa/v1/account/*`
API surface, `returnUrl` handling, and most tests — is unbuilt.
`websites/PXA.Admin` and its `Admin*Controller`s already implement almost
every Customer Portal capability system-wide. Strategy: **extract shared
logic into services once, mirror it into a tenant-scoped `Account*Controller`,
and have the Admin controller call the same service** — never duplicate
last-owner-protection, one-time-secret, or session-revocation logic a second
time.

## Judgment calls (recorded, not re-litigated per phase)

1. Application-service layer lives in `PXA.WebApi/Application/`, not
   `src/Core/PXA.Application` (the latter has zero Identity/EF/Mail coupling
   today).
2. One extended `PxaApiProblems` registry (new `PXAAPI009`–`014` constants),
   not a second class.
3. New `PxaAccountPermissions` policy set, mapped from the same org-scoped
   role claims Admin already uses.
4. `returnUrl` validation is frontend-only (`websites/shared/returnUrl.js`) —
   no backend redirect surface exists or should be created.
5. Account's portal shell mirrors Admin's shape but does not import a shared
   runtime module — only pure/stateless code is actually shared.
6. `websites/PXA.Account/src/main.js` splits into a router + per-page modules
   from day one, not a single ~2000-line file.
7. Login states: "verification-required" gets its own code; "administratively
   disabled" stays folded into generic invalid-credentials; "suspended" is a
   dashboard banner, not a login-blocking branch.
8. Localization scope = mail-template-body only (locale-keyed variant with
   English fallback), not a locale-aware URL/router scheme.
9. Account closure implements request/cancel only; an automated purge
   executor is explicitly out of scope for this pass.
10. New rate-limit policy added only for service-account/API-key creation —
    the one genuinely new abuse vector customer self-service introduces.
11. **Mid-Phase-3 pivot: PXA.Account frontend is TypeScript, not JavaScript**
    (explicit user request). Applied retroactively to all of PXA.Account's
    `src`/`tests`, not just files written after the request. `websites/shared/*`
    stays plain JS by explicit choice (Admin/Company still import it as-is);
    only Account's own files converted.

## Phase 0 — Schema & cross-cutting infrastructure

- [x] Add `Locale`/`Country` columns to `PxaIdentityUser` + EF migration
      (`20260719162518_AddAccountLocaleCountryAndClosureRequests`).
- [x] New `AccountClosureRequest` entity + `DbSet` on `PxaDbContext` + migration
      (same migration as above; table `administration.account_closure_requests`).
- [x] Extend `PxaApiProblems.cs`: fixed `ResolveCode` missing `423 Locked` case;
      added `AccountLocked`, `VerificationRequired`, `TrialAlreadyClaimed`,
      `OrganizationSlugUnavailable`, `LastOwnerProtected`, `ClosureConflict`
      (`PXAAPI009`–`014`).
- [x] New `PxaAccountPermissions.cs` + role mapping in `PxaRoles.cs`
      (OrganizationAdministrator/Manager/Editor/Viewer each get an appropriate
      subset; Editor/Viewer now carry the self-scoped Account permissions every
      member needs) + registration loop in `Program.cs`.
- [x] Unit tests: `PxaApiProblemsTests.cs` (new) + `PxaSecurityContractsTests.cs`
      (extended — Admin/Account permission vocabularies verified disjoint,
      every `PxaAccountPermissions` entry verified mapped to a role). 15 tests
      green with `dotnet test --filter PxaSecurityContractsTests|PxaApiProblemsTests`;
      `AdminMutationContractTests` regression-checked, still passes.

## Phase 1 — Application-service extraction for registration/Trial

- [x] `PXA.WebApi/Application/Identity/RegistrationValidation.cs` (pure,
      unit-testable) extracted from `AccountRegistrationController.Register`.
      Request/response DTOs moved alongside it into
      `CustomerRegistrationContracts.cs` so the Application layer owns its
      own contracts instead of depending on the Controllers namespace.
- [x] `CustomerRegistrationService.cs` + `TrialActivationService.cs` — own the
      transaction, entity writes, token issue, mail enqueue. Registration now
      also persists `user.Locale`/`user.Country` (previously only written to
      `AuditEvent.DetailsJson`), using the Phase 0 columns.
- [x] `AccountRegistrationController` reduced from ~250 lines to a thin
      parse → service → map-result mapper (56 lines).
- [x] `tests/PXA.Api.Tests/RegistrationValidationTests.cs` (12 tests, no database).
- [x] Existing `AccountRegistrationControllerTests.cs`,
      `AuthControllerTests`, `IdentityMailFlowTests` still green against real
      Postgres (`PXA_RUN_POSTGRES_TESTS=1`) — refactor is behavior-preserving.
- [x] Closes: API-And-Data → "Use application services for registration and
      Trial orchestration rather than controller-owned transactions."

## Phase 2 — `returnUrl` validation + explicit auth-state UI

- [x] `websites/shared/returnUrl.js`: `sanitizeReturnUrl()` allowlisting
      Designer/Demo/Documentation/Account origins only (both local and
      production tables; Company and Admin are never in the allowlist).
- [x] Wired into `websites/PXA.Account/src/main.js`: consumed on successful
      login and on the already-authenticated redirect from `/login`,
      `/register`, `/`; silent fallback to `/dashboard` on rejection.
- [x] `AuthController.Login`: split unconfirmed-email into its own
      `403`/`VerificationRequired` response (previously folded into the
      generic invalid-credentials response alongside disabled accounts,
      which stay generic). New title-sniff case in
      `PxaApiProblems.ResolveCode` ("Email verification") mirrors the
      existing CSRF/"Organization context" convention.
- [x] Adopted Admin's `pxa:session-expired`/`pxa:access-denied`/
      `pxa:api-offline` CustomEvent pattern in `websites/PXA.Account/src/api.js`
      (code/traceId now captured too); `main.js` listens for
      `pxa:session-expired` and shows the expired-session state.
- [x] New `POST /api/pxa/v1/auth/resend-verification` on
      `AccountRegistrationController`/`CustomerRegistrationService.ResendVerificationAsync`
      (enumeration-safe: identical response for known/unknown email; superseds
      the prior token via the existing `IdentityActionTokenService` semantics).
      Login form now offers a "Resend verification email" action on the
      verification-required response.
- [x] "Account locked" (423) already had a distinct backend message; fixed a
      latent gap where `ResolveCode` had no `423` case (now maps to
      `AccountLocked`, `PXAAPI009`, Phase 0).
- [x] "Suspended-account" state deferred to Phase 3/6 — it needs real
      organization/subscription status data the dashboard doesn't fetch yet;
      will render as a dashboard banner per the plan's judgment call, not a
      login-blocking branch.
- [x] `websites/PXA.Account/tests/returnUrl.test.js` (8 tests) +
      `package.json` `"test"` script (`node --test tests/*.test.js`).
- [x] Backend tests: `PxaApiProblemsTests` extended (title-sniff case);
      `AccountRegistrationControllerTests` updated (pre-verification login now
      expects 403 + `VerificationRequired` code) and extended with a new
      `Resend_verification_is_enumeration_safe_and_reissues_a_usable_token`
      integration test. All Postgres-backed regression tests green.
- [x] Closes: Customer Authentication → returnUrl bullet fully; explicit-state
      bullet closes for expired-session/locked-account/verification-required
      (suspended-account carries over to Phase 3/6).

## Phase 3 — Customer Portal shell (frontend architecture)

- [x] **Mid-phase pivot: PXA.Account converted from JS to TypeScript** (user
      request, applied to the whole app, not just new code). Added
      `tsconfig.json` (vanilla TS, DOM lib, `moduleResolution: bundler`,
      `allowJs: true` so the still-JS `websites/shared/*.js` imports keep
      resolving, `strict: true`); `type-check` npm script (`tsc --noEmit`,
      reusing `tsc` from `pxa-designer/node_modules` the same way `vite` is
      already borrowed — no new install). `@types/node` resolved via
      `typeRoots` pointed at `pxa-designer/node_modules/@types` (no local
      node_modules needed for types). `src/global.d.ts` declares `*.css` for
      the side-effect import. All of Phase 0–2's `api.js`/`main.js` and this
      phase's new files renamed `.ts`; `websites/shared/siteLinks.js` and
      `returnUrl.js` intentionally stay `.js` (out of scope per this decision).
      `index.html` script tag and `package.json` `test` script
      (`node --experimental-strip-types --test tests/*.test.ts`) updated to match.
- [x] `websites/PXA.Account/src/shell.ts` (`renderShell`/`renderNavigation`/
      `bindShellEvents`/`closeAccountNavigation`, Account-branded, typed
      against `UserInfo` from `api.ts`).
- [x] `websites/PXA.Account/src/pages/{dashboard,profile,organization,
      subscription,usage,licenses,developerAccess,security,support}.ts` —
      `dashboard.ts` is real (org identity/role/org-count from the existing
      `/auth/me` response, already available; no new backend endpoint needed
      since `AccountEntitlementsController` only checks one capability at a
      time and can't back a summary view — deferred to Phase 6's
      subscription/entitlement endpoints as originally planned). The other
      eight are intentionally thin "coming soon" pages via a shared
      `stub.ts` helper, reachable through real navigation/routing, filled in
      by Phases 4–8. `closure.ts` deferred to Phase 8 (not in primary nav).
- [x] `main.ts` restructured into router + shell + page-module dispatch
      (`portalPages` map + `portalPaths` set) instead of growing a single
      flat file; added a document-level Escape-key handler and mobile-nav
      close-on-navigate, mirroring Admin's accessibility pattern.
- [x] `websites/PXA.Account/tests/accessibility-contract.test.ts` (adapted
      from Admin's, account-namespaced assertions) + existing
      `returnUrl.test.ts` still green. `npm run type-check` clean; `npm run
      build` clean (19 modules); dev-server smoke check confirmed every new
      module loads (200) through Vite.
- [x] Closes: Customer Portal → "Add dashboard... routes" (scaffold + real
      dashboard; per-resource functional pages close in Phases 4–8).

## Phase 4 — Profile self-service

- [ ] `AccountProfileController.cs` (`/api/pxa/v1/account/profile`): `GET`
      self, `PATCH /display-name`, `PATCH /locale`, `POST /email-change/request`
      (reuses existing `AuthController.ConfirmEmailChange` for confirm side),
      `POST /password-change` (revokes other sessions).
- [ ] Frontend `pages/profile.js` + `api.js` additions.
- [ ] `tests/PXA.Api.Tests/AccountProfileControllerTests.cs`.
- [ ] Closes: Customer Portal → "Let customers update display name, locale,
      email, and password through verified flows."

## Phase 5 — Organization & members

- [ ] Extract `AdminOrganizationsController`'s member logic (incl.
      `IsLastOrganizationAdministratorAsync`) into
      `PXA.WebApi/Application/Organizations/OrganizationMembershipService.cs`,
      shared by Admin and Account.
- [ ] `AccountOrganizationController.cs` (`/api/pxa/v1/account/organization`):
      `GET`/`PATCH` org profile, `GET`/`POST /members`, `DELETE /members/{userId}`.
- [ ] Role assignment restricted to customer-facing role allowlist (never
      `System Administrator`).
- [ ] `tests/PXA.Api.Tests/AccountOrganizationControllerTests.cs` incl.
      last-owner-protection and cross-tenant 404 cases.
- [ ] Closes: Customer Portal → invite/remove/assign roles + last-owner
      protection bullets.

## Phase 6 — Subscription, usage, licenses (read views)

- [ ] `PXA.WebApi/Application/Subscriptions/SubscriptionQueryService.cs`
      shared read logic.
- [ ] `AccountSubscriptionController.cs` / `AccountLicensesController.cs`,
      scoped by construction to `tenantContext.OrganizationId` (no id route
      parameter).
- [ ] Customer-safe response DTOs (not reused verbatim from Admin's DTOs).
- [ ] `tests/PXA.Api.Tests/AccountSubscriptionControllerTests.cs`,
      `AccountLicensesControllerTests.cs`.
- [ ] Closes: Customer Portal → "Show edition... seats, limits, current
      usage" and "Show offline licenses..." bullets.

## Phase 7 — Developer access + security sessions

- [ ] `AccountServiceAccountsController.cs` mirroring
      `AdminServiceAccountsController`'s one-time-secret pattern, org-scoped.
- [ ] New rate-limit policy `"account-service-accounts"` (partitioned by
      active-org claim).
- [ ] `AccountSecurityController.cs` mirroring `AdminUsersController`
      session list/revoke, scoped to self only.
- [ ] `tests/PXA.Api.Tests/AccountServiceAccountsControllerTests.cs`,
      `AccountSecurityControllerTests.cs`.
- [ ] Closes: Customer Portal → developer-access and sessions bullets.

## Phase 8 — Account & organization closure requests

- [ ] `AccountClosureController.cs`: `POST /account`, `POST /organization`
      (owner-only), `POST /{requestId}/cancel`.
- [ ] Config-driven retention window.
- [ ] `tests/PXA.Api.Tests/AccountClosureControllerTests.cs`.
- [ ] Closes: Customer Portal → "Provide account closure and organization
      closure requests with retention-safe workflows."

## Phase 9 — Mail: notifications, Trial-expiry, localization, newsletter

- [ ] Enqueue `identity.new-login`, `identity.lockout`,
      `identity.credential-changed` at their respective call sites.
- [ ] `TrialExpiryNotificationService.cs` — verify `PxaMailProcessor`'s
      hosting model first before adding a new hosted service.
- [ ] `RegisterAccountRequest.SubscribeToNewsletter` (optional, unchecked by
      default, stored separately from required transactional mail).
- [ ] Locale-keyed template variants (`{templateKey}.{locale}` + English
      fallback) using Phase 0's `Locale` column.
- [ ] Closes: all four remaining Mail bullets.

## Phase 10 — Company integration: campaign attribution

- [ ] Allowlisted `utm_source`/`utm_medium`/`utm_campaign` passthrough,
      Company → Account registration link → `RegisterAccountRequest.
      CampaignContext` → re-validated server-side → stored only in
      `AuditEvent.DetailsJson`.
- [ ] `AccountRegistrationControllerTests.cs` case: non-allowlisted key dropped.
- [ ] Closes: Company Integration → campaign attribution bullet. (Pricing/Trial
      copy stays deferred — not in scope.)

## Phase 11 — Cross-cutting hardening: audit & logging

- [ ] `[PxaAuditedMutation]` on every mutation endpoint added in Phases 4–8.
- [ ] `tests/PXA.Api.Tests/AccountMutationContractTests.cs` (reflection
      contract, sibling to `AdminMutationContractTests.cs`).
- [ ] Logging sweep of new code for raw secret/token/password leakage.
- [ ] Closes: Security-And-Privacy → remaining audit + structured-logging bullets.

## Phase 12 — Final test-matrix consolidation

- [ ] `RegistrationConflictTests.cs`: Individual Developer path, duplicate
      org slug, repeated Trial, concurrent same-email registration race.
- [ ] `CrossTenantAccessMatrixTests.cs`: table-driven sweep across every
      Phase 4–8 endpoint.
- [ ] Company→Account return-flow DOM-level test.
- [ ] Build+smoke check for `websites/PXA.Account`.
- [ ] Re-read `PXA.Account.md` top to bottom; confirm every box checked
      except the four Deferred Decisions and the deferred pricing-copy line.

## Notes

- Reuse rather than duplicate: `AdminOrganizationsController`,
  `AdminSubscriptionsController`, `AdminLicensesController`,
  `AdminServiceAccountsController`, `AdminUsersController` are the extraction
  sources for Phases 5–7.
- Shared infra reused as-is: `IPxaTenantContext`, `PxaValidateAntiforgeryAttribute`,
  existing rate-limit policies (`"registration"`, `"identity-action"`,
  `"invitations"`), `IPxaMailQueue.Enqueue(...)`.
