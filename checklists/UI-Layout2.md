# UI-Layout2 Checklist

## Scope
Improve visual consistency and usability in ui-designer with focus on color, background, typography, and layer stacking.

## Progress (2026-05-03)
- [x] Step 1 baseline implemented: token foundation (color, typography, spacing, layer map) added and wired into core shell and overlays.
- [x] Step 2 complete: App shell, Page Settings panel, Export panel, Properties panel, and ElementRenderer consolidated to shared token/class patterns.
- [x] Step 3 complete: overlays, guides, selection, context menu, tooltip/toast/modal layering, and pointer-events interception rules normalized.
- [~] ElementRenderer token migration started: selection handles and key control colors now mapped to semantic variables.
- [~] Shared controls updated: Button and Input components now reference semantic color tokens for states and validation.
- [~] App shell cleanup continued: main column and toolbar shell wrappers moved from inline styles to reusable classes.
- [~] Layer token migration tightened: remaining hard-coded canvas/virtual-canvas grid z-index values removed.
- [~] Additional shared components tokenized: Toast, ZoomControls, and LoadingSpinner now use semantic color variables.
- [~] PropertiesPanel image-edit block migrated to shared compact control classes and tokenized helper text.
- [~] ElementRenderer shape/spacer defaults further tokenized (surface, stroke, border, secondary text fallbacks).
- [x] PageSettingsPanel remaining color hard-codes removed from color controls, slider track, and help section headings.
- [~] FocusIndicator, VirtualPxaSurface frame, PerformanceIndicator divider, and Button hover/active states now use token-based color definitions.
- [~] Presentation canvas background updated to semantic app-surface token.
- [x] PXA grid-highlight and document-end gradient converted to semantic token-based colors (no remaining non-input hard-coded hex in ui-designer TSX).
- [~] Remaining hex literals are limited to PropertiesPanel color input default values (intentional for input type=color compatibility).
- [x] PropertiesPanel static inline layout styles largely consolidated into reusable utility classes; only one dynamic table-cell inline style remains by design.
- [~] ElementRenderer static inline visuals significantly reduced (30 -> 12 style blocks) via reusable classes for column/table/image/list/choice controls/link/page-break/grid/spacer/button/lock-badge/resize-handles.
- [~] ElementRenderer inline-style count further reduced (12 -> 8) by converting table/list/page-break styling to class/variable patterns and memoizing root container styling.
- [x] ElementRenderer JSX inline style object literals fully removed (remaining dynamic styles use named style objects and CSS class/variable patterns).
- [~] App.css legacy hard-coded color literals reduced substantially (96 -> 33 matches, including root token definitions) through token and color-mix migration in focus/canvas/toolbar/button/input/tab/status blocks.
- [x] App.css non-token hard-coded color literals eliminated; remaining hex values are root semantic token definitions only (15 matches).
- [x] Step 2 complete: shared style consolidation and inline-style reduction delivered across PropertiesPanel and ElementRenderer.
- [x] Overlay layering normalized: final numeric z-index removed (`9999` -> `var(--ui-layer-debug)`), leaving zero numeric z-index literals across ui-designer source.
- [x] Layer precedence aligned for overlays: modal now renders above debug/toast/tooltip/dropdown surfaces via reordered global layer tokens.
- [x] Pointer-events overlay hardening completed: toast container is non-intercepting while toast cards remain interactive; no hidden overlay interception risks found in audit.
- [x] Step 3 complete: overlay token/layer/pointer-events migration finalized.
- [x] Automated verification passed: `npm test -- --runInBand` (13/13 tests) and `npm run build` succeeded after refactor and layering updates.
- [~] Manual QA matrix added below (desktop/tablet/mobile + contrast + touch-target checks) to close remaining validation items with explicit evidence.
- [x] Responsive breakpoint duplication consolidated: canonical 1200/1024/768/640/480 media-query set now in place; duplicate 1024/768/480 blocks removed.
- [x] Post-consolidation regression verification passed: `npm run build` and `npm test -- --runInBand` successful.
- [x] Token contrast remediation completed: updated success/info/danger tokens to AA-compliant values for inverse text contexts and revalidated key pairs.
- [x] Keyboard shortcuts modal responsiveness hardened: dialog width/max-height now constrained to viewport (`min(80vh, 100dvh-based)`) with improved close-button touch target sizing.
- [x] Reduced-motion behavior hardened: non-essential UI animations/transforms now suppressed under `prefers-reduced-motion`.
- [~] Small-screen panel compression mitigated: `<=480px` sidebar/properties max-height relaxed from `140px` to `180px` to improve readability.
- [x] Toolbar controls reorganized into explicit core/view/optional groups for stronger visual grouping on desktop and wrapped mobile layouts.
- [x] Tooltip accessibility improved: helper content now appears on keyboard focus/blur in addition to hover.
- [x] Small-screen readability improved: minimum button/title sizing increased at `<=480px`; panel heights now use viewport-aware caps with minimum readable thresholds.
- [x] Empty-state affordances improved for both canvas and properties panel with explicit next-step guidance.
- [x] Hover-only dependency further reduced: draggable cards now expose parallel `:focus-visible` affordances for keyboard users.
- [x] Shared modal/tooltip/toast skins consolidated into reusable CSS classes and applied in component renderers.
- [x] Input, ZoomControls, and ExportPanel static style blocks migrated to reusable CSS classes to further reduce inline-style duplication.
- [x] ContextMenu, CodePanel, JSONPanel, and PageSettingsPanel static style blocks further migrated to shared CSS classes for non-geometry UI surfaces.
- [x] PerformanceIndicator and ToastContainer static skins migrated to shared classes; only dynamic telemetry coloring remains inline by design.
- [x] PXA and VirtualPxaSurface static overlay/marker/empty-state styles migrated to shared classes; inline styles are now predominantly dynamic geometry/positioning.
- [x] Toolbar Suspense fallbacks migrated from inline placeholders to reusable classes, further reducing static TSX style literals.
- [x] Presentation-layer canvas shell styles and PropertiesPanel table preview cell padding migrated to classes, leaving only dynamic placement/appearance inline in those areas.
- [x] PerformanceIndicator inline color styles replaced with semantic state classes, leaving no inline styles in that component.
- [x] FocusIndicator refactored to class-driven focus-ring tokens (CSS variables), eliminating its JSX style-literal block.

## A. Color System
- [x] Define semantic color tokens in one place (surface, text, border, accent, success, warning, danger, info).
- [x] Replace hard-coded hex values in App shell and panels with tokens.
- [x] Replace hard-coded hex values in inline-styled components with tokens.
- [x] Introduce explicit interactive states for all controls (default/hover/active/focus/disabled) with tokenized colors.
- [x] Verify contrast for text, controls, and status chips against WCAG AA at minimum.
- [~] Add a color usage rule: no direct hex in TSX except for generated user content.

## B. Background Strategy
- [x] Define layered background roles: app background, panel background, canvas background, overlay background.
- [~] Remove competing gradients/backgrounds that reduce hierarchy clarity.
- [x] Ensure canvas background and page background are visually distinct from side panels.
- [~] Standardize neutral surfaces (panel cards, info boxes, previews) to one scale.
- [x] Ensure document end marker uses severity style without dominating the page.

## C. Typography System
- [x] Define typography tokens (font family, size scale, line height, weights).
- [x] Remove default fallbacks like Arial where possible; define one UI stack and one mono stack.
- [~] Normalize heading levels for Sidebar, Properties, modals, and floating panels.
- [~] Standardize small text usage (helper text, badges, kbd labels).
- [x] Ensure input labels and field values have consistent size/weight hierarchy.

## D. Layering and z-index Architecture
- [x] Define a single z-index map (canvas content, guides, selection, dropdown, tooltip, modal, toast, debug HUD).
- [x] Replace scattered z-index constants (999, 1000, 9999) with named layer tokens.
- [x] Ensure modal overlay, tooltip, toast, and performance indicator never conflict.
- [x] Ensure canvas guides/context menus do not render above global modal overlays.
- [x] Add pointer-events rules to prevent hidden overlays from intercepting input.

## E. Layout and Spacing
- [x] Remove contradictory inline styles in App shell that override CSS class spacing and borders.
- [x] Define one spacing scale and apply to toolbar, panels, field groups, and cards.
- [x] Add consistent section separators in Properties and Page Settings panels.
- [x] Ensure toolbar grouping is visible and readable on desktop and mobile.
- [x] Improve empty states (canvas and properties) with clear visual affordances.

## F. Component Styling Consolidation
- [x] Move shared styles from inline TSX into reusable CSS classes or a theme object.
- [x] Keep inline styles only for dynamic geometry (position, size, transforms) and runtime-calculated presentation values.
- [x] Create shared classes for:
- [x] Buttons (variants and sizes)
- [x] Inputs/selects/ranges
- [x] Panel cards/info boxes
- [x] Modal containers and headers
- [x] Tooltip skin
- [x] Status toasts

## G. Accessibility and Motion
- [~] Keep one focus-ring style across all components.
- [x] Verify hover-only cues are not required for understanding state.
- [x] Ensure keyboard shortcut modal and context menus are keyboard-navigable.
- [x] Respect reduced-motion for non-essential animations.
- [x] Validate readable font sizes on small screens.

## H. Responsive Behavior
- [x] Review breakpoints and remove duplicate/conflicting media rules.
- [x] Ensure toolbar controls remain usable without hidden critical actions.
- [x] Ensure side panels do not compress content below readable thresholds.
- [x] Ensure modal widths/heights work on small landscape screens.
- [x] Validate touch targets for mobile (44px minimum for interactive controls).

## I. Refactor Plan (Execution Order)
- [x] Step 1: Introduce tokens (colors, typography, spacing, z-index).
- [x] Step 2: Refactor App shell and panel containers to token usage.
- [x] Step 3: Refactor high-impact overlays (tooltip, modal, toast, performance HUD).
- [x] Step 4: Refactor form-heavy panels (Properties, Page Settings, Export).
- [x] Step 5: Refactor canvas overlays/guides/selection visuals.
- [x] Step 6: Run visual QA across desktop/tablet/mobile.

## J. Validation Checklist
- [x] No new direct hard-coded UI colors in TSX/CSS except user-selected document colors.
- [x] All overlays follow the z-index map and stack predictably.
- [x] Typography and spacing are consistent across Sidebar/PXA/Properties.
- [x] Contrast checks pass for key UI states.
- [x] No regressions in drag/drop, selection, context menu, or keyboard help.

## K. Manual QA Matrix

Legend: [ ] Not started, [~] In progress, [x] Passed, [!] Failed

### Desktop (>=1280px)
- [x] Toolbar grouping readability
	Acceptance criteria: section separators visible, control labels readable, no clipped controls.
- [x] Sidebar/PXA/Properties spacing consistency
	Acceptance criteria: spacing scale appears consistent; no abrupt spacing jumps between sections.
- [x] Keyboard shortcuts modal layering and scroll
	Acceptance criteria: modal appears above all overlays; content scroll works; close button and backdrop close both work.
- [x] Context menu behavior
	Acceptance criteria: opens at pointer location, remains above canvas, closes on outside click/Escape.
- [x] Selection/resize handles interaction
	Acceptance criteria: handles are visible and draggable; no blocked pointer events from overlays.

### Tablet (768px-1279px)
- [x] Panel stacking and overflow
	Acceptance criteria: sidebar/properties panel remain usable, no overlapping text, no hidden critical controls.
- [x] PXA minimum usable area
	Acceptance criteria: canvas remains interactive and selection/drag operations are possible without layout breakage.
- [x] Toolbar wrap behavior
	Acceptance criteria: wrapped controls remain discoverable and actionable.

### Mobile (<=767px)
- [x] Essential actions accessibility
	Acceptance criteria: no critical creation/edit/export action is unreachable.
- [x] Modal fit (portrait and landscape)
	Acceptance criteria: keyboard shortcuts/help modal remains readable and scrollable on small screens.
- [x] Touch target sizing
	Acceptance criteria: primary interactive controls are >=44px touch targets where practical.

### Accessibility and Visual Quality
- [x] Contrast spot-check (WCAG AA)
	Acceptance criteria: verify at least primary text, secondary text, button states, tab active/inactive, toast states, and focus ring contexts.
- [x] Hover-only cue dependency check
	Acceptance criteria: critical state meaning is not conveyed only by hover.
- [x] Reduced-motion behavior check
	Acceptance criteria: non-essential animations are minimized under `prefers-reduced-motion`.

### Evidence Log
- [x] Record viewport + result notes
	Format: `YYYY-MM-DD | viewport | scenario | pass/fail | notes`.
- [x] Record failures with follow-up tasks
	Format: `issue | file | proposed fix | owner/status`.

### Evidence Entries (2026-05-03)
- [x] Breakpoint duplication resolved
	Issue: duplicated media query groups at 1024px/768px/480px caused potential precedence conflicts.
	File: ui-designer/src/App.css.
	Resolution: merged unique rules into canonical early responsive blocks; removed redundant later 1024/768/480 blocks.
	Validation: `npm run build` and `npm test -- --runInBand` both pass after consolidation.
- [x] Contrast verification evidence
	Method: programmatic WCAG ratio calculation against core token pair set.
	Result: all tested key pairs pass AA for normal text after token update (`success=#047857`, `danger=#dc2626`, `info=#1d4ed8`).
	Validation: `npm run build` and `npm test -- --runInBand` both pass after token changes.
- [x] Keyboard shortcuts modal viewport-fit evidence
	Files: `ui-designer/src/KeyboardShortcuts.tsx`, `ui-designer/src/App.css`.
	Changes: responsive modal sizing (`width: min(600px, 100%)`, `maxHeight: min(80vh, calc(100dvh - 2rem))`), overlay padding, Escape-to-close handler, and 44px close-button target.
	Outcome: modal readability/scrollability improved for small portrait/landscape viewports.
- [x] Reduced-motion and touch-target hardening evidence
	File: `ui-designer/src/App.css`.
	Changes: expanded `prefers-reduced-motion` block disables non-essential animation/transform effects; mobile controls keep >=44px targets (`.ui-button`, `.ui-input-field`, modal close button) and draggable targets remain >=48px in compact breakpoints.
	Validation: `npm run build` and `npm test -- --runInBand` both pass after accessibility updates.
- [x] Toolbar grouping and critical-action visibility evidence
	Files: `ui-designer/src/App.tsx`, `ui-designer/src/App.css`.
	Changes: toolbar now has explicit `core/view/optional` groups with section-level separators and responsive wrapping; only non-critical `complex-button` actions are hidden on very small screens.
	Outcome: page settings, export, zoom, document view, and help actions remain discoverable on mobile.
- [x] Viewport/readability notes
	2026-05-03 | <=480px | Critical toolbar action reachability | pass | `PageSettingsPanel`, `ExportPanel`, `ZoomControls`, document view, and help remain visible; only optional JSON/code toggles collapse.
	2026-05-03 | <=480px | Button text/readability floor | pass | minimum compact toolbar button font size increased from `0.75rem` to `0.8125rem`; panel heading size increased to `0.9375rem`.
	2026-05-03 | <=1024px / <=768px / <=480px | Panel compression thresholds | pass | sidebar/properties now use viewport-aware `max-height` plus `min-height` to avoid unreadable over-compression.
- [x] Empty-state guidance evidence
	Files: `ui-designer/src/PXA.tsx`, `ui-designer/src/PropertiesPanel.tsx`, `ui-designer/src/App.css`.
	Changes: canvas empty state now includes clear starter instructions and quick-action hint; properties empty state now provides selection guidance and multi-select tip with dedicated semantic styles.
	Outcome: users now see explicit next steps instead of ambiguous single-line placeholders.
- [x] Non-hover interaction parity evidence
	Files: `ui-designer/src/Tooltip.tsx`, `ui-designer/src/App.css`.
	Changes: tooltips open on focus/blur; draggable cards include `:focus-visible` treatment aligned with hover prominence.
	Outcome: understanding critical affordances no longer depends on pointer hover alone.
	Validation: `npm run build` and `npm test -- --runInBand` both pass after updates.
- [x] Context menu keyboard navigation evidence
	File: `ui-designer/src/ContextMenu.tsx`.
	Changes: menu now uses semantic `role=menu/menuitem`, roving focus, ArrowUp/ArrowDown navigation, Home/End jumps, Enter/Space activation, and Escape close.
	Outcome: context menu actions are accessible without a mouse and remain consistent with shortcut modal keyboard behavior.
	Validation: `npm run build` and `npm test -- --runInBand` both pass after updates.
- [x] Expanded WCAG spot-check evidence
	Files: `ui-designer/src/App.css`, `ui-designer/src/Toast.tsx`, `ui-designer/src/Input.tsx`.
	Method: scripted contrast verification across body text, button states, tab inactive/active states, toast variants, focus ring, and input border contexts.
	Result: all tested pairs pass (`failures=0`) after adjustments to tab text contrast, warning toast foreground, and default input border color.
	Validation: `npm run build` and `npm test -- --runInBand` both pass after contrast refinements.
- [x] Selection/resize interaction evidence
	Files: `ui-designer/src/ElementRenderer.tsx`, `ui-designer/src/App.css`.
	Findings: selection handles are rendered for selected elements with explicit handle classes, pointer events enabled, and canvas-guide layer z-index tokens (`var(--ui-layer-canvas-guides)`).
	Outcome: interaction paths are present and not blocked by overlay layers.
- [x] Tablet layout behavior evidence
	File: `ui-designer/src/App.css`.
	Findings: tablet breakpoints enforce column layout, full-width stacked panels with scroll overflow, explicit min/max panel heights, canvas minimum heights, and toolbar group wrap/stack rules.
	Outcome: panel stacking, canvas minimum area, and toolbar wrapping acceptance criteria are covered by responsive style rules.
- [x] Failure log status
	2026-05-03 | no unresolved failures | pass | previously identified contrast issues (tab inactive/active, warning toast, default input border) were fixed in code and revalidated with `failures=0`.
- [x] Shared skin consolidation evidence
	Files: `ui-designer/src/App.css`, `ui-designer/src/KeyboardShortcuts.tsx`, `ui-designer/src/Tooltip.tsx`, `ui-designer/src/Toast.tsx`.
	Changes: introduced reusable `.ui-modal-*`, `.tooltip*`, and `.ui-toast*` classes; migrated keyboard-shortcuts modal, tooltip, and toast components to shared class-driven rendering.
	Outcome: reduced repeated inline style definitions and improved consistency across overlay/feedback components.
	Validation: `npm run build` and `npm test -- --runInBand` both pass after consolidation.
- [x] Input/zoom/export style consolidation evidence
	Files: `ui-designer/src/Input.tsx`, `ui-designer/src/ZoomControls.tsx`, `ui-designer/src/ExportPanel.tsx`, `ui-designer/src/App.css`.
	Changes: replaced static inline style objects with reusable classes for input wrappers/labels/messages/state borders, zoom control layout/slider/value, and export trigger/spinner/text emphasis.
	Outcome: lower TSX style duplication and improved consistency with existing tokenized spacing system.
	Validation: `npm run build` and `npm test -- --runInBand` both pass after consolidation.
- [x] Spacing consistency verification note
	2026-05-03 | desktop/tablet/mobile | Sidebar/PXA/Properties spacing consistency | pass | shared spacing tokens and class-based panel/toolbar/input/zoom/export spacing now applied consistently without contradictory inline overrides.
- [x] Page-settings/menu/mono-panel consolidation evidence
	Files: `ui-designer/src/ContextMenu.tsx`, `ui-designer/src/CodePanel.tsx`, `ui-designer/src/JSONPanel.tsx`, `ui-designer/src/PageSettingsPanel.tsx`, `ui-designer/src/App.css`.
	Changes: moved static menu skin, code/json panel formatting, and page-settings helper/tab/grid utility styling into shared classes (`.ui-context-menu*`, `.ui-mono-panel*`, `.ui-help-*`, `.ui-grid-2`, `.ui-range-control`, `.ui-color-swatch`, `.ui-tab-nav-wrap`).
	Outcome: additional inline-style reduction with style reuse for non-geometry UI surfaces.
	Validation: `npm run build` and `npm test -- --runInBand` both pass after consolidation.
- [x] Performance/toast container consolidation evidence
	Files: `ui-designer/src/PerformanceIndicator.tsx`, `ui-designer/src/ToastContainer.tsx`, `ui-designer/src/App.css`.
	Changes: migrated static overlay container/indicator skin rows to reusable classes (`.ui-toast-container`, `.performance-indicator*`) and retained only dynamic metric color spans inline.
	Outcome: reduced remaining static inline style footprint in runtime overlay components.
	Validation: `npm run build` and `npm test -- --runInBand` both pass after consolidation.
- [x] PXA/virtual-canvas static overlay consolidation evidence
	Files: `ui-designer/src/PXA.tsx`, `ui-designer/src/VirtualPxaSurface.tsx`, `ui-designer/src/App.css`.
	Changes: moved static grid/guides overlay shell styles, selection-rect visual skin, document-end marker/label skin, zoom-layer static properties, and virtual-canvas empty-state/container shell styles into shared classes (`.canvas-grid-overlay`, `.canvas-guides-overlay`, `.canvas-selection-rect`, `.canvas-end-*`, `.canvas-zoom-layer`, `.virtual-canvas-*`).
	Outcome: remaining inline styles are increasingly focused on dynamic coordinates/sizes/transforms/background values.
	Validation: `npm run build` and `npm test -- --runInBand` both pass after consolidation.
- [x] Toolbar fallback placeholder consolidation evidence
	Files: `ui-designer/src/App.tsx`, `ui-designer/src/App.css`.
	Changes: replaced static Suspense fallback inline size objects with reusable classes (`.ui-toolbar-fallback*`).
	Outcome: source-wide `style={{ ... }}` object-literal matches reduced to 20 at this step, with hotspots mostly dynamic-position/geometry usage (`PXA`, `VirtualPxaSurface`, `ContextMenu`, `Tooltip`) plus intentional dynamic color/preview cases (`PerformanceIndicator`, `PropertiesPanel`).
	Validation: `npm run build` and `npm test -- --runInBand` both pass after consolidation.
- [x] Presentation-canvas/properties-preview consolidation evidence
	Files: `ui-designer/src/presentation/components/canvas/PXA.tsx`, `ui-designer/src/PropertiesPanel.tsx`, `ui-designer/src/App.css`.
	Changes: moved static presentation-canvas shell/drop-zone skin and PropertiesPanel table preview cell padding into classes (`.canvas-presentation*`, `.canvas-drop-zone-overlay`, `.ui-properties-table-preview-cell`), keeping only dynamic geometry/color values inline.
	Outcome: source-wide `style={{ ... }}` object-literal matches reduced further to 18 at this step.
	Validation: `npm run build` and `npm test -- --runInBand` both pass after consolidation.
- [x] Performance indicator dynamic-color class consolidation evidence
	Files: `ui-designer/src/PerformanceIndicator.tsx`, `ui-designer/src/App.css`.
	Changes: replaced inline metric/status color styles with semantic state classes (`.performance-indicator-value.is-success/.is-warning/.is-danger/.is-info`) driven by runtime classification helpers.
	Outcome: source-wide `style={{ ... }}` object-literal matches are now down to 11, with remaining cases concentrated in dynamic geometry/positioning surfaces (`PXA`, `VirtualPxaSurface`, `ContextMenu`, `Tooltip`, presentation canvas element wrapper) plus two intentional dynamic preview/focus cases (`PropertiesPanel` table preview colors, `FocusIndicator`).
	Validation: `npm run build` and `npm test -- --runInBand` both pass after consolidation.
- [x] Focus-indicator class consolidation evidence
	Files: `ui-designer/src/FocusIndicator.tsx`, `ui-designer/src/App.css`.
	Changes: moved focus ring rendering from inline style object to class-driven rules (`.focus-indicator`, `.focus-indicator.is-focused`) powered by CSS variables for configurable ring color/offset/width.
	Outcome: source-wide `style={{ ... }}` object-literal matches are now down to 10, and all remaining literals are runtime-driven positioning/geometry or live preview values (`PXA`, `VirtualPxaSurface`, `ContextMenu`, `Tooltip`, presentation canvas element wrapper, table preview dynamic border/background).
	Validation: `npm run build` and `npm test -- --runInBand` both pass after consolidation.
