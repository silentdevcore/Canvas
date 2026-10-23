# PXA Unified Migration Frameworks

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Historical PXA/Canvas product references, implementation notes and compatibility identifiers are retained; task statuses are unchanged.

## Goal

Provide one migration architecture while keeping PDF, Spreadsheet, and Report as separate product domains.
The canonical taxonomy is `PXA.Migration.<Domain>.<Kind>.<Provider>`.

## P0 - Registry And API

- [x] Add typed migration domain, kind, input, artifact, and provider descriptor contracts.
- [x] Create one canonical catalog for all PDF Code, PDF Viewer, Spreadsheet Code, and Report Designer providers.
- [x] Expose the catalog through `GET /api/pxa/v1/migrations/catalog`.
- [x] Add provider-detail, examples, convert, and preview routes under the canonical domain/kind/provider path.
- [x] Return one result envelope with typed artifacts, diagnostics, summary, fidelity, source preservation, and metadata.
- [x] Keep the existing migration routes as compatibility adapters for one major release.
- [x] Add deprecation and successor-link headers to compatibility responses.
- [x] Declare the supported source extensions and text/binary behavior for every Designer provider in the canonical catalog.
- [x] Reject uploaded source names whose extension does not match the selected provider and input type.

## P0 - Authorization

- [x] Add domain/kind read and execute permissions.
- [x] Add canonical provider permissions containing domain, kind, and provider.
- [x] Accept existing migration permission claims as compatibility aliases for one major release.
- [x] Show canonical migration permissions in Admin grouped by domain and kind.
- [x] Reject cross-domain, cross-kind, and unknown-provider requests.

## P0 - Designer

- [x] Keep PDF Code and PDF Viewer under `/pdf/migrations`.
- [x] Keep Spreadsheet Code under `/spreadsheet/migrations` without PDF providers or preview behavior.
- [x] Keep Report Designer as the technical `report/designer` migration kind while presenting it under the PDF product at `/pdf/migrations/designer`.
- [x] Keep the primary navigation focused on PDF and Spreadsheet; do not expose a one-item Report product area.
- [x] Replace static frontend provider catalogs with the API catalog.
- [x] Introduce a shared migration workspace for provider selection, source, results, diagnostics, and actions.
- [x] Replace the framework dropdown with a searchable provider sidebar grouped by migration workflow.
- [x] Present source editing and migration results in one responsive three-column workbench.
- [x] Add dedicated Preview, Generated Output, and Diagnostics result views without duplicating conversion state.
- [x] Show accepted source extensions and provider maturity directly in the provider list.
- [x] Integrate Nutrient Viewer into the shared PDF Designer Migration provider list.
- [x] Require the original PDF and combine it with JavaScript, TypeScript, or Instant JSON into one editable `DesignExportDto`.
- [x] Map supported annotations to Designer elements and apply supported form values to matching imported fields.
- [x] Preserve unmatched form values, Viewer settings, and the normalized annotation sidecar as design metadata with diagnostics.
- [x] Render the same backend PDF preview and use the same Open-in-Designer handoff as report providers.
- [x] Redirect the former standalone Viewer Migration route to the Nutrient provider in PDF Designer Migration.
- [x] Redirect legacy migration routes to their canonical domain routes.
- [x] Localize the new navigation and workspace in all six Designer languages.
- [x] Drive the source-file picker from the selected provider contract, including text reports, packaged RDLX/TRDP reports, and Nutrient configuration files.
- [x] Keep DevExpress `.resx` files as optional resources instead of accepting them as standalone report sources.

## P1 - Documentation And Contributor Experience

- [x] Separate public migration documentation into PDF, Spreadsheet, and Report sections.
- [x] Document canonical v1 routes and list old routes only under compatibility.
- [x] Document Nutrient Code and Viewer as separate kinds in one provider family.
- [x] Add a provider coverage table with status, inputs, outputs, preview, tests, and documentation.
- [x] Update contributor guidance so a provider is registered once in the canonical catalog.
- [x] Link this checklist from `Migration-Namespace-Taxonomy.md`.

## Tests And Release

- [x] Test catalog uniqueness, aliases, valid domain/kind combinations, and required metadata.
- [x] Verify all 33 provider projects have catalog, API, permission, UI, test, and documentation coverage.
- [x] Add canonical API contract tests for catalog, details, examples, convert, preview, and errors.
- [x] Add compatibility tests for routes, provider aliases, diagnostic IDs, and deprecation headers.
- [x] Add authorization tests for domain, kind, provider, and cross-domain denial.
- [x] Add Designer route, filtering, Nutrient Viewer, Report handoff, and localization tests.
- [x] Test valid and invalid source-file/provider combinations across all Designer migration providers.
- [x] Run relevant .NET suites, Designer tests/typecheck/build, and Documentation tests/build.
- [x] Add a customer-facing minor release fragment.

## Deferred

- [ ] Add Spreadsheet Datasource providers only when a real backend datasource migrator exists.
- [ ] Remove compatibility routes and permission aliases in the next major version.

## Acceptance Criteria

- [x] PDF and Spreadsheet are distinct product areas; Report Designer remains a distinct API domain exposed within the PDF migration experience.
- [x] Every provider is discoverable from one backend catalog and appears only in its valid domain and kind.
- [x] Every canonical conversion route uses the same result envelope without flattening kind-specific artifacts.
- [x] Nutrient Viewer produces one editable PXA design with backend preview and no disconnected user-facing artifact bundle.
- [x] Existing integrations continue to work during the compatibility period.
