# PXA Account Registration Checklist

## Goal

Provide one secure customer-registration flow exclusively through PXA Account. Registration creates the correct organization ownership model, verifies the email address, activates one 30-day Premium Trial, and safely returns eligible users to the requested PXA product.

## Priorities

- [ ] P0: Keep all customer registration UI on PXA Account.
- [ ] P0: Complete secure Individual Developer and Company registration.
- [ ] P0: Require email verification before authentication and Trial activation.
- [ ] P0: Integrate safe Designer return and authorization-code handoff.
- [ ] P1: Add policy-consent history and invitation acceptance.
- [ ] P2: Add configurable bot protection, enterprise SSO onboarding, and paid checkout.

## Dependencies

- [ ] Use the existing ASP.NET Core Identity, PostgreSQL, mail outbox, and action-token infrastructure.
- [ ] Use `PXA.Designer-Authentication.md` for post-login Designer handoff.
- [ ] Use `PXA.Subscription-Licensing.md` for Trial and entitlement definitions.
- [ ] Use `PXA.Mail-Service.md` for verification and security mail delivery.
- [ ] Keep PXA Admin registration and administrator bootstrap outside this customer flow.

## Account-Only Entry Points

- [ ] Keep the registration page at `PXA.Account/register`.
- [ ] Do not add registration forms to PXA Company, Designer, Documentation, Demo, or Admin.
- [ ] Make Company Trial and registration calls to action link to PXA Account.
- [ ] Make Designer registration links point to PXA Account.
- [ ] Preserve allowlisted campaign parameters across the Account redirect.
- [ ] Preserve only an allowlisted absolute `returnUrl`.
- [ ] Reject external, protocol-relative, non-HTTP(S), Admin, and malformed return destinations.
- [ ] Keep registration API access same-origin through the Account `/api` reverse proxy.

## Registration Types

- [ ] Support `Individual Developer` and `Company` as independent account types.
- [ ] Require display name, email address, password, account type, Terms acceptance, and Privacy acknowledgement.
- [ ] Require company name only for Company registration.
- [ ] Normalize and validate email addresses before persistence.
- [ ] Validate display and organization names with documented length and character limits.
- [ ] Apply the existing secure password and breached-password policy.
- [ ] Allow locale and country to be captured through validated values.
- [ ] Keep marketing consent optional and independent from contractual acceptance.

## Policy Consent

- [ ] Store the accepted Terms version and UTC acceptance timestamp.
- [ ] Store the acknowledged Privacy version and UTC acknowledgement timestamp.
- [ ] Store marketing consent, withdrawal, and source separately.
- [ ] Do not preselect optional marketing consent.
- [ ] Require renewed acceptance only when a policy version explicitly requires it.
- [ ] Avoid storing raw secrets or unnecessary personal data in consent audit records.

## Security And Privacy

- [ ] Require an antiforgery token for registration and resend operations.
- [ ] Keep fixed-window registration and identity-action rate limits.
- [ ] Return generic accepted responses for duplicate or unrelated email addresses.
- [ ] Prevent registration responses from exposing identity, organization, or invitation existence.
- [ ] Use cryptographically secure, hashed, single-use email-verification tokens.
- [ ] Expire verification tokens and reject replay.
- [ ] Do not create an authenticated session before email verification.
- [ ] Audit registration, verification, Trial activation, invitation acceptance, and rejected abuse without recording passwords or tokens.
- [ ] Add configurable CAPTCHA or equivalent bot protection before public launch without coupling domain logic to one provider.

## Organization Creation

- [ ] Create a personal organization for every Individual Developer registration.
- [ ] Assign the registering Individual Developer as the single organization owner.
- [ ] Apply the Individual Developer seat limit and workspace defaults.
- [ ] Create a company organization for every Company registration.
- [ ] Assign the registering Company user as Organization Administrator.
- [ ] Create user, organization, membership, role assignment, verification state, and required outbox records atomically.
- [ ] Roll back the complete registration transaction when any required record fails.
- [ ] Prevent duplicate organization creation during concurrent submissions.

## Email Verification And Trial

- [ ] Queue a localized transactional verification email through the mail outbox.
- [ ] Keep resend responses generic and rate-limited.
- [ ] Verify the token and email address in one transaction.
- [ ] Mark the user email as confirmed only after successful token validation.
- [ ] Activate exactly one 30-day Premium Trial after successful verification.
- [ ] Create the Trial subscription and effective product entitlements atomically.
- [ ] Prevent Trial duplication through token replay, resend, invitation, or concurrent requests.
- [ ] Do not hard-code Designer access by edition; expose it through the resulting entitlement set.
- [ ] Direct the verified user to PXA Account login with only a safe preserved destination.

## Login And Product Return

- [ ] Keep login credentials exclusively on PXA Account.
- [ ] Preserve a safe Designer destination through registration, verification, and login.
- [ ] Create a Designer authorization-code handoff after login when the validated target is PXA Designer.
- [ ] Return directly to other allowlisted PXA surfaces only according to their authentication contract.
- [ ] Fall back to the Account dashboard when the destination is missing or invalid.
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
- [ ] Provide clear Individual Developer and Company explanations.
- [ ] Show Terms, Privacy, and marketing choices independently.
- [ ] Add loading, submitted, verification-pending, resend, unavailable, and completion states.
- [ ] Support desktop and mobile layouts.
- [ ] Localize registration, verification, and recovery content consistently with Account locale support.

## Tests

- [ ] Unit-test registration validation for both account types.
- [ ] Test policy-version and marketing-consent separation.
- [ ] Test safe campaign and return-URL preservation.
- [ ] Test that Company and Designer expose links but no registration forms.
- [ ] Integration-test Individual Developer registration against PostgreSQL.
- [ ] Integration-test Company registration against PostgreSQL.
- [ ] Test atomic user, organization, membership, role, outbox, and Trial creation.
- [ ] Test duplicate email and organization behavior without enumeration.
- [ ] Test concurrent registration submissions and transaction rollback.
- [ ] Test verification success, expiry, malformed token, replay, and resend.
- [ ] Test exactly one Trial and correct entitlement assignment.
- [ ] Test that login is rejected before verification.
- [ ] Test invitation acceptance for existing and new users.
- [ ] Test that invitations create neither another organization nor another Trial.
- [ ] Test Designer return through the authorization-code handoff.
- [ ] Test accessibility, keyboard navigation, responsive layout, and localized messages.

## Acceptance Criteria

- [ ] Customer registration is available only through the PXA Account user interface.
- [ ] Both account types create the correct organization and owner role.
- [ ] No user can sign in before email verification.
- [ ] Verification creates exactly one 30-day Premium Trial and its entitlements.
- [ ] Registration and recovery responses do not reveal unrelated account existence.
- [ ] Designer registration links return verified entitled users through the secure handoff.
- [ ] Invitation acceptance never creates an unintended organization or Trial.
- [ ] Contractual acceptance and optional marketing consent remain separate and auditable.

## Deferred Work

- [ ] Select and integrate the production bot-protection provider.
- [ ] Add enterprise SSO and domain-claim onboarding.
- [ ] Add billing-provider checkout and paid conversion.
- [ ] Define policy-driven age or regional registration restrictions if required.
