# PXA API Docker Checklist

## Goal

Deliver `PXA.WebApi` as a secure, observable, and offline-capable Linux container for PXA Cloud and customer-managed PXA Server installations.

## Priorities

- [ ] P0: Build and run the API and its OCR worker in a production container.
- [ ] P0: Add production health, security, configuration, and persistence boundaries.
- [ ] P1: Deliver the API, Designer, and reverse proxy as a Docker Compose bundle.
- [ ] P2: Add an all-in-one evaluation image and additional CPU architectures.

## Dependencies

- [ ] Stabilize the public API contract tracked in `PXA.SDK-Roadmap.md`.
- [ ] Replace the in-memory template repository with a persistent implementation.
- [x] Define the signed offline-license format and local signature/validity validation policy.
- [ ] Inventory native OCR, font, image, and PDF runtime dependencies.

## Container Image

- [ ] Add a multi-stage Dockerfile using .NET 10 SDK and ASP.NET runtime images.
- [ ] Publish `PXA.WebApi` as a release build for Linux.
- [ ] Run as a dedicated non-root user and listen on port `8080`.
- [ ] Copy the existing OCR executable into the application-owned `ocr-worker/` directory.
- [ ] Include or mount compatible Tesseract and Leptonica libraries.
- [ ] Support mounted OCR language data and font directories.
- [ ] Configure writable temporary, data, and job directories explicitly.
- [ ] Add deterministic image labels for product, version, revision, and license.
- [ ] Build `linux/amd64` first and track `linux/arm64` as P2.

## Runtime Configuration

- [ ] Map API, storage, OCR, font, logging, upload, timeout, and license settings to environment variables.
- [ ] Support Docker secrets or read-only mounted files for credentials and license material.
- [ ] Keep secrets and customer documents out of image layers and logs.
- [x] Add `/health/live` and `/health/ready` endpoints with database and configured SMTP dependency checks.
- [ ] Mount the Data Protection key directory on persistent encrypted storage in the API container.
- [ ] Add graceful shutdown and bounded request/job cancellation.
- [ ] Define cleanup rules for temporary uploads, generated files, and failed OCR jobs.

## Security And Licensing

- [ ] Enable production authentication and authorization instead of the disabled development middleware.
- [x] Support API keys through a dedicated header or Bearer transport without changing SDK behavior.
- [ ] Restrict CORS to configured origins.
- [ ] Apply request-body, multipart-upload, decompression, and execution limits.
- [ ] Disable or protect debug, diagnostics, OpenAPI, and migration execution endpoints in production.
- [x] Validate signed offline-license signatures and validity without requiring internet access.
- [ ] Mount the private issuing key only in trusted Cloud/Admin deployments and distribute only the public verification key to customer servers.
- [ ] Enforce licensed products, expiry, tenant, and instance limits in the API.
- [ ] Produce structured audit events without document contents or credentials.

## PXA Server Bundle

- [ ] Add Docker Compose services for API, Designer, and reverse proxy.
- [ ] Expose the Designer at `/` and the API at `/api` through one customer-facing origin.
- [ ] Add named volumes for persistent data and explicit read-only mounts for configuration.
- [ ] Define restart policies, health dependencies, resource limits, and log rotation.
- [ ] Add a later `pxa-server` all-in-one image for evaluation and small installations.

## Tests

- [ ] Build the image from a clean checkout.
- [ ] Start the image with no optional OCR assets and verify a useful readiness result.
- [ ] Run API, PDF generation, migration, import, export, spreadsheet, and OCR smoke tests.
- [ ] Verify temporary-file cleanup and graceful shutdown during active work.
- [ ] Verify that the container runs without root privileges.
- [ ] Scan the final image for known vulnerabilities and generate an SBOM.
- [x] Generate an SPDX SBOM from the final container filesystem in CI.
- [ ] Test online and fully offline license scenarios.

## Acceptance Criteria

- [ ] The API starts from a versioned image with one documented command.
- [ ] The OCR worker is owned and launched by the API container, not the Designer.
- [ ] Customer documents can be processed without an internet connection.
- [ ] Health checks distinguish process liveness from service readiness.
- [ ] Persistent data survives container replacement.
- [ ] The Compose bundle exposes one stable origin for Designer and API.
