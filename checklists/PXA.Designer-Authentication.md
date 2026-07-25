# PXA Designer Authentication Checklist

## Goal

Protect every PXA Designer workflow so that only active, email-verified users with an effective Designer entitlement can use the application. Authentication remains owned by PXA Account and is transferred to the Designer through a short-lived authorization-code handoff.

## Priorities

- [ ] P0: Protect all Designer routes and API operations.
- [ ] P0: Implement the Account-to-Designer authorization-code handoff.
- [ ] P0: Enforce active-user, verified-email, session, tenant, and entitlement checks.
- [ ] P1: Add organization switching and complete session-management UX.
- [ ] P2: Add enterprise SSO providers without changing the Designer session boundary.

## Dependencies

- [ ] Use the existing ASP.NET Core Identity users and persistent `UserSession` infrastructure.
- [ ] Use the active organization resolved by the authenticated server context.
- [ ] Use `PXA.Account-Registration.md` for registration and verification behavior.
- [ ] Use `PXA.Designer-Template-Persistence.md` for tenant-owned template authorization.
- [ ] Use `PXA.Subscription-Licensing.md` for Designer entitlement calculation.
- [ ] Keep Account and Designer behind same-origin `/api` reverse-proxy routes in each deployment.

## Access Rules

- [ ] Require an authenticated, active, and email-verified user for every Designer route.
- [ ] Require an effective Designer entitlement instead of checking edition names directly.
- [ ] Permit Free only when its effective entitlement explicitly enables the requested Designer capability.
- [ ] Deny suspended, expired, deactivated, or revoked accounts and sessions.
- [ ] Resolve the active organization from authenticated claims and persistent membership state.
- [ ] Re-evaluate session validity and entitlements on security-sensitive API operations.
- [ ] Return standard Problem Details for unauthenticated, forbidden, expired, and suspended access.

## Account Redirect

- [ ] Redirect unauthenticated Designer visitors to the PXA Account login page.
- [ ] Include the absolute Designer URL as an allowlisted `returnUrl`.
- [ ] Preserve the original Designer path, query parameters, and hash only after validation.
- [ ] Never redirect to protocol-relative, non-HTTP(S), external, Admin, or unconfigured origins.
- [ ] Provide a registration link that points only to `PXA.Account/register`.
- [ ] Do not add login or registration credential forms to the Designer.

## Authorization-Code Handoff

- [ ] Add an authenticated Account endpoint that creates a Designer authorization handoff.
- [ ] Add a Designer same-origin API endpoint that exchanges the handoff for a local session.
- [ ] Use `/auth/callback` as the Designer callback route.
- [ ] Make every authorization code cryptographically random, single-use, and valid for two minutes.
- [ ] Store only a cryptographic hash of the authorization code in PostgreSQL.
- [ ] Bind the handoff to user ID, active organization ID, Designer origin, return path, PKCE challenge, creation time, expiry time, and consumption time.
- [ ] Require PKCE S256 and a browser-generated state value.
- [ ] Keep the PKCE verifier and state in session-scoped browser memory.
- [ ] Compare state before exchanging the code.
- [ ] Consume the code atomically so concurrent exchanges cannot both succeed.
- [ ] Reject expired, consumed, malformed, wrong-origin, wrong-tenant, and wrong-PKCE exchanges.
- [ ] Re-check user, session, membership, and Designer entitlement during exchange.
- [ ] Create a persistent Designer `UserSession` and issue a Designer-host session cookie.
- [ ] Remove code, state, and error parameters from browser history immediately after processing.
- [ ] Apply `Referrer-Policy: no-referrer` to the callback response and avoid logging authorization codes.

## Cookie And CSRF Security

- [ ] Preserve host-only HttpOnly cookies for Account and Designer.
- [ ] Keep the production `__Host-` cookie prefix, Secure policy, and root path.
- [ ] Do not introduce a parent-domain authentication cookie.
- [ ] Keep SameSite protection compatible with the top-level Account-to-Designer redirect.
- [ ] Require the existing antiforgery mechanism for authenticated state-changing Designer requests.
- [ ] Rotate or invalidate the Designer cookie when the underlying persistent session changes.
- [ ] Revoke the current Designer session on Designer sign-out.
- [ ] Keep global cross-application sign-out as a separate future capability.

## Designer Application

- [ ] Add an authentication bootstrap that calls `GET /api/pxa/v1/auth/me`.
- [ ] Prevent protected route content from rendering before bootstrap completes.
- [ ] Add route states for loading, unauthenticated, verification required, entitlement denied, suspended, expired session, offline API, and incompatible API version.
- [ ] Add an error boundary around authentication and callback processing.
- [ ] Add a user menu with Account, Subscription, Organization, Security, and Sign out actions.
- [ ] Allow organization switching only through the authenticated API.
- [ ] Refresh user and entitlement state after organization switching.
- [ ] Clear tenant-specific Designer state when the active organization changes.
- [ ] Preserve a safe post-login destination without creating redirect loops.

## API Authorization

- [ ] Protect template creation, reading, updates, versions, publication, archive, and restore.
- [ ] Protect import, export, migration, rendering, conversion, PDF Viewer mutation, and Spreadsheet operations.
- [ ] Apply product-specific entitlement checks in addition to authentication.
- [ ] Exempt only explicitly public health, metadata, login handoff, and static gallery operations.
- [ ] Never authorize a request from organization identifiers supplied by the client.
- [ ] Audit successful and rejected handoffs, organization changes, session revocation, and entitlement denial without recording secrets.

## Tests

- [ ] Unit-test return URL and Designer-origin allowlists.
- [ ] Unit-test PKCE, state, code hashing, expiry, and single-use behavior.
- [ ] Integration-test Account login followed by a successful Designer handoff.
- [ ] Test authorization-code replay and concurrent exchange.
- [ ] Test expired code, invalid state, invalid PKCE, wrong origin, and modified return path.
- [ ] Test inactive, unverified, locked, suspended, and deleted users.
- [ ] Test expired and revoked persistent sessions.
- [ ] Test missing, expired, and organization-specific Designer entitlements.
- [ ] Test cross-tenant organization switching and stale organization claims.
- [ ] Test that protected Designer APIs return 401 or 403 consistently.
- [ ] Test antiforgery enforcement on state-changing Designer requests.
- [ ] Test callback URL cleanup and redirect-loop prevention.
- [ ] Run Designer desktop and mobile authentication smoke tests.

## Acceptance Criteria

- [ ] An anonymous visitor cannot view or invoke protected Designer functionality.
- [ ] Login credentials are entered only on PXA Account.
- [ ] A verified entitled user returns to the original safe Designer destination after login.
- [ ] A handoff code cannot be reused, extended, redirected, or exchanged by another origin.
- [ ] Account and Designer retain separate host-only session cookies.
- [ ] Removing a user, membership, session, or entitlement removes effective Designer access.
- [ ] Cross-tenant data and operations remain inaccessible after login and organization switching.

## Deferred Work

- [ ] Add enterprise OIDC and SAML providers.
- [ ] Add coordinated global sign-out across PXA applications.
- [ ] Add MFA step-up policies for selected Designer operations.
