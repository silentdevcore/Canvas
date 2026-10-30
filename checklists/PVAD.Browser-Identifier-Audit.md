# PVAD Browser Identifier Audit

Reviewed: 2026-10-18. Scope: browser storage, internal events and in-memory caches.
See [rename checklist](PVAD.Rename-Plan.md) and
[migration notes](../docs/pvad-rename-migration-notes.md).

## Canonical Writes

| Area | New identifier | Legacy handling |
| --- | --- | --- |
| Language | `pvad_locale` | Read the old preference once; canonical value wins |
| Sidebar preferences | `pvad-designer:*` | Same-origin recoverable migration |
| Element groups and recent content | `pvad-designer-*-v1` | Same-origin recoverable migration |
| Element favorites | `pvad-designer-element-favorites-v1:<user>` | Only the exact user's cache is read |
| Export preference | `pvad_export_format` | Recoverable read of [compatibility entry `compat-8b064f2336a7`](../docs/reference/compatibility-identifiers.v1.json) |
| Workbook drafts | `pvad-spreadsheet:v1:<user>:<organization>` | No automatic adoption of unassigned drafts |
| Code recovery | `pvad-code-editor-draft-v2` | Explicit import remains required |
| Auth transaction | `pvad.designer.auth-transaction` | One-time legacy read, original state/PKCE, ten-minute limit |
| Document handoffs | `pvad_migration_designer_handoff`, `pvad_pdf_viewer_handoff` | Owner-bound, five-minute envelopes; old raw payloads rejected |
| Status caches | `pvad-status-loop-v1`, `pvad-status-recovery-v1` | Legacy caches migrated; recovery consumed once |
| Account/Admin events | `pvad:api-*`, `pvad:session-expired`, `pvad:access-denied`, `pvad:rerender` | Producers and listeners changed together |
| Viewer navigation event | `pvad-designer:navigate-element` | Canonical internal dispatch |
| Content drag payload | `application/x-pvad-content-block` | Drops also read old MIME payloads |
| Telemetry bridge | `__pvadBrowserTelemetry` | [compatibility entry `compat-99ffa4a80fcf`](../docs/reference/compatibility-identifiers.v1.json) is one shared compatibility alias, not another collector |
| Transient browser owners | `pvad-json`, `pvad-imported-font-faces-*` | New marker/style owners, no document rewrite |

Neutral identifiers such as `editor-storage` remain unchanged. No production
customer database, cookies or cryptographic material was modified by this stage.
Unassigned workbook recovery reads only named recovery keys, never enumerates
other users' scoped entries, and exports without deletion or automatic assignment.

## Explicitly Deferred Contracts

- Authentication and antiforgery cookies now write PVAD names with legacy readers,
  chunk cleanup and conflict rejection. Ticket protection schemes and keyring identity
  remain stable. Deployed-browser/session validation is still open. The storage-notice
  cookie remains legacy. See [identity compatibility](../docs/pvad-identity-compatibility.md).
- API clients now write `X-PVAD-Application` and `X-PVAD-CSRF`. The server normalizes
  compatible old headers before authentication; diagnostics retain their separate
  contract mapping. See [HTTP compatibility](../docs/pvad-http-compatibility.md).
- Runtime configuration now reads `__PVAD_CONFIG__` with a validated
  [compatibility entry `compat-1fabf8f94004`](../docs/reference/compatibility-identifiers.v1.json) compatibility input. Deployment scripts/hosts still need the
  broader runtime rollout verification; this is not stored user preference data.
- CSS classes, DOM data selectors, schema IDs, native-format markers and published
  historical content remain part of their separate rename tasks.

## Verification

- Complete Designer Jest run: 77 suites, 672 tests passed.
- Account: 47 tests; Admin: 36; Company/shared/legal: 46 passed. Shared tests
  occur in multiple application commands; counts are not unique test identities.
- All five website builds and the Designer build passed, with existing Vite
  module-format/chunk-size warnings. Designer and Account typechecks passed.
- No authenticated production-browser, cookie rollover or deployment verification
  is claimed. Those acceptance gates remain open in the main checklist.
