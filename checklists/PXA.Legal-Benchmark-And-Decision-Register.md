# PXA Legal Benchmark and Decision Register

## Goal

Turn the six remaining Legal launch blockers into explicit PXA decisions. The
research below records useful market patterns, but it does not copy competitor
terms and does not replace review by qualified Swiss legal counsel with
international software, consumer, and privacy expertise.

## Research Rules

- [x] Prefer current Swiss primary legal sources for the operator baseline and
  primary sources from each proposed sales market for mandatory local requirements.
- [x] Use supplier-owned Legal pages only to identify structure and operational patterns.
- [x] Keep every company, commercial, retention, and counsel approval decision explicit.
- [x] Treat competitor wording as non-reusable reference material.
- [x] Record research date: 2026-08-09.

## 1. Verified Operator Identity

Research findings:

- [x] Review the Swiss online-provider identity and contracting disclosures under
  Article 3 paragraph 1 letter s of the Unfair Competition Act.
- [x] Compare structured operator disclosures from established software suppliers.
- [x] Confirm that the same verified identity should feed Imprint, Terms, Privacy,
  DPA, invoices, and contractual confirmations.

PXA decision proposal:

- [ ] Obtain the exact legal name, legal form, registered address, authorized
  representatives, competent Commercial Register, register number, Swiss UID or VAT ID if
  issued, email address, telephone or another direct contact channel, and any
  applicable supervisory authority from verified company records.
- [ ] Assign one accountable company owner to approve the operator profile.
- [ ] Store one canonical operator profile and render it into every applicable
  Legal document rather than maintaining independent copies.
- [ ] Require a four-eyes review before the profile can be used by a Production
  Legal publication or checkout confirmation.

Sources:

- Swiss SECO online-trade requirements: https://www.seco.admin.ch/de/onlinehandel
- PDF24 Imprint: https://www.pdf24.org/de/impressum
- Nutrient Imprint: https://www.nutrient.io/legal/impressum

## 2. Legal Document Set and Responsibility Model

Research findings:

- [x] Review GemBox's separation between website terms and product EULA.
- [x] Review Syncfusion's separate Privacy, Cookie, EULA, SLA, GDPR, Security,
  and Subprocessor publications.
- [x] Review Syncfusion's controller-versus-processor distinction.
- [x] Review Atlassian's DPA, TOM, subprocessor notice, and Legal archive model.

PXA decision proposal:

- [x] Prepare separate English-authoritative candidates for Website and Cloud
  Subscription Terms, B2B Terms, B2C Terms, On-Premise/CLI/SDK EULA, Privacy,
  Cookie and Storage Policy, Imprint, Withdrawal Information, DPA, TOM,
  Subprocessors, and any promised SLA.
- [x] Make English the sole authoritative language and treat future localized
  versions as market-specific notices or translations unless expressly agreed otherwise.
- [ ] Define PXA as controller for account, billing, security, Legal evidence,
  and service administration data, and as processor for customer document and
  template content where the customer determines purpose and means.
- [ ] Complete the Article 28 DPA schedules for processing scope, documented
  instructions, confidentiality, security measures, deletion or return,
  assistance, audit, and subprocessors.
- [ ] Publish a table of subprocessors with service, purpose, products, location,
  transfer safeguard, effective date, and change-notification mechanism.
- [ ] Have qualified Swiss counsel with international-market expertise approve every binding text; do not
  adapt competitor clauses by substitution.

Sources:

- GemBox Terms: https://www.gemboxsoftware.com/company/terms-of-service
- GemBox EULA: https://www.gemboxsoftware.com/pricing/eula
- Syncfusion Legal Center: https://www.syncfusion.com/legal/
- Syncfusion Privacy: https://www.syncfusion.com/legal/privacy-policy/
- Syncfusion Subprocessors: https://www.syncfusion.com/legal/sub-processors/
- Atlassian DPA: https://www.atlassian.com/legal/data-processing-addendum
- Atlassian Security Measures: https://www.atlassian.com/legal/security-measures
- GDPR Article 28: https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:32016R0679

## 3. Retention, Deletion, and Legal Holds

Research findings:

- [x] Compare PDF24's one-hour uploaded-file deletion with Nutrient's
  product-specific and no-retention processing options.
- [x] Review purpose-based account and Legal retention disclosures from
  Syncfusion, DevExpress, and GemBox.
- [x] Review German tax-record retention baselines separately from product data.

PXA decision proposal:

- [ ] Counsel must approve every category in
  `product-metadata/data-processing-inventory.json`; research does not approve a
  retention period.
- [x] Offer a transient Cloud processing mode that removes uploaded and generated
  document content after successful asynchronous result download and no later
  than 24 hours after terminal completion.
- [x] Keep the current seven-day retained-job mode only as an explicit customer
  feature with visible expiry.
- [x] Define and enforce a separate 30-day minimization period for terminal job metadata.
- [ ] Define template archive, soft-delete, account-closure grace, final purge,
  backup propagation, and object-storage deletion periods.
- [ ] Retain invoice and booking evidence according to the applicable eight-year
  German rules, business correspondence where applicable for six years, and
  books or records subject to ten-year rules; counsel and tax advice must map
  each PXA entity to the applicable category.
- [ ] Define pseudonymization and deletion behavior for Legal acceptance and
  audit evidence without adding full IP addresses.
- [ ] Define bounded backup rotation, deletion propagation, documented Legal
  Holds, and release approval for every retained category.

Sources:

- PDF24 deletion practice: https://tools.pdf24.org/en/faq
- Nutrient Privacy: https://www.nutrient.io/legal/privacy/
- Syncfusion Privacy: https://www.syncfusion.com/legal/privacy-policy/
- DevExpress Privacy: https://www.devexpress.com/aboutus/privacy-policy.xml
- German AO Section 147: https://www.gesetze-im-internet.de/ao_1977/__147.html
- German UStG Section 14b: https://www.gesetze-im-internet.de/ustg_1980/__14b.html

## 4. Paid Consumer Checkout and Withdrawal

Research findings:

- [x] Compare Syncfusion and DevExpress presentation of subscription duration,
  seats, renewal, usage, and post-expiry rights.
- [x] Confirm that supplier models differ and PXA must explicitly decide whether
  subscription expiry ends access or only updates and support.
- [x] Review German pre-contract, payment-button, durable-confirmation, withdrawal,
  and current electronic-withdrawal-function requirements.
- [x] Confirm that the electronic withdrawal function has applied since
  2026-06-19 to qualifying online distance contracts.

PXA decision proposal:

- [ ] Decide prices, currency, VAT handling, account and seat model, billing
  cadence, Trial conversion, auto-renewal, cancellation effect, grace period,
  refund policy, usage limits, and payment provider before enabling B2C checkout.
- [ ] Show the gross total, taxes, term, renewal, cancellation consequences,
  selected seats, usage limits, and payment method immediately before ordering.
- [ ] Use an unambiguous payment-obligation button and preserve the exact order
  summary and accepted document versions in the durable confirmation.
- [ ] Capture early performance of a digital service through a separate explicit
  request and the legally required acknowledgement; do not bundle it with Terms.
- [ ] Add a permanently reachable `Withdraw contract` entry while the withdrawal
  period runs, collect contract identification and confirmation contact, require
  a second `Confirm withdrawal` action, and send an immediate durable receipt
  containing the submission content, date, and time.
- [ ] Update the withdrawal information and model form with the electronic
  function location.
- [ ] Keep paid B2C checkout disabled until commercial decisions, implementation,
  automated tests, and counsel approval are complete.

Sources:

- Syncfusion Subscription FAQ: https://www.syncfusion.com/legal/subscription-faq/
- DevExpress Delivery Model: https://www.devexpress.com/Support/delivery-model.xml
- German BGB Section 312j: https://www.gesetze-im-internet.de/bgb/__312j.html
- German BGB Section 312f: https://www.gesetze-im-internet.de/bgb/__312f.html
- German BGB Section 356a: https://www.gesetze-im-internet.de/bgb/__356a.html
- Official withdrawal model: https://www.gesetze-im-internet.de/bgbeg/art_253anlage_1.html
- German VSBG Section 36: https://www.gesetze-im-internet.de/vsbg/__36.html

## 5. Browser Storage and Optional Tracking

Research findings:

- [x] Confirm that a consent banner is unnecessary when no consent-requiring
  technology is used.
- [x] Confirm that optional tracking requires prior informed consent and that
  rejection and withdrawal must be as accessible as acceptance.
- [x] Compare Syncfusion's dedicated Cookie Policy without importing its tracker set.

PXA decision proposal:

- [x] Launch with necessary first-party storage only and the existing informative
  Storage Notice instead of a misleading `Accept all` banner.
- [x] Keep `optionalStorageEnabled` false in the machine-readable inventory.
- [ ] Before adding analytics, advertising, cross-site pixels, session replay, or
  another optional technology, implement a Consent Center with equal `Accept
  all`, `Reject all`, and `Customize` choices and no preselected optional category.
- [ ] Block optional scripts and network requests until consent, record the
  consent-policy version, and expose equally easy withdrawal from every public site.
- [ ] Update the processing, provider, browser-storage, Privacy, and Cookie
  inventories in the same reviewed change that introduces an optional technology.

Sources:

- BfDI Cookie Banner guidance: https://www.bfdi.bund.de/DE/Buerger/Inhalte/Telemedien/Cookie-Banner.html
- DSK Orientation Guide: https://www.bfdi.bund.de/SharedDocs/Downloads/DE/DSK/Orientierungshilfen/OH_Digitale-Dienste.pdf?__blob=publicationFile&v=1
- German TDDDG Section 25: https://www.gesetze-im-internet.de/ttdsg/__25.html
- Syncfusion Cookie Policy: https://www.syncfusion.com/legal/cookie-policy/

## 6. Final Legal Content Scan and Change Transparency

Research findings:

- [x] Review Atlassian's effective-date archive, prior versions, subprocessor
  notifications, and separate Legal publications.
- [x] Review GitHub's public policy source history and material-change notice model.
- [x] Confirm that the PXA immutable publication and content-hash architecture can
  support equivalent transparency without exposing internal review material.

PXA decision proposal:

- [x] Add a repeatable Legal launch-readiness validator for development and a
  fail-closed Production mode.
- [x] Publish effective dates, content hashes, change summaries, and prior public
  versions on the customer-facing Legal pages.
- [x] Add a Legal-update notification path for newly approved documents through
  persistent Designer notifications and the Account Legal updates page.
- [ ] Run the final scan after verified operator data and counsel-approved German
  copy are installed.
- [ ] Fail Production release on unresolved operator identity, unapproved
  processing or retention, draft markers, bracketed operator placeholders,
  obsolete EU ODR URLs, enabled optional storage without consent controls, or an
  enabled B2C checkout without the complete withdrawal workflow.
- [ ] Require Legal and Product sign-off on the generated launch-readiness report.

Sources:

- Atlassian Legal Archive: https://www.atlassian.com/legal/archives
- Atlassian Subprocessors: https://www.atlassian.com/legal/sub-processors
- GitHub Site Policy repository: https://github.com/github/site-policy

## Current Decision Status

- [x] Research for points 1-6 is complete and mapped to PXA.
- [x] Technical launch blockers remain machine-detectable.
- [ ] Company owner has supplied and approved the operator profile.
- [ ] Qualified Swiss counsel has approved the English document set and retention matrix.
- [ ] Product and Finance have approved the B2C commercial model.
- [x] A worldwide price and commerce proposal is recorded in `PXA.Global-Commerce-And-Pricing.md`.
- [x] Consolidate operator, pricing, Merchant-of-Record, B2B-first, first-market, and retention decisions in `PXA.Commercial-Launch-Decision-Packet.md`.
- [ ] Tax, Legal, Product, and Finance have approved the supported-country matrix and versioned price book.
- [ ] The electronic withdrawal workflow has been implemented and approved.
- [x] Optional tracking remains disabled.
- [ ] Final Production Legal scan and sign-off are complete.
