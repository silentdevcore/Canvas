# PXA Designer Scrollable Workspace

## Goal

Make page navigation, the document workspace, and both editor side panels adapt to wide and tall documents without clipping content. Both desktop side panels must match the complete editor-stage height and scroll internally when their content is taller than the stage.

## Priority

P0 usability fix for the PXA Designer.

## Scope

### 1. Horizontally Scrollable Page Navigation

- [x] Allow `.editor-page-strip` to scroll horizontally when all page thumbnails and actions do not fit.
- [x] Keep every page thumbnail, page number, and add-page action reachable without shrinking them below their stable dimensions.
- [x] Support mouse wheel/trackpad horizontal scrolling and touch panning.
- [x] Keep the active page visible after selection, page creation, duplication, deletion, and reordering.
- [x] Do not introduce a vertical scrollbar inside the page strip.
- [x] Render a custom horizontal scrollbar that remains visible independently of operating-system overlay settings.

### 2. Scrollable Page Grid

- [x] Allow `.editor-page-grid` to scroll left, right, up, and down when the scaled page is larger than the available editor workspace.
- [x] Preserve usable space around every page edge so elements at the page boundary remain selectable and resizable.
- [x] Keep page zoom centered while the page fits; switch to natural top/left overflow positioning when it exceeds the workspace.
- [x] Ensure wide landscape pages, oversized custom pages, and high zoom levels remain fully reachable.
- [x] Keep drag, resize, marquee selection, context menus, and drop coordinates correct after scrolling.
- [x] Prevent workspace scrolling from moving the tool or inspector panels unintentionally.
- [x] Keep a persistent page-viewport scrollbar visible with arrow controls and a keyboard-accessible slider.

### 3. Editor-Stage-Height Side Panels

- [x] Remove the fixed-height behavior from `.editor-panel.editor-tool-panel` and `.editor-panel.editor-inspector-panel`.
- [x] Set both desktop panels to the complete `.editor-stage` height, including its header, page viewport, and paging strip.
- [x] Observe `.editor-stage` with `ResizeObserver` and update both panels whenever its rendered height changes.
- [x] Scroll each panel internally when its controls require more space than the editor stage provides.
- [x] Keep panel backgrounds and borders exactly aligned to the editor-stage height.
- [x] Keep both panels aligned when page size, orientation, zoom, paging, or responsive layout changes.
- [x] Retain the existing responsive drawer behavior on tablet and mobile layouts.

## Accessibility And Interaction

- [x] Make scroll containers keyboard reachable and usable without a pointer.
- [x] Preserve visible focus indicators inside scrolled content.
- [x] Do not trap keyboard focus or wheel input in an empty scroll direction.
- [x] Keep scrollbars visible or discoverable according to the operating-system setting.
- [x] Do not depend on auto-hiding macOS native scrollbars for the two primary horizontal navigation surfaces.
- [x] Respect right-to-left UI direction when horizontal scrolling is used.

## Tests

- [x] Add component or DOM contract tests for page-strip horizontal overflow.
- [x] Add tests for two-axis page-grid overflow at high zoom and with a landscape/custom page.
- [x] Add tests that side-panel height follows the complete editor-stage height and retains internal overflow.
- [x] Verify page selection automatically reveals the active page thumbnail.
- [ ] Verify element drag, resize, marquee selection, and context-menu placement after horizontal and vertical scrolling.
- [x] Run `npm test` in `pxa-designer`. *(26 suites, 262 tests passed.)*
- [x] Run `npm run build` in `pxa-designer`.
- [ ] Perform desktop smoke tests at 1280x720, 1440x900, and 1920x1080.
- [ ] Perform mobile and tablet smoke tests without regressing drawer navigation.

## Acceptance Criteria

- [ ] Every page-navigation item remains reachable when the page strip is wider than its container.
- [ ] Every edge of an oversized or zoomed page can be reached by scrolling in both axes.
- [ ] Tool and inspector panels match the complete editor-stage height and scroll internally when their content is taller.
- [ ] Existing editing interactions remain accurate after scrolling.
- [ ] No editor control overlaps, clips, or causes an unintended page-level horizontal scrollbar.

## Out Of Scope

- [ ] Virtualizing pages or editor controls.
- [ ] Replacing the existing zoom model.
- [ ] Redesigning the tablet/mobile drawers.
- [ ] Changing PDF pagination or document page-break semantics.
