# Paired PXA / PVAD Job Cutover

This isolated integration test uses the real PXA WebApi, persistence and worker
assemblies built from immutable commit `fce423cf8c5f5e71eff6178f712fe4e5eb5af69a`,
and the current PVAD assemblies. The shared harness is compiled separately against
each generation; historical application sources are exported, not rewritten.

## Run

Requires .NET 10, Docker, Git, tar and cached NuGet dependencies:

```bash
node tools/job-cutover/run.mjs
```

The runner exports baseline sources to a new temporary directory, builds each
generation sequentially and runs the opt-in API test. No branch switch, worktree
reset, production host, real account or existing database is used. Legacy source
and build outputs are retained for inspection; synthetic database and object data
are cleaned up by the test. Builds use only the local NuGet cache by default.
`PVAD_CUTOVER_NUGET_SOURCE` can explicitly select a package feed; the manual CI
workflow uses NuGet.org on its clean runner. No such network override is needed
for the local cached run.

The test starts PostgreSQL with database name `pvad_cutover_test`. The harness
requires an explicit test permission flag and a temporary `pvad-binary-cutover-*`
object root. Connection strings are passed through process environment variables,
not command-line arguments or reports. All users/documents are synthetic.

## Scenario

1. PXA applies its real database migrations and creates an organization, user,
   role/membership, subscription, entitlement, template, input object and two jobs
   with historical payloads/filenames.
2. A PXA worker claims the first job and pauses inside a controlled importer.
3. The parent kills that OS process and confirms it exited.
4. Only then, the PVAD harness advances lease expiry for the test and cancels the
   other persisted Pending job. This avoids waiting for the ten-minute lease and
   is not a recommended production procedure.
5. A fresh PVAD process recovers and completes the interrupted job.
6. Fresh PVAD and PXA processes verify IDs, role/subscription/template links,
   attempts, status, tenant isolation, input/output access, pending migrations and
   applied migration IDs. The parent compares physical column/table metadata.

The importer is a deterministic test adapter, not a real PDF/OCR engine. This test
proves one paired-binary job/storage boundary, not every provider, worker or format.
It does not establish mixed-version rolling safety, encryption at rest, production
backup/restore, exactly-once side effects or orphan-free crash handling.

## Separate Build / Test

An exported baseline may be reused by building this project with
`-p:LegacyRoot=/absolute/export/path`, then building it again without that property.
The outputs have different names (`PXA.JobCutover.dll` and `PVAD.JobCutover.dll`).
The legacy output is under `bin/legacy/Debug/net10.0`; the current output is under
`bin/Debug/net10.0`. Canonicalize the exported source path on macOS to avoid
mixing `/tmp` and `/private/tmp` in MSBuild incremental output tracking.

The opt-in test needs `PVAD_RUN_BINARY_CUTOVER_TESTS=1`,
`PVAD_LEGACY_JOB_HARNESS` and `PVAD_CURRENT_JOB_HARNESS` pointing to those files.
Do not point it at production data. Keep old workers stopped during real cutover;
this test intentionally does not run both worker versions concurrently.
