# PXA Commercial Launch Decision Packet

## Goal

Provide one approval packet for the first commercial PXA launch. Recommendations
are based on the current product, Legal, pricing, tax, privacy, and country
research. Checked preparation items are complete; unchecked decisions require an
accountable human approval and, where stated, qualified professional advice.

## Recommended Decision Order

- [x] Priority 1: collect and verify the operator identity before binding Legal publication.
- [x] Priority 2: approve or revise the proposed price book before billing integration.
- [x] Priority 3: evaluate Paddle as the preferred Merchant of Record and retain Stripe Managed Payments as the fallback.
- [x] Priority 4: launch paid sales B2B-first; keep paid B2C disabled.
- [x] Priority 5: use Germany and EU business customers as the first approval path.
- [x] Priority 6: obtain Legal and Tax approval for all twelve proposed retention categories.

## Decision 1: Operator Identity

Recommendation: maintain one verified operator profile used by Imprint, Terms,
Privacy, DPA, order forms, invoices, and durable confirmations.

> Deferred on 2026-08-14: the verified company details are not available yet.
> Keep the existing bracketed placeholders in candidate documents, keep
> Production Legal publication and paid checkout fail-closed, and resume this
> decision when official Swiss company records and public contact channels are
> available. Do not infer, generate, or publish substitute operator details.

- [ ] Legal company name:
- [ ] Legal form:
- [ ] Registered address:
- [ ] Authorized representative or representatives:
- [ ] Register court and register number:
- [ ] VAT ID and business identification number, where issued:
- [ ] Public Legal email address:
- [ ] Direct contact channel required by applicable law:
- [ ] Supervisory authority, where applicable:
- [ ] Company owner confirms the values against current official records.
- [ ] Independent Legal approver confirms the profile for publication.

Decision owner: Company Management and Legal.

Resume trigger: Company Management supplies the fields above from official
records. The next implementation step is then a canonical, versioned operator
profile with four-eyes approval and rendering into every applicable Legal and
commercial document.

## Decision 2: Pilot Price Book

Recommendation: approve the current values as a limited commercial pilot, then
review willingness-to-pay and actual usage before a broad public launch.

| Offer | Monthly | Annual | Included |
| --- | ---: | ---: | --- |
| Free Individual | 0 | 0 | 1 seat, 500 operations/month |
| 30-day Trial | 0 | 0 | up to 5 evaluation seats, 5,000 operations |
| Premium Individual | USD/EUR 49 or GBP 42 | USD/EUR 490 or GBP 420 | 1 seat, 5,000 operations/month |
| Premium Company | USD/EUR 199 or GBP 169 | USD/EUR 1,990 or GBP 1,690 | 5 seats, 25,000 operations/month |
| Enterprise Cloud | Quote | Internal qualification floor USD 18,000/year | Negotiated |
| Enterprise On-Premise | Quote | Internal qualification floor USD 30,000/year | Negotiated |

- [ ] Product approves plan boundaries and included capabilities.
- [ ] Finance approves prices, annual discount, margin, and operation-pack economics.
- [ ] Tax approves gross/net display and supported currency treatment.
- [ ] Legal approves renewal, cancellation, Trial, price-change, and grandfathering language.
- [ ] Sales validates the Enterprise qualification floors without publishing them as guaranteed prices.
- [ ] Product schedules a price review after the first 20 paid customers or 90 paid-launch days, whichever occurs first.

Decision owners: Product, Finance, Tax, Legal, and Sales.

## Decision 3: Merchant of Record

Recommendation: evaluate Paddle first because PXA needs global SaaS subscriptions,
B2B tax evidence, localized payments, invoicing, refunds, chargebacks, and tax
remittance under one accountable seller model. Keep Stripe Managed Payments as
the fallback. Do not sign a provider solely from list pricing.

- [ ] Confirm that Paddle accepts the PXA business, products, API/SDK model, On-Premise sales, and target countries.
- [ ] Obtain a written fee proposal including international cards, PayPal, refunds, chargebacks, FX, payouts, and volume tiers.
- [ ] Verify B2B VAT/GST ID handling, reverse-charge evidence, invoice corrections, credit notes, and tax exports.
- [ ] Verify subscription upgrades, downgrades, proration, grace periods, dunning, cancellation, and webhook replay.
- [ ] Complete DPA, subprocessor, region, transfer, security, availability, exit, and data-export review.
- [ ] Run the same scored review for Stripe Managed Payments as fallback.
- [ ] Keep Lemon Squeezy as a small-volume comparison only unless its long-term product and migration path is contractually clear.
- [ ] Record the approved provider and contract effective date in the versioned Commerce catalog.

Decision owners: Finance, Tax, Legal, Security, and Engineering.

Sources:

- Paddle Merchant of Record: https://www.paddle.com/paddle-101
- Stripe pricing and Managed Payments: https://stripe.com/pricing
- Lemon Squeezy pricing: https://www.lemonsqueezy.com/pricing

## Decision 4: B2B-First Launch

Recommendation: allow only verified business purchases in the first paid launch.
Free registration and Trial evaluation may remain available to individuals, but
must not silently convert into a paid consumer subscription.

- [ ] Approve B2B-only paid checkout for the first release.
- [ ] Require legal business name, billing address, country, business status, and applicable tax ID.
- [ ] Validate EU VAT IDs through an approved VIES workflow and preserve minimized evidence.
- [ ] Route uncertain or invalid business-status cases to Sales instead of treating them as B2B automatically.
- [ ] Keep automatic Trial-to-paid conversion disabled.
- [ ] Keep every B2C market status at `review-required` or `blocked`.
- [ ] Prevent consumer payment methods from bypassing the Account and country gates.

Decision owners: Product, Finance, Tax, and Legal.

## Decision 5: Operator Country And First Markets

Recommendation: complete the Swiss operator baseline first, then enable verified
business customers country by country. Switzerland uses the approved Swiss tax and
invoice model; EU business sales additionally require the applicable EUR, VAT,
reverse-charge, privacy, and transfer controls.

- [ ] Complete the Swiss operator identity and authoritative English Legal documents.
- [ ] Approve PXA's Swiss VAT, UID, accounting, invoice, and export-of-services model.
- [ ] Approve a CHF price book or document why another checkout currency is used for Swiss customers.
- [ ] Approve EU B2B place-of-supply, VIES, reverse-charge, evidence, and invoice rules.
- [ ] Approve EU GDPR controller/processor roles, DPA, TOM, subprocessors, SCCs, and rights workflow.
- [ ] Approve an EU Cloud processing region and backup region.
- [ ] Approve Switzerland for B2B while leaving Swiss B2C disabled until its commercial and Legal workflow is complete.
- [ ] Approve Germany for B2B only through the Country Readiness matrix and keep German B2C disabled until its local-language and consumer workflow is complete.
- [ ] Approve additional EU B2B countries only through the Country Readiness matrix.
- [ ] Keep UK, US, Australia, Canada, New Zealand, and non-EU EEA paid sales gated until their reviews complete.

Decision owners: Company Management, Tax, Legal, Privacy, and Security.

Sources:

- EU VAT OSS: https://europa.eu/youreurope/business/finance-and-tax/vat/one-stop-shop/index_en.htm
- EU VAT and VIES: https://europa.eu/youreurope/business/finance-and-tax/vat/index_en.htm
- GDPR: https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:32016R0679
- Swiss SECO online-trade requirements: https://www.seco.admin.ch/de/onlinehandel
- Swiss FDPIC privacy-statement guidance: https://www.edoeb.admin.ch/en/privacy-statements-on-the-internet

## Decision 6: Retention Proposals

These are review baselines, not approved periods. Legal, Tax, Security, and
product owners must map each persisted entity, backup copy, processor, and Legal
Hold before changing `approvalStatus` in the processing inventory.

### 1. Identity and Authentication

- [ ] Keep active account data for the account lifetime.
- [ ] Keep browser sessions for their configured maximum of eight hours and delete expired action tokens through bounded cleanup.
- [ ] After completed closure, delete or pseudonymize profile and credential metadata within 30 days unless another approved obligation applies.
- [ ] Propose 12 months for minimized security-event evidence; exclude secrets and document content.

### 2. Organizations and Access

- [ ] Keep organization and current membership data for the contract lifetime.
- [ ] Preserve resources through the 30-day closure window, then purge or pseudonymize memberships within 30 days after completion.
- [ ] Retain minimized role and administrator-change evidence with the approved audit period.

### 3. Subscriptions, Usage, and Licensing

- [ ] Keep active subscription and entitlement state for the contract lifetime.
- [ ] Propose 24 months for detailed usage events, followed by anonymous aggregates where needed for capacity planning.
- [ ] Map invoices and booking records to applicable eight-year tax retention and contractual correspondence to the applicable six-year category.
- [ ] Retain offline-license lifecycle evidence for the license term plus the approved claims period.

### 4. Designer Content

- [ ] Keep active templates and immutable versions until customer deletion or account closure.
- [ ] Propose a 30-day recoverable trash period followed by active-store purge within seven days.
- [ ] Apply backup expiry and Legal Holds separately; do not promise immediate deletion from an immutable backup.

### 5. Background Document Jobs

- [ ] Add a no-retention mode with input and result deletion immediately after retrieval or no later than 24 hours after completion.
- [ ] Keep the existing seven-day retained-job mode only as an explicit customer choice.
- [ ] Propose 30 days for minimized terminal job metadata, with document content and object keys removed at object expiry.

### 6. Transactional Mail

- [ ] Approve or revise current defaults: delivered and suppressed metadata 30 days, cancelled metadata 14 days, dead-letter metadata 90 days.
- [ ] Remove message body and sensitive template values as soon as delivery operations no longer require them.
- [ ] Preserve only separate security or Legal evidence when another approved category requires it.

### 7. Legal Governance and Evidence

- [ ] Keep published Legal document versions and publication approvals permanently while PXA must prove publication history.
- [ ] Propose acceptance evidence for the contract term plus the applicable claims limitation period.
- [ ] Pseudonymize user identity after account deletion where proof can remain effective without direct identity.
- [ ] Keep full IP addresses excluded unless a documented necessity and period are approved.

### 8. Administrative Audit

- [ ] Propose 12 months for standard minimized audit events and an optional contracted 24-month Enterprise period.
- [ ] Pseudonymize deleted actors while preserving action, target category, outcome, organization, and timestamp where required.
- [ ] Apply incident and Legal Holds before scheduled cleanup.

### 9. Product Observability

- [ ] Approve or revise current defaults: metrics 90 days, logs 30 days, traces 14 days, and Alertmanager state 5 days.
- [ ] Keep customer document bodies, template content, credentials, and direct identifiers out of telemetry.
- [ ] Require a separately approved, expiring exception for temporary debug retention.

### 10. Browser Storage

- [ ] Approve the per-key lifetimes in `product-metadata/browser-storage.json`.
- [ ] Delete tenant working state at sign-out and organization switch where technically defined.
- [ ] Keep optional analytics and marketing storage disabled until Consent Center approval.

### 11. Account Closure

- [ ] Approve the existing 30-day cancellation and export window.
- [ ] Execute downstream deletion or pseudonymization within 30 days after completed closure, subject to holds and category exceptions.
- [ ] Propose 12 months for minimized closure-request workflow evidence without retaining the deleted account profile.

### 12. Backup and Recovery

- [ ] Propose a 35-day encrypted rolling backup window with automatic expiry and access audit.
- [ ] Keep backups in the approved customer region unless a reviewed transfer safeguard applies.
- [ ] Ensure restored data immediately re-enters current deletion, closure, and Legal Hold processing.
- [ ] Document exceptional incident or Legal Hold extensions with owner, reason, scope, and release date.

Decision owners: Legal, Tax, Privacy, Security, Product, and Operations.

## Approval Meeting Output

- [ ] Record decision, approver, timestamp, evidence, and conditions for each section.
- [ ] Update the Commerce catalog only after Decisions 2-5 are approved.
- [ ] Update the processing inventory only after each retention category receives explicit Legal approval.
- [ ] Generate new immutable Legal versions rather than editing published content.
- [ ] Run Commerce, Country Readiness, Legal launch, Company, Account, and API tests.
- [ ] Keep Production and paid checkout fail-closed until all applicable gates pass.
