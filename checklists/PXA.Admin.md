# PXA Admin Checklist

## Goal

Deliver a standalone, tenant-aware PXA administration application for users, organizations, permissions, subscriptions, licenses, service access, mail delivery, and audit history.

## Application Identity

- [x] Create the standalone application under `websites/PXA.Admin`.
- [ ] Use `https://admin.powerdoxautomation.com` as the public Cloud host.
- [ ] Support `https://admin.{customer-host}` for customer-managed deployments.
- [x] Use `http://localhost:5177` for local development.
- [ ] Reserve `powerdox/pxa-admin` as the future container image.
- [x] Access the backend through same-origin `/api` routing.
- [x] Share PXA design tokens and navigation conventions without coupling the Admin build to PXA Designer.
- [x] Keep Admin out of PXA.Company navigation; administrators enter the dedicated Admin host directly.

## Priorities

- [ ] P0: Replace demo authentication and establish persistent identity, tenancy, roles, and audit.
- [ ] P0: Deliver secure user and organization administration.
- [ ] P1: Deliver subscription, license, service-account, and mail administration.
- [ ] P2: Add enterprise identity federation, SCIM, advanced teams, and custom roles.

## Dependencies

- [ ] Use the subscription and entitlement model from `PXA.Subscription-Licensing.md`.
- [ ] Use invitation, reset, and notification delivery from `PXA.Mail-Service.md`.
- [ ] Integrate the Admin container with `PXA.Api-Docker.md` and the shared PXA Server bundle.
- [x] Define the production database and migration strategy before replacing in-memory identity data.

## Identity Foundation

- [x] Replace hard-coded demo users with ASP.NET Core Identity and EF Core persistence.
- [x] Remove the custom token format and fixed signing secret.
- [x] Add standard authentication and authorization middleware to the production pipeline.
- [x] Use secure host-only HttpOnly, Secure, SameSite browser cookies.
- [x] Add CSRF protection for state-changing browser requests.
- [ ] Support API keys or standards-based delegated tokens for SDKs and service accounts.
- [ ] Implement login, logout, current-user, password reset, email verification, and session revocation flows.
- [ ] Apply rate limits, lockout, password policy, token expiry, and security-event logging.

Current identity implementation:

- [x] Add versioned and legacy-compatible login, logout, current-user, and CSRF endpoints.
- [x] Require confirmed email and active-user status during login.
- [x] Configure password complexity, failed-login lockout, and bounded cookie expiry.
- [x] Revalidate active-user status and the Identity security stamp on authenticated requests.
- [x] Support immediate cookie-session revocation through security-stamp rotation.
- [x] Return organization memberships and roles from the current-user endpoint.
- [x] Verify login, logout, cookie flags, CSRF, organization context, and session revocation against PostgreSQL.
- [ ] Connect password reset and email verification to the persistent mail outbox.
- [ ] Add security-event audit records and authentication rate limiting.

## Tenant And Data Model

- [ ] Model organizations, users, memberships, roles, permissions, invitations, and sessions.
- [ ] Model service accounts, API keys, subscriptions, licenses, entitlements, and audit events.
- [ ] Use a default organization for single-customer On-Premise installations.
- [ ] Enforce tenant scoping in repositories, application services, API policies, and tests.
- [ ] Prevent tenant identifiers supplied by clients from bypassing the authenticated tenant context.
- [ ] Use soft deletion and retention rules for identities referenced by audit records.

## Roles And Policies

- [ ] Provide System Administrator, Organization Administrator, Manager, Editor, and Viewer roles.
- [x] Define policies including `users.read`, `users.create`, `users.update`, `users.disable`, `roles.assign`, `subscriptions.manage`, `licenses.manage`, and `audit.read`.
- [ ] Keep roles independent from subscription and product entitlements.
- [ ] Restrict System Administrator capabilities to explicitly authorized PXA operators.
- [ ] Prevent administrators from removing their own last required administrative membership.
- [ ] Audit every privileged role, permission, subscription, and license change.

## Admin API

- [x] Add versioned Admin endpoints under `/api/pxa/v1/admin`.
- [x] Add paginated user listing with server-side search, filter, and sorting.
- [ ] Add user detail, invitation, creation, update, activation, deactivation, soft deletion, and bulk operations.
- [ ] Add password-reset initiation and active-session revocation without returning secret tokens.
- [ ] Add organization, membership, role, and permission administration.
- [ ] Add subscription assignment, Trial extension, seat management, suspension, renewal, and usage inspection.
- [ ] Add offline-license generation, replacement, revocation, and download as audited actions.
- [ ] Add service-account and API-key creation, rotation, revocation, and last-used metadata.
- [ ] Add mail queue, delivery status, and failure inspection without exposing message bodies or secrets by default.
- [ ] Add immutable, paginated audit-event search and export.
- [ ] Return consistent Problem Details errors and stable authorization diagnostics.

## Admin User Interface

- [ ] Add routes for dashboard, users, user details, organizations, roles, subscriptions, licenses, service accounts, mail delivery, audit, and settings.
- [x] Build a work-focused admin shell with restrained navigation and responsive layouts.
- [ ] Add user tables with name, email, organization, role, status, products, and last login.
- [ ] Add server-driven search, filters, sorting, pagination, selection, and bulk actions.
- [ ] Add invitation and edit forms with inline validation and accessible error summaries.
- [ ] Add role, membership, product entitlement, and seat controls appropriate to the current administrator.
- [ ] Add subscription, usage, Trial, renewal, expiry, and offline-license views.
- [ ] Add service-account and API-key workflows that reveal a new secret only once.
- [ ] Add audit history to relevant user, organization, subscription, and license detail views.
- [ ] Add explicit loading, empty, forbidden, offline, stale, failure, and destructive-confirmation states.
- [ ] Prevent UI visibility rules from replacing server-side authorization.

Current Admin shell implementation:

- [x] Add a dedicated login route with session discovery and authenticated redirects.
- [x] Connect login, logout, current-user, and CSRF flows to `/api/pxa/v1/auth`.
- [x] Reject authenticated users without System Administrator or Organization Administrator roles in the Admin shell.
- [x] Add dashboard, users, organizations, roles, subscriptions, licenses, service accounts, mail, audit, and settings routes.
- [x] Show explicit unavailable states for areas whose tenant-scoped Admin APIs are not implemented.
- [x] Add responsive sidebar, organization context, account context, loading, login-error, forbidden, and not-found states.
- [x] Add an opt-in Development-only administrator bootstrap without source-controlled credentials or production seeds.
- [x] Add user detail routes after the user administration API is available.

Current user administration implementation:

- [x] Resolve the active organization exclusively from authenticated claims for user queries and mutations.
- [x] Add tenant-scoped user listing and detail endpoints with stable Problem Details failures.
- [x] Add CSRF-protected activation, deactivation, and organization-role assignment endpoints.
- [x] Prevent self-deactivation and removal or deactivation of the last active Organization Administrator.
- [x] Rotate the affected user's security stamp after status changes to revoke active sessions.
- [x] Persist organization-role assignments separately from global Identity roles.
- [x] Persist audit events for successful user-status and role mutations.
- [x] Connect the Users table to server-side search, status filtering, and pagination.
- [x] Add a user detail view with account metadata, status control, and organization-role controls.
- [x] Add loading, empty, API-failure, and destructive-confirmation states to user administration.

## Deployment And Operations

- [x] Produce an independent Admin build.
- [ ] Produce the future static web-server image.
- [x] Use relative same-origin API paths and a development-only Vite proxy without hard-coded hosts in application code.
- [ ] Route the customer Admin host to the Admin application and `/api` to PXA API.
- [ ] Add compatibility checks between Admin and API versions.
- [ ] Add CSP, security headers, cache rules, health visibility, and structured client-error reporting.
- [ ] Add the Admin service to Cloud hosting and optional PXA Server Docker Compose profiles.

## Tests

- [ ] Unit-test identity, authorization policies, tenant resolution, and privileged application services.
- [x] Integration-test the implemented user Admin endpoints against real PostgreSQL.
- [x] Test user-list and detail cross-tenant isolation, CSRF enforcement, status changes, role assignment, last-administrator protection, and audit creation.
- [ ] Test cross-tenant access attempts and identifier tampering.
- [ ] Test login, logout, invitation, verification, reset, lockout, expiry, and session revocation.
- [ ] Test role, seat, subscription, license, service-account, and API-key workflows.
- [ ] Verify that every privileged mutation creates an audit event.
- [ ] Test keyboard navigation, focus management, screen-reader labels, and responsive layouts.
- [ ] Run end-to-end tests for System Administrator and Organization Administrator journeys.
- [ ] Verify that Manager, Editor, Viewer, anonymous, and suspended users cannot access unauthorized Admin data.

## Acceptance Criteria

- [ ] PXA Admin is deployed independently from PXA Designer.
- [ ] Non-admin users cannot load or invoke unauthorized Admin capabilities.
- [ ] Every query and mutation respects organization isolation.
- [ ] Every privileged change is attributable through immutable audit metadata.
- [ ] Subscription access, application roles, and product entitlements remain distinct.
- [ ] No password, reset token, invitation token, API-key secret, or mail credential is exposed in logs or normal API responses.
