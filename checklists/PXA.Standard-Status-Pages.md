# PXA Standard Status Pages

## Goal

Provide consistent, accessible, localized status pages across PXA.Company,
PXA.Account, PXA.Admin, PXA.Designer, PXA.Documentation, and PXA.Demo. Users
must understand what happened, whether their work is safe, and what action is
available without exposing internal implementation details.

## Priorities

- [ ] P0: Define the shared status-page contract and essential HTTP/error states.
- [ ] P0: Add static fallbacks that work when JavaScript or the API is unavailable.
- [ ] P0: Integrate authentication, entitlement, and maintenance states with safe
  return navigation.
- [ ] P1: Add localization, diagnostics, telemetry, and application-specific
  recovery actions.
- [ ] P1: Validate accessibility, responsive behavior, and reverse-proxy routing.
- [ ] P2: Add hosted incident updates and richer offline recovery where useful.

## Status Catalog

- [ ] Add a generic unexpected-error page for HTTP 500.
- [ ] Add a not-found page for HTTP 404 with product-aware navigation.
- [ ] Add unauthenticated and session-expired pages for HTTP 401.
- [ ] Add forbidden, entitlement-denied, suspended, and trial-expired variants for
  HTTP 403 without presenting them as the same problem.
- [ ] Add bad-request and invalid-link pages for HTTP 400.
- [ ] Add conflict and stale-revision recovery for HTTP 409.
- [ ] Add expired-link and request-timeout handling for HTTP 408/410.
- [ ] Add file-too-large handling for HTTP 413.
- [ ] Add rate-limit handling for HTTP 429 with bounded retry information.
- [ ] Add upstream/service-unavailable pages for HTTP 502/503/504.
- [ ] Add planned-maintenance, offline, incompatible-version, and browser-not-
  supported states.
- [ ] Keep form validation, background-job failure, and toast notifications in
  their existing contextual UI unless the whole application cannot continue.

## Shared Page Contract

- [ ] Define a typed status model with status key, HTTP status, localized title,
  concise explanation, safe diagnostic code, correlation ID, retry policy,
  primary action, secondary action, and support link.
- [ ] Separate customer-safe messages from server logs and Problem Details.
- [ ] Never show exception messages, stack traces, SQL, paths, hostnames, tokens,
  tenant IDs, or personal information.
- [ ] Use one clear primary action such as Retry, Sign in, Return to Designer,
  View subscription, or Go home.
- [ ] Preserve only allowlisted same-origin or configured PXA return destinations.
- [ ] Explain whether unsaved work is preserved, recoverable, or at risk.
- [ ] Add copy-diagnostic-details without copying sensitive data.
- [ ] Map stable API diagnostic codes to page states instead of parsing prose.

## Design And Application Integration

- [ ] Build a shared visual specification using PXA design tokens, logo assets,
  typography, spacing, buttons, icons, and focus styles.
- [ ] Keep each application independently deployable; share a package or generated
  assets rather than importing another website bundle.
- [ ] Use compact product navigation only where the application can load safely.
- [ ] Provide a minimal standalone shell for fatal startup and asset-load errors.
- [ ] Preserve the current application identity so an Admin error does not look
  like a Company marketing page.
- [ ] Avoid decorative cards and marketing copy; prioritize status, recovery, and
  support information.
- [ ] Support light/dark contexts where the host application supports them.
- [ ] Do not rely on remote images, fonts, analytics, or APIs to render a fatal
  error page.

## Routing And Hosting

- [ ] Add application-level catch-all routes after all valid SPA routes.
- [ ] Configure reverse proxies and static hosts to serve the correct application
  404 rather than redirecting every unknown URL to the home page.
- [ ] Configure server error handling for HTML navigation and RFC 9457 Problem
  Details for API requests.
- [ ] Preserve HTTP status codes; rendering a friendly page must not convert a 404
  or 500 into HTTP 200.
- [ ] Provide static `404.html`, `500.html`, and maintenance fallbacks where the
  hosting platform supports them.
- [ ] Prevent service workers and caches from trapping users on obsolete status
  pages.
- [ ] Define safe retry behavior and avoid automatic retry loops.

## Authentication And Authorization

- [ ] Route unauthenticated users to PXA.Account only when authentication can
  recover the requested action.
- [ ] Keep sign-in and registration exclusively in PXA.Account.
- [ ] Distinguish missing permission, missing entitlement, inactive organization,
  suspended account, expired trial, and failed Designer handoff.
- [ ] Preserve PKCE/state and allowlisted return paths during recoverable handoff
  errors.
- [ ] Reuse the existing Designer diagnostic codes and subscription actions.
- [ ] Do not reveal whether inaccessible tenant resources exist.

## Accessibility And Localization

- [ ] Localize shared states in all six Designer languages and use English
  fallback for untranslated applications.
- [ ] Set document language, meaningful page title, one H1, logical focus order,
  and initial focus on the status heading.
- [ ] Announce dynamically entered fatal states without repeated screen-reader
  output.
- [ ] Meet WCAG contrast, zoom, reflow, reduced-motion, keyboard, and touch-target
  requirements.
- [ ] Avoid idioms in source copy and allow text expansion without clipping.
- [ ] Provide RTL layout for Arabic without reversing codes, URLs, or action order
  incorrectly.

## Observability And Privacy

- [ ] Record status key, route template, application, version, correlation ID,
  recovery action, and bounded technical category.
- [ ] Do not record document content, form values, query secrets, uploaded
  filenames, source code, or full URLs containing customer data.
- [ ] Link server traces through correlation IDs without exposing trace payloads.
- [ ] Deduplicate repeated client errors and bound telemetry retries.
- [ ] Monitor error-page loops, failed recovery actions, and unknown diagnostic
  mappings.

## Validation

- [ ] Add a contract test for every status key, HTTP status, localization key, and
  safe recovery action.
- [ ] Test valid routes before catch-all routes in every application.
- [ ] Test direct navigation, refresh, deep links, reverse proxy, disabled
  JavaScript, offline mode, and failed asset loading.
- [ ] Test authentication and entitlement scenarios without open redirects or
  tenant disclosure.
- [ ] Verify server-rendered/static fallbacks retain the intended HTTP status.
- [ ] Run accessibility scans plus keyboard and screen-reader smoke tests.
- [ ] Capture desktop and mobile screenshots for every shared layout class.
- [ ] Scan rendered pages for secrets, stack traces, localhost links, legacy
  Canvas branding, and untranslated raw keys.

## Acceptance Criteria

- [ ] Every PXA application has a branded 404 and recoverable fatal-error path.
- [ ] Authentication, authorization, entitlement, offline, and server failures are
  distinguishable and actionable.
- [ ] Status pages remain usable when the API, JavaScript application, or remote
  assets fail.
- [ ] HTTP semantics, security boundaries, accessibility, and localization remain
  correct.
- [ ] No status page leaks internal or customer-sensitive information.

## Dependencies And References

- [ ] Use [PXA.Web-Design-System.md](PXA.Web-Design-System.md) for shared visual
  tokens and components.
- [ ] Align Designer blocking states with
  [PXA.Designer-Authentication.md](PXA.Designer-Authentication.md) and
  [PXA.Designer-Releases-And-Notifications.md](PXA.Designer-Releases-And-Notifications.md).
- [ ] Align API errors and observability with the existing LMA and API contracts.
- [ ] Use approved assets from
  [PXA.Brand-And-Logo-System.md](PXA.Brand-And-Logo-System.md).

## Assumptions

- Status pages are product UI, while API callers continue receiving Problem
  Details.
- Each deployment can render a minimal local fallback without another PXA host.
- Product-specific recovery actions are adapters over one shared status contract.
- Incident-management and public status-site hosting remain separate concerns.

