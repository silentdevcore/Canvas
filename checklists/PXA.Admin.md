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
- [x] Support tenant-bound API keys for SDKs and service accounts; delegated tokens remain future work.
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
- [x] Connect invitations and password reset to the persistent mail outbox.
- [ ] Connect email verification changes for existing accounts to the persistent mail outbox.
- [ ] Add security-event audit records and authentication rate limiting.

## Tenant And Data Model

- [ ] Model organizations, users, memberships, roles, permissions, invitations, and sessions.
- [ ] Model service accounts, API keys, subscriptions, licenses, entitlements, and audit events.
- [ ] Use a default organization for single-customer On-Premise installations.
- [ ] Enforce tenant scoping in repositories, application services, API policies, and tests.
- [ ] Prevent tenant identifiers supplied by clients from bypassing the authenticated tenant context.
- [ ] Use soft deletion and retention rules for identities referenced by audit records.

## Roles And Policies

- [x] Provide System Administrator, Organization Administrator, Manager, Editor, and Viewer roles.
- [x] Define policies including `users.read`, `users.create`, `users.update`, `users.disable`, `roles.assign`, `subscriptions.read`, `subscriptions.manage`, `licenses.manage`, `audit.read`, `mail.read`, and `mail.manage`.
- [x] Define separate `organizations.read` and `organizations.manage` policies for tenant administration.
- [x] Keep roles independent from subscription and product entitlements.
- [ ] Restrict System Administrator capabilities to explicitly authorized PXA operators.
- [x] Prevent administrators from changing their own active organization role or removing the last active Organization Administrator.
- [ ] Audit every privileged role, permission, subscription, and license change.

## Admin API

- [x] Add versioned Admin endpoints under `/api/pxa/v1/admin`.
- [x] Add paginated user listing with server-side search, filter, and sorting.
- [ ] Add user detail, invitation, creation, update, activation, deactivation, soft deletion, and bulk operations.
- [ ] Add password-reset initiation and active-session revocation without returning secret tokens.
- [ ] Add organization, membership, role, and permission administration.
- [x] Add subscription creation, tenant-scoped inspection, lifecycle changes, explicit entitlements, and seat assignment/revocation.
- [ ] Add Trial extension, renewal workflow, usage inspection, and billing integration.
- [x] Add offline-license generation, revocation, validation, and download as audited actions; replacement remains future work.
- [x] Add service-account and API-key creation, rotation through replacement keys, revocation, and last-used metadata.
- [x] Add mail queue, delivery status, failure inspection, retry, and cancellation without exposing message bodies or secrets.
- [x] Add immutable, paginated audit-event search and Enterprise CSV/JSON export.
- [ ] Return consistent Problem Details errors and stable authorization diagnostics.

## Admin User Interface

- [x] Add routes for dashboard, users, user details, organizations, roles, subscriptions, licenses, service accounts, mail delivery, audit, and settings.
- [x] Build a work-focused admin shell with restrained navigation and responsive layouts.
- [ ] Add user tables with name, email, organization, role, status, products, and last login.
- [ ] Add server-driven search, filters, sorting, pagination, selection, and bulk actions.
- [ ] Add invitation and edit forms with inline validation and accessible error summaries.
- [ ] Add role, membership, product entitlement, and seat controls appropriate to the current administrator.
- [x] Add subscription detail, Trial extension, renewal, grace-period, cancellation, entitlement, seat, and lifecycle-history views.
- [x] Add usage and offline-license views.
- [x] Add service-account and API-key workflows that reveal a new secret only once.
- [x] Add lifecycle and actor history to subscription detail views.
- [x] Add an Audit workspace with server-side search, action/target/outcome/time filters, details, pagination, and export.
- [x] Add a Roles & Permissions workspace with protected definitions, permission matrix, member details, assignment, and revocation.
- [ ] Add audit history to user, organization, and license detail views.
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

Current organization administration implementation:

- [x] Add tenant-scoped organization list, detail, update, and member-list endpoints.
- [x] Allow System Administrators to list, create, update, and select any active organization.
- [x] Restrict Organization Administrators to their authenticated active organization.
- [x] Add CSRF-protected membership attachment and soft-removal for existing PXA users.
- [x] Prevent self-removal and removal of the last active Organization Administrator.
- [x] Reissue the authenticated cookie with the selected tenant and tenant-specific role claims.
- [x] Preserve System Administrator access while deriving organization roles only from the selected tenant.
- [x] Add organization list and detail views with search, status filtering, creation, editing, membership management, and tenant switching.
- [x] Persist audit events for organization creation/update and membership add/remove operations.

Current role administration implementation:

- [x] Expose only the four protected organization roles through stable route keys; keep System Administrator outside tenant assignment APIs.
- [x] Return tenant-scoped member counts, paginated members, assignment actor, and grouped permission metadata.
- [x] Display the complete role-permission matrix while keeping product entitlements explicitly separate.
- [x] Assign and revoke roles only for memberships in the authenticated active organization.
- [x] Reject self-role changes, foreign users, unknown roles, and removal of the last active Organization Administrator.
- [x] Rotate the affected user's security stamp after every role change so stale claims cannot remain active.
- [x] Audit role assignment and revocation with actor, role, target user, tenant, and outcome metadata.

Current invitation and recovery implementation:

- [x] Add a CSRF-protected administrator invitation endpoint for the active organization.
- [x] Create invited Identity users, memberships, organization roles, action tokens, outbox messages, and audit events transactionally.
- [x] Add public invitation acceptance with password policy enforcement and membership activation.
- [x] Add enumeration-resistant password-reset request and single-use reset confirmation endpoints.
- [x] Add Invite user, Accept invitation, Forgot password, and Choose password views.
- [x] Add tenant-scoped Mail delivery status UI without payload or token exposure.
- [x] Add transport summary, delivery filters, authorized Retry/Cancel actions, and destructive confirmation to Mail delivery.

Current subscription administration implementation:

- [x] Separate subscription read access from System-Administrator-only lifecycle mutation access.
- [x] Add PostgreSQL models and migration for current subscriptions, entitlements, seats, and lifecycle events.
- [x] Add a real Subscription page with organization selection, filters, capability selection, seat limits, and lifecycle controls.
- [x] Audit subscription creation, updates, and seat changes without coupling roles to entitlements.
- [x] Add explicit Trial extension, renewal, grace-period, cancellation, entitlement editing, seat controls, and actor-attributed history.
- [x] Add a central tenant-aware entitlement evaluator with stable decision codes for API and hosted-worker use.
- [x] Persist idempotent period-scoped usage events and expose operation-level usage summaries.
- [x] Issue, verify, download, list, and revoke ECDSA-signed Enterprise offline licenses.
- [x] Add tenant-scoped service accounts and hashed API keys with immediate revocation and last-used tracking.
- [x] Accept API keys through `X-PXA-API-Key` or Bearer authentication without granting Admin permissions.
- [x] Enforce API and product entitlements for protected product routes when Production enforcement is enabled.

Current audit administration implementation:

- [x] Scope every audit list, detail, filter, and export query to the authenticated active organization.
- [x] Resolve actor names and email addresses without exposing identity or event data from another tenant.
- [x] Add server-side search plus action, target type, outcome, actor, time range, and direction filters.
- [x] Keep audit events read-only and return malformed historical details as unavailable structured data.
- [x] Limit exports to 50,000 filtered rows and neutralize spreadsheet formula prefixes in CSV fields.
- [x] Restrict CSV and JSON export to Enterprise subscriptions and audit each successful export.
- [x] Add event detail, loading, empty, failure, pagination, filter reset, and export states to PXA Admin.

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
- [x] Test organization scoping, System Administrator tenant switching, membership attachment, updates, and audit creation against PostgreSQL.
- [ ] Test cross-tenant access attempts and identifier tampering.
- [ ] Test login, logout, invitation, verification, reset, lockout, expiry, and session revocation.
- [x] Test implemented subscription lifecycle, entitlement, seat, tenant, and audit workflows against PostgreSQL.
- [x] Test license, service-account, API-key authentication/revocation, product enforcement, and usage workflows against PostgreSQL.
- [x] Test audit filtering, details, anonymous rejection, cross-tenant isolation, Enterprise CSV/JSON export, and export auditing against PostgreSQL.
- [x] Test protected role catalog, permission metadata, member counts, cross-tenant isolation, assignment, revocation, self-protection, and role audit events against PostgreSQL.
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
