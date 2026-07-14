# PXA.Documentation Strategy

## Source Layout
- `websites/PXA.Documentation` is the product-first documentation website.
- Existing `docs` content stays in place for the MVP and remains the generated/reference documentation source.
- The documentation website links into `docs` for DocFX, OpenAPI, schema, and cookbook content instead of moving those files immediately.
- Historical checklists remain implementation history and planning context; they are not the product documentation source of truth.

## Search
- MVP search should be client-side and scoped to the documentation homepage index first.
- Search index inputs:
  - editor sections
  - code sections
  - migration guides
  - cookbook topics
  - demo examples
  - reference links
- Full-text indexing of `docs` can come later after the content model is stable.

## Versioning
- MVP version label is `current`.
- Public navigation can later expose `latest`, released versions, and archive links.
- Versioned docs should not fork checklists; checklists remain historical project records.

## Integration Rules
- Product docs explain current behavior.
- DocFX/OpenAPI outputs provide generated reference material.
- Demo examples provide runnable or inspectable examples.
- Checklists capture roadmap, parity, audit, and migration history.
