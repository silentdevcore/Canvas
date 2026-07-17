# PXA Admin Checklist

## Goal

Deliver a standalone, tenant-aware PXA administration application for users, organizations, permissions, subscriptions, licenses, service access, mail delivery, and audit history.

## Application Identity

- [ ] Create the standalone application under `websites/PXA.Admin`.
- [ ] Use `https://admin.powerdoxautomation.com` as the public Cloud host.
- [ ] Support `https://admin.{customer-host}` for customer-managed deployments.
- [ ] Use `http://localhost:5177` for local development.
- [ ] Reserve `powerdox/pxa-admin` as the future container image.
- [ ] Access the backend through same-origin `/api` routing.
- [ ] Share PXA design tokens and navigation conventions without coupling the Admin build to PXA Designer.

## Priorities

- [ ] P0: Replace demo authentication and establish persistent identity, tenancy, roles, and audit.
- [ ] P0: Deliver secure user and organization administration.
- [ ] P1: Deliver subscription, license, service-account, and mail administration.
- [ ] P2: Add enterprise identity federation, SCIM, advanced teams, and custom roles.

## Dependencies

- [ ] Use the subscription and entitlement model from `PXA.Subscription-Licensing.md`.
- [ ] Use invitation, reset, and notification delivery from `PXA.Mail-Service.md`.
- [ ] Integrate the Admin container with `PXA.Api-Docker.md` and the shared PXA Server bundle.
- [ ] Define the production database and migration strategy before replacing in-memory identity data.

## Identity Foundation

- [ ] Replace hard-coded demo users with ASP.NET Core Identity and EF Core persistence.
- [ ] Remove the custom token format and fixed signing secret.
- [ ] Add standard authentication and authorization middleware to the production pipeline.
- [ ] Use secure host-only HttpOnly, Secure, SameSite browser cookies.
- [ ] Add CSRF protection for state-changing browser requests.
- [ ] Support API keys or standards-based delegated tokens for SDKs and service accounts.
- [ ] Implement login, logout, current-user, password reset, email verification, and session revocation flows.
- [ ] Apply rate limits, lockout, password policy, token expiry, and security-event logging.

## Tenant And Data Model

- [ ] Model organizations, users, memberships, roles, permissions, invitations, and sessions.
- [ ] Model service accounts, API keys, subscriptions, licenses, entitlements, and audit events.
- [ ] Use a default organization for single-customer On-Premise installations.
- [ ] Enforce tenant scoping in repositories, application services, API policies, and tests.
- [ ] Prevent tenant identifiers supplied by clients from bypassing the authenticated tenant context.
- [ ] Use soft deletion and retention rules for identities referenced by audit records.

## Roles And Policies

- [ ] Provide System Administrator, Organization Administrator, Manager, Editor, and Viewer roles.
- [ ] Define policies including `users.read`, `users.create`, `users.update`, `users.disable`, `roles.assign`, `subscriptions.manage`, `licenses.manage`, and `audit.read`.
- [ ] Keep roles independent from subscription and product entitlements.
- [ ] Restrict System Administrator capabilities to explicitly authorized PXA operators.
- [ ] Prevent administrators from removing their own last required administrative membership.
- [ ] Audit every privileged role, permission, subscription, and license change.

## Admin API

- [ ] Add versioned Admin endpoints under `/api/pxa/v1/admin`.
- [ ] Add paginated user listing with server-side search, filter, and sorting.
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
- [ ] Build a work-focused admin shell with restrained navigation and responsive layouts.
- [ ] Add user tables with name, email, organization, role, status, products, and last login.
- [ ] Add server-driven search, filters, sorting, pagination, selection, and bulk actions.
- [ ] Add invitation and edit forms with inline validation and accessible error summaries.
- [ ] Add role, membership, product entitlement, and seat controls appropriate to the current administrator.
- [ ] Add subscription, usage, Trial, renewal, expiry, and offline-license views.
- [ ] Add service-account and API-key workflows that reveal a new secret only once.
- [ ] Add audit history to relevant user, organization, subscription, and license detail views.
- [ ] Add explicit loading, empty, forbidden, offline, stale, failure, and destructive-confirmation states.
- [ ] Prevent UI visibility rules from replacing server-side authorization.

## Deployment And Operations

- [ ] Produce an independent Admin build and future static web-server image.
- [ ] Add runtime API configuration and remove hard-coded development hosts.
- [ ] Route the customer Admin host to the Admin application and `/api` to PXA API.
- [ ] Add compatibility checks between Admin and API versions.
- [ ] Add CSP, security headers, cache rules, health visibility, and structured client-error reporting.
- [ ] Add the Admin service to Cloud hosting and optional PXA Server Docker Compose profiles.

## Tests

- [ ] Unit-test identity, authorization policies, tenant resolution, and privileged application services.
- [ ] Integration-test Admin endpoints against a real relational database.
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
