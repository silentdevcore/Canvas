# UI Designer Implementation Checklist

## Step 2 — Implementation Plan
- [x] 1. Setup React project
- [x] 2. Create layout structure
- [x] 3. Implement drag & drop
- [x] 4. Implement JSON state
- [x] 5. Implement property editor
- [x] 6. Implement code generator
- [x] 7. Connect everything

---

## Step 3 — MVP Implementation
### Components
- [x] App
- [x] Sidebar (elements)
- [x] Canvas
- [x] ElementRenderer
- [x] PropertiesPanel

### Features
- [x] Add elements (Text, Column, Table)
- [x] Nest elements
- [x] Select element
- [x] Edit properties (text, font size, table data)
- [x] Show JSON live
- [x] Generate C# code

---

## Drag & Drop
- [x] Use a modern library (e.g. dnd-kit or similar)

## State
- [x] Use React state or Zustand (recommended)

## Rendering
- [x] Render elements dynamically based on JSON

---

## Step 4 — Demo
- [x] Adding text
- [x] Editing text
- [x] Nested layout
- [x] JSON output
- [x] Generated C# code

---

## Step 5 — Extensibility

### How to Add New Element Types (Example: Images)

To add a new element type like Images, follow these steps:

1. **Update ElementType** in `store.ts`:
   ```typescript
   export type ElementType = 'Text' | 'Column' | 'Table' | 'Image';
   ```

2. **Update store logic** in `store.ts` addElement function:
   ```typescript
   } else if (type === 'Image') {
     newElement = {
       id,
       type,
       props: { src: 'https://example.com/image.png', width: 100, height: 100 },
     };
   }
   ```

3. **Update ElementRenderer** in `ElementRenderer.tsx`:
   ```typescript
   } else if (element.type === 'Image') {
     const { src, width, height } = element.props;
     content = <img src={src} width={width} height={height} alt="Element" />;
   }
   ```

4. **Update PropertiesPanel** in `PropertiesPanel.tsx`:
   ```typescript
   } else if (element.type === 'Image') {
     // Add image property inputs (src, width, height)
   }
   ```

5. **Update Sidebar** in `Sidebar.tsx`:
   ```typescript
   const ELEMENTS = [
     { type: 'Text', label: 'Text' },
     { type: 'Column', label: 'Column' },
     { type: 'Table', label: 'Table' },
     { type: 'Image', label: 'Image' },
   ];
   ```

6. **Update codegen** in `codegen.ts`:
   ```typescript
   if (el.type === 'Image') {
     return `${pad}new ImageElement { Source = "${el.props.src}", Width = ${el.props.width}, Height = ${el.props.height} }`;
   }
   ```

### How to Add a Styling System

To add styling capabilities to elements, extend the property system:

1. **Add style properties** to element props in `store.ts`:
   ```typescript
   props: { 
     text: 'Text', 
     fontSize: 16,
     style: { backgroundColor: '#fff', color: '#000', border: '1px solid #ccc' }
   }
   ```

2. **Update ElementRenderer** to apply styles:
   ```typescript
   const style = element.props.style || {};
   return (
     <div style={style}>
       {content}
     </div>
   );
   ```

3. **Update PropertiesPanel** with style editors:
   ```typescript
   // Add color pickers, border inputs, etc.
   <input type="color" value={element.props.style?.backgroundColor} />
   ```

4. **Update codegen** to generate styled C#:
   ```typescript
   const styleProps = Object.entries(el.props.style || {})
     .map(([key, value]) => `${key} = "${value}"`)
     .join(', ');
   return `${pad}new StyledElement { ${styleProps}, Content = ${content} }`;
   ```

- [x] Explain how to add Images
- [x] Explain how to add Tables (implemented)
### How to Add Responsive Layout

To add responsive design capabilities:

1. **Add responsive properties** to elements:
   ```typescript
   props: {
     // Base properties
     width: '100%',
     // Responsive breakpoints
     breakpoints: {
       mobile: { width: '100%', fontSize: 14 },
       tablet: { width: '50%', fontSize: 16 },
       desktop: { width: '25%', fontSize: 18 }
     }
   }
   ```

2. **Update ElementRenderer** with responsive logic:
   ```typescript
   const getResponsiveProps = (element) => {
     const width = window.innerWidth;
     if (width < 768) return element.props.breakpoints?.mobile || {};
     if (width < 1024) return element.props.breakpoints?.tablet || {};
     return element.props.breakpoints?.desktop || {};
   };
   ```

3. **Add responsive property editors** in PropertiesPanel.

4. **Update codegen** for responsive C# generation.

### How to Add Saving/Loading Templates

To add template persistence:

1. **Add save/load functions** to store:
   ```typescript
   saveTemplate: (name: string) => {
     const template = { name, elements: get().elements, rootIds: get().rootIds };
     localStorage.setItem(`template_${name}`, JSON.stringify(template));
   },
   loadTemplate: (name: string) => {
     const data = localStorage.getItem(`template_${name}`);
     if (data) {
       const template = JSON.parse(data);
       set({ elements: template.elements, rootIds: template.rootIds, selectedId: null });
     }
   }
   ```

2. **Add UI controls** in a new TemplatesPanel component.

3. **Export/import** functionality for JSON files.

- [x] Explain how to add Styling system
- [x] Explain how to add Responsive layout
- [x] Explain how to add Saving/loading templates

---

## Rules
- [x] Clean, modular code
- [x] No overengineering
- [x] Focus on scalability
- [x] Avoid hacks
