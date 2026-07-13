# Implementation Plan: CraftMyPDF-Style UI Designer

## Overview
Build a user-friendly UI designer inspired by CraftMyPDF.com, focusing on template gallery and guided onboarding. Replace the current complex, developer-focused interface with an intuitive, beginner-friendly design tool that guides users through template creation with visual templates and progressive feature disclosure.

The new UI will prioritize simplicity, visual appeal, and user experience over feature complexity, making document template design accessible to non-technical users while maintaining the powerful backend capabilities.

## Types
Define clean, user-focused type definitions that abstract away technical complexity.

### Core UI Types
```typescript
// User experience states
type OnboardingStep = 'welcome' | 'template-selection' | 'customization' | 'preview' | 'export';
type ViewMode = 'gallery' | 'editor' | 'preview';

// Simplified element types for UX
type SimpleElementType = 'text' | 'image' | 'shape' | 'table' | 'line';

// Template categories for gallery
type TemplateCategory = 'invoice' | 'receipt' | 'certificate' | 'letter' | 'card' | 'report' | 'label';

// User-friendly element properties
interface SimpleElementProps {
  id: string;
  type: SimpleElementType;
  x: number;
  y: number;
  width: number;
  height: number;
  content?: string;
  style?: SimpleStyle;
  dataBinding?: string; // Simplified binding
}

// Simplified styling (hide complexity)
interface SimpleStyle {
  fontSize?: number;
  fontWeight?: 'normal' | 'bold';
  color?: string;
  backgroundColor?: string;
  textAlign?: 'left' | 'center' | 'right';
}
```

### State Management Types
```typescript
interface UIDesignerState {
  currentStep: OnboardingStep;
  viewMode: ViewMode;
  selectedTemplate?: Template;
  elements: SimpleElementProps[];
  selectedElementId?: string;
  isOnboardingActive: boolean;
  onboardingProgress: number;
}

interface Template {
  id: string;
  name: string;
  category: TemplateCategory;
  thumbnail: string;
  description: string;
  isPro?: boolean;
  tags: string[];
}
```

## Files
Complete restructuring of the UI designer with new components and simplified architecture.

### New Files to Create
- `pxa-designer/`: New root directory for redesigned UI
- `pxa-designer/components/Gallery/TemplateGallery.tsx`: Main gallery component
- `pxa-designer/components/Gallery/TemplateCard.tsx`: Individual template cards
- `pxa-designer/components/Gallery/CategoryFilter.tsx`: Category filtering
- `pxa-designer/components/Onboarding/OnboardingWizard.tsx`: Step-by-step onboarding
- `pxa-designer/components/Onboarding/WelcomeScreen.tsx`: Initial welcome
- `pxa-designer/components/Editor/SimplePxaSurface.tsx`: Simplified canvas
- `pxa-designer/components/Editor/InlineEditor.tsx`: Click-to-edit interface
- `pxa-designer/components/Editor/ElementToolbar.tsx`: Context-sensitive toolbar
- `pxa-designer/components/Preview/LivePreview.tsx`: Real-time preview
- `pxa-designer/hooks/useOnboarding.ts`: Onboarding state management
- `pxa-designer/hooks/useTemplateGallery.ts`: Gallery data management
- `pxa-designer/data/templates.ts`: Template definitions and metadata
- `pxa-designer/styles/gallery.css`: Gallery-specific styles
- `pxa-designer/styles/onboarding.css`: Onboarding styles
- `pxa-designer/styles/editor.css`: Simplified editor styles
- `pxa-designer/utils/templateAdapter.ts`: Convert between simple and complex models

### Existing Files to Modify
- `src/App.tsx`: Replace with new entry point that routes between gallery and editor
- `src/store.ts`: Extend with new UI state while maintaining compatibility
- `src/main.tsx`: Update to use new App component

### Files to Remove
- `src/Sidebar.tsx`: Replaced by inline element toolbar
- `src/PropertiesPanel.tsx`: Replaced by inline editing
- `src/PxaSurface.tsx`: Replaced by SimplePxaSurface
- `src/VirtualPxaSurface.tsx`: Not needed in simplified version
- Complex panels: DataPanel, JSONPanel, CodePanel, etc.

### Configuration Updates
- `package.json`: Add new UI dependencies (framer-motion for animations, react-beautiful-dnd for simpler drag-drop)
- `vite.config.ts`: Update build configuration for new structure
- `tsconfig.json`: Ensure proper TypeScript configuration

## Functions
Simplified function interfaces that hide complexity from users.

### New Functions
- `createSimpleElement(type: SimpleElementType, position: Point): SimpleElementProps`: Create elements with sensible defaults
- `adaptToComplexModel(simpleElements: SimpleElementProps[]): ComplexElement[]`: Convert UX model to backend model
- `getSuggestedElements(templateId: string): SimpleElementType[]`: Suggest elements based on template type
- `validateSimpleTemplate(elements: SimpleElementProps[]): ValidationResult`: User-friendly validation
- `exportSimpleTemplate(elements: SimpleElementProps[]): Promise<Blob>`: Export to PDF with progress feedback

### Modified Functions
- `useDesignerStore`: Extend with new UI state properties
- `addElement`: Simplify to work with SimpleElementType
- `updateElementProps`: Adapt to work with SimpleElementProps

### Removed Functions
- Complex binding functions (hidden from UX)
- Expression validation (handled internally)
- Advanced formatting options (progressive disclosure)

## Classes
Minimal class structure focused on user experience.

### New Classes
- `TemplateGalleryManager`: Manages template loading and filtering
- `OnboardingManager`: Handles onboarding flow and progress
- `SimpleElementFactory`: Creates elements with appropriate defaults
- `UXStateManager`: Manages UI state transitions

### Modified Classes
- `LocalStorageTemplateRepository`: Add methods for gallery templates
- Extend existing store with new UI state

## Dependencies
New dependencies for improved UX and simplified interactions.

### New Packages
- `framer-motion`: Smooth animations and transitions
- `react-beautiful-dnd`: Simpler drag-and-drop than @dnd-kit
- `react-icons`: Beautiful icon set for UI elements
- `clsx`: Utility for conditional CSS classes
- `react-hot-toast`: Better toast notifications

### Version Updates
- `react`: Keep at 18.x for compatibility
- `typescript`: Update to latest for better DX
- `vite`: Update to latest for performance

## Testing
Comprehensive testing for the new UX-focused components.

### New Test Files
- `pxa-designer/components/Gallery/TemplateGallery.test.tsx`
- `pxa-designer/components/Onboarding/OnboardingWizard.test.tsx`
- `pxa-designer/components/Editor/SimplePxaSurface.test.tsx`
- `pxa-designer/hooks/useOnboarding.test.ts`
- `pxa-designer/utils/templateAdapter.test.ts`

### Integration Tests
- Gallery to Editor workflow
- Onboarding completion flow
- Template customization and export
- Error handling and recovery

## Implementation Order
Logical sequence to build the new UI designer incrementally.

1. **Setup Project Structure**: Create new directory structure and basic components
2. **Build Template Gallery**: Core gallery with template cards and filtering
3. **Implement Onboarding**: Welcome screen and guided workflow
4. **Create Simple Editor**: Basic canvas with inline editing
5. **Add Live Preview**: Real-time preview functionality
6. **Integrate Export**: Connect to existing PDF generation
7. **Polish UX**: Animations, transitions, and visual refinements
8. **Testing & QA**: Comprehensive testing and user experience validation
9. **Migration**: Update routing and remove old components
10. **Performance Optimization**: Code splitting and lazy loading