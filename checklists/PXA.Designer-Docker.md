# PXA Designer Docker Checklist

## Goal

Deliver `pxa-designer` as a production-ready static web application that connects to PXA Cloud or a local PXA Server through the same API contract.

## Priorities

- [ ] P0: Produce a secure Vite production image with working SPA routing.
- [ ] P0: Remove environment-specific API addresses from browser code.
- [ ] P1: Add API compatibility, connectivity, and background-job UX.
- [ ] P2: Include the Designer in the PXA Server all-in-one evaluation image.

## Dependencies

- [ ] Use the API container and Compose contract from `PXA.Api-Docker.md`.
- [ ] Use the canonical contract and version policy from `PXA.SDK-Roadmap.md`.
- [ ] Define the minimum compatible API version exposed by PXA Server.

## Container Image

- [ ] Add a multi-stage image that builds the Vite application with Node.js.
- [ ] Serve the generated assets from an unprivileged static web server.
- [ ] Configure SPA fallback to `index.html` for direct routes and browser refreshes.
- [ ] Add immutable caching for hashed assets and no-cache handling for runtime configuration.
- [ ] Add content security, frame, referrer, MIME-sniffing, and permissions headers.
- [ ] Add image metadata for product, version, revision, and compatible API range.

## API Configuration

- [ ] Use relative `/api` requests by default.
- [ ] Add runtime configuration so one image works in Cloud and On-Premise deployments.
- [ ] Remove hard-coded `localhost` URLs and development port labels from production paths.
- [ ] Keep the local Vite proxy available only for development.
- [ ] Detect unreachable, unauthorized, incompatible, and unhealthy API states.
- [ ] Show actionable UI states instead of blank pages or raw browser errors.

## Worker And Job UX

- [ ] Submit OCR and other long-running operations through the API only.
- [ ] Do not package or execute the OCR worker in the Designer container.
- [ ] Display queued, running, completed, failed, cancelled, and expired job states.
- [ ] Allow safe retry and cancellation where the API contract supports them.
- [ ] Preserve the current design when a background operation fails.

## PXA Server Integration

- [ ] Add the Designer service to the shared Docker Compose bundle.
- [ ] Route `/api` to PXA API and all other routes to the Designer.
- [ ] Verify operation behind TLS-terminating customer reverse proxies.
- [ ] Document base-path support and forwarded-header expectations.
- [ ] Add the built Designer assets to the later all-in-one evaluation image.

## Tests

- [ ] Run type checking, unit tests, linting, and the production build.
- [ ] Smoke-test direct navigation and refresh for all application routes.
- [ ] Test Cloud and local PXA Server runtime configuration.
- [ ] Test API offline, unauthorized, incompatible-version, and server-error states.
- [ ] Smoke-test Designer, migration, import, export, PDF Viewer, and Spreadsheet workflows.
- [ ] Verify responsive behavior on representative desktop and mobile viewports.
- [ ] Verify that production assets contain no hard-coded development hostnames.

## Acceptance Criteria

- [ ] The same Designer image works with Cloud and On-Premise API endpoints.
- [ ] Direct routes render correctly after browser refresh.
- [ ] API or worker failures never produce an unexplained blank page.
- [ ] The Designer contains no backend worker executable.
- [ ] The Compose bundle provides one stable URL for the complete product.
