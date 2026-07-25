# PXA Account Registration Checklist

## Goal

Provide one secure customer-registration flow exclusively through PXA Account. Registration creates the correct organization ownership model, verifies the email address, activates one 30-day Premium Trial, and safely returns eligible users to the requested PXA product.

## Priorities

- [x] P0: Keep all customer registration UI on PXA Account.
- [x] P0: Complete secure Individual Developer and Company registration.
- [x] P0: Require email verification before authentication and Trial activation.
- [x] P0: Integrate safe Designer return and authorization-code handoff.
- [ ] P1: Add policy-consent history and invitation acceptance.
- [ ] P2: Add configurable bot protection, enterprise SSO onboarding, and paid checkout.

## Dependencies

- [x] Use the existing ASP.NET Core Identity, PostgreSQL, mail outbox, and action-token infrastructure.
- [x] Use `PXA.Designer-Authentication.md` for post-login Designer handoff.
- [ ] Use `PXA.Subscription-Licensing.md` for Trial and entitlement definitions.
- [ ] Use `PXA.Mail-Service.md` for verification and security mail delivery.
- [ ] Keep PXA Admin registration and administrator bootstrap outside this customer flow.

## Account-Only Entry Points

- [x] Keep the registration page at `PXA.Account/register`.
- [x] Do not add registration forms to PXA Company, Designer, Documentation, Demo, or Admin.
- [x] Make Company Trial and registration calls to action link to PXA Account.
- [x] Make Designer registration links point to PXA Account.
- [x] Preserve allowlisted campaign parameters across the Account redirect.
- [x] Preserve only an allowlisted absolute `returnUrl`.
- [x] Reject external, protocol-relative, non-HTTP(S), Admin, and malformed return destinations.
- [x] Keep registration API access same-origin through the Account `/api` reverse proxy.

## Registration Types

- [x] Support `Individual Developer` and `Company` as independent account types.
- [x] Require display name, email address, password, account type, Terms acceptance, and Privacy acknowledgement.
- [x] Require company name only for Company registration.
- [x] Normalize and validate email addresses before persistence.
- [x] Validate display and organization names with documented length and character limits.
- [ ] Apply the existing secure password and breached-password policy.
- [x] Allow locale and country to be captured through validated values.
- [x] Keep marketing consent optional and independent from contractual acceptance.

## Policy Consent

- [x] Store the accepted Terms version and UTC acceptance timestamp.
- [x] Store the acknowledged Privacy version and UTC acknowledgement timestamp.
- [ ] Store marketing consent, withdrawal, and source separately.
- [x] Do not preselect optional marketing consent.
- [ ] Require renewed acceptance only when a policy version explicitly requires it.
- [x] Avoid storing raw secrets or unnecessary personal data in consent audit records.

## Security And Privacy

- [x] Require an antiforgery token for registration and resend operations.
- [x] Keep fixed-window registration and identity-action rate limits.
- [x] Return generic accepted responses for duplicate or unrelated email addresses.
- [x] Prevent registration responses from exposing identity, organization, or invitation existence.
- [x] Use cryptographically secure, hashed, single-use email-verification tokens.
- [x] Expire verification tokens and reject replay.
- [x] Do not create an authenticated session before email verification.
- [ ] Audit registration, verification, Trial activation, invitation acceptance, and rejected abuse without recording passwords or tokens.
- [ ] Add configurable CAPTCHA or equivalent bot protection before public launch without coupling domain logic to one provider.

## Organization Creation

- [x] Create a personal organization for every Individual Developer registration.
- [x] Assign the registering Individual Developer as the single organization owner.
- [x] Apply the Individual Developer seat limit and workspace defaults.
- [x] Create a company organization for every Company registration.
- [x] Assign the registering Company user as Organization Administrator.
- [x] Create user, organization, membership, role assignment, verification state, and required outbox records atomically.
- [x] Roll back the complete registration transaction when any required record fails.
- [x] Prevent duplicate organization creation during concurrent submissions.

## Email Verification And Trial

- [x] Queue a localized transactional verification email through the mail outbox.
- [x] Keep resend responses generic and rate-limited.
- [x] Verify the token and email address in one transaction.
- [x] Mark the user email as confirmed only after successful token validation.
- [x] Activate exactly one 30-day Premium Trial after successful verification.
- [x] Create the Trial subscription and effective product entitlements atomically.
- [ ] Prevent Trial duplication through token replay, resend, invitation, or concurrent requests.
- [x] Do not hard-code Designer access by edition; expose it through the resulting entitlement set.
- [x] Direct the verified user to PXA Account login with only a safe preserved destination.

## Login And Product Return

- [x] Keep login credentials exclusively on PXA Account.
- [ ] Preserve a safe Designer destination through registration, verification, and login.
- [x] Create a Designer authorization-code handoff after login when the validated target is PXA Designer.
- [ ] Return directly to other allowlisted PXA surfaces only according to their authentication contract.
- [x] Fall back to the Account dashboard when the destination is missing or invalid.
- [ ] Show verification-required, expired-link, already-used, suspended, and service-unavailable states without leaking account existence.

## Invitation Acceptance

- [ ] Route invitation acceptance through PXA Account.
- [ ] Allow an existing user to authenticate before accepting an invitation.
- [ ] Allow a new invited user to set credentials and verify ownership through the invitation flow.
- [ ] Add the user only to the inviting organization.
- [ ] Do not create a second personal or company organization for invitation acceptance.
- [ ] Do not activate a second Trial through invitation acceptance.
- [ ] Enforce invitation expiry, single use, intended email, role bounds, and tenant ownership.
- [ ] Audit successful and rejected invitation acceptance.

## User Experience

- [ ] Provide accessible field labels, inline validation, error summaries, and keyboard focus management.
- [ ] Preserve entered non-secret values after recoverable validation failures.
- [ ] Never repopulate password fields.
- [x] Provide clear Individual Developer and Company explanations.
- [x] Show Terms, Privacy, and marketing choices independently.
- [ ] Add loading, submitted, verification-pending, resend, unavailable, and completion states.
- [x] Support desktop and mobile layouts.
- [ ] Localize registration, verification, and recovery content consistently with Account locale support.

## Tests

- [x] Unit-test registration validation for both account types.
- [x] Test policy-version and marketing-consent separation.
- [ ] Test safe campaign and return-URL preservation.
- [ ] Test that Company and Designer expose links but no registration forms.
- [x] Integration-test Individual Developer registration against PostgreSQL.
- [x] Integration-test Company registration against PostgreSQL.
- [x] Test atomic user, organization, membership, role, outbox, and Trial creation.
- [x] Test duplicate email and organization behavior without enumeration.
- [ ] Test concurrent registration submissions and transaction rollback.
- [ ] Test verification success, expiry, malformed token, replay, and resend.
- [x] Test exactly one Trial and correct entitlement assignment.
- [x] Test that login is rejected before verification.
- [ ] Test invitation acceptance for existing and new users.
- [ ] Test that invitations create neither another organization nor another Trial.
- [x] Test Designer return through the authorization-code handoff.
- [ ] Test accessibility, keyboard navigation, responsive layout, and localized messages.

## Acceptance Criteria

- [x] Customer registration is available only through the PXA Account user interface.
- [x] Both account types create the correct organization and owner role.
- [x] No user can sign in before email verification.
- [x] Verification creates exactly one 30-day Premium Trial and its entitlements.
- [x] Registration and recovery responses do not reveal unrelated account existence.
- [ ] Designer registration links return verified entitled users through the secure handoff.
- [ ] Invitation acceptance never creates an unintended organization or Trial.
- [x] Contractual acceptance and optional marketing consent remain separate and auditable.

## Deferred Work

- [ ] Select and integrate the production bot-protection provider.
- [ ] Add enterprise SSO and domain-claim onboarding.
- [ ] Add billing-provider checkout and paid conversion.
- [ ] Define policy-driven age or regional registration restrictions if required.
