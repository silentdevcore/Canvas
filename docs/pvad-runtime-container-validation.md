# PVAD Runtime and Container Validation

## Scope

This follow-up services the developer CLI and the locally built WebApi and
Observability Relay candidates. It does not publish images, change registry
ownership, approve retention policies, accept the NPOI EULA, or complete a rename
gate. `VERSION` remains `1.1.0`.

The source baseline is commit `6202131286f25f5abee245d72d5ce1738efd0eb2`, plus
the explicitly reviewed WebApi Dockerfile servicing changes. Candidate labels
identify that base commit; they do not mean the working tree is a tagged release.
The local build receipts additionally bind each Dockerfile hash, build timestamp
and immutable image ID.

## Developer CLI

The default `/usr/local/bin/dotnet` now selects official Microsoft SDK
`10.0.113`, .NET `10.0.13`, and ASP.NET Core `10.0.13`, installed under
`/usr/local/share/dotnet-patched/10.0.113-x64`. The downloaded archive was checked
against its previously verified official SHA-512 before installation.

The launcher sets `DOTNET_ROOT` and `DOTNET_ROOT_X64` for its child processes.
This preserves the existing x64 execution architecture used by the full
regression suite and bundled macOS OCR libraries. It is not approval of a macOS
arm64 OCR deployment. Older Homebrew/native installations remain installed for
rollback; already running applications and IDE-managed SDKs are not restarted or
silently switched.

The first launcher check exited with code 143 after printing the new version;
the installer restored the old symlink automatically. A separate successful
check preceded the final switch. Both installation attempts are retained in the
local evidence. Subsequent default-CLI checks loaded CoreLib, PKCS and XML from
the new system installation and passed 145 Core and 140 Generator tests.

To inspect the effective toolchain:

```bash
command -v dotnet
dotnet --info
dotnet --list-runtimes
```

The previous CLI symlink was
`../Cellar/dotnet/10.0.105/bin/dotnet`. An operator can restore that exact link if
necessary, but the old runtime is not the approved security baseline. Coordinate
future Homebrew upgrades with this explicitly installed launcher; do not assume
that an IDE or an absolute old executable path selects the new runtime.

## Container Changes

The WebApi image explicitly installs `zlib1g` during its existing APT update and
OCR dependency installation. This services an already installed base-image
package instead of leaving it at the base layer's older version.

The OCR package also needs the names expected by Tesseract.NET 5.2.0:

- `native/x64/libleptonica-1.82.0.so` resolves to the distribution's `liblept.so.5`.
- `native/x64/libtesseract50.so` resolves to the distribution's `libtesseract.so.5`.
- The worker-local `libdl.so` resolves to the distribution's `libdl.so.2`.

These are image-local symlinks, not downloaded native binaries or host-library
mounts. The Dockerfile selects the correct Debian multiarch library directory
for `amd64` or `arm64` and fails the build if a target is missing. The wrapper's
`x64` search-folder label means a 64-bit pointer size, not necessarily an x86
CPU. See its tagged [platform selection](https://github.com/charlesw/tesseract/blob/5.2.0/src/Tesseract/Internal/InteropDotNet/SystemManager.cs),
[library constants](https://github.com/charlesw/tesseract/blob/5.2.0/src/Tesseract/Interop/Constants.cs)
and [Unix loader](https://github.com/charlesw/tesseract/blob/5.2.0/src/Tesseract/Internal/InteropDotNet/UnixLibraryLoaderLogic.cs).

The execution user, read-only filesystem policy, persistent data layout, network
configuration and disabled/non-hardened CodeWorker default are not weakened.

## Evidence Location and Review

Local reports are under
`artifacts/dependencies/runtime-container-2026-10-25/`; the directory name records
the work's starting date. Final builds and checks run on 2026-10-26. Artifacts
are ignored, are not signed release attestations, and must be retained by the
operator before workspace cleanup.

Initial attempts remain separate from final validation. In particular, the
first runtime/compression baselines, the first container scans, the internal
Docker network port issue, an invalid synthetic entitlement enum, and the two
native OCR loader failures are not relabeled as successful runs.

For every final candidate, verify:

1. Its OCI version, complete source commit and build timestamp agree with the
   build receipt, and its immutable image ID agrees across build, scan and smoke.
2. Its actual shared runtimes are read from the image, not inferred from a
   moving `10.0` Docker tag or the developer SDK.
3. Trivy scans the saved image archive without receiving the Docker socket.
   Scan all severities and unfixed findings; do not add vulnerability exclusions.
4. The matching SPDX JSON SBOM has a nonempty package inventory. Preserve the
   raw scanner output and SHA-256 of each scan and SBOM.
5. Local integration runs as the image's non-root user, with read-only rootfs,
   dropped capabilities, no-new-privileges, bounded resources, fresh PostgreSQL,
   a dedicated data volume, and synthetic keys/entitlements only.

Example inventory commands, after building a fresh local candidate:

```bash
docker image inspect pvad-webapi:runtime-validation-62021312
docker run --rm --network none --entrypoint dotnet \
  pvad-webapi:runtime-validation-62021312 --list-runtimes
docker image save --output artifacts/webapi-candidate.tar \
  pvad-webapi:runtime-validation-62021312
```

Run the scanner against that archive using an immutable scanner image digest,
separate read-only input and writable report mounts, and a dated vulnerability
database. Re-export and rescan after changing any image bytes. Do not reuse an
earlier report for a rebuilt image under the same local tag.

## Deployment Boundaries

The unmodified Production retention startup gate rejects the current candidate
because `data-processing-inventory.json` is not production-approved. This is an
expected fail-closed result, not permission to alter approval data. The API's
technical integration checks therefore use `Staging`, which retains secure
cookies and non-Development worker requirements, without claiming Production
startup approval.

The staging contract exercises:

- liveness, database-backed readiness and version metadata;
- anonymous Admin documentation and auth denial;
- anonymous, Designer-origin and invalid API-key export denial;
- authenticated PDF and PNG byte signatures and MIME types;
- disabled-entitlement and revoked-key denial;
- the actual packaged OCR worker reading a synthetic image;
- CodeWorker package presence while execution stays disabled;
- readiness failure with liveness preserved after stopping the test database;
- Relay liveness, its packaged health command and malformed-alert rejection.

Separate negative Production attempts captured the expected retention exception
but did not finish through Docker's foreground attach or reach an exited
container state within the local 30-second/10-second diagnostic bounds. Those
attempts remain failed and were explicitly cleaned up. They do not certify
normal abort completion or supervisor/restart behavior. The successful Staging
run is independent; it does not rerun or mark that unresolved diagnostic green.

This is not a test of public TLS/proxy/CDN hosts, browser authentication,
outbound alert delivery, real mail, all document features, Linux/amd64 execution,
or the hardened C# sandbox. It must not be counted as a new full .NET suite on
Linux or as release, license or external cutover approval.

Before Production, resolve the unchanged retention and NPOI decisions, review
remaining scanner findings, and repeat the appropriate deployment tests on
the exact approved release images. Neither an SPDX inventory nor a clean
High/Critical scan proves that no lower-severity risk exists.

## Final Local Results - 2026-10-26

Both final images contain .NET and ASP.NET Core `10.0.14`; this is distinct from
the developer's `10.0.13` runtime. Production C# sources are unchanged. The final
build receipt records the current WebApi Dockerfile SHA-256 and full base commit.

| Surface | Final immutable image ID | Trivy findings | SPDX packages |
| --- | --- | --- | --- |
| WebApi | `sha256:c5106323487c514b4f860fc74d9fa460f6927e87e407712d034b281ce41d3544` | 0 Critical, 0 High, 49 Medium, 9 Low; 0 secrets | 400 |
| Relay | `sha256:094b155737a8a241b86e3e4c51d83809bfe62b2acd567b9fd2d8744c0e86f143` | 0 Critical, 0 High, 21 Medium, 5 Low; 0 secrets | 105 |

No remaining finding offers a fix version in that particular scan. This is not
an accepted risk exception or a promise that a future scan stays unchanged.
The initial API scan's fixable zlib finding is absent after the targeted update.

Trivy `0.74.0` used immutable image
`aquasec/trivy@sha256:62b1e65e8869bc4b4c6aa4fa2b21595256c7c2f6018a9d9ad61caf87187c1969`.
No `ignore-unfixed`, severity-only report, vulnerability exclusion or secret
suppression was applied. Final scan and SBOM reports are in
`verified-final-scan/`; build logs and the receipt use `verified-final-*`.

`staging-complete-smoke/report.json` records a successful run against the same
two image IDs, with 14 expected HTTP outcomes, actual OCR text extraction,
Relay checks, unchanged security controls and cleanup. The synthetic database,
API, Relay, private keys, signing secret, named data volume and test network
were removed. The PostgreSQL container used automatic removal; its disappearance
after the readiness-loss test is expected, not a failed cleanup.

The default-CLI suites passed 145 Core and 140 Generator tests. The 42 Node
container, compliance, versioning and release-author contracts also passed.
These counts are separate from the earlier full 3,437-pass solution run and
are not new Linux full-suite totals.

The owner approved the exact public security-fragment text before it was added.
The separate OCR fix fragment describes the compatible packaging correction.
Neither fragment increments `VERSION` or authorizes publication.

## Production Startup Exit Follow-Up - 2026-10-26

The previously unresolved abort/exit diagnostic is now fixed and independently
regression-tested. The earlier failed attempts above remain failed evidence;
this follow-up does not retroactively approve them or the Production policies.

A minimal console/host probe in the unchanged API image reproduced the delayed
exit for an unhandled exception without an API database, worker or network.
Returning an exit code or catching the typed host startup failure completed
promptly. The observed process reported `CoreDumping: 0`; this does not establish
a general .NET defect or a kernel core-dump cause.

The retention gate still checks exactly the same approval conditions. It now
logs `PVADSTART001` (Critical, event 1001) and throws a dedicated
`PvadRetentionApprovalRequiredException`. The application lifetime wrapper
handles only that sealed exception after `RunAsync` disposes the host and
returns configuration exit code `78`. It does not force termination, catch
unrelated failures, match exception messages or turn rejection into exit `0`.
Normal graceful shutdown continues to return `0`.

The new candidate is based on commit
`669d00bb65a761eb98e349531be99d4f304fea96`, plus the recorded uncommitted startup
changes. Its receipt binds the entry point, gate, lifetime wrapper and Dockerfile
hashes to build time `2026-10-26T07:14:50.472Z` and immutable image
`sha256:17f07bd1a5efbe06ea291595ffe85fb12814b98972e6c323aabfc5faa8c34b89`.
It is an unpublished Linux/arm64 candidate, not a release tag.

Evidence is retained separately under
`artifacts/dependencies/startup-exit-2026-10-26/`:

- The actual old image fails the new container regression: it remains running
  beyond the 15-second bound and is cleaned up after the failed verdict.
- The new image independently exits `78` in 548 ms, with `PVADSTART001`, no HTTP
  listener, no unhandled exception and no OOM kill. The fixture keys, helper
  container, API container and dedicated volume are removed.
- A separate normal Staging control on the same image returns HTTP 200 for
  liveness and version, then exits `0` within 139 ms of SIGTERM. This control
  does not claim database-backed workflows or Production approval.
- All 12 focused retention/lifetime tests pass, including host disposal, normal
  shutdown and propagation of unrelated, cancellation and aggregate failures.
- The complete API project passes 1,174 tests with 80 explicitly skipped and
  zero failures against a disposable PostgreSQL database. The initial database
  preparation failure remains separately recorded; only the successful repeat
  is counted. This is not a new full-solution or Linux test total.
- A fresh Trivy scan of the exact new image reports zero Critical/High findings
  and zero secrets, retaining 49 Medium and 9 Low findings. Its matching SPDX
  inventory contains 400 packages. Raw JSON and SHA-256 receipts are under
  `scan/`; no severity, unfixed-finding or secret exclusions are used.

The permanent CI regression runs immediately after the API image build:

```bash
node tools/configuration/production-startup-smoke.mjs --image pvad-webapi:sbom
node --test tools/configuration/production-startup-smoke.test.mjs
```

The smoke test resolves an immutable local image ID, uses a network-isolated,
read-only, capability-dropped, bounded non-root API with synthetic keys only,
and requires independent exit before cleanup. It needs no real database or
retention-policy edits. If the source inventory is genuinely approved later,
the report explicitly says `not-applicable` rather than manufacturing a
rejection or claiming successful Production startup. Reports are written to
`artifacts/sbom/webapi-startup/` and included in the existing CI artifact upload.
The ten Node contracts reject forced kills, OOM, timeout, missing diagnostics,
an HTTP listener and unhandled aborts as successful rejection evidence.

The new Patch release fragment describes delayed-startup-shutdown correction.
`VERSION`, published release history, NPOI licensing and retention approvals
remain unchanged. The previous Relay scan remains historical evidence for its
unchanged image, not a new Relay scan. Remaining Medium/Low risk, approved
release scans and deployed Production smoke tests are still open.

## Related Records

- [Cryptographic dependency update](pvad-cryptography-security-update.md)
- [Dependency security checklist](../checklists/PVAD.Dependency-Security-And-Compliance.md)
- [NPOI decision](../checklists/PVAD.NPOI-License-Decision.md)
