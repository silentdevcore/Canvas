# UI Routing — Implementation Checklist

## Setup
- [x] Install react-router-dom v6
- [x] Add BrowserRouter to main.tsx
- [x] Add historyApiFallback to vite.config.ts

## Data Layer
- [x] Create src/data/templates.ts — extract TEMPLATES, CATEGORIES, CATEGORY_CONFIG
- [x] Export TemplateDefinition type alias
- [x] Export getTemplatesByCategory() helper

## Hooks
- [x] Create src/hooks/useTemplateLoader.ts — loadTemplate(), loadBlank()

## Pages
- [x] Create src/pages/IndexPage.tsx — category grid landing
- [x] Create src/pages/TemplatePage.tsx — full template browser
- [x] Create src/pages/CreatePage.tsx — editor with redirect guard

## Template Detail Panel
- [x] TemplateDetailPanel component inside TemplatePage
- [x] Animated right side-panel (Framer Motion spring)
- [x] Visual preview area with category accent color
- [x] Category badge, name, description, tags
- [x] "Use it" button → loadTemplate + navigate('/create')
- [x] Backdrop overlay click to close
- [x] Mobile: bottom-sheet layout (100vw, 85vh, rounded top)

## Template Browser (TemplatePage)
- [x] URL param sync: /template?category=invoice pre-selects filter
- [x] Search bar (real-time, name + description + tags)
- [x] Category filter tabs (reuse CategoryFilter component)
- [x] Sort: Default | A–Z | By Category
- [x] Empty state when no results
- [x] Grid card click → opens detail panel (not immediate load)

## Index Page
- [x] 15 category cards (.idx-category-grid)
- [x] Each card: icon, name, count badge, description
- [x] Category card click → /template?category=id
- [x] Hero: "Browse templates" → /template, "Blank canvas" → loadBlank()

## Navigation
- [x] Rewrite App.tsx as <Routes> shell
- [x] CreatePage redirect guard (no template → navigate('/'))
- [x] Back button in editor → setCurrentTemplate(null) + navigate('/template')

## Styling
- [x] .idx-category-grid and card styles
- [x] .tpl-detail-panel and sub-elements
- [x] .tpl-use-button
- [x] .pdf-sort-select
- [x] Mobile breakpoints for grid + panel

## Cleanup
- [x] Modify TemplateCard.tsx to import CATEGORY_CONFIG from src/data/templates.ts
- [x] Delete TemplateGallery.tsx
- [x] npx tsc --noEmit → zero new errors (2 pre-existing in CodePreviewPane/JsonEditorPane)
