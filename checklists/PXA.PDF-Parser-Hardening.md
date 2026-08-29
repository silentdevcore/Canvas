# PXA PDF Parser Hardening

## Purpose

The PDF trust stack — inspection, signature validation, DocMDP enforcement, and every
future redaction and compliance capability — runs on `PXA.Importer`'s object parser and
stream decoders. Those layers currently accept arbitrary attacker-controlled input from
an authenticated but publicly reachable endpoint without bounding what that input
expands into.

This checklist covers the bounded-parsing item of `PXA.PDF-Trust-Foundation.md`
(Milestone 1, Typed PDF Object Model) and the cancellation and duplicate-object items
that share the same plumbing.

A trust verdict is only worth as much as the parser that produced it. A limit breach
must therefore never be silent: it produces a diagnostic, and a document that tripped a
limit can never be reported as structurally `valid`.

## Confirmed Defects

These were verified in the code on `feature/pdf-trust-foundation` before this checklist
was written. All are reachable through `POST /api/pxa/v1/pdf/trust/inspect`, which
accepts up to 100 MB of arbitrary PDF.

- [x] **Unbounded parser recursion.** `ParseObject` -> `ParseArray` -> `ParseObject` and
  `ParseObject` -> `ParseDictionaryOrStream` -> `ParseObject` carry no depth parameter or
  counter. Deeply nested input overflows the stack, and `StackOverflowException` cannot
  be caught in .NET, so it terminates the API process rather than failing the request.
- [x] **Unbounded flate decompression.** `FlateDecodeStreamDecoder.Decode` copies into an
  unbounded `MemoryStream` with no output cap and no ratio check.
- [x] **Unbounded LZW decompression.** `LzwStreamDecoder.Decode` accumulates into an
  unbounded `List<byte>`.
- [x] **Predictor allocation bomb.** `PdfPredictor.Decode` derives `rowLength` from the
  attacker-controlled `/Colors`, `/BitsPerComponent`, and `/Columns` and allocates it
  directly, with no sanity bound and unchecked integer arithmetic.
- [x] **Object-stream count bomb.** `ParseObjectStream` allocates a list sized from the
  attacker-controlled `/N` before reading any entry.
- [x] **Cyclic `/Length` reference recursion.** `ResolveInteger` -> `ResolveReference` ->
  `TryParseIndirectObject` -> `ParseObject` re-enters on a `/Length` that references an
  object whose own `/Length` references the first. The object cache is only populated
  after a successful parse, so a cycle never hits it.

## Limits Model

- [x] Add a typed `PdfParseLimits` record rather than scattered constants, carried on
  `PdfImporterOptions` so callers can tighten limits without forking the parser.
- [x] Choose defaults that no legitimate document reaches, and document the reasoning
  for each; a limit that fires on real documents is a bug, not protection.
- [x] Allow a decompression floor so that small streams which legitimately expand by a
  large ratio are not rejected by the ratio rule alone.
- [x] Never make a limit breach silent: every breach records a `PXAPDFPARSE` diagnostic
  carrying the limit name, the observed value, and the object it applies to.

## Enforcement

- [x] Bound object nesting depth across arrays, dictionaries, and streams.
- [x] Bound array element and dictionary entry counts.
- [x] Bound the total number of indirect objects parsed from one document.
- [x] Bound decoded stream bytes and the decompression ratio for every built-in decoder,
  enforced during decoding rather than after it.
  - [x] Validate CCITT columns, rows, initial capacity, and every decoded row before
    allocation or append; do not rely on the registry's post-decode size check.
- [x] Bound predictor row length and reject non-sensical predictor parameters.
- [x] Bound object-stream entry counts before allocating from `/N`.
- [x] Detect and break indirect-reference cycles during resolution, and bound resolution
  depth.
- [x] Thread `CancellationToken` through document graph parsing.
- [x] Bound page count.
- [x] Recover at object and stream granularity so one hostile object degrades to a
  diagnostic instead of failing the whole document.

## Reporting

- [x] Surface parser diagnostics in the trust report alongside cross-reference
  diagnostics.
- [x] Refuse a `valid` structure status for any document that tripped a parse limit.
- [x] Keep diagnostic messages free of document content.

## Verification

- [x] Build a hostile-input corpus: deep array nesting, deep dictionary nesting, a flate
  bomb, an LZW bomb, a predictor bomb, an inflated `/N`, and a cyclic `/Length` pair.
- [x] Prove each fixture is bounded and produces the expected diagnostic instead of
  exhausting memory or the stack.
- [x] Prove the limits do not change parsing results for the existing corpus.
- [x] Run `dotnet build PXA.sln`, the Importer and API suites, and `git diff --check`.

## Reader Divergence

Bounding damage is not enough on its own: a parser that reads different bytes than the
viewer showing the document produces trust verdicts about a document nobody sees.

- [x] Read the trailer `/XRefStm` of hybrid-reference files and prefer those entries,
  reporting disagreement with the classic table rather than silently choosing one.
- [x] Restrict the raw `N G obj` scan to documents with no usable cross-reference data,
  and report when recovery by scanning occurred.
- [x] Report object definitions that no cross-reference revision references, excluding
  candidates inside parsed stream bytes so that binary payloads do not produce false
  findings.
- [x] Refuse a `valid` structure status for documents with unreferenced definitions.

## Explicitly Deferred

- [ ] Universal repair of arbitrary malformed or hostile PDFs; this checklist bounds
  damage, it does not add recovery heuristics.
- [ ] Differential and coverage-guided fuzzing, which remains tracked in the trust
  foundation Verification Matrix.
