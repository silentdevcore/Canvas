# Designer Migration: Crystal Reports (`.rpt`) → Canvas Designer

Per-designer companion to the roadmap [`Designer-Migration.md`](Designer-Migration.md).

- **Designer:** Crystal Reports · **Manufacturer:** SAP (originally Crystal Services / Seagate)
- **Format:** `.rpt` — **proprietary binary** (OLE2 / Compound File Binary).
- **Status:** ⛔ **Blocked** — not auto-convertible in this stack. No converter shipped (deliberately).

---

## Why it's blocked (not just "not started")

Unlike every other designer in this suite, `.rpt` is **not a parseable text format**:

1. **Proprietary binary.** `.rpt` is an OLE2 / Compound File Binary (CFB) container whose internal
   streams (`Contents`, `ReportInfo`, `SummaryInformation`, `QESession`, `ReportParametersStream`) are
   **undocumented and SAP-controlled**. There is **no open-source, neutral, cross-platform parser** for
   the layout — confirmed against the available references.
2. **Requires the SAP Crystal Reports SDK.** The only supported way to read a `.rpt`'s layout is the SAP
   Crystal Reports SDK for .NET/Java — which is **Windows/COM-based** and not usable on Canvas's
   cross-platform `net10.0` target.
3. **Binary input doesn't fit the endpoint.** `POST /api/migration/report-to-design` takes a
   `sourceCode` **string**; a binary `.rpt` can't be passed as text without a base64/file-upload path
   (the same binary-upload gap noted for packaged `.rdlx`/`.trdp` and JSON `.mrt`).

Building a converter that "parses" `.rpt` without the SDK would produce garbage and misrepresent what
works, so **no converter is shipped**. This file records the decision.

## Possible paths (all V2 / out-of-scope for now)

- **SDK-backed `RptToXml` (Windows only).** On a Windows host with the SAP Crystal SDK, convert
  `.rpt → XML` (e.g. the community `RptToXml` tool, which uses the RAS SDK), then write an
  `Canvas.Migration.CrystalXml` converter against that XML. Needs: a Windows build/runtime + the SDK +
  a binary/file upload path on the endpoint.
- **User-side re-export.** Crystal can't export to RDL directly, but a third-party Crystal→SSRS migration
  can produce **RDL**, which already converts via `Canvas.Migration.Rdl`. Document this as the
  recommended manual workaround.
- **Binary upload + CFB string extraction (low value).** Detect the CFB signature and extract readable
  strings (field names, captions) — but **no geometry/layout** is recoverable, so the result isn't a
  usable design. Not worth doing.

## Recommendation

Leave Crystal **blocked** and steer users to the SDK-export → RDL/XML path. Revisit only if a Windows +
SAP-SDK build target is added to Canvas.
