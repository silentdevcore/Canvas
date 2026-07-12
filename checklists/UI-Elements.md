# UI Elements Implementation Checklist

## Overview
This checklist covers the implementation of additional UI elements for the PXA PDF Designer. Each element should follow the established pattern: update types, store logic, renderer, properties panel, sidebar, and codegen.

## Core Elements (High Priority)

### Image Element
- [x] Add 'Image' to ElementType in `store.ts`
- [x] Update store addElement logic with image defaults (src, width, height, alt)
- [x] Implement ImageRenderer in `ElementRenderer.tsx` with `<img>` tag
- [x] Add image property editors in `PropertiesPanel.tsx` (src URL, dimensions, fit mode)
- [x] Add Image to ELEMENTS array in `Sidebar.tsx`
- [x] Update `codegen.ts` to generate ImageElement C# code
- [x] Add image upload/file selection capability

### Shape Elements

#### Rectangle
- [x] Add 'Rectangle' to ElementType
- [x] Update store with rectangle properties (width, height, fillColor, strokeColor, strokeWidth)
- [x] Implement RectangleRenderer with `<div>` styled as rectangle
- [x] Add rectangle property editors (colors, dimensions, border radius)
- [x] Add Rectangle to sidebar
- [x] Update codegen for RectangleElement

#### Circle
- [x] Add 'Circle' to ElementType
- [x] Update store with circle properties (radius, centerX, centerY, fillColor, strokeColor)
- [x] Implement CircleRenderer with styled `<div>` (border-radius: 50%)
- [x] Add circle property editors
- [x] Add Circle to sidebar
- [x] Update codegen for CircleElement

#### Line
- [x] Add 'Line' to ElementType
- [x] Update store with line properties (x1, y1, x2, y2, strokeColor, strokeWidth, lineCap)
- [x] Implement LineRenderer with SVG `<line>` or styled `<div>`
- [x] Add line property editors
- [x] Add Line to sidebar
- [x] Update codegen for LineElement

### Advanced Elements (Medium Priority)

#### Link Element
- [x] Add 'Link' to ElementType
- [x] Update store with link properties (url, text, x, y, width, height)
- [x] Implement LinkRenderer with clickable styled element
- [x] Add link property editors (URL, display text, dimensions)
- [x] Add Link to sidebar
- [x] Update codegen for LinkAnnotation

#### List Element
- [x] Add 'List' to ElementType
- [x] Update store with list properties (items, ordered, markerStyle)
- [x] Implement ListRenderer with `<ul>`/`<ol>` and `<li>` elements
- [x] Add list property editors (add/remove items, toggle ordered)
- [x] Add List to sidebar
- [x] Update codegen for ListElement

#### PageBreak Element
- [x] Add 'PageBreak' to ElementType
- [x] Update store with page break properties (style indicator)
- [x] Implement PageBreakRenderer with visual indicator
- [x] Add page break to sidebar
- [x] Update codegen for page break logic

## Layout Elements (Medium Priority)

#### Grid/Container
- [x] Add 'Grid' to ElementType
- [x] Update store with grid properties (rows, columns, gap, alignment)
- [x] Implement GridRenderer with CSS Grid
- [x] Add grid property editors
- [x] Add Grid to sidebar
- [x] Update codegen for GridElement

#### Spacer
- [x] Add 'Spacer' to ElementType
- [x] Update store with spacer properties (width, height, flexGrow)
- [x] Implement SpacerRenderer with styled div
- [x] Add spacer property editors
- [x] Add Spacer to sidebar
- [x] Update codegen for SpacerElement

## Interactive Elements (Low Priority)

#### Button/Form Elements
- [x] Add 'Button' to ElementType
- [x] Update store with button properties (text, style, action)
- [x] Implement ButtonRenderer
- [x] Add button property editors
- [x] Add Button to sidebar
- [x] Update codegen (may not be applicable for PDF)

#### Checkbox/Radio
- [x] Add form input elements
- [x] Update store and rendering
- [x] Add property editors
- [x] Update codegen

## Implementation Guidelines

### Code Structure
- Follow existing patterns in the codebase
- Keep components modular and reusable
- Use TypeScript for type safety
- Maintain consistent naming conventions

### Properties System
- Extend the props system for each element type
- Provide sensible defaults
- Include validation where appropriate
- Support both simple and advanced properties

### Rendering
- Use appropriate HTML/CSS for visual representation
- Ensure elements are properly positioned and styled
- Consider responsive design for the designer interface
- Provide visual feedback for selection and editing

### Code Generation
- Generate valid C# code for the PXA PDF library
- Include all necessary properties and configurations
- Maintain code readability and structure
- Support nested element hierarchies

### Testing
- Test drag & drop functionality
- Test property editing
- Test code generation output
- Verify visual accuracy in designer

## Priority Implementation Order
1. Image (most requested)
2. Rectangle (basic shape)
3. Circle (complementary shape)
4. Line (vector element)
5. Link (interactive element)
6. List (text structure)
7. Grid/Container (layout)
8. PageBreak (document control)
9. Spacer (layout utility)
10. Advanced elements as needed

## Element Properties Panels
- [x] Text element properties (font, color, size, alignment, etc.)
- [x] Image element properties (src, dimensions, alt text)
- [x] Rectangle element properties (dimensions, colors, borders)
- [x] Circle element properties (radius, colors, stroke)
- [x] Line element properties (coordinates, stroke, cap style)
- [x] Link element properties (URL, text, dimensions)
- [x] List element properties (ordered/unordered, items management)
- [x] Table element properties (rows, columns, styling, cell data)
- [x] Column element properties (background, padding, gap, opacity)
- [x] PageBreak element properties (style, color, thickness)
- [x] Grid element properties (rows, columns, gap, justify/align, background)
- [x] Spacer element properties (dimensions, flex-grow, background)
- [x] Button element properties (text, style, action, colors, dimensions)
- [x] Checkbox element properties (label, checked, size, color)
- [x] Radio element properties (label, checked, group, size, color)

## Notes
- Each element should integrate seamlessly with the existing drag & drop system
- Consider performance implications for complex elements
- Maintain backward compatibility with existing designs
- Document any special implementation considerations