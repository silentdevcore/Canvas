# PVAD Rename Rollback And Cutover Runbook

## Authorization And Preconditions

This is a procedure, not evidence of a production deployment or restore. An
authorized operator must record the environment, rollout owner, backup owner,
restore owner, approved window, exact old/new image digests and success criteria.
These external assignments are still open; do not invent owners or credentials.

Before changing anything, save a consistent database backup, object-store version
or snapshot, Data Protection keyring, license/signing-key references and deployed
configuration. Store secrets only in the approved secret store. Verify restoration
in an isolated environment and retain the result. An untested backup is not a
rollback guarantee. Keep existing physical volumes and migration history.

## Controlled Cutover

1. Put mutations into maintenance mode and drain HTTP traffic and queued jobs.
2. Stop old workers and confirm lease/processing state. Do not run old and new
   workers concurrently without separate mixed-version evidence.
3. Deploy matching PVAD API, code/OCR workers and frontend artifacts. Deploy the
   Nginx environment hook together with its template. Validate configuration alias
   conflicts before starting services.
4. Verify readiness, canonical/legacy API paths, host-only Account/Designer/Admin
   cookies, CSRF, PKCE, tenant isolation, template revisions, jobs and downloads.
5. Resume bounded traffic only after those checks pass; monitor duplicate job
   effects, authentication failures and error rates. Record the release digest.

## Rollback Decision

Stop new writes first. Capture failure diagnostics without document content or
secrets. Restore the previous paired API/worker/frontend images and configuration
only if the previous binaries can read every newly written format/purpose/schema.
Source compatibility readers do not imply old-binary forward compatibility.

- **Database:** never edit applied migration IDs. If incompatible migrations or
  writes occurred, use the approved restore procedure with an explicit data-loss
  window. Do not blindly run migration Down operations against customer data.
- **Encrypted data:** retain both required keyrings and purpose readers. Newly
  protected PVAD payloads may be unreadable to old PXA binaries. Drain or export
  safely before rollback; restore paired database/object snapshots if necessary.
- **Jobs:** retain leases, operation IDs and outputs. Check whether external side
  effects already happened before replaying anything. No exactly-once promise is
  made. Never force-expire a live worker lease as a recovery shortcut.
- **Identity:** retain ticket protection purposes and signing keys; do not broaden
  cookie domains. If old session readers cannot accept new tickets, perform a
  coordinated sign-out instead of attempting to rewrite credentials or sessions.
- **Files:** keep originals. Old binaries may not read new PVADP/PVADS packages;
  offer an explicitly supported export or keep a compatible reader available.
- **Licenses/legal:** do not reverse signed replacements, mutate published legal
  text, acceptance hashes or audits. Follow approved successor/revocation and
  evidence-preservation procedures, even when rolling application binaries back.
- **URLs/config:** preserve legacy adapters and redirects for the approved window.
  Revert DNS/CDN/mail/registry changes only with their authorized external owners.

## Evidence And Remaining Gates

The [paired-binary harness](../tools/job-cutover/README.md) provides synthetic
database/job evidence, not production restore certification. See
[identity](pvad-identity-compatibility.md),
[licenses/jobs](pvad-license-and-job-compatibility.md),
[HTTP](pvad-http-compatibility.md) and
[hosting](pvad-frontend-host-validation.md) compatibility notes.
Production backup/restore, external owner approval and TLS/CDN deployment checks
remain required before release. Never overwrite a released tag to correct a failed
cutover; prepare a new patch release through the normal release process.
