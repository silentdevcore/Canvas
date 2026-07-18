# PXA Mail Service Checklist

## Goal

Deliver one application-level mail service for identity, security, subscription, and newsletter communication with replaceable Cloud and On-Premise transports.

## Priorities

- [ ] P0: Deliver secure transactional email for invitations, verification, and password reset.
- [ ] P0: Add reliable queued delivery, templates, audit metadata, and operational visibility.
- [ ] P1: Add subscription and security notifications.
- [ ] P1: Add isolated marketing consent, newsletter, and suppression workflows.
- [ ] P2: Add additional provider adapters and advanced campaign analytics.

## Dependencies

- [ ] Consume identity and administrator events from `PXA.Admin.md`.
- [ ] Consume subscription lifecycle events from `PXA.Subscription-Licensing.md`.
- [ ] Define public Company, Admin, Designer, and support callback URLs per environment.
- [ ] Select the first Cloud transactional provider before production rollout.

## Service Architecture

- [x] Define one mail application interface independent of SMTP and provider SDKs.
- [ ] Add a replaceable Cloud transactional-provider adapter.
- [x] Add customer-configured SMTP transport for On-Premise installations.
- [x] Add a development mail catcher or in-memory transport.
- [x] Add mail-disabled mode for isolated deployments with explicit administrative status.
- [ ] Keep provider credentials, SMTP credentials, and signing secrets in external secret storage.
- [x] Separate message composition, queueing, transport, and delivery-status processing.
- [x] Persist ASP.NET Core Data Protection keys so queued payloads survive API restarts.

## Transactional Email

- [x] Send user invitation messages with seven-day expiry.
- [ ] Send email-verification messages.
- [x] Send password-reset and password-changed notifications.
- [ ] Send new-login, lockout, credential-change, and security warnings.
- [ ] Send Trial start, Trial expiry warning, subscription change, suspension, renewal, and license-expiry messages.
- [ ] Send seat, role, organization, API-key, and service-account security notifications.
- [ ] Deliver required transactional messages independently from marketing consent.

## Marketing Email

- [ ] Keep marketing recipients, consent, preferences, and templates separate from transactional delivery.
- [ ] Support newsletter subscription with explicit consent and double opt-in.
- [ ] Support product news, release announcements, events, and approved commercial messages.
- [ ] Add one-click unsubscribe and preference-management links.
- [ ] Record consent source, timestamp, policy version, confirmation, and withdrawal.
- [ ] Enforce bounce, complaint, manual, and unsubscribe suppression before sending.
- [ ] Prevent marketing workflows from using identity addresses without valid consent.

## Templates And Localization

- [x] Create versioned templates with stable template keys for implemented identity messages.
- [x] Provide HTML and plain-text variants for implemented identity messages.
- [ ] Support localized subject, body, dates, numbers, links, and directionality.
- [ ] Select language from user preference with organization and system fallbacks.
- [ ] Add consistent PXA branding, accessible structure, and meaningful link labels.
- [ ] Preview templates with synthetic data before activation.
- [ ] Never include passwords, full API keys, license signing material, or document contents.

## Queue And Delivery Lifecycle

- [x] Use a PostgreSQL outbox so invitation transactions and mail intent cannot diverge.
- [x] Define `pending`, `scheduled`, `sending`, `delivered`, `failed`, `suppressed`, `cancelled`, and `dead_letter` states.
- [x] Add unique idempotency keys to prevent duplicate queued messages.
- [x] Add bounded exponential retry for transient failures.
- [x] Move permanently failed messages to a dead-letter state.
- [x] Store provider message ID, template version, recipient reference, attempts, timestamps, and sanitized failure reason.
- [ ] Avoid storing message bodies longer than required for delivery and support.
- [ ] Add retention and deletion rules for queue, delivery, consent, and audit metadata.

## Secure Action Tokens

- [x] Generate cryptographically strong, purpose-bound invitation and password-reset tokens.
- [x] Store only SHA-256 hashes of action tokens while encrypting required outbox payloads with Data Protection.
- [x] Make invitation and password-reset tokens single-use and short-lived.
- [x] Bind implemented tokens to recipient, tenant, purpose, and expiry.
- [x] Invalidate superseded invitation/reset tokens and consume successful tokens.
- [x] Avoid revealing whether an email address exists during public reset requests.

## Provider Events And Operations

- [ ] Validate webhook signatures, timestamps, event IDs, and expected providers.
- [ ] Process delivery, delay, bounce, complaint, and suppression events idempotently.
- [ ] Reject replayed, malformed, unsigned, and cross-tenant callback events.
- [x] Expose tenant-scoped delivery status and sanitized failure metadata to authorized administrators.
- [x] Add tenant-scoped, authorized manual retry and cancellation with audit events.
- [ ] Add alerts for queue backlog, provider outage, authentication failure, and abnormal bounce rates.
- [ ] Keep recipient addresses and provider payloads out of general application logs.

## Configuration

- [x] Configure sender name, sender address, Admin action URL, and enabled state by environment.
- [x] Configure SMTP host, port, TLS mode, authentication, and timeout for On-Premise.
- [ ] Configure Cloud provider credentials and webhook secrets externally.
- [x] Validate configuration at startup and expose safe readiness diagnostics.
- [x] Add a Mailpit service for local SMTP capture without delivering messages externally.
- [ ] Document operation when mail is disabled and the administrative alternatives for invitation and reset workflows.

## Tests

Current identity-mail verification:

- [x] Test invitation creation, encrypted outbox payload, delivery, activation, and token reuse rejection against PostgreSQL.
- [x] Test neutral unknown-account reset requests, password reset, password-changed mail, and new-password login.
- [x] Verify that stored token hashes, protected payloads, and Admin mail responses do not expose action tokens.
- [x] Verify SMTP delivery against local Mailpit and check SMTP reachability through API readiness.
- [x] Test manual retry, cancellation, tenant scoping, and audit creation against PostgreSQL.

- [ ] Unit-test template selection, localization, token creation, consent, suppression, and retry classification.
- [ ] Automate SMTP and selected Cloud-provider transport integration tests in CI.
- [ ] Test invitation, verification, password-reset, subscription, and security-notification flows.
- [ ] Complete retry, duplicate prevention, scheduling, dead-letter, and disabled-mail edge-case coverage.
- [ ] Test valid and invalid provider callbacks, replay protection, bounces, and complaints.
- [ ] Test double opt-in, unsubscribe, preference updates, and transactional delivery after marketing opt-out.
- [ ] Test HTML and plain-text rendering in left-to-right and right-to-left languages.
- [ ] Verify that logs and Admin responses contain no secret tokens, credentials, or complete message bodies.

## Acceptance Criteria

- [ ] Cloud and On-Premise use the same application mail interface.
- [ ] On-Premise can use customer SMTP or operate explicitly with mail disabled.
- [ ] Transactional delivery is independent from newsletter consent.
- [ ] Password-reset and invitation tokens are secure, single-use, purpose-bound, and expiring.
- [ ] Delivery retries do not create duplicate customer messages.
- [ ] Administrators can diagnose delivery failures without seeing sensitive message data.
