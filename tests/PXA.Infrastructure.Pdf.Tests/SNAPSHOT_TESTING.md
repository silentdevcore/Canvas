# Snapshot Testing Notes (Canvas.Infrastructure.Pdf.Tests)

## Golden test
- Test: `PdfGoldenSnapshotTests.ToBytes_ShouldMatchGoldenHash_ForRepresentativeDocument`
- Baseline SHA256:
  - `c755ea7c20139f17f1873e1f2e28b1514ecfceefef1adad35ec34418a7b5d214`

## Update workflow
When intended serialization changes occur:
1. Run the golden test and capture the new hash from failure output.
2. Verify changes are expected (document structure/feature behavior).
3. Update `expectedHash` in the test.
4. Re-run full infrastructure test suite.

## Determinism notes
- The snapshot document does not set dynamic metadata timestamps.
- Compression is disabled for the snapshot (`CompressContentStreams = false`) to keep byte output deterministic.
