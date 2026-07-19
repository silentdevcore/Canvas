# PXA Product Roadmap

## Goal

Maintain one prioritized index of product work outside PXA Admin. Detailed implementation tasks remain owned by the linked checklists.

## P0 - Customer Platform

- [ ] Deliver customer registration, sign-in, Trial activation, organization ownership, and self-service account management through `PXA.Account.md`.
- [ ] Complete transactional and security email flows tracked in `PXA.Mail-Service.md`.
- [ ] Harden subscription lifecycle, limits, offline licensing, and customer visibility through `PXA.Subscription-Licensing.md`.
- [ ] Replace in-memory templates and jobs with persistent metadata and object-storage boundaries from `PXA.Database.md`.

## P0 - Product Quality

- [ ] Complete durable, tenant-protected viewer state, complex redaction coverage, signatures, and remaining form/annotation fidelity from `PdfTools-WebViewer-Feature-Gaps.md`.
- [ ] Implement the high-priority PDF engine gaps from `PxaPdf-Provider-Feature-Gaps.md`, especially digital signatures, secure redaction, advanced forms, accessibility, and attachments.
- [ ] Complete importer fidelity and format wiring through `Importer-New-Featuers.md`, `PDF-Importer.md`, and `UI-Importer-Features.md`.
- [ ] Complete Word, image, and spreadsheet conversion gaps through `Word-Converter.md`, `Image-Converter2.md`, and `Spreadsheet-Import-Export.md`.

## P1 - Designer And Migration

- [ ] Continue the incremental Designer architecture work tracked in `UI-Clean-Architecture.md` without blocking product fixes on a full rewrite.
- [ ] Complete advanced elements, live preview, page settings, and remaining UI fixes through the focused UI checklists.
- [ ] Consolidate provider-neutral Roslyn behavior and finish provider compile/snapshot coverage from `Code-Migrations.md` and its provider checklists.
- [ ] Close remaining report-designer fidelity gaps in the RDL, DevExpress, JasperReports, and related designer-migration checklists.

## P1 - Documentation And Product Sites

- [ ] Keep product documentation aligned with implementation through `Documentation-Audit.md`.
- [x] Connect PXA.Company to PXA.Account for `Sign in` and `Start free trial` without exposing PXA Admin.
- [ ] Continue production examples and source-linked scenarios in PXA.Demo as product capabilities mature.

## Deferred Platform Work

- [ ] Build API and Designer container deployments through `PXA.Api-Docker.md` and `PXA.Designer-Docker.md` after the application contracts stabilize.
- [ ] Stabilize OpenAPI and then deliver Java, TypeScript/JavaScript, Python, and later SDKs through `PXA.SDK-Roadmap.md`.
- [ ] Add Cloud hosting, billing-provider integration, enterprise identity federation, SCIM, and advanced disaster recovery in their later roadmap phases.

## Roadmap Rules

- [x] Use this file as a priority index, not as a duplicate technical specification.
- [x] Keep detailed acceptance criteria and tests in the linked feature checklists.
- [x] Treat unchecked historical parent tasks in older checklists as audit candidates until verified against the current codebase.
- [x] Keep Deployment, Billing, and P2 Enterprise work explicitly deferred until selected as an active milestone.
