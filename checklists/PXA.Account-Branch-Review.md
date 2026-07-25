# PXA Account Branch Review Checklist

## Scope

Track the findings from the full review of branch `pxa-account-all-open-points`.
This checklist covers corrective work only. Product roadmap items remain in
`PXA.Account.md` and deployment work remains deferred.

## P0 Security And Data Isolation

- [x] Invalidate affected authenticated sessions when organization roles are replaced.
- [x] Invalidate affected authenticated sessions when an organization membership is removed.
- [x] Verify active membership and organization state during cookie validation.
- [x] Reject API-key authentication when its organization is not active.
- [x] Reject entitlement evaluation when its organization is not active.
- [x] Revoke or block browser sessions, service accounts, and API keys after organization closure. The requesting session remains restricted to the cancellable closure workflow.
- [x] Add integration tests proving that a logged-in user immediately loses removed permissions.
- [x] Add integration tests proving that closed organizations cannot use cookies, API keys, or product entitlements.

## P0 Account Frontend Isolation

- [x] Reset every page-level data cache after logout by replacing the SPA document.
- [x] Reset every page-level data cache after session expiry by replacing the SPA document.
- [x] Reset organization-scoped data after organization or identity changes.
- [x] Add a regression test for user A logout followed by user B login in the same SPA runtime.

## P0 Account Recovery

- [x] Generate PXA Account password-reset links with the Account base URL.
- [x] Keep PXA Admin recovery links separate from customer Account recovery.
- [x] Add a mail-flow test asserting the correct reset host for each application surface.

## P1 Validation And API Contracts

- [x] Validate and normalize registration country as an ISO 3166-1 alpha-2 code.
- [x] Validate and normalize locale within the persisted 16-character limit.
- [x] Replace the free-form country field with a constrained UI control or two-character input.
- [x] Return the documented Account-specific Problem Details codes from real endpoints for slug, last-owner, and closure conflicts.
- [x] Add endpoint-level tests for Trial, slug, last-owner, and closure conflict codes.
- [x] Restrict `returnUrl` origins to the active runtime environment.
- [x] Add production tests that reject localhost return URLs.

## P1 Authorization And UX

- [x] Keep intentional tenant-scoped Admin API access for Organization Administrator and Manager roles while reserving system operations for explicitly allowlisted System Administrators.
- [x] Separate customer and Admin permissions if the applications must have strict authorization boundaries.
- [x] Expose effective Account capabilities to the frontend.
- [x] Hide or disable organization and developer-access mutations for users without the required permission.
- [x] Provide a consistent forbidden state for inaccessible Account routes.

## P1 Privacy And Mail

- [x] Store marketing consent as a first-class user preference with grant and withdrawal timestamps, source, and immutable consent history.
- [x] Keep transactional delivery independent from marketing consent and verify password-reset delivery without consent.
- [x] Leave newsletter delivery disabled and reject marketing templates from the transactional queue until double opt-in, unsubscribe, and suppression handling exist.

## P1 Checklist And Test Accuracy

- [x] Reopen Account-specific Problem Details completion until all endpoint contracts are verified.
- [x] Reopen responsive and accessibility testing claims that currently rely only on source-text checks.
- [x] Add automated desktop and mobile browser smoke tests for Account-to-Designer onboarding and access-denial states.
- [x] Add DOM-level keyboard, focus, validation, and signed-in return-flow tests.
- [x] Update `PXA.Account.md` only when each corrected behavior is verified.

## Branch Hygiene

- [x] Keep Account commits `76ae0f97` through `16d64574` together; the audited history contains the complete contiguous Phase 0-12 sequence.
- [ ] Extract Designer redesign commits `e319afeb` and `3ec60517` to a dedicated branch before merging this mixed branch.
- [ ] Extract localization and template commits `df1764ec` and `dd411030` to a dedicated branch before merging this mixed branch.
- [ ] Extract Markdown importer commit `03071fab` to a dedicated branch before merging this mixed branch.
- [x] Confirm that the Admin platform base commit `a719476b` is intentional: Account reuses its persistent identity, mail, subscription, and administration foundation.

## Validation

- [x] Run targeted PXA WebApi account, authentication, membership, closure, entitlement, and API-key tests.
- [x] Run the PXA Account unit tests, type check, and production build.
- [x] Run the PXA Company build after shared return-URL changes.
- [x] Run `git diff --check`.
- [x] Confirm that the worktree contains no generated build output.
- [x] Run the complete solution build. `dotnet build PXA.sln --no-restore -p:UseSharedCompilation=false -m:1` completed with 0 errors; existing dependency, analyzer, obsolete-API, nullable, and license warnings remain.

## Acceptance Criteria

- [x] Removed roles and memberships stop authorizing existing sessions immediately.
- [x] A closed organization cannot access products through browser cookies, API keys, or entitlements; only the requesting session can cancel its pending closure.
- [x] Account data from one identity is never rendered for another identity because logout and expiry replace the SPA document.
- [x] Account password-reset messages always lead to PXA Account.
- [x] Registration rejects invalid country and locale values without a database exception.
- [x] Public error responses use stable, tested Account-specific codes for every reserved lifecycle condition.
- [x] Production return URLs cannot redirect to localhost or another environment.
- [x] Checklist completion reflects automated evidence rather than manual assumptions.
