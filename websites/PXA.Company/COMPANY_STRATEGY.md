# PXA.Company Strategy

## Domain And Subdomains
- Product/web name remains `Power Dox Automation`.
- Developer/product short name remains `PXA`.
- MVP local sites map to future public properties:
  - `PXA.Company` -> main marketing website
  - `PXA.Documentation` -> documentation website
  - `PXA.Demo` -> demo gallery
- Recommended public structure:
  - `powerdoxautomation.com` for Company
  - `docs.powerdoxautomation.com` for Documentation
  - `demos.powerdoxautomation.com` for Demo
- Keep local development ports unchanged for now: Company `5173`, Documentation `5174`, Demo `5175`.

## Pricing And Trial
- MVP pricing remains placeholder content.
- Three buyer paths stay visible:
  - Trial: evaluation path into demos and docs
  - Team: product adoption path for engineering teams
  - Enterprise: migration-heavy/support-led path
- No paid checkout, license enforcement, or trial signup is implemented in the MVP.
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
