# UI-Template Checklist

## Scope
Create a dynamic template system similar to CraftMyPDF where users design reusable templates and generate final PDFs by merging template layout with runtime JSON data.

## Definition of Done
- [x] Users can create a template visually (drag/drop + property editing).
- [x] Template supports bindings, conditionals, loops, and formatters.
- [x] Template can be versioned and previewed with sample payloads.
- [x] Template can be rendered into valid PDF output through API and batch workflows.
- [x] Validation, error reporting, audit logs, and tests are in place.

## A. Product and Functional Requirements
- [x] Define supported template use cases (invoice, quote, report, certificate, label).
- [x] Define required dynamic features:
- [x] Data binding (text, image, barcode/QR, table).
- [x] Conditional visibility and conditional styling.
- [x] Repeating sections and table rows.
- [x] Basic expressions and formatting (date, number, currency, uppercase, truncation).
- [x] Pagination controls (keep together, repeat header, page break hints).
- [x] Define non-functional requirements:
- [x] Maximum pages and element count per template.
- [x] Render latency targets (single and batch).
- [x] Reliability target (error budget and retry policy).

## B. Template Data Model
- [x] Define canonical template schema (JSON) with explicit version field.
- [x] Define node/element model:
- [x] Stable IDs, type, position, size, z-order, style, bindings.
- [x] Parent-child and grouping semantics.
- [x] Define page model:
- [x] Page size, orientation, margins, bleed/safe zones.
- [x] Header/footer and page numbering placeholders.
- [x] Define dynamic model:
- [x] Binding path syntax (example: customer.name).
- [x] Conditional expression fields.
- [x] Repeat descriptors (array source + item alias).
- [x] Define schema migration rules between versions.

## C. Designer UX (UI-Designer)
- [x] Add template mode in designer (new/open/save/version).
- [x] Add Data panel:
- [x] Declare sample JSON payload.
- [x] Browse payload paths and insert bindings.
- [x] Display missing/invalid path indicators.
- [x] Add Binding editor in properties panel:
- [x] Bind value, fallback value, formatter chain.
- [x] Conditional visibility and conditional style controls.
- [x] Repeat controls for container/table elements.
- [x] Add template metadata editor:
- [x] Template name, category, locale, currency, owner tags.
- [x] Add preview states:
- [x] Design mode (placeholders).
- [x] Data preview mode (resolved values).
- [x] Error preview mode (missing fields and expression errors).

## D. Expression and Binding Engine
- [x] Implement safe expression evaluator (no arbitrary code execution).
- [x] Implement binding resolver with dot-path and array index support.
- [x] Implement formatter library:
- [x] Date/time formatter.
- [x] Number/currency formatter.
- [x] Text helpers (trim, case, truncate).
- [x] Implement fallback and null-handling behavior.
- [x] Implement deterministic evaluation order for nested/repeated elements.
- [x] Add cycle and recursion guards.

## E. Rendering Pipeline
- [x] Define render stages:
- [x] Parse and validate template.
- [x] Expand dynamic sections (conditions/loops).
- [x] Resolve styles and layout.
- [x] Render to PDF backend.
- [x] Add text measurement and overflow strategy:
- [x] Wrap, clip, ellipsis, shrink-to-fit policies.
- [x] Add image resolution/fetch policy (remote/local/cache/timeouts).
- [x] Add page-break and orphan/widow control for repeating blocks.
- [x] Ensure deterministic rendering across environments.

## F. API and Integration
- [x] Define template APIs:
- [x] Create template.
- [x] Update template.
- [x] Get template/version history.
- [x] Validate template.
- [x] Define render APIs:
- [x] Render with payload (sync for small jobs).
- [x] Render batch (async jobs + status endpoint).
- [x] Webhook callbacks for async completion.
- [x] Define idempotency and deduplication strategy.
- [x] Define authentication/authorization model per tenant/project.

## G. Validation and Error Handling
- [x] Schema validation for template and payload.
- [x] Preflight validation report before rendering:
- [x] Missing binding paths.
- [x] Type mismatches.
- [x] Invalid expressions or formatter arguments.
- [x] Runtime validation report during render:
- [x] Element-level warnings/errors with IDs and page references.
- [x] Friendly error messages in UI and API response.
- [x] Partial render policy (fail-fast vs best-effort) configurable.

## H. Security and Compliance
- [x] Enforce sandboxed expression engine.
- [x] Restrict remote asset domains and content types.
- [x] Add input size limits and payload sanitization.
- [x] Add tenant isolation controls for templates and assets.
- [x] Add secrets handling policy for API keys and webhooks.
- [x] Add audit trail for template changes and render requests.

## I. Performance and Scalability
- [x] Add template compile/cache layer for repeated renders.
- [x] Add asset cache with TTL and invalidation.
- [x] Add render worker queue for async jobs.
- [x] Add concurrency control and backpressure strategy.
- [x] Benchmark targets:
- [x] Small payload single render.
- [x] Large payload single render.
- [x] Batch render throughput.
- [x] Add memory and timeout guards to prevent runaway jobs.

## J. Testing Strategy
- [x] Unit tests:
- [x] Binding path resolver.
- [x] Expression evaluator.
- [x] Formatter library.
- [x] Conditional/repeat expansion.
- [x] Integration tests:
- [x] Template save/load/version cycle.
- [x] Render API with realistic payloads.
- [x] Batch job lifecycle and webhooks.
- [x] Golden snapshot tests for PDF output consistency.
- [x] Negative tests for invalid templates/payloads.
- [x] Security tests for expression sandbox and asset restrictions.

## K. Observability and Operations
- [x] Add structured logs for template compile and render stages.
- [x] Add metrics:
- [x] Render success rate.
- [x] P50/P95/P99 render latency.
- [x] Queue depth and retry rates.
- [x] Asset fetch failures.
- [x] Add trace IDs from API request to final render artifact.
- [x] Add operational runbook for incident response.

## L. Rollout Plan
- [x] Phase 1: Static template with simple bindings.
- [x] Phase 2: Conditionals and formatters.
- [x] Phase 3: Repeats/tables and pagination controls.
- [x] Phase 4: Batch rendering, webhooks, and versioning.
- [x] Phase 5: Hardening (security, performance, observability).
- [x] Feature flag rollout and staged tenant enablement.

## M. Acceptance QA Matrix
Legend: [ ] Not started, [~] In progress, [x] Passed, [!] Failed

### Authoring
- [x] Create/edit/save template
- [x] Insert data bindings from payload browser
- [x] Configure conditions and repeat blocks

### Preview
- [x] Render preview with valid payload
- [x] Display missing-field warnings clearly
- [x] Stable visual output across repeated previews

### API
- [x] Sync render returns valid PDF bytes/file
- [x] Async batch render job completes with callback
- [x] Error payload contains actionable diagnostics

### Reliability
- [x] Retry behavior works for transient failures
- [x] Idempotency prevents duplicate renders
- [x] Queue and worker recovery validated

### Security
- [x] Expression sandbox rejects unsafe constructs
- [x] Unauthorized template access blocked
- [x] Remote asset restrictions enforced

## N. Required Property Matrix (Gap to Dynamic Templates)

### 🎭 Demo Gallery and Showcase Features (Added)
- [x] Demo template gallery with professional examples
- [x] Category filtering and template preview
- [x] Sample data payloads for each template type
- [x] Interactive feature explanations and use cases
- [x] One-click template loading and testing

Implementation targets:
- [x] ui-designer/src/demo/DemoTemplates.ts: professional template examples
- [x] ui-designer/src/DemoGallery.tsx: interactive gallery component
- [x] Professional Invoice, Certificate, and Report templates
- [x] Feature showcase with data binding, expressions, conditionals, repeats

### 🚀 One-Click Deployment and DevOps (Added)
- [x] Automated deployment script with prerequisite checks
- [x] Service management (start/stop/restart/cleanup)
- [x] Production build options and optimizations
- [x] Health monitoring and logging integration
- [x] Cross-platform compatibility (macOS, Linux, Windows)

Implementation targets:
- [x] deploy.sh: comprehensive deployment automation
- [x] Service orchestration and dependency management
- [x] Production vs development environment handling
- [x] Automated testing and validation in deployment

### 📚 Complete Documentation Suite (Added)
- [x] Comprehensive README.md with setup and API docs
- [x] PROJECT_SUMMARY.md with architecture and achievements
- [x] Performance benchmarks and optimization metrics
- [x] Deployment guides for Docker and cloud platforms
- [x] Contributing guidelines and development workflow

Implementation targets:
- [x] README.md: complete project documentation
- [x] PROJECT_SUMMARY.md: executive overview and roadmap
- [x] Performance metrics and benchmark tracking
- [x] Multi-platform deployment instructions

### 🔧 Advanced Template Engine Features (Enhanced)
- [x] Template literal support in expressions
- [x] Nullish coalescing and optional chaining
- [x] Array and object utility functions
- [x] Date and time manipulation functions
- [x] String processing and formatting helpers

Implementation targets:
- [x] ui-designer/src/template/expressionEngine.ts: enhanced evaluation
- [x] Safe execution environment with utility libraries
- [x] Template literal interpolation support
- [x] Advanced JavaScript expression features

### 📊 Enterprise Observability Stack (Enhanced)
- [x] Multi-level caching with intelligent invalidation
- [x] Performance monitoring and bottleneck detection
- [x] Error categorization and actionable diagnostics
- [x] Request tracing and correlation IDs
- [x] Health checks and system status monitoring

Implementation targets:
- [x] ui-designer/src/template/cache.ts: intelligent caching
- [x] ui-designer/src/observability/: logging and metrics
- [x] Performance monitoring and alerting
- [x] Comprehensive error tracking and reporting

### 🏗️ Clean Architecture Implementation (Validated)
- [x] Domain-driven design with proper separation
- [x] CQRS pattern for optimized read/write operations
- [x] Repository pattern with abstraction layers
- [x] Dependency injection and inversion of control
- [x] SOLID principles throughout codebase

Implementation targets:
- [x] src/Core/PXA.Core/: domain entities and abstractions
- [x] src/Core/PXA.Application/: use cases and business logic
- [x] PXA.WebApi/: clean API layer
- [x] Proper architectural boundaries and patterns

### 1) Binding Properties (Critical)
- [x] Add dataPath per bindable field (example: customer.name).
- [x] Add fallbackValue.
- [x] Add required flag and requiredMessage.
- [x] Add valueType hint (string, number, boolean, date, image-url).
- [x] Add bindingScope (root, loop-item, parent).

Implementation targets:
- [x] ui-designer/src/PropertiesPanel.tsx: add Binding section for each relevant element.
- [x] ui-designer/src/store.ts: persist binding properties in element props and template export.
- [x] ui-designer/src/ElementRenderer.tsx: resolve preview values from sample payload.
- [x] ui-designer/src/domain/value-objects/ElementType.ts: mark which elements are bindable.

### 2) Expression and Conditional Properties (Critical)
- [x] Add visibleWhen expression.
- [x] Add enabledWhen expression (for interactive controls where relevant).
- [x] Add valueExpression for computed text/value.
- [x] Add styleExpression map (color, fontWeight, background, opacity).
- [x] Add safeExpressionMode flag and validation errors.

Implementation targets:
- [x] ui-designer/src/PropertiesPanel.tsx: add conditional/expression editors.
- [x] ui-designer/src/store.ts: store expressions and expression validation state.
- [x] ui-designer/src/ElementRenderer.tsx: evaluate expressions for preview mode.
- [x] Add new module ui-designer/src/template/expressionEngine.ts for safe evaluation.

### 3) Repeat and Collection Properties (Critical)
- [x] Add repeatSource path (array source).
- [x] Add itemAlias and indexAlias.
- [x] Add emptyBehavior (hide, show-placeholder, keep-template).
- [x] Add maxItems and pageBreakBetweenItems options.
- [x] Add rowTemplateMode for table/list/grid repeat semantics.

Implementation targets:
- [x] ui-designer/src/PropertiesPanel.tsx: add repeat controls for container/table/list/grid elements.
- [x] ui-designer/src/store.ts: persist repeat descriptors.
- [x] ui-designer/src/ElementRenderer.tsx: preview repeated instances with sample payload.
- [x] Add new module ui-designer/src/template/repeatExpander.ts.

### 4) Formatting Properties (High)
- [x] Add formatter pipeline (ordered list).
- [x] Add formatter arguments and defaults.
- [x] Add locale and currency override.
- [x] Add timezone override for datetime formatting.
- [x] Add number precision and rounding mode.

Implementation targets:
- [x] ui-designer/src/PropertiesPanel.tsx: formatter builder UI.
- [x] ui-designer/src/store.ts: formatter config persistence.
- [x] ui-designer/src/ElementRenderer.tsx: formatter preview.
- [x] Add new module ui-designer/src/template/formatters.ts.

### 5) Overflow and Layout Behavior Properties (High)
- [x] Add textOverflow policy (wrap, clip, ellipsis, shrink).
- [x] Add maxLines and lineClamp.
- [x] Add keepTogether flag for grouped content.
- [x] Add avoidPageBreakInside for block elements.
- [x] Add anchor/alignment options for absolute elements.

Implementation targets:
- [x] ui-designer/src/PropertiesPanel.tsx: overflow/pagination controls.
- [x] ui-designer/src/ElementRenderer.tsx: preview overflow behavior.
- [x] PXA/Pdf renderer pipeline: final pagination behavior parity with preview.

### 6) Image-Specific Dynamic Properties (High)
- [x] Add imageFit mode (contain, cover, fill, none).
- [x] Add crop and focal point properties.
- [x] Add remoteFetchPolicy (allowlist, timeout, retry).
- [x] Add placeholder and fallback image strategy.
- [x] Add preserveAspectRatio toggle.

Implementation targets:
- [x] ui-designer/src/PropertiesPanel.tsx: image dynamic/render controls.
- [x] ui-designer/src/ElementRenderer.tsx: image preview behavior.
- [x] PDF/image backend code: apply same fit/crop semantics in final render.

### 7) Table and List Dynamic Properties (High)
- [x] Add tableDataPath/listDataPath.
- [x] Add headerRepeatOnPageBreak.
- [x] Add per-column binding and formatter.
- [x] Add rowStriping and conditional row styles.
- [x] Add emptyRowsPolicy.

Implementation targets:
- [x] ui-designer/src/PropertiesPanel.tsx: table/list data-binding editors.
- [x] ui-designer/src/ElementRenderer.tsx: preview with array payload.
- [x] PDF renderer: paginated table/list rendering parity.

### 8) Validation and Diagnostics Properties (High)
- [x] Add elementValidationMode (strict, warn, ignore).
- [x] Add customErrorMessage per binding/expression.
- [x] Add debugLabel and diagnosticId per element.
- [x] Add preflight status fields for missing paths/type errors.

Implementation targets:
- [x] ui-designer/src/PropertiesPanel.tsx: validation and diagnostics section.
- [x] ui-designer/src/store.ts: diagnostics state storage.
- [x] Add new module ui-designer/src/template/validation.ts.

### 9) Template Metadata and Versioned Properties (Medium)
- [x] Add templateVersion and schemaVersion.
- [x] Add createdBy/updatedBy metadata.
- [x] Add locale defaults and formatting profile.
- [x] Add migrationHints for backward compatibility.

Implementation targets:
- [x] ui-designer/src/infrastructure/repositories/LocalStorageTemplateRepository.ts: metadata persistence.
- [x] Template repository interface and API layer: version-aware save/load.

### 10) Element Parity Additions (Medium)
- [x] Add QRCode element properties (value, ecc level, size, quiet zone).
- [x] Add Barcode element properties (symbology, value, checksum, width/height).
- [x] Add Signature element properties (label, signerNamePath, datePath, imagePath).
- [x] Add RichText/HTML block properties (sanitized html, style profile).

Implementation targets:
- [x] ui-designer/src/store.ts and ui-designer/src/domain/value-objects/ElementType.ts: add new element types.
- [x] ui-designer/src/Sidebar.tsx: expose new draggable elements.
- [x] ui-designer/src/PropertiesPanel.tsx: property editors for each new element type.
- [x] ui-designer/src/ElementRenderer.tsx: preview rendering branches.

### Gate to Start Implementation
- [x] Freeze and approve this property matrix as MVP-v1 scope.
- [x] Mark each property group with owner and phase in rollout section.
- [x] Add test cases per property group in section J before coding.

## Notes
- [x] Keep all template features deterministic and auditable.
- [x] Prefer explicit schema versions and migrations over implicit behavior changes.
- [x] Treat preview and final render pipelines as equivalent to avoid surprises.
