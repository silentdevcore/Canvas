# PXA Standard Status Pages

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Historical PXA/Canvas product references, implementation notes and compatibility identifiers are retained; task statuses are unchanged.

## Goal

Provide consistent, accessible, localized status pages across PXA.Company,
PXA.Account, PXA.Admin, PXA.Designer, PXA.Documentation, and PXA.Demo. Users
must understand what happened, whether their work is safe, and what action is
available without exposing internal implementation details.

## Priorities

- [x] P0: Define the shared status-page contract and essential HTTP/error states.
- [x] P0: Add static fallbacks that work when JavaScript or the API is unavailable.
- [x] P0: Integrate authentication, entitlement, and maintenance states with safe
  return navigation.
- [x] P1: Add localization, diagnostics, telemetry, and application-specific
  recovery actions.
- [x] P1: Validate automated accessibility and responsive behavior.
- [ ] P1: Validate reverse-proxy routing and HTTP status preservation in a deployed
  environment.
- [x] P1: Validate the production Nginx contract locally with real HTTP requests,
  including deep links, 404 semantics, cache headers, immutable assets, and API
  gateway failures.
- [ ] P2: Add hosted incident updates and richer offline recovery where useful.

## Status Catalog

- [x] Add a generic unexpected-error page for HTTP 500.
- [x] Add a not-found page for HTTP 404 with product-aware navigation.
- [x] Add unauthenticated and session-expired pages for HTTP 401.
- [x] Add forbidden, entitlement-denied, suspended, and trial-expired variants for
  HTTP 403 without presenting them as the same problem.
- [x] Add bad-request and invalid-link pages for HTTP 400.
- [x] Add conflict and stale-revision recovery for HTTP 409.
- [x] Add expired-link and request-timeout handling for HTTP 408/410.
- [x] Add file-too-large handling for HTTP 413.
- [x] Add rate-limit handling for HTTP 429 with bounded retry information.
- [x] Add upstream/service-unavailable pages for HTTP 502/503/504.
- [x] Add planned-maintenance, offline, incompatible-version, and browser-not-
  supported states.
- [x] Keep form validation, background-job failure, and toast notifications in
  their existing contextual UI unless the whole application cannot continue.

## Shared Page Contract

- [x] Define a typed status model with status key, HTTP status, localized title,
  concise explanation, safe diagnostic code, correlation ID, retry policy,
  primary action, secondary action, and support link.
- [x] Separate customer-safe messages from server logs and Problem Details.
- [x] Never show exception messages, stack traces, SQL, paths, hostnames, tokens,
  tenant IDs, or personal information.
- [x] Use one clear primary action such as Retry, Sign in, Return to Designer,
  View subscription, or Go home.
- [x] Preserve only allowlisted same-origin or configured PXA return destinations.
- [x] Explain whether unsaved work is preserved, recoverable, or at risk.
- [x] Add copy-diagnostic-details without copying sensitive data.
- [x] Map stable API diagnostic codes to page states instead of parsing prose.

## Design And Application Integration

- [x] Build a shared visual specification using PXA design tokens, logo assets,
  typography, spacing, buttons, icons, and focus styles.
- [x] Keep each application independently deployable; share a package or generated
  assets rather than importing another website bundle.
- [x] Use compact product navigation only where the application can load safely.
- [x] Provide a minimal standalone shell for fatal startup and asset-load errors.
- [x] Preserve the current application identity so an Admin error does not look
  like a Company marketing page.
- [x] Avoid decorative cards and marketing copy; prioritize status, recovery, and
  support information.
- [x] Confirm that current host applications expose no page-wide dark context;
  status pages inherit host tokens when such a context is introduced.
- [x] Do not rely on remote images, fonts, analytics, or APIs to render a fatal
  error page.

## Routing And Hosting

- [x] Add application-level catch-all routes after all valid SPA routes.
- [x] Configure reverse proxies and static hosts to serve the correct application
  404 rather than redirecting every unknown URL to the home page.
- [x] Configure RFC 9457 Problem Details for API requests and unexpected API
  failures.
- [x] Configure deployed server error handling for HTML navigation.
- [x] Preserve HTTP status codes; rendering a friendly page must not convert a 404
  or 500 into HTTP 200.
- [x] Provide static `404.html`, `500.html`, and maintenance fallbacks where the
  hosting platform supports them.
- [x] Confirm that no PXA web application currently registers a service worker.
- [x] Configure deployed cache headers so obsolete status pages cannot trap users.
- [x] Define safe retry behavior and avoid automatic retry loops.

## Authentication And Authorization

- [x] Route unauthenticated users to PXA.Account only when authentication can
  recover the requested action.
- [x] Keep sign-in and registration exclusively in PXA.Account.
- [x] Distinguish missing permission, missing entitlement, inactive organization,
  suspended account, expired trial, and failed Designer handoff.
- [x] Preserve PKCE/state and allowlisted return paths during recoverable handoff
  errors.
- [x] Reuse the existing Designer diagnostic codes and subscription actions.
- [x] Do not reveal whether inaccessible tenant resources exist.

## Accessibility And Localization

- [x] Localize shared states in all six Designer languages and use English
  fallback for untranslated applications.
- [x] Set document language, meaningful page title, one H1, logical focus order,
  and initial focus on the status heading.
- [x] Announce dynamically entered fatal states without repeated screen-reader
  output.
- [x] Meet WCAG contrast, zoom, reflow, reduced-motion, keyboard, and touch-target
  requirements.
- [x] Avoid idioms in source copy and allow text expansion without clipping.
- [x] Provide RTL layout for Arabic without reversing codes, URLs, or action order
  incorrectly.

## Observability And Privacy

- [x] Record status key, route template, application, version, correlation ID,
  recovery action, and bounded technical category.
- [x] Do not record document content, form values, query secrets, uploaded
  filenames, source code, or full URLs containing customer data.
- [x] Link server traces through correlation IDs without exposing trace payloads.
- [x] Deduplicate repeated client errors and bound telemetry retries.
- [x] Monitor repeated error-page loops using bounded session data.
- [x] Monitor unknown diagnostic mappings without recording unknown codes.
- [x] Monitor failed recovery actions after deployed navigation outcomes are
  observable.

## Validation

- [x] Add a contract test for every status key, HTTP status, localization key, and
  safe recovery action.
- [x] Test valid routes before catch-all routes in every application.
- [x] Test direct navigation, refresh, deep links, reverse proxy, disabled
  JavaScript, offline mode, and failed asset loading.
- [x] Test authentication and entitlement scenarios without open redirects or
  tenant disclosure.
- [x] Verify server-rendered/static fallbacks retain the intended HTTP status.
- [x] Run automated accessibility scans and keyboard smoke tests.
- [ ] Run manual screen-reader smoke tests with VoiceOver or NVDA.
- [x] Capture desktop and mobile screenshots for every shared layout class.
- [x] Scan rendered pages for secrets, stack traces, localhost links, legacy
  Canvas branding, and untranslated raw keys.

## Validation Evidence

- [x] Run `npm run test:e2e:status` against production previews of all six web
  applications; all eleven browser scenarios pass.
- [x] Store desktop and mobile status-page captures for all six applications in
  the ignored `artifacts/status-pages/` test-output directory.
- [x] Verify direct navigation, refresh, JavaScript-disabled static fallbacks,
  offline authentication, Arabic RTL, and distinct entitlement states.
- [x] Verify Designer authorization handoff strips untrusted nested return URLs
  while preserving allowlisted local route parameters.
- [x] Run Axe WCAG 2.1 AA scans, keyboard recovery, 200-percent text reflow,
  reduced-motion, 44-pixel target, and asset-independent fallback checks.
- [x] Validate bounded application-version, status-category, correlation, and
  status-loop telemetry without recording URLs or customer content.
- [x] Validate recovery selection, successful navigation, and repeated status-page
  failure outcomes without retaining full URLs or customer data.
- [x] Run the production Nginx boundary smoke test: known route `200`, unknown
  route `404`, no-store status page, no-cache app shell, immutable hashed asset,
  and unavailable API as `503 application/problem+json`.

## Acceptance Criteria

- [x] Every PXA application has a branded 404 and recoverable fatal-error path.
- [x] Authentication, authorization, entitlement, offline, and server failures are
  distinguishable and actionable.
- [x] Status pages remain usable when the API, JavaScript application, or remote
  assets fail.
- [x] HTTP semantics, security boundaries, automated accessibility, and
  localization remain
  correct.
- [x] No status page leaks internal or customer-sensitive information.

## Dependencies And References

- [x] Use [PVAD.Web-Design-System.md](PVAD.Web-Design-System.md) for shared visual
  tokens and components.
- [x] Align Designer blocking states with
  [PVAD.Designer-Authentication.md](PVAD.Designer-Authentication.md) and
  [PVAD.Designer-Releases-And-Notifications.md](PVAD.Designer-Releases-And-Notifications.md).
- [x] Align API errors and observability with the existing LMA and API contracts.
- [ ] Use approved assets from
  [PVAD.Brand-And-Logo-System.md](PVAD.Brand-And-Logo-System.md).

## Assumptions

- Status pages are product UI, while API callers continue receiving Problem
  Details.
- Each deployment can render a minimal local fallback without another PXA host.
- Product-specific recovery actions are adapters over one shared status contract.
- Incident-management and public status-site hosting remain separate concerns.

## External Completion Gates

- Repeat the Nginx boundary smoke test at every public hostname after the CDN,
  ingress, TLS, and production reverse proxy are deployed.
- Complete a manual VoiceOver or NVDA session; automated Axe, keyboard,
  zoom, reflow, reduced-motion, RTL, and touch-target coverage already passes.
- Replace the temporary text mark only after the assets in
  `PVAD.Brand-And-Logo-System.md` are formally approved.
- Add hosted incident updates and richer offline recovery as a P2 operational
  enhancement; this does not block the status-page implementation.
