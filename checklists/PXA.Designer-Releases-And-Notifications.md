# PXA Designer Releases and Notifications

## Goal

- [x] Provide one versioning, release-note, feature-maturity, toast, and notification system for PXA Designer.
- [x] Keep the Designer and public Documentation consistent through shared product metadata.
- [x] Keep API compatibility versions separate from the Designer product version.

## P0 - Versioning and Release Notes

- [x] Keep `pxa-designer/package.json` as the Designer SemVer source.
- [x] Inject Designer version, build time, and Git commit into production builds.
- [x] Add a validated, curated release manifest shared by Designer, WebApi, and Documentation.
- [x] Require one manifest entry for every shipped Designer version.
- [x] Support Added, Improved, Fixed, Security, Deprecated, and Breaking sections.
- [x] Show the current version and a "What's New" action in the Designer user menu.
- [x] Add an accessible release-notes drawer and show it once for each unseen release.
- [x] Persist release-read state per user in PostgreSQL.
- [x] Add public release-note navigation and version pages to PXA.Documentation.

## P0 - Toasts

- [x] Add one global toast provider based on the existing `react-hot-toast` dependency.
- [x] Support success, info, warning, error, and loading states.
- [x] Support stable IDs, deduplication, actions, controlled duration, and loading-to-result transitions.
- [x] Replace browser `alert()` calls and local Designer toast implementations.
- [x] Localize toast UI and accessibility labels in all six Designer languages.
- [x] Keep blocking access, offline, security, and license failures in persistent UI states.

## P1 - Feature Maturity and Gates

- [x] Add stable feature IDs with Alpha, Beta, and Stable maturity.
- [x] Treat New as a temporary marker independent of maturity.
- [x] Disable Alpha features by default and require an allowed user opt-in.
- [x] Allow organization policy to block Alpha and centrally disable Beta.
- [x] Keep server-side feature checks authoritative for protected operations.
- [x] Combine feature gates with, but do not replace, subscription entitlements.
- [x] Preserve provider coverage statuses such as pilot, full, and skeleton.
- [x] Audit user and organization feature-preference changes.

## P1 - Notification Center

- [x] Add a global header bell with an unread counter.
- [x] Support Release, System, Security, Subscription, and Action Required categories.
- [x] Persist targeted notifications and per-user read/dismiss state in PostgreSQL.
- [x] Add paginated list, unread-count, mark-read, mark-all-read, and dismiss APIs.
- [x] Enforce user and organization isolation on every notification operation.
- [x] Merge release announcements into the center without creating one database row per user.
- [x] Refresh at bootstrap, browser focus, and every 60 seconds.
- [ ] Keep real-time push as a later enhancement.

## Security and Accessibility

- [x] Validate internal Designer action URLs and reject unsafe external destinations.
- [x] Never expose internal tickets, secrets, customer data, or sensitive security details.
- [x] Use polite live regions for normal feedback and alert semantics for failures.
- [x] Support keyboard navigation, focus trapping, Escape close, and responsive layouts.
- [x] Ensure feature gates cannot be bypassed by calling the API directly.

## Testing

- [x] Validate manifest schema, SemVer, uniqueness, ordering, feature references, and current-version coverage.
- [x] Test Alpha, Beta, Stable, New, organization policy, user preference, and entitlement combinations.
- [x] Test notification tenant isolation, expiry, pagination, read state, and dismissal.
- [x] Test toast deduplication, loading transitions, actions, and accessibility semantics.
- [ ] Test the unseen-release drawer and cross-device release-read state.
- [x] Test Documentation sidebar, focus mode, search, filters, and internal links.
- [x] Run WebApi tests and build.
- [x] Run Designer tests, type check, and production build.
- [x] Run PXA.Documentation production build and desktop/mobile smoke tests.

## Acceptance Criteria

- [x] Designer version and release notes are generated from explicit, reviewable sources.
- [x] Designer and Documentation display the same release content.
- [x] Alpha is opt-in, Beta is visibly marked, and Stable has no maturity badge.
- [x] Important messages remain available after transient toasts disappear.
- [x] Release-read and notification-read state follow the authenticated user across devices.
- [x] Existing Designer authentication, tenant isolation, and entitlement behavior remain intact.

## Verification Record

- [x] `PXA.Api.Tests`: 261 passed; 59 existing PostgreSQL-gated tests skipped by configuration.
- [x] Designer: type check passed; 287 tests passed; production build passed.
- [x] Documentation: 3 release-contract tests passed; production build passed.
- [x] Documentation desktop and mobile smoke checks passed without horizontal overflow.
- [x] Documentation release search, focus mode, channel filters, and version navigation passed in Chromium.
- [x] `git diff --check` passed.

## Later Work

- [ ] Add real-time notification delivery.
- [ ] Add external release publishing automation.
- [ ] Localize release notes, notification content, feature descriptions, and related fallback states in all six Designer languages.
- [ ] Align the release drawer, notification center, feature badges, and toast presentation more closely with the established PXA Designer visual design.
- [ ] Keep notifications clickable after they are read so users can reopen and review their complete content multiple times.
- [ ] Add release authoring and notification publishing tools to PXA Admin.
