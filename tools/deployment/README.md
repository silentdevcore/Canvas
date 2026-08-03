# PXA Deployment Control

PXA deployment validation consumes an existing immutable stable release. It
never calculates a version, changes `VERSION`, creates a tag, or rebuilds a
release artifact.

The `PXA Deployment Validation` workflow verifies:

- the requested stable `vX.Y.Z` tag and exact checked-out commit;
- the shared stable release-manifest entry;
- all seven required GitHub Release archives and SHA-256 digests;
- published WebApi and Observability Relay container tags and digests;
- environment, operation, actor, workflow run, and optional source run.

The protected environment job creates a machine-readable deployment evidence
artifact with status `validated`. No Cloud or On-Premise target adapter is
configured yet, so the workflow must not be interpreted as a successful target
deployment. A later adapter will consume the same verified release and update
the evidence to `succeeded` or `failed` after target health checks.

Retry and rollback are new workflow runs against an existing stable version.
They require the source workflow run ID and never change version metadata or
release notes.

Repository environments:

- `pxa-staging` accepts deployment validation only from protected branches.
- `pxa-production` additionally requires approval by the configured release
  operator before the environment job can create evidence.

Both environments are serialized independently by workflow concurrency. The
workflow requires explicit confirmation and fails before release downloads when
the operation, version, environment, or source run is invalid.
