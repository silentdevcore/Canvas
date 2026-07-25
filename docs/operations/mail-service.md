# Mail Service Operations

PXA queues transactional mail in PostgreSQL and delivers it through a configured transport. Queue payloads are protected with ASP.NET Core Data Protection. Rendered HTML and plain-text message bodies are created only during delivery and are not stored in the outbox.

## Delivery modes

Configure `Mail:Transport` as one of:

- `Development`: captures messages in memory for local development and automated tests.
- `Smtp`: sends through the customer-configured SMTP server.
- `Disabled`: retains pending mail intents without attempting delivery.

Use external secret storage for SMTP credentials and persist the configured Data Protection key directory across API restarts. Never place credentials in committed configuration files.

## Disabled mail

Disabled mode is intended for isolated installations that cannot send email. The Admin mail status reports delivery as disabled, and queued messages remain pending. PXA does not expose invitation or password-reset tokens through the Admin API.

Administrators should either configure SMTP and let the queue resume or use an approved identity-administration procedure to restore account access. Do not copy protected payloads or token records from the database.

## Retry and recovery

Transient failures use bounded exponential retries. A message moves to `dead_letter` after five failed attempts. Unknown or removed templates fail permanently. Authorized tenant administrators can inspect sanitized failure metadata and retry or cancel eligible messages through PXA Admin.

Investigate SMTP connectivity, credentials, TLS configuration, and sender policy before retrying a failed batch. Retry actions are audited and idempotency keys prevent a workflow from enqueueing the same message twice.

## Retention

The API periodically deletes terminal outbox records in bounded batches:

- Delivered and suppressed: 30 days by default.
- Cancelled: 14 days by default.
- Dead letter: 90 days by default.

Configure these periods with `Mail:DeliveredRetentionDays`, `Mail:CancelledRetentionDays`, and `Mail:DeadLetterRetentionDays`. `Mail:RetentionCleanupIntervalMinutes` controls cleanup frequency and `Mail:RetentionBatchSize` bounds each deletion.

Pending, scheduled, sending, and failed messages are never removed by retention cleanup. Consent and immutable security audit records follow their own future retention policies.

## Privacy

Admin list responses contain masked recipient addresses and never include protected payloads or rendered bodies. General application logs must not contain recipient addresses, action tokens, credentials, provider payloads, or document contents. Provider IDs and sanitized failure categories are sufficient for operational diagnosis.
