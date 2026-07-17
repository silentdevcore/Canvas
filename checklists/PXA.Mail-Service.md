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

- [ ] Define one mail application interface independent of SMTP and provider SDKs.
- [ ] Add a replaceable Cloud transactional-provider adapter.
- [ ] Add customer-configured SMTP transport for On-Premise installations.
- [ ] Add a development mail catcher or in-memory transport.
- [ ] Add mail-disabled mode for isolated deployments with explicit administrative status.
- [ ] Keep provider credentials, SMTP credentials, and signing secrets in external secret storage.
- [ ] Separate message composition, queueing, transport, and delivery-status processing.

## Transactional Email

- [ ] Send user invitation and invitation-expiry messages.
- [ ] Send email-verification messages.
- [ ] Send password-reset and password-changed notifications.
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

- [ ] Create versioned templates with stable template keys.
- [ ] Provide HTML and plain-text variants for every message.
- [ ] Support localized subject, body, dates, numbers, links, and directionality.
- [ ] Select language from user preference with organization and system fallbacks.
- [ ] Add consistent PXA branding, accessible structure, and meaningful link labels.
- [ ] Preview templates with synthetic data before activation.
- [ ] Never include passwords, full API keys, license signing material, or document contents.

## Queue And Delivery Lifecycle

- [ ] Use an outbox or durable queue so business transactions and mail intent cannot diverge.
- [ ] Define `pending`, `scheduled`, `sending`, `delivered`, `failed`, `suppressed`, `cancelled`, and `dead_letter` states.
- [ ] Add idempotency keys to prevent duplicate messages after retries.
- [ ] Add bounded exponential retry for transient failures.
- [ ] Move permanently failed messages to a dead-letter state with safe administrative retry.
- [ ] Store provider message ID, template version, recipient reference, attempts, timestamps, and sanitized failure reason.
- [ ] Avoid storing message bodies longer than required for delivery and support.
- [ ] Add retention and deletion rules for queue, delivery, consent, and audit metadata.

## Secure Action Tokens

- [ ] Generate cryptographically strong, purpose-bound invitation, verification, reset, consent, and unsubscribe tokens.
- [ ] Store only hashed action tokens where server-side persistence is required.
- [ ] Make sensitive action tokens single-use and short-lived.
- [ ] Bind tokens to recipient, tenant, purpose, and expiry.
- [ ] Invalidate superseded tokens and all relevant tokens after successful completion.
- [ ] Avoid revealing whether an email address exists during public reset requests.

## Provider Events And Operations

- [ ] Validate webhook signatures, timestamps, event IDs, and expected providers.
- [ ] Process delivery, delay, bounce, complaint, and suppression events idempotently.
- [ ] Reject replayed, malformed, unsigned, and cross-tenant callback events.
- [ ] Expose queue health, delivery status, failure rate, and dead-letter count to authorized administrators.
- [ ] Add alerts for queue backlog, provider outage, authentication failure, and abnormal bounce rates.
- [ ] Keep recipient addresses and provider payloads out of general application logs.

## Configuration

- [ ] Configure sender name, sender address, reply-to address, public URLs, and support contact by environment.
- [ ] Configure SMTP host, port, TLS mode, authentication, timeout, and certificate validation for On-Premise.
- [ ] Configure Cloud provider credentials and webhook secrets externally.
- [ ] Validate configuration at startup and expose safe readiness diagnostics.
- [ ] Document operation when mail is disabled and the administrative alternatives for invitation and reset workflows.

## Tests

- [ ] Unit-test template selection, localization, token creation, consent, suppression, and retry classification.
- [ ] Integration-test SMTP, development transport, and the selected Cloud provider adapter.
- [ ] Test invitation, verification, password-reset, subscription, and security-notification flows.
- [ ] Test retry, duplicate prevention, scheduling, dead-letter, manual retry, and disabled-mail behavior.
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
