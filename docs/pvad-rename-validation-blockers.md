# PVAD Rename Validation Blockers

## Broad .NET Run

The initial full solution run was interrupted. The first API run passed
1,045 tests, failed one stale mutation-count assertion and skipped 76 opt-in tests.
The expected Admin count was updated from 49 to 50 with an explicit assertion for
the licensed replacement operation. All authorization/CSRF/audit checks remain;
the focused rerun passed. Skipped tests are not counted as successful evidence.

The subsequent complete default solution run exited successfully: 53 TRX files,
3,294 passed, zero failed, 78 opt-in PostgreSQL tests skipped (76 API, two
Persistence). Full database validation remains open; skipped results are not
passed evidence. All 221 Export tests also passed inside this complete run.
An additional opt-in Persistence rerun passed all 10 tests without skips, including
the two previously skipped real PostgreSQL migration tests. The subsequent full
opt-in API selection plus three protected-runbook contracts passes 79/79, zero
skips. It includes all 76 formerly skipped API cases with the paired-binary
cutover explicitly enabled. These are combined-run results, not one all-opt-in
Solution invocation. See the 2026-10-21 follow-up in PVAD.Rename-Test-Report.md.

That first opt-in run exposed three stale expectations/paths (Trial capability
set, persisted workspace state, missing renamed operator-guide source), now
corrected and covered. It also observed two template-update HTTP 500 responses.
Those did not recur in the focused retry, five independent repetitions or final
full selection. Their root cause remains unknown; test-only exception capture
is retained and no production concurrency fix is claimed.

The unencrypted PDF regression incorrectly rejected a trailer `/ID`, which the
baseline writer already emits. The test now verifies no `/Encrypt` and successful
password-free parsing through the independent reader. No serializer change was
made to satisfy that assertion.

## Findings and Follow-up Evidence

- External PDF corpus: resolved. The synthetic signing certificate expired relative
  to the current system clock. An internal-only validation clock now makes the
  historical corpus comparison deterministic at the certificate validity midpoint.
  Separate checks before/after validity require invalid certificate status while
  preserving valid cryptographic integrity. Production retains the system clock;
  the clock is not a public or serialized policy setting. All 645 Importer tests
  passed without skips; fixture bytes and expected reports remain unchanged.
- PDF golden hash: resolved with an isolated comparison against unmodified
  pre-rename HEAD fce423cf8c5f5e71eff6178f712fe4e5eb5af69a. A probe referencing
  both original PXA.Pdf and current PVAD.Pdf generated the exact same representative
  document byte-for-byte. Both hashes were
  `256569dcef40dd183fdc288ecdb6b4fc9507aaa023df23ecd2e4f8986d37b5b6`.
  The previous expected hash was already stale before this rename. The test now
  uses the reproduced hash and independently checks page count, A4 dimensions and
  extracted title/body. All 53 Infrastructure.Pdf tests pass without skips.
  Local probe source and original HEAD extraction: artifacts/rename/golden-probe/.
- Export suite remained active without completion in the broad run. A bounded
  `--blame-hang-timeout 120s --blame-hang-dump-type none` rerun passed 219 tests and
  then aborted its testhost. The diagnostic sequence records two active tests:
  `WordPdfFidelityTests.SamplePack_ProducesPdfAndDocxArtifacts_AndComparesGeometry_WhenLibreOfficeAvailable`
  and `RealWorldCorpusFidelityTests.PublicRealWorldCorpus_HasImmutableSourcesAndImportsThroughProductionAdapters`.
  Active-at-abort does not prove root cause. Run them independently and inspect
  timeout, CPU/memory and optional external-tool behavior.
  The Word/PDF sample-pack test subsequently passed alone in 55 seconds with
  LibreOffice and visual comparison. This does not establish full-suite completion.
  The real-world corpus test also passed alone in 109 seconds, actively consuming
  CPU. The former 120-second shared-run limit was too tight to establish a hang;
  it must not be described as a confirmed deadlock or provider failure.
  A further parallel run still aborted with both tests active. Both heavy classes
  now share a nonparallel xUnit collection (all assertions and fixtures retained).
  The rebuilt complete Export suite then passed 221/221 tests, zero skips, in
  3 minutes 39 seconds. Resource isolation resolves this validation blocker;
  it does not prove an underlying production renderer performance fix.

Local diagnostic artifacts are under `artifacts/rename/export-diagnostic/` and are
not committed test baselines. The interrupted solution run did not complete every
remaining test project; do not aggregate partial counts into a green full suite.

## Container Findings

The arm64 API image builds and contains both renamed worker packages. Non-root
startup initially failed because the PDF Trust revocation cache defaulted below
unwritable App_Data. Docker/Compose now configure it under owned persistent
`/app/data`. A rebuilt image starts with read-only root and a new anonymous test
data volume; liveness returns 200 and missing-database readiness returns 503.
Positive database-backed readiness now passes in an isolated follow-up: a fresh
PostgreSQL database was initialized from the actual generated EF migration SQL.
The non-root API with read-only root returns readiness 200; stopping only that
test database changes readiness to 503 while liveness stays 200. Both worker
DLLs and the built-in healthcheck were verified. New test resources were removed;
no existing customer volume or database was used. Exact image ID and results are
in artifacts/rename/container-readiness-result.json. This uses the previously
built validation image, not an assertion that every later source edit is packaged.
Other image/platform combinations remain open. The default OCI version is 1.1.0; revision/build-time default to unknown
when not supplied, so release-grade metadata validation remains open.

The online container restore also reported NU1902/NU1903 advisories for
System.Security.Cryptography.Pkcs (10.0.8/10.0.10) and
System.Security.Cryptography.Xml (10.0.10). No dependency update was smuggled into
the rename. Review affected paths, select supported fixes, rerun security tests
and scan final images before release. Build success is not security approval.
