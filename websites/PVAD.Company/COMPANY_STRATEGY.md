# PVADE Company Strategy

## Domain And Subdomains
- Product/web name is `PVADE`; technical names use `PVAD`.
- The owner selected `pvade.com` and `support@pvade.com`. This does not establish
  domain, mailbox, sender or package-publisher registration.
- Proposed deployment origins, all **unverified**, follow the
  [candidate profile](../../deploy/rename/pvade-targets.candidate.json):
  - `https://pvade.com` for Company
  - `https://docs.pvade.com` for Documentation
  - `https://demos.pvade.com` for Demo
  - `https://account.pvade.com` for Account
- These origins now supply production link defaults, not evidence of available services.
- MVP local sites map to future public properties:
  - `PVAD.Company` -> main marketing website
  - `PVAD.Documentation` -> documentation website
  - `PVAD.Demo` -> demo gallery
  - `PVAD.Account` -> customer registration and self-service
- Current source fallbacks, pending a coordinated deployment:
  - `https://pvade.com/` for Company
  - `https://docs.pvade.com/` for Documentation
  - `https://demos.pvade.com/` for Demo
  - `https://account.pvade.com/` for Account
- Keep local development ports unchanged for now: Company `5173`, Documentation `5174`, Demo `5175`, Account `5178`.
- New deployment configuration uses `VITE_PVAD_*_URL`; `VITE_PXA_*` remains a
  conflict-checked compatibility input. Configure installed origins explicitly
  when retaining legacy hosts. Backend product-return and Designer handoff defaults
  match the chosen product origins without automatically trusting old domains.
  DNS, routing, per-client OAuth callbacks and mail delivery still require
  deployment configuration and validation.

## Pricing And Trial
- MVP pricing remains placeholder content.
- Three buyer paths stay visible:
  - Trial: evaluation path into demos and docs
  - Team: product adoption path for engineering teams
  - Enterprise: migration-heavy/support-led path
- Trial signup and email verification start in PVAD.Account. The commercial task states in this original strategy remain historical planning context; consult the current subscription, Account and legal checklists before release.
- Future pricing work should define edition boundaries, support levels, and license terms before adding forms or payments.

## Contact Path
- MVP contact path remains a sales/contact placeholder section.
- Preferred first implementation: mail/contact link or simple static form endpoint.
- CRM, ticketing, and account-based routing are post-MVP decisions.
- Contact copy should route migration-heavy questions toward enterprise evaluation.

## Integration Rules
- Company pages should link to concrete Demo routes whenever possible.
- Company pages should link to Documentation sections for product proof.
- Pricing text must stay clearly placeholder until licensing is decided.
- Customer sign-in and Trial calls to action must route to PVAD.Account; PVADE Admin must not be linked from Company pages.
