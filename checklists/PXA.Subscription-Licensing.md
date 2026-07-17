# PXA Subscription And Licensing Checklist

## Goal

Define a consistent subscription and entitlement model for PXA Cloud, PXA Server, public SDKs, and future commercial billing without coupling product access to application roles.

## Priorities

- [ ] P0: Define editions, account types, lifecycle states, and entitlement semantics.
- [ ] P0: Define server-side enforcement and signed offline licenses.
- [ ] P1: Add usage metering, subscription administration, and customer-facing status.
- [ ] P2: Integrate a billing provider after commercial pricing is approved.

## Dependencies

- [ ] Align organization, user, membership, and administrator ownership with `PXA.Admin.md`.
- [ ] Align offline deployment and license mounting with `PXA.Api-Docker.md`.
- [ ] Align SDK entitlement behavior with `PXA.SDK-Roadmap.md`.
- [ ] Define exact prices and numeric usage limits before enabling paid checkout.

## Subscription Dimensions

- [ ] Keep subscription edition independent from account type.
- [ ] Support the `Individual Developer` account type with one owner and a personal workspace.
- [ ] Support the `Company` account type with organizations, multiple seats, shared resources, administrators, teams, and centralized billing.
- [ ] Support monthly and annual billing periods where an edition is billable.
- [ ] Support Cloud, On-Premise, and future hybrid deployment entitlements.

## Editions

- [ ] Define `Free` as restricted entry-level Cloud usage with configurable product and usage limits.
- [ ] Define `Trial` as 30 days of Premium capabilities with evaluation limits and a clear expiry path.
- [ ] Define `Premium` as paid production usage with API and public SDK access.
- [ ] Define `Enterprise` as negotiated Cloud or On-Premise usage with offline licensing, SSO, audit, SLA, and advanced administration.
- [ ] Keep exact prices and Free/Premium numeric quotas out of code until approved commercially.
- [ ] Define allowed upgrade, downgrade, and conversion paths between editions.

## Lifecycle

- [ ] Define `pending`, `trialing`, `active`, `past_due`, `grace_period`, `suspended`, `cancelled`, and `expired` states.
- [ ] Define start, renewal, cancellation-effective, grace-period, suspension, and expiry timestamps.
- [ ] Define immediate versus end-of-period effects for upgrades, downgrades, and cancellation.
- [ ] Define Trial expiry, extension, conversion, and duplicate-Trial prevention rules.
- [ ] Preserve an immutable history of subscription and license state transitions.

## Entitlements

- [ ] Model product access for Generator, Designer, Migration, Importer, PDF Viewer, Spreadsheet, API, and SDK capabilities.
- [ ] Model seat limits and seat assignment for Company accounts.
- [ ] Model processed pages or operations, concurrent jobs, maximum file size, data retention, support level, and offline permission.
- [ ] Distinguish hard limits, soft warnings, overage-capable limits, and informational usage.
- [ ] Define entitlement precedence for edition defaults, negotiated overrides, and temporary grants.
- [ ] Return stable entitlement-denied and quota-exceeded error codes from the API.
- [ ] Expose effective entitlements and current usage to authorized Admin users.
- [ ] Never infer product entitlement from an application role.

## Organization And Seat Management

- [ ] Assign each Company subscription to exactly one organization.
- [ ] Define owner, billing contact, technical contact, and organization administrator responsibilities.
- [ ] Prevent active seat assignments from exceeding the effective seat entitlement.
- [ ] Define seat invitation, assignment, transfer, removal, and deactivated-user behavior.
- [ ] Preserve organization resources when a user loses a seat.
- [ ] Define account-owner transfer and organization closure workflows.

## Offline And On-Premise Licensing

- [ ] Define a signed offline license envelope with license ID, customer, organization, edition, account type, products, limits, validity, instance limits, and signature metadata.
- [ ] Sign licenses with an asymmetric key so PXA Server contains only the verification key.
- [ ] Validate signature, validity, product version, deployment identity, and instance limits locally.
- [ ] Reject modified, malformed, revoked, not-yet-valid, and expired licenses with stable diagnostics.
- [ ] Support controlled grace periods without requiring internet access.
- [ ] Define license renewal, replacement, revocation-list import, backup, and disaster-recovery behavior.
- [ ] Keep private signing keys outside source control, Docker images, and customer deployments.

## Enforcement And Metering

- [ ] Centralize entitlement evaluation in one application service used by API endpoints and workers.
- [ ] Enforce entitlements server-side regardless of Designer or SDK behavior.
- [ ] Record usage with tenant, product, operation, quantity, timestamp, request ID, and source.
- [ ] Make usage recording idempotent for retried jobs.
- [ ] Avoid storing document content or customer secrets in usage events.
- [ ] Define aggregation, retention, reconciliation, and administrative correction rules.
- [ ] Emit threshold warnings before hard limits are reached.

## Administration And Customer Experience

- [ ] Add subscription, entitlement, usage, seat, and license views to PXA Admin.
- [ ] Support Trial extension, suspension, renewal, entitlement override, and license generation as audited privileged actions.
- [ ] Show customers their edition, renewal or expiry date, limits, usage, and upgrade path.
- [ ] Send lifecycle notifications through `PXA.Mail-Service.md`.
- [ ] Update PXA.Company pricing and license content after commercial decisions are approved.
- [ ] Keep billing-provider integration behind an application abstraction.

## Tests

- [ ] Test every edition and account-type combination.
- [ ] Test lifecycle transitions, grace periods, cancellation, suspension, expiry, and Trial conversion.
- [ ] Test seat assignment races and organization isolation.
- [ ] Test entitlement overrides, quota thresholds, exhausted quotas, and idempotent usage.
- [ ] Test valid, tampered, expired, future-dated, revoked, and version-incompatible offline licenses.
- [ ] Test Cloud and fully offline PXA Server enforcement.
- [ ] Test that role changes cannot grant unlicensed products.

## Acceptance Criteria

- [ ] Edition, account type, deployment mode, role, and product entitlement are separate concepts.
- [ ] Trial provides Premium capabilities for 30 days unless an audited extension exists.
- [ ] Enterprise supports signed offline licenses without a permanent internet connection.
- [ ] All protected operations enforce effective entitlements on the server.
- [ ] Customers and administrators can explain every access denial from visible subscription state and stable diagnostics.
- [ ] No exact public price or unapproved numeric quota is hard-coded during roadmap implementation.
