# PXA Subscription And Licensing Checklist

## Goal

Define a consistent subscription and entitlement model for PXA Cloud, PXA Server, public SDKs, and future commercial billing without coupling product access to application roles.

## Priorities

- [x] P0: Define editions, account types, lifecycle states, and entitlement semantics.
- [ ] P0: Define server-side enforcement and signed offline licenses.
- [ ] P1: Add usage metering, subscription administration, and customer-facing status.
- [ ] P2: Integrate a billing provider after commercial pricing is approved.

## Dependencies

- [x] Align organization, user, membership, and administrator ownership with `PXA.Admin.md`.
- [ ] Align offline deployment and license mounting with `PXA.Api-Docker.md`.
- [ ] Align SDK entitlement behavior with `PXA.SDK-Roadmap.md`.
- [ ] Define exact prices and numeric usage limits before enabling paid checkout.

## Subscription Dimensions

- [x] Keep subscription edition independent from account type.
- [x] Model the `Individual Developer` account type with a single seat.
- [x] Model the `Company` account type with organizations and configurable seats.
- [x] Support none, monthly, and annual billing periods independently from pricing.
- [x] Support Cloud, On-Premise, and hybrid deployment modes.

## Editions

- [x] Model `Free` with explicit configurable entitlements instead of hard-coded quotas.
- [x] Model `Trial` with an automatic 30-day default period and explicit entitlements.
- [x] Model `Premium` as an independently configurable edition.
- [x] Model `Enterprise` with Cloud, On-Premise, or hybrid deployment mode.
- [x] Keep exact prices and Free/Premium numeric quotas out of code until approved commercially.
- [ ] Define allowed upgrade, downgrade, and conversion paths between editions.

## Lifecycle

- [x] Define `pending`, `trialing`, `active`, `past_due`, `grace_period`, `suspended`, `cancelled`, and `expired` states.
- [x] Persist start, period-end, cancellation-effective, grace-period, and Trial-expiry timestamps.
- [x] Enforce an explicit lifecycle transition matrix in the Admin API.
- [x] Default Trial expiry to 30 days and prevent multiple current subscriptions per organization.
- [x] Preserve append-only subscription lifecycle events for implemented mutations.

## Entitlements

- [x] Model stable capability keys for Generator, Designer, Migration, Importer, PDF Viewer, Spreadsheet, API, and SDK access.
- [x] Model seat limits, assignment, revocation, and active-seat counts.
- [x] Model optional numeric limits and units without selecting commercial quota values.
- [ ] Distinguish hard limits, soft warnings, overage-capable limits, and informational usage.
- [x] Define entitlement sources for edition defaults, negotiated overrides, and temporary grants.
- [x] Return stable entitlement decision and limit-exceeded codes from the central evaluator API.
- [x] Expose effective entitlements, limits, units, sources, and expiry to authorized Admin users.
- [x] Never infer product entitlement from an application role.

## Organization And Seat Management

- [x] Assign each current Company subscription to exactly one organization.
- [ ] Define owner, billing contact, technical contact, and organization administrator responsibilities.
- [x] Prevent normal Admin API seat assignment from exceeding the configured seat entitlement.
- [x] Add seat assignment and revocation for active organization memberships.
- [ ] Define seat transfer automation and deactivated-user cleanup behavior.
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

- [x] Centralize subscription, state, expiry, capability, and requested-quantity evaluation in one scoped service usable by API endpoints and hosted workers.
- [ ] Apply mandatory entitlement enforcement to every protected product endpoint before public API authentication rollout.
- [ ] Record usage with tenant, product, operation, quantity, timestamp, request ID, and source.
- [ ] Make usage recording idempotent for retried jobs.
- [ ] Avoid storing document content or customer secrets in usage events.
- [ ] Define aggregation, retention, reconciliation, and administrative correction rules.
- [ ] Emit threshold warnings before hard limits are reached.

## Administration And Customer Experience

- [x] Add a live subscription list, creation form, filters, lifecycle control, seats, and explicit capability selection to PXA Admin.
- [x] Add a detail view for Trial extension, renewal, grace period, cancellation, entitlement overrides, seat assignment, and lifecycle history.
- [ ] Add offline-license generation and usage-metering views.
- [ ] Show customers their edition, renewal or expiry date, limits, usage, and upgrade path.
- [ ] Send lifecycle notifications through `PXA.Mail-Service.md`.
- [ ] Update PXA.Company pricing and license content after commercial decisions are approved.
- [ ] Keep billing-provider integration behind an application abstraction.

## Tests

Current subscription foundation verification:

- [x] Test Trial creation, automatic 30-day expiry, explicit entitlements, duplicate prevention, and lifecycle audit events.
- [x] Test seat assignment and configured seat-limit rejection against PostgreSQL.
- [x] Test tenant-scoped read access and System-Administrator-only mutation access.
- [x] Test entitlement evaluation, configured limit rejection, Trial extension, renewal, grace period, scheduled cancellation, detail data, and lifecycle history.

- [ ] Test every edition and account-type combination.
- [ ] Test lifecycle transitions, grace periods, cancellation, suspension, expiry, and Trial conversion.
- [ ] Test seat assignment races and organization isolation.
- [ ] Test entitlement overrides, quota thresholds, exhausted quotas, and idempotent usage.
- [ ] Test valid, tampered, expired, future-dated, revoked, and version-incompatible offline licenses.
- [ ] Test Cloud and fully offline PXA Server enforcement.
- [ ] Test that role changes cannot grant unlicensed products.

## Acceptance Criteria

- [x] Edition, account type, deployment mode, role, and product entitlement are separate persisted concepts.
- [ ] Trial provides Premium capabilities for 30 days unless an audited extension exists.
- [ ] Enterprise supports signed offline licenses without a permanent internet connection.
- [ ] All protected operations enforce effective entitlements on the server.
- [x] Administrators can inspect subscription state and receive stable entitlement decision codes without exposing billing internals.
- [ ] No exact public price or unapproved numeric quota is hard-coded during roadmap implementation.
