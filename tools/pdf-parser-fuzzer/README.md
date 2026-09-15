# PXA PDF Parser Fuzzing Foundation

This runner executes a deterministic, bounded mutation corpus against the PDF
importer and Trust Inspector. Every mutation is handled by a child process so a
hang or process-fatal parser failure cannot take down the parent runner.

The report contains only mutation identifiers, SHA-256 hashes, statuses, exit
codes, and durations. Malformed input may be rejected successfully; a parser
diagnostic must never result in a valid structure verdict.

Run from the repository root:

```bash
tools/pdf-parser-fuzzer/run.sh
```

This is a deterministic smoke corpus, not a replacement for coverage-guided
fuzzing, cross-platform resource limits, or differential parser testing.
