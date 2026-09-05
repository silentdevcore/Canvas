# PXA PDF Redaction Preflight

## Purpose

PXA ships a redaction workflow today. It is not a visual overlay — it drops scene-graph
elements and regenerates the document — but it produces no evidence, so nobody can tell
what it removed, what it could not see, and what it silently changed elsewhere.

This checklist covers the first stage of `PXA.PDF-Trust-Foundation.md` Milestone 3: a
preflight that mutates nothing and reports, per redaction region, exactly what would be
removed and what PXA cannot confidently account for.

Preflight must be honest about its own blind spot. The current implementation tests
intersection only against content the importer modelled, so anything it failed to model
is invisible to the redaction decision. Preflight exists to make that visible.

## Correcting the Record

- [x] Reconcile the contradiction between `PdfTools-WebViewer-Feature-Gaps.md`, which
  ticks `Secure redaction workflow`, and `PxaPdf-Provider-Feature-Gaps.md`, which records
  secure redaction as `missing`.
- [x] State the shipped behaviour accurately in both: content is removed by regenerating
  the document from an interpreted model, which is sound for drawn content and unverified
  for everything else.
- [x] Do not describe preflight as removal. It reports; it never mutates.

## Region Analysis

- [x] Accept typed regions in page coordinates and validate them against page geometry
  before analysing.
- [x] Walk the full scene graph, including nested groups, and classify every element
  against every region as not intersecting, partially overlapping, or fully contained.
- [x] Report fully contained text, paths, and images as removable. Report partially covered
  images as removable only when their bounded samples can be decoded and rewritten safely.
- [x] Report unsupported partial text and path overlap as `reviewRequired`; split supported
  text runs by original glyph bytes, and rewrite supported image and mask samples by pixel
  while unsupported or undecodable content remains blocked. Pattern-painted paths are removed
  as complete paint units when their selected tiling or shading resource is valid.
- [x] Report the covered text run count and character count as evidence without echoing
  the covered text into the report.

## Blind Spots

These are the cases the current redaction cannot see. Each must be reported rather than
silently passed over.

- [x] Pages that carry content streams but produced no scene-graph elements, meaning the
  importer modelled nothing to intersect.
- [x] Annotations and form-field widgets overlapping a region, whose values and appearance
  streams are not scene-graph content.
- [x] Optional content groups, where visibility depends on viewer state.
- [x] Pattern resources are inspected only when an intersecting paint operation selects them;
  unused and out-of-region patterns do not block, while malformed selected patterns do.
- [x] Deferred or undecodable stream filters, where coverage cannot be assessed.
- [x] Document-level carriers of the same text: metadata, XMP, embedded files, and
  attachments.
- [x] Actions and JavaScript.
- [x] Documents with more than one revision, which retain covered content until a full
  rewrite drops earlier revisions.
- [x] Encrypted documents, which are unsupported until encrypted serialization exists.

## Result Model

- [x] Return `removable`, `reviewRequired`, or `unsupported` per region and for the
  document, never collapsing unknown into removable.
- [x] Derive the document capability from the worst region or finding.
- [x] Carry stable `PXAPDFREDACT` diagnostic codes with page and region context.
- [x] Include the document SHA-256 so a preflight result can be correlated with a later
  apply without storing content.
- [x] Keep covered text, metadata values, and attachment contents out of the report.

## API

- [x] Expose `POST /api/pxa/v1/pdf/redactions/preflight` as an authenticated, entitled,
  rate-limited streaming upload endpoint.
- [x] Require a dedicated redaction inspection permission.
- [x] Bound request size, region count, and cancellation.

## Verification

- [x] Prove a fully covered text run reports removable and a partially covered run is split
  into retained and removed glyphs without shifting retained content.
- [x] Prove each blind spot produces its diagnostic on a fixture that contains it.
- [x] Prove preflight never mutates the input bytes.
- [x] Prove the report contains no covered text.
- [x] Run `dotnet build PXA.sln`, the Importer and API suites, and `git diff --check`.

## Explicitly Deferred

- [x] Applied irreversible redaction, verification, and audit evidence — delivered, and
  now tracked in the Milestone 3 section of `PXA.PDF-Trust-Foundation.md`.
- [x] Partial removal within a single text run, preserving original glyph bytes and positions
  for retained glyphs.
- [x] Partial removal within supported raw, lossless, DCT/JPEG, and inline images, with
  shared XObject cloning and post-write pixel verification.
- [x] Partial removal within bounded grayscale/RGB JPX and embedded JBIG2 images, including
  safe `/JBIG2Globals` resolution, deterministic lossless replacement, and post-write
  sample verification. Unsupported component layouts and malformed codec data fail closed.
- [x] Append-only tenant persistence and authenticated retrieval of the complete bounded
  redaction audit record, including integrity verification before delivery.
- [x] RFC 3161 timestamping for signed audit records, with fail-closed behavior when a
  configured timestamp authority cannot produce valid evidence.
