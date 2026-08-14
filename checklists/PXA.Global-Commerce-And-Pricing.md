# PXA Global Commerce and Pricing

## Goal

Prepare PXA for worldwide commercial distribution without treating one German
checkout, one currency, or one privacy model as globally sufficient. This
checklist contains a researched pricing proposal, not approved public pricing.
Human approval of the proposed price book, Merchant of Record, B2B-first scope,
and first market is tracked in `PXA.Commercial-Launch-Decision-Packet.md`.

## Research Baseline

- [x] Record the public DevExpress Reporting price of USD 799.99 per developer
  for the first subscription year and USD 399.99 for an on-time renewal.
- [x] Record the public DevExpress Universal price of USD 2,299.99 per developer.
- [x] Record Aspose.Total pricing of USD 5,999 for one developer and one
  deployment location, and USD 17,997 for one developer with unlimited
  deployment locations.
- [x] Record Adobe Acrobat Services' 500 free document transactions per month
  and quote-based paid volume plans.
- [x] Record Nutrient's annual, component-, deployment-, and usage-dependent
  quote model.
- [x] Record the current Paddle and Lemon Squeezy Merchant-of-Record baseline of
  5% plus USD 0.50 per transaction, subject to provider review and extra fees.
- [x] Treat all external prices as a dated 2026-08-09 benchmark that must be
  rechecked before publication.

Sources:

- DevExpress Reporting: https://www.devexpress.com/subscriptions/reporting/
- Aspose.Total: https://purchase.aspose.com/pricing/total/family/
- Adobe Acrobat Services: https://developer.adobe.com/document-services/pricing/main/
- Adobe transaction definitions: https://developer.adobe.com/document-services/docs/overview/limits
- Nutrient SDK Pricing: https://www.nutrient.io/sdk/pricing/
- Paddle Merchant of Record: https://www.paddle.com/paddle-101
- Lemon Squeezy Pricing: https://www.lemonsqueezy.com/pricing

## Recommended Launch Price Book

All prices below exclude jurisdiction-specific taxes for business display. A
consumer-facing checkout must display the legally required tax-inclusive final
price. Annual pricing represents approximately two months free.

### Free

- [ ] Approve USD 0, EUR 0, and GBP 0.
- [ ] Include one Individual Developer seat.
- [ ] Include 500 standard document operations per month.
- [ ] Include Designer, Generator, PDF Viewer, basic Import/Export, and community support.
- [ ] Exclude production SLA, offline licenses, SSO, and retained high-volume jobs.
- [ ] Prevent paid overages; require an explicit upgrade.

### Trial

- [ ] Approve a 30-day Premium Trial without requiring a payment method.
- [ ] Include one workspace, up to five evaluation users, and 5,000 operations.
- [ ] Include all Premium products while excluding production SLA and offline licensing.
- [ ] Preserve customer export and deletion options when the Trial expires.

### Premium Individual Developer

- [ ] Approve USD 49 monthly or USD 490 annually.
- [ ] Set initial EUR list price to EUR 49 monthly or EUR 490 annually.
- [ ] Set initial GBP list price to GBP 42 monthly or GBP 420 annually.
- [ ] Include one named seat, 5,000 standard operations per month, all public
  SDKs, all Designer products, and standard email support.
- [ ] Price additional operation packs at USD/EUR 25 per 5,000 operations or the
  locally approved equivalent; do not silently create overage invoices.

### Premium Company

- [ ] Approve USD 199 monthly or USD 1,990 annually.
- [ ] Set initial EUR list price to EUR 199 monthly or EUR 1,990 annually.
- [ ] Set initial GBP list price to GBP 169 monthly or GBP 1,690 annually.
- [ ] Include five named seats, shared organization resources, Admin controls,
  API and SDK access, and 25,000 standard operations per month.
- [ ] Propose additional seats at USD/EUR 29 or GBP 25 per month.
- [ ] Propose additional 25,000-operation packs at USD/EUR 99 or GBP 85.
- [ ] Add an optional Growth tier only after usage data proves a gap between
  Premium Company and Enterprise.

### Enterprise

- [ ] Use annual quotes rather than public self-service checkout.
- [ ] Use USD 18,000 per year as the internal Cloud qualification floor.
- [ ] Use USD 30,000 per year as the internal On-Premise qualification floor.
- [ ] Price hybrid deployment, offline licensing, SSO, dedicated regions,
  premium SLA, migration services, and OEM/redistribution rights separately.
- [ ] Never publish the internal qualification floors as guaranteed prices.
- [ ] Require an executed order form, DPA where applicable, security review,
  support scope, usage allowance, and deployment rights.

## Usage Metric

- [ ] Define one standard operation as one product operation producing or
  analyzing up to 50 document pages.
- [ ] Count each chained operation separately so Create plus OCR plus Export is
  three operations.
- [ ] Define OCR, advanced analysis, large report migration, and AI-assisted
  operations through transparent multipliers before billing them.
- [ ] Display estimated consumption before an interactive operation where the
  multiplier is greater than one.
- [ ] Make usage idempotent for retries and never bill failed platform operations.
- [ ] Provide threshold notifications at 70%, 90%, and 100%.
- [ ] Publish a stable metric table and preserve historical pricing definitions
  for existing contracts.

## Currency and Regional Pricing

- [ ] Launch public price books in USD, EUR, and GBP.
- [ ] Select price book from verified billing country rather than IP address alone.
- [ ] Keep list prices stable for a quarter; do not apply live exchange rates at checkout.
- [ ] Review FX movement quarterly and announce material price changes in advance.
- [ ] Add CAD, AUD, JPY, and other local currencies only after demand and payment
  method coverage justify their operational cost.
- [ ] Avoid purchasing-power-parity discounts at launch; introduce controlled
  country promotions only with anti-arbitrage and tax review.
- [ ] Preserve currency, tax treatment, price-book version, and displayed total
  in the immutable order record.

## Worldwide Market Model

- [x] Add a sourced, machine-validated Country Readiness matrix for all 35 initial candidates.
- [x] Keep EU-27, non-EU EEA, UK, US, and later AU/CA/NZ review paths separate.
- [x] Research Australia, Canada, and New Zealand and replace the combined placeholder with separate tax, privacy, consumer, localization, currency, and data-region gates.
- [x] Record the current Australian A$75,000 and New Zealand NZ$60,000 remote-service GST thresholds as dated review inputs rather than permanent configuration.
- [x] Require a Canadian province and territory matrix for GST/HST/PST, Privacy, consumer subscriptions, and French-language obligations.
- [x] Define independent B2B, B2C, tax, privacy, localization, and processing-region statuses without approving a market.
- [x] Add a German country override for operator details, Legal copy, electronic withdrawal, DDG, VSBG, and checkout review.
- [ ] Define `worldwide` as all supported countries after sanctions, export,
  payment-provider, tax, consumer-law, and product-use screening, not every
  jurisdiction by default.
- [ ] Maintain machine-readable supported, restricted, and sales-assisted country lists.
- [ ] Start self-service with reviewed launch regions: EU/EEA, United Kingdom,
  United States, Canada, Australia, and New Zealand.
- [ ] Add other regions through a documented country-readiness review.
- [ ] Keep high-risk, sanctioned, embargoed, and unsupported jurisdictions blocked.
- [ ] Obtain export-control review for encryption, OCR, document analysis, and
  restricted end uses before worldwide activation.
- [ ] Keep B2B and B2C availability separately configurable by billing country.
- [ ] Prefer a B2B-first launch; enable B2C only per reviewed jurisdiction.
- [x] Embed the versioned Commerce and Country Readiness catalogs in the API and expose one fail-closed paid-checkout decision for Account workflows.

## Global Legal and Privacy Modules

- [ ] Keep one verified Swiss operator identity and English authoritative core
  documents while adding country-specific notices or schedules where required.
- [ ] Build a jurisdiction matrix for contract language, consumer rights,
  cancellation, renewal, invoice fields, dispute notices, accessibility, and age limits.
- [ ] Cover GDPR/EEA, UK GDPR, US state privacy laws, Canadian privacy rules, and
  Australian privacy requirements before the corresponding public launch.
- [ ] Add EU Standard Contractual Clauses, the UK transfer addendum, and other
  transfer mechanisms where provider or customer transfers require them.
- [ ] Offer EU and US processing regions first; make region selection and
  subprocessors visible in Enterprise orders and the DPA.
- [ ] Define data-subject request ownership and statutory response workflows per region.
- [ ] Use locale-specific Legal versions without implying that translation alone
  makes a country commercially ready.

## Global Tax and Billing

- [ ] Prefer a Merchant of Record for Free-to-Premium self-service launch so VAT,
  GST, sales tax, invoicing, payment localization, fraud, and remittance have one
  accountable commercial owner.
- [ ] Evaluate Paddle, Lemon Squeezy, and Stripe Managed Payments against country
  coverage, B2B VAT IDs, refunds, chargebacks, invoicing, data processing,
  migration, API quality, and current fees.
- [ ] Keep the billing integration behind the existing provider abstraction.
- [ ] Validate business tax IDs and support reverse-charge evidence where applicable.
- [ ] Display tax-inclusive consumer totals and the required business tax treatment.
- [ ] Keep direct invoicing for negotiated Enterprise agreements with tax-adviser approval.
- [ ] Reconcile provider orders, subscription state, refunds, disputes, taxes,
  payouts, and PXA entitlements through idempotent webhooks.
- [ ] Never store raw payment-card data in PXA.

Sources:

- OECD International VAT/GST Guidelines: https://www.oecd.org/en/publications/international-vat-gst-guidelines_9789264271401-en.html
- Paddle Merchant of Record: https://www.paddle.com/paddle-101
- Lemon Squeezy Sales Tax and VAT: https://docs.lemonsqueezy.com/help/payments/sales-tax-vat
- Stripe Pricing and Managed Payments: https://stripe.com/pricing

## Price Governance

- [x] Store the researched proposal in the machine-readable, non-public `product-metadata/global-commerce-catalog.json`.
- [x] Validate plan IDs, currencies, country candidates, approval owners, annual discounts, usage safety, and fail-closed commerce state in CI.
- [ ] Assign Product, Finance, Tax, and Legal owners to approve the price book.
- [ ] Store approved plans, currencies, quotas, effective dates, and grandfathering
  rules as versioned commercial configuration rather than UI constants.
- [ ] Require four-eyes approval for publishing or scheduling a price-book version.
- [ ] Keep existing customer prices through the promised term and record every
  migration to a new price-book version.
- [ ] Test gross/net calculations, rounding, zero-decimal currencies, refunds,
  upgrades, downgrades, proration, Trial conversion, failed renewal, and cancellation.
- [ ] Run willingness-to-pay interviews and a controlled pricing experiment before
  treating the proposed prices as final.
- [ ] Recheck competitor prices and Merchant-of-Record fees immediately before approval.

## Acceptance Criteria

- [ ] Every country sold to has an approved commerce-readiness record.
- [ ] Public prices identify currency, tax treatment, billing period, included
  seats, included usage, renewal, cancellation, and support.
- [ ] Subscription edition, account type, seat, usage, deployment, and OEM rights
  remain separate commercial dimensions.
- [ ] Consumer checkout cannot activate globally from one country approval.
- [ ] PXA does not claim worldwide availability in blocked or unreviewed countries.
- [ ] No proposed price becomes public or billable before Product, Finance, Tax,
  and Legal approval.
