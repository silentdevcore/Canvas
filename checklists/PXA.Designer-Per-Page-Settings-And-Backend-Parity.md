# PXA Designer Per-Page Settings and Backend Parity

## P0 - Canonical Contract

- [x] Add optional physical layout overrides to every design page.
- [x] Keep document metadata, security, localization, styles, numbering, and watermark configuration document-wide.
- [x] Resolve page overrides over document defaults with an A4 portrait fallback.
- [x] Normalize orientation and dimensions consistently in TypeScript and C#.
- [x] Preserve legacy documents without page overrides.
- [x] Synchronize JSON schema, OpenAPI, C# Model, C# PDF, and Base64 representations.

## P0 - Designer

- [x] Edit the active page's effective physical layout in Page Settings.
- [x] Add override status, Apply to all pages, and Reset to document default actions.
- [x] Make new and duplicated pages inherit the active page layout.
- [x] Use effective dimensions, margins, background, and bleed in Canvas, thumbnails, guides, constraints, and content insertion.
- [x] Render every Live Preview page with its own effective layout.
- [x] Diagnose elements that overflow after a page layout change.
- [x] Keep layout mutations undoable and autosaved.

## P0 - Backend and Export

- [x] Carry effective page layout through the shared layout planner.
- [x] Render mixed PDF page sizes, orientations, backgrounds, margins, and bleed.
- [x] Preserve page dimensions through C# PDF snapshots and code roundtrips.
- [x] Render mixed layouts in PNG, JPEG, TIFF, and SVG output.
- [x] Emit per-page sections or page styles in DOCX, ODT, and HTML.
- [x] Leave non-page-oriented data formats unchanged.

## P1 - Import and Compatibility

- [x] Preserve mixed MediaBox/CropBox geometry during PDF import.
- [x] Preserve DOCX sections and ODT page layouts where exposed by the source.
- [x] Keep report migrations on document defaults unless the provider supplies page geometry.
- [x] Add stable normalization diagnostics for invalid page layouts.

## Validation and Delivery

- [x] Add a canonical portrait, landscape, and custom-size fixture.
- [x] Test Designer editing, inheritance, reset, apply-all, undo, autosave, and reload.
- [x] Test PDF MediaBox, image dimensions, DOCX sections, ODT layouts, and HTML print CSS.
- [x] Test JSON, C# Model, C# PDF, Base64, import, localization, RTL, assets, and shared elements.
- [x] Add contract coverage for every page layout field and renderer.
- [x] Update PXA.Documentation with mixed-layout screenshots and workflows.
- [x] Add a customer-facing minor release fragment.
- [x] Run Designer, Documentation, .NET, visual parity, and browser smoke validation.

## Acceptance Criteria

- [x] One document can contain portrait, landscape, and custom-size pages.
- [x] Designer Canvas and Live Preview match backend PDF geometry for every page.
- [x] All page-oriented exports preserve the effective page layout.
- [x] Existing documents continue to render unchanged.
- [x] New page layout fields cannot bypass contract and parity validation.
