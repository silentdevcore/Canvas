# Isolated Linux Visual Validation

This local-only runner tests a committed snapshot, not a writable host worktree.
It runs the enforced Serif/Mono and complete element-catalog packs sequentially
with the existing guards and thresholds. Failed packs retain their evidence;
failure is never treated as approval. It publishes nothing.

From the repository root:

```bash
mkdir -p artifacts/font-linux/source artifacts/font-linux/evidence
git archive --format=tar --output=artifacts/font-linux/source/source.tar HEAD
docker build -t pvad-visual-parity-linux:local tools/visual-parity/linux
docker run --rm --init --user "$(id -u):$(id -g)" \
  --cpus=1.5 --memory=3g --shm-size=512m --pids-limit=512 \
  --mount "type=bind,src=$(pwd)/artifacts/font-linux/source,dst=/input,readonly" \
  --mount "type=bind,src=$(pwd)/artifacts/font-linux/evidence,dst=/evidence" \
  -e "PVAD_BUILD_COMMIT=$(git rev-parse HEAD)" \
  pvad-visual-parity-linux:local
```

Use the same user ID for archive/output ownership. The non-root runner creates
its own temporary container workspace and home. It mounts no Docker socket,
customer database, credentials, host package cache or host build outputs. Source
input is read-only; only generated evidence is written to the host.

The image uses .NET 10, Node 22 and Playwright 1.64.1, matching the project
browser dependency. Record the exact image ID, input commit/archive hash and
driver hashes for each validation, together with `environment.json` and
`run.log`. Base tags can advance; a tag alone is not immutable evidence.
The runtime requires package-registry access for its clean npm/NuGet restore.

Inspect:

- `artifacts/font-linux/evidence/font-families/visual-parity-report.html`
- `artifacts/font-linux/evidence/all-elements/visual-parity-report.html`
- `artifacts/font-linux/evidence/environment.json`
- `artifacts/font-linux/evidence/run.log`

Use a fresh output directory or archive previous evidence before another run.
The runtime architecture follows Docker's selected Linux platform. A local
Linux/ARM64 result does not certify Linux/x64, Windows, an authenticated customer
workflow or remote CI. macOS owner approvals never implicitly approve Linux
raster pixels. Existing macOS reports and pinned baselines must not be replaced.
