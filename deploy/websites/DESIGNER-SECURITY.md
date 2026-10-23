# Designer Browser Security Profile

## Status: Draft, Not Deployment Approved

The profile is optional. Without a matching file in `/etc/nginx/pvad/`, the
shared website template does not add these security headers. No production
deployment or browser association has been changed.

After compatibility approval, mount `designer.headers.conf` read-only at
`/etc/nginx/pvad/designer.headers.conf` in the Designer Nginx container. Deploy
the updated Nginx template with the profile. Do not apply the Designer profile
to Company, Account, Admin or Documentation without their own review.

Every location includes the optional profile explicitly because location-level
cache headers otherwise override inherited Nginx headers. This includes real
404 pages and unavailable-API responses.

## Enforced Baseline

- Same-origin base URLs and form submissions.
- No embedding of the Designer in another page (`frame-ancestors 'none'`).
- Same-origin and blob object sources, to retain native PDF integrations.
- Strict-origin-when-cross-origin referrer policy.

Applications intentionally embedding Designer need a separately reviewed frame
ancestor policy. Do not replace it with a wildcard.

## Resource Policy Is Report-only

The resource candidate permits local/blob workers and frames, current Google
font origins, HTTPS images and the pinned Monaco CDN version used by the current
loader. Inline script and eval are not allowed by the candidate. Inline styles
remain permitted for the current React/editor styling model.

Report-only violations do **not** block resources. This is not a strict script
execution policy or proof of XSS protection. The current external Monaco loader,
optional unsafe expression evaluation and all preview/export workflows must be
reviewed before promoting the resource policy to enforcement.

There is no report endpoint or third-party reporting collector. Browser tests
record only directive names and dispositions, not document URLs or source text.

## Evidence And Required Follow-up

- The earlier baseline browser check passed base-URL enforcement, report-only
  inline-script diagnostics and both project imports on desktop/mobile.
- The native PDF-frame probe timed out both with CSP and with CSP bypassed;
  no corresponding CSP violation was observed. Native PDF display and print
  are unverified, not passed or proven broken by the policy.
- The final profile allows local/blob object sources. Its contract tests pass,
  but the requested final browser run was not approved. Do not treat earlier
  results as validation of this exact revision.
- The production smoke now covers HTML blob-frame loading, a blob worker, PDF
  download signature and both editor imports. Run it after approval, then repeat
  the full local validation bundle. A synthetic HTML frame is not native PDF
  viewing or printing evidence.
- Complete real code-editor loading, font/image/binding workflows, native PDF
  display/print and live authentication tests before deployment approval.

```bash
node --test deploy/websites/*.test.mjs
node tools/configuration/native-project-production-smoke.mjs
node tools/configuration/rename-local-validation.mjs
```

Browser/container commands require environment permission. OS installation,
external domains, publisher registration and production authorization remain
separate tasks. No combined rename gate is closed by this draft.
