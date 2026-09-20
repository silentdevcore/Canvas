# PXA PDF Parser Fuzzing Foundation

The deterministic runner executes a bounded mutation corpus against the PDF
importer, Trust Inspector, and every installed independent parser from qpdf,
Poppler, and MuPDF. Every mutation is handled by isolated child processes so a
hang or process-fatal parser failure cannot take down the parent runner.

The report contains only mutation identifiers, SHA-256 hashes, parser decisions,
exit codes, durations, and differential flags. It never stores source document
content. Parser disagreement is evidence for review rather than an automatic
failure; a crash or timeout fails the run. A PXA parser diagnostic must never
result in a valid structure verdict.

Run from the repository root:

```bash
tools/pdf-parser-fuzzer/run.sh
```

For a bounded coverage-guided AFL++ campaign, install `afl-fuzz` and the
`SharpFuzz.CommandLine` tool, then run:

```bash
PXA_PDF_FUZZ_COVERAGE_SECONDS=300 tools/pdf-parser-fuzzer/run-coverage.sh
```

The coverage runner instruments only the disposable publish output, enforces
per-input time and a 512 MiB managed-heap limit, stores findings below ignored
`artifacts/`, and fails when AFL++ reports a crash or hang. Never instrument a
normal development or release build in place.
