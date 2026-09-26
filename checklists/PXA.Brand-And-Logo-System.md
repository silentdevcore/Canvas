# PXA Brand And Logo System

## Goal

Create a distinctive, professional logo system for **Power Dox Automation** and
its short product name **PXA**. The system must work across websites, Designer,
Admin, Account, Documentation, Demo, SDKs, CLI, containers, documents, and the
future PXAP/PXAS project formats.

## Priorities

- [ ] P0: Confirm brand strategy, naming, trademark constraints, and visual brief.
- [ ] P0: Design and approve a scalable symbol, PXA wordmark, and full product
  lockup.
- [ ] P0: Deliver production vector masters and accessible application assets.
- [ ] P1: Integrate the approved system across every PXA product surface.
- [ ] P1: Publish usage rules, asset tests, and migration guidance.
- [ ] P2: Add motion, event, partner, and campaign variants only after the core
  identity is stable.

## Brand Brief

- [ ] Position PXA as a precise, dependable document-automation platform rather
  than a single-purpose PDF utility.
- [ ] Express structured transformation, reliable output, and automation without
  depending on generic document-sheet, folded-page, lightning, or magic-wand
  clichés.
- [ ] Keep **PXA** as the primary compact product mark and **Power Dox Automation**
  as the full descriptive name.
- [ ] Ensure the identity remains credible for developers, enterprise buyers,
  administrators, and end users.
- [ ] Define desired attributes and exclusions before drawing concepts.
- [ ] Confirm pronunciation, capitalization, and whether the full wordmark uses
  `Power Dox Automation` consistently in every locale.

## Research And Legal Clearance

- [ ] Audit current PXA marks, text logos, favicons, colors, and inconsistent
  branding across the repository.
- [ ] Review competitor identities to avoid visual imitation and category
  confusion.
- [ ] Commission trademark and company-name clearance in intended sales markets;
  repository and web searches are not legal clearance.
- [ ] Check symbol similarity, wordmark similarity, app-store presentation,
  domains, social handles, and relevant Nice classes.
- [ ] Record designer, font, icon, and generated-asset licenses with transferable
  commercial rights.
- [ ] Require explicit ownership assignment for externally commissioned artwork.

## Concept Development

- [ ] Produce at least three genuinely different black-and-white concepts before
  applying color.
- [ ] Evaluate every concept at 16 px, 24 px, 32 px, app-header size, document
  footer size, presentation size, and monochrome print.
- [ ] Test the symbol without text, the PXA wordmark, and the full lockup.
- [ ] Reject concepts that depend on gradients, fine strokes, tiny interior gaps,
  or color to remain recognizable.
- [ ] Test distinctiveness beside major PDF, document, reporting, automation, and
  developer-tool brands.
- [ ] Select one direction through a recorded review covering meaning,
  distinctiveness, scalability, accessibility, and international suitability.

## Required Logo Family

- [ ] Create a primary horizontal lockup: symbol, PXA, and Power Dox Automation.
- [ ] Create a compact horizontal PXA lockup for application headers.
- [ ] Create a symbol-only mark for favicons, app icons, avatars, and small UI.
- [ ] Create a stacked lockup for square or narrow placements.
- [ ] Create one-color black, one-color white, grayscale, light-background, and
  dark-background variants.
- [ ] Define minimum sizes, clear space, alignment, optical corrections, and
  prohibited uses.
- [ ] Define whether product qualifiers such as Designer, Admin, Documentation,
  Demo, API, and CLI are typography extensions rather than separate logos.
- [ ] Create coherent PXAP and PXAS file icons without changing the core mark.

## Color And Typography

- [ ] Reconcile the logo palette with the existing PXA Web Design System instead
  of introducing an unrelated brand palette.
- [ ] Define primary, secondary, neutral, and semantic colors with HEX, RGB, CMYK,
  and print-safe references.
- [ ] Verify contrast when the mark carries essential readable text.
- [ ] Select commercially usable display and UI typefaces with documented
  fallbacks and redistribution rights.
- [ ] Ensure the wordmark remains artwork and does not require a licensed font at
  runtime.
- [ ] Test black-and-white office printing, PDF export, dark mode, and high-
  contrast environments.

## Production Assets

- [ ] Store editable vector masters in an approved design-source location.
- [ ] Export optimized SVG assets without editor metadata, embedded scripts,
  external resources, or inaccessible text dependencies.
- [ ] Export PNG assets at required application, social, and documentation sizes.
- [ ] Generate favicon ICO/PNG/SVG and web-app manifest icons.
- [ ] Generate macOS, Windows, Linux, container-registry, NuGet, npm, Maven, PyPI,
  CLI, and project-file assets only where the platform requires them.
- [ ] Preserve transparent backgrounds and avoid raster upscaling.
- [ ] Add an asset manifest containing variant, dimensions, checksum, intended
  use, background, and source version.
- [ ] Keep deprecated assets during one migration window and remove them only
  after all consumers have moved.

## Product Integration

- [ ] Replace text-only and temporary PXA marks in Company, Account, Admin,
  Designer, Documentation, and Demo.
- [ ] Apply the compact lockup consistently to navigation while preserving each
  application's product qualifier.
- [ ] Update login, email, generated document metadata, Docker, SDK, package,
  installer, CLI, OpenAPI, and social-preview surfaces.
- [ ] Add the approved logo to standard status pages without making fatal pages
  depend on remote assets.
- [ ] Add accessible names and mark decorative symbol duplicates as hidden.
- [ ] Avoid embedding large Base64 logo copies in source code or generated
  documents.
- [ ] Verify no legacy Canvas marks or conflicting temporary PXA badges remain.

## Governance

- [ ] Publish a concise brand guide with downloadable approved assets.
- [ ] Define who may approve new variants and prevent ad-hoc recoloring or
  distortion.
- [ ] Version the asset family and record changes in release notes when product
  surfaces change.
- [ ] Provide templates for presentations, documentation screenshots, social
  previews, and partner usage.
- [ ] Define co-branding and powered-by rules separately from the primary logo.
- [ ] Keep customer branding and white-label behavior separate from PXA's own
  identity.

## Validation

- [ ] Validate every SVG for well-formed markup, bounded paths, no scripts, no
  external references, and deterministic optimization.
- [ ] Verify sharp rendering at all declared sizes and device-pixel ratios.
- [ ] Test light, dark, monochrome, print, high-contrast, and color-vision
  scenarios.
- [ ] Test header fit and non-overlap across desktop and mobile applications.
- [ ] Verify favicon, social preview, package registry, file association, and PDF
  rendering outputs.
- [ ] Run a repository branding scan for old, duplicated, or unapproved assets.
- [ ] Obtain written product approval and required legal clearance before public
  release.

## Acceptance Criteria

- [ ] One approved source asset family covers symbol, PXA wordmark, and full
  Power Dox Automation lockup.
- [ ] The mark remains recognizable, legible, and distinctive from 16 px to print
  and presentation sizes.
- [ ] Every public PXA surface uses an approved variant consistently.
- [ ] Assets have clear ownership, licensing, accessibility, and usage rules.
- [ ] PXAP and PXAS receive related file icons without becoming separate brands.

## Dependencies And References

- [ ] Use [PXA.Web-Design-System.md](PXA.Web-Design-System.md) as the source of
  existing UI tokens and integration patterns.
- [ ] Supply local fallback assets to
  [PXA.Standard-Status-Pages.md](PXA.Standard-Status-Pages.md).
- [ ] Supply file icons only after
  [PXA.Native-Designer-Formats.md](PXA.Native-Designer-Formats.md) finalizes the
  format identities.
- [ ] Align visible names with [Rename-To-PXA.md](Rename-To-PXA.md).

## Assumptions

- The public company and product name remains Power Dox Automation, shortened to
  PXA.
- The primary logo master is vector artwork; generated bitmap concepts may support
  exploration but are not production masters.
- Logo creation does not replace formal trademark clearance.
- Product-specific labels extend one brand system rather than creating separate
  logos for every application.

