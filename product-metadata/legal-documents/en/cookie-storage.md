# Cookie and Storage Policy

Effective date: the effective date shown for the published version.

This Policy explains the first-party cookies, Local Storage, and Session Storage used by PVADE. At launch, PVADE uses necessary security storage and user-requested application preferences. It does not use advertising cookies, cross-site tracking, or optional marketing storage.

## 1. Necessary cookies

- `PVAD.StorageNotice` (legacy equivalent: [compatibility entry `compat-7c69ebfa621d`](/docs/reference/compatibility-identifiers.v1.json)) stores only the notice version for 180 days so the necessary-storage notice is not repeatedly shown.
- `__Host-PVAD.Session` (legacy equivalent: [compatibility entry `compat-9471a2eae6b0`](/docs/reference/compatibility-identifiers.v1.json)), or its development equivalent, authenticates Account and Admin sessions for up to eight hours with sliding renewal. It is HttpOnly.
- `__Host-PVAD.Designer.Session` (legacy equivalent: [compatibility entry `compat-e9b3b9431521`](/docs/reference/compatibility-identifiers.v1.json)), or its development equivalent, authenticates the isolated Designer session for up to eight hours with sliding renewal. It is HttpOnly.
- `__Host-PVAD.Antiforgery` (legacy equivalent: [compatibility entry `compat-436582ad4e35`](/docs/reference/compatibility-identifiers.v1.json)), or its development equivalent, protects state-changing requests for the browser session. It is HttpOnly.

Host-only and __Host- cookie protections are used in secure production deployments. Authentication and anti-forgery cookies are technically necessary to provide secure signed-in services.

## 2. Session Storage

PVADE uses tab-scoped Session Storage for one-time Designer authentication, migration-to-Designer handoff, and PDF Viewer handoff. These values can include a PKCE verifier or user-selected document workflow data. They are consumed by the target flow or removed when the tab closes.

## 3. Local Storage preferences

PVADE stores choices explicitly made by a user, including:

- selected interface language;
- editor mode;
- code-editor language;
- last export format;
- PDF and Spreadsheet sidebar state; and
- other comparable presentation preferences disclosed in the interface.

These preferences remain until changed, cleared, or removed during sign-out or organization switching where applicable.

## 4. Local working copies

The Designer may keep a local Spreadsheet workbook or code/design draft to recover from an accidental refresh. These values may contain content entered by the user. They remain until replaced, cleared, signed out, switched to another organization, or removed through browser site-data controls. Organization-owned server templates are stored separately and are governed by the Privacy Notice and DPA.

## 5. Storage notice

Because the current storage is necessary or requested for application functionality, PVADE displays an informational notice with an Understood action instead of an Accept all banner. A persistent Cookie and Storage Settings link provides access to this Policy and available controls.

## 6. Your controls

You can clear PVADE site data through browser settings. Blocking necessary cookies can prevent login, security validation, and protected operations. Clearing Local Storage removes preferences and local recovery copies but does not delete organization data stored on the server.

## 7. Optional technologies

Before PVADE introduces optional analytics, advertising, session replay, or marketing technologies that require consent, it will provide equally accessible Accept all, Reject all, and Customize actions; leave optional categories off by default; block optional requests before consent; record the policy version; and permit easy withdrawal.

## 8. Changes and contact

The machine-readable browser-storage inventory and this Policy are reviewed together when a PVAD-owned storage mechanism changes. Material changes are identified by version and effective date.

Questions: [PRIVACY EMAIL ADDRESS]

## Technical Reference

This candidate references Compatibility Identifiers v1, SHA-256 `19667963b4e062821e81be371c9e81d0ae1df5b33c54fa9b9c237d76f4960906`. The candidate manifest pins that versioned technical reference; it does not change contractual rights or authorize publication.
