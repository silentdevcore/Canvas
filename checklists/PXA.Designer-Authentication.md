# PXA Designer Authentication Checklist

## Goal

Protect every PXA Designer workflow so that only active, email-verified users with an effective Designer entitlement can use the application. Authentication remains owned by PXA Account and is transferred to the Designer through a short-lived authorization-code handoff.

## Priorities

- [x] P0: Protect all Designer routes and API operations.
- [x] P0: Implement the Account-to-Designer authorization-code handoff.
- [x] P0: Enforce active-user, verified-email, session, tenant, and entitlement checks.
- [x] P1: Add organization switching and the Account/session user menu.
- [ ] P2: Add enterprise SSO providers without changing the Designer session boundary.

## Dependencies

- [x] Use the existing ASP.NET Core Identity users and persistent `UserSession` infrastructure.
- [x] Use the active organization resolved by the authenticated server context.
- [x] Use `PXA.Account-Registration.md` for registration and verification behavior.
- [x] Use `PXA.Designer-Template-Persistence.md` for tenant-owned template authorization.
- [x] Use `PXA.Subscription-Licensing.md` for Designer entitlement calculation.
- [x] Keep Account and Designer behind same-origin `/api` reverse-proxy routes in each deployment.

## Access Rules

- [x] Require an authenticated, active, and email-verified user for every Designer route.
- [x] Require an effective Designer entitlement instead of checking edition names directly.
- [x] Permit Free only when its effective entitlement explicitly enables the requested Designer capability.
- [x] Deny suspended, expired, deactivated, or revoked accounts and sessions.
- [x] Resolve the active organization from authenticated claims and persistent membership state.
- [x] Re-evaluate session validity and entitlements on every Designer API operation.
- [x] Return standard Problem Details for unauthenticated, forbidden, expired, and suspended access.

## Account Redirect

- [x] Redirect unauthenticated Designer visitors to the PXA Account login page.
- [x] Include the absolute Account authorization continuation as an allowlisted `returnUrl`.
- [x] Preserve the original Designer path, query parameters, and hash only after validation.
- [x] Never redirect to protocol-relative, non-HTTP(S), external, Admin, or unconfigured origins.
- [x] Provide registration through the PXA Account login page only.
- [x] Do not add login or registration credential forms to the Designer.

## Authorization-Code Handoff

- [x] Add an authenticated Account endpoint that creates a Designer authorization handoff.
- [x] Add a Designer same-origin API endpoint that exchanges the handoff for a local session.
- [x] Use `/auth/callback` as the Designer callback route.
- [x] Make every authorization code cryptographically random, single-use, and valid for two minutes.
- [x] Store only a cryptographic hash of the authorization code in PostgreSQL.
- [x] Bind the handoff to user ID, active organization ID, Designer origin, return path, PKCE challenge, creation time, expiry time, and consumption time.
- [x] Require PKCE S256 and a browser-generated state value.
- [x] Keep the PKCE verifier and state in session-scoped browser storage.
- [x] Compare state before exchanging the code.
- [x] Consume the code atomically so concurrent exchanges cannot both succeed.
- [x] Reject expired, consumed, malformed, wrong-origin, wrong-tenant, and wrong-PKCE exchanges.
- [x] Re-check user, session, membership, and Designer entitlement during exchange.
- [x] Create a persistent Designer `UserSession` and issue a Designer-host session cookie.
- [x] Remove code, state, and error parameters from browser history immediately after processing.
- [x] Apply `Referrer-Policy: no-referrer` to the callback response and avoid logging authorization codes.

## Cookie And CSRF Security

- [x] Preserve separate host-only HttpOnly cookies for Account and Designer.
- [x] Keep the production `__Host-` cookie prefix, Secure policy, and root path.
- [x] Do not introduce a parent-domain authentication cookie.
- [x] Keep SameSite protection compatible with the top-level Account-to-Designer redirect.
- [x] Require the existing antiforgery mechanism for authenticated state-changing Designer requests.
- [x] Rotate or invalidate the Designer cookie when the underlying persistent session changes.
- [x] Revoke the current Designer session on Designer sign-out.
- [ ] Keep global cross-application sign-out as a separate future capability.

## Designer Application

- [x] Add an authentication bootstrap that calls `GET /api/pxa/v1/auth/me`.
- [x] Prevent protected route content from rendering before bootstrap completes.
- [x] Add explicit route states for verification, disabled accounts, suspended organizations, expired subscriptions, missing entitlements, offline access, and incompatible API versions.
- [x] Add guarded error handling around authentication and callback processing.
- [x] Add a user menu with Account, Subscription, Organization, Security, and Sign out actions.
- [x] Allow organization switching only through the authenticated API.
- [x] Refresh user and entitlement state after organization switching.
- [x] Clear tenant-specific Designer state when the active organization changes.
- [x] Preserve a safe post-login destination without creating redirect loops.

## API Authorization

- [x] Protect template creation, reading, updates, versions, publication, archive, and restore when called through Designer.
- [x] Protect import, export, migration, rendering, conversion, PDF Viewer mutation, and Spreadsheet operations when called through Designer.
- [x] Apply the Designer entitlement check in addition to authentication.
- [x] Exempt only the CSRF bootstrap, handoff exchange, and sign-out prerequisites from the Designer entitlement gate.
- [x] Never authorize a request from organization identifiers supplied by the client.
- [x] Audit successful and rejected handoffs plus Designer entitlement denials without storing raw codes or PKCE values.

## Tests

- [ ] Unit-test return URL and Designer-origin allowlists.
- [ ] Unit-test PKCE, state, code hashing, expiry, and single-use behavior.
- [x] Integration-test Account login followed by a successful Designer handoff.
- [x] Test authorization-code replay and concurrent exchange.
- [x] Test expired code, invalid state, invalid PKCE, wrong origin, and server-owned return-path restoration.
- [ ] Test inactive, unverified, locked, suspended, and deleted users; unverified users and suspended organizations are covered.
- [ ] Test expired and revoked persistent sessions; revoked Designer sessions are covered.
- [ ] Test missing, expired, and organization-specific Designer entitlements; disabled and expired entitlements plus organization switching are covered.
- [x] Test cross-tenant organization switching and stale organization claims.
- [x] Test that protected Designer APIs return 401 or 403 consistently.
- [x] Test antiforgery enforcement on state-changing Designer requests.
- [ ] Test callback URL cleanup and redirect-loop prevention.
- [x] Run Designer desktop and mobile authentication smoke tests.

## Acceptance Criteria

- [x] An anonymous visitor cannot view or invoke protected Designer functionality.
- [x] Login credentials are entered only on PXA Account.
- [x] A verified entitled user returns to the original safe Designer destination after login.
- [x] A handoff code cannot be reused, extended, redirected, or exchanged by another origin.
- [x] Account and Designer retain separate host-only session cookies.
- [x] Removing a user, membership, session, or entitlement removes effective Designer access.
- [x] Cross-tenant organization switching is membership-restricted; template data isolation remains tracked in `PXA.Designer-Template-Persistence.md`.

## Deferred Work

- [ ] Add enterprise OIDC and SAML providers.
- [ ] Add coordinated global sign-out across PXA applications.
- [ ] Add MFA step-up policies for selected Designer operations.
