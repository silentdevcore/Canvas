# PXA Designer Element Library and Favorites

## P0 - Element Organization

- [x] Use one typed registry as the authoritative source for element categories, ordering, search keywords, and supported document modes.
- [x] Preserve all existing element type identifiers and serialized document compatibility.
- [x] Organize elements into Text and Data, Forms and Input, Tables and Charts, Media and Codes, Shapes and Layout, Document and Navigation, and Word Elements.
- [x] Add localized element search and persisted collapsed-group state.
- [x] Keep Word-only elements hidden in PDF mode without removing them from Favorites.
- [x] Add a contract test covering the registry, Designer tools, documentation catalog, and backend-supported element types.

## P0 - Personal Favorites

- [x] Add an accessible star action to every element.
- [x] Show non-empty Favorites first while retaining elements in their normal categories.
- [x] Persist favorites per user in PostgreSQL independently of the active organization.
- [x] Add authenticated list and replace APIs under `/api/pxa/v1/designer/preferences/element-favorites`.
- [x] Reject unknown element identifiers server-side.
- [x] Use optimistic updates, rollback notifications, and a last-known local cache.
- [x] Test synchronization across sessions, devices, and organization switches.

## P0 - Content Library

- [x] Replace the standalone Form Block action with Elements and Content sidebar modes.
- [x] Add typed `block` and `pageKit` content entries with localized metadata and previews.
- [x] Reuse existing gallery template content for complete page kits.
- [x] Add Forms, Invoices, Books, Certificates, Presentations, and Reports categories.
- [x] Support search, category filtering, preview, click insertion, and block drag-and-drop.
- [x] Insert blocks into the current page and page kits after the active page.
- [x] Keep the existing Template Gallery replacement workflow unchanged.

## P0 - Safe Insertion

- [x] Remap page and element identifiers and update internal references.
- [x] Namespace only conflicting form field names.
- [x] Preserve bindings, localization, RTL, styles, assets, and extension data.
- [x] Validate an insertion before changing the document.
- [x] Apply every insertion as one undoable and autosaved transaction.
- [x] Fit differently sized page kits proportionally into the current document format.
- [x] Show scaling, incompatibility, offline, empty, and failure states.

## P1 - Experience and Delivery

- [x] Localize labels, search, warnings, status text, and accessibility labels in all six Designer languages.
- [x] Support complete keyboard navigation and responsive sidebar scrolling.
- [x] Track recently used content locally and separately from Favorites.
- [x] Add unit, API, persistence, integration, desktop, and mobile smoke tests.
- [x] Update Designer documentation after the optimized behavior is stable.
- [x] Add a customer-facing minor release fragment.

Mobile smoke coverage currently validates the existing protected "Desktop required" state; interactive mobile editing remains outside the current Designer capability.

## Dependencies

- [x] Persistent Designer authentication and PostgreSQL are available.
- [x] Existing template definitions remain valid and can be adapted into library page kits.
- [x] Designer autosave and undo history remain the persistence boundary for inserted content.

## Acceptance Criteria

- [x] Every supported Designer element has exactly one authoritative category.
- [x] Personal favorites synchronize across devices and organizations.
- [x] Forms, invoices, books, certificates, presentations, and reports can be inserted as blocks or page kits.
- [x] Insertions never create duplicate identifiers, broken references, or partial document changes.
- [x] Differently sized content is fitted predictably to the current document page size.
- [x] A new element cannot be added without registry and coverage validation.
