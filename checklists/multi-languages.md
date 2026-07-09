# Multi-Language PDF Support — Implementation Checklist

**Status: COMPLETE** — Core implemented 2026-05-24 (99 tests). Phase 11 (Inspector UX + Element-Level Scope) completed 2026-05-25.

## What Was Built

Each canvas text element can now declare a `language` (BCP-47 tag) and `textDirection` (`ltr`/`rtl`). When exported to PDF, the system embeds the appropriate Noto TrueType font and encodes text as UTF-16BE hex strings using Identity-H encoding per the PDF specification. RTL scripts (Arabic, Hebrew) are rendered in visual order. If no font file is found, the system falls back silently to Type1 without throwing.

---

## Phase 1 — Data Model Changes

### Backend (C#)

- [x] **Add `Language` and `TextDirection` to `TextConfig`**
  File: `Canvas.Domain/ValueObjects/DesignerElement.cs`
  Added to `TextConfig`, `RichTextConfig`, and `TextFieldConfig`.

- [x] **Update `MigratePropsToConfig()` in `DesignerElement`**
  File: `Canvas.Domain/ValueObjects/DesignerElement.cs`
  Reads `language` and `textDirection` from the legacy `Props` dictionary.

- [x] **Add `Language` and `TextDirection` to `ElementDto`**
  File: `src/Canvas.Core/Contracts/DesignExportDto.cs`
  Added alongside the existing `Locale` field.

### Frontend (TypeScript)

- [x] **Add `language` and `textDirection` to `SimpleElement`**
  File: `ui-designer-v2/src/types.ts`
  Added `language?: string` and `textDirection?: 'ltr' | 'rtl'`.

---

## Phase 2 — PDF Backend: Embedded Font Infrastructure

- [x] **Create `PdfEmbeddedFont` class**
  New file: `Canvas/Pdf/PdfEmbeddedFont.cs`
  Full TTF binary parser for `cmap` format-4 and `hmtx` tables. Exposes `GetGlyphId`, `MeasureWidth`, `BaseFontName`, `IsRtlCapable`, `FontBytes`.

- [x] **Create `PdfFontLoader` class**
  New file: `Canvas/Pdf/PdfFontLoader.cs`
  Maps BCP-47 language tags to Noto font files with in-memory caching. `TryLoad` returns false gracefully when font file is absent.

  Language-to-font mapping:
  | Language(s) | Font File | Script |
  |---|---|---|
  | `ar`, `ur`, `fa` | NotoSansArabic-Regular.ttf | Arabic / RTL |
  | `he`, `yi` | NotoSansHebrew-Regular.ttf | Hebrew / RTL |
  | `zh`, `zh-CN`, `zh-TW` | NotoSansSC-Regular.otf | Chinese |
  | `ja` | NotoSansJP-Regular.otf | Japanese |
  | `ko` | NotoSansKR-Regular.otf | Korean |
  | `hi`, `mr`, `ne` | NotoSansDevanagari-Regular.ttf | Devanagari |
  | `th` | NotoSansThai-Regular.ttf | Thai |
  | `el`, `ru`, `uk`, `bg`, (default) | NotoSans-Regular.ttf | Greek / Cyrillic / Latin |

- [x] **Extend `TextElement` record with embedded font fields**
  File: `Canvas/Pdf/Layout/TextElement.cs`
  Added `PdfEmbeddedFont? EmbeddedFont = null`, `string? Language = null`, `string? TextDirection = null`.

- [x] **Extend `PdfDrawTextOptions` with `Language` and `TextDirection`**
  File: `Canvas/Pdf/PdfDrawTextOptions.cs`

- [x] **Extend `PdfParagraphOptions` with `Language` and `TextDirection`**
  File: `Canvas/Pdf/PdfParagraphOptions.cs`

- [x] **Create `PdfTextEncoding` public utility class**
  New file: `Canvas/Pdf/PdfTextEncoding.cs`
  `EncodeAsHexUtf16Be(string)` → `<FEFF...>` UTF-16BE hex string with BOM.
  `ReverseForRtl(string)` → grapheme-cluster reversal via `StringInfo`.
  (Moved out of `PdfCanvasRenderer` which is `internal`, to enable test access.)

- [x] **Branch text rendering in `PdfCanvasRenderer.RenderText` on font type**
  File: `Canvas/Pdf/Rendering/PdfCanvasRenderer.cs`
  Embedded font path uses `<FEFF...> Tj` hex encoding. RTL applies `ReverseForRtl()`. Width uses `EmbeddedFont.MeasureWidth()`. Type1 path unchanged.

- [x] **Emit embedded font object chain in `PdfWriter`**
  File: `Canvas/Pdf/Serialization/PdfWriter.cs`
  5-object chain per font: FontStream (FlateDecode), FontDescriptor, ToUnicode CMap, CIDFont (with `/W` widths array), Type0 composite font.

- [x] **Include embedded font resources in page `/Font` dictionary**
  File: `Canvas/Pdf/Serialization/PdfWriter.cs`
  Resource names `EF1`, `EF2` … appear alongside `F1`, `F2` …

- [x] **Pass embedded font resource names to `PdfCanvasRenderer.RenderPage`**
  Added `IReadOnlyDictionary<PdfEmbeddedFont, string>? embeddedFontResourceNames` parameter.

- [x] **Update `PdfPage.DrawText` to resolve embedded font**
  File: `Canvas/Pdf/PdfPage.cs`
  Calls `_fontLoader?.TryLoad(options.Language, out embeddedFont)` and threads result into `TextElement`.

---

## Phase 3 — Frontend UI Changes

- [x] **Add language selector to the Typography section**
  File: `ui-designer-v2/src/components/Editor/SimpleCanvas.tsx`
  Dropdown with 14 languages. Auto-sets `textDirection: 'rtl'` for Arabic, Hebrew, Farsi, Urdu, Yiddish, Divehi.

- [x] **Add LTR / RTL direction toggle**
  Two buttons below the language selector allowing manual override.

- [x] **Add Noto font entries to the font family list**
  Added: `Noto Sans Arabic`, `Noto Sans Hebrew`, `Noto Sans SC`, `Noto Sans TC`, `Noto Sans JP`, `Noto Sans KR`, `Noto Sans Devanagari`, `Noto Sans Thai`.

- [x] **Apply `dir` and `lang` attributes to text element previews**
  `dir={element.textDirection || 'ltr'}` and `lang={element.language}` on rendered text divs.

---

## Phase 4 — API Integration Layer

- [x] **Thread `Language` and `TextDirection` through `DesignJsonMapper`**
  File: `PXA.WebApi/Infrastructure/DesignJsonMapper.cs`
  `BuildParaOptions` and `PdfDrawTextOptions` construction now pass `el.Language` and `el.TextDirection`.

- [x] **Register `PdfFontLoader` as a singleton in DI**
  File: `PXA.WebApi/Program.cs`

- [x] **Add `Pdf:FontsDirectory` to `appsettings.json`**
  File: `PXA.WebApi/appsettings.json`

- [x] **Document Noto font deployment step**
  See `fonts/README.md` for download and placement instructions.

---

## Phase 5 — Tests

- [x] **Unit tests: `PdfFontLoader` graceful failure and RTL classification**
  File: `tests/Canvas.Export.Tests/MultiLanguagePdfTests.cs`

- [x] **Unit tests: UTF-16BE hex encoding**
  `EncodeAsHexUtf16Be("A")` → `<FEFF0041>`, BOM always present, Arabic chars encoded correctly.

- [x] **Unit tests: RTL reversal**
  Grapheme-cluster reversal verified for ASCII and Arabic strings.

- [x] **Integration test: PDF generation without embedded font**
  Bytes start with `%PDF`, no exception.

- [x] **Integration test: Arabic text with missing font file**
  Graceful fallback to Type1 — valid PDF produced.

- [x] **Domain model round-trip test**
  `ElementDto { Language = "ar", TextDirection = "rtl" }` serializes and deserializes correctly.

- [x] **Full integration test via `DesignJsonMapper`**
  Arabic text element exported to PDF bytes starting with `%PDF`.

---

## Known Limitations

- **Arabic cursive shaping** (ligatures, contextual glyph forms) is NOT implemented. Text shows individual glyphs in visual order but without cursive joining. Full shaping requires HarfBuzz.NET — planned future enhancement.
- **Mixed-direction paragraphs** (BiDi algorithm UAX #9) are not implemented. Each element is treated as a single-direction run.
- **Vertical CJK text** is not supported.
- **Font subsetting** is not implemented — the full font file is embedded, increasing PDF size for large Noto fonts.

---

## Deployment

Font files must be placed in the `fonts/` directory next to the running assembly (or the path configured in `appsettings.json` under `Pdf:FontsDirectory`). See `fonts/README.md` for details.

---

---

# Document Localization System — Extension

**Status: COMPLETE** — Implemented 2026-05-25. 110 tests pass (11 new localization tests).

## Overview

Phases 1–5 solved *font rendering* (how non-Latin characters appear in a PDF).
This extension solves *document localization* (one template, multiple language versions, each with its own property values).

### Core Concept

A document declares:
- A **system language** — automatically detected from the browser locale (`navigator.language`); used as the primary/fallback language. Not user-configurable.
- A set of **active languages** — languages the user explicitly enables for this document (e.g. `["de", "ar", "en"]`)
- A list of **localized properties** — template variables like `{{SUBJECT}}` that carry a value per language

On export, one PDF is generated per active language. For each, the engine substitutes the language-specific property values before rendering.

### User Flow

1. Open Document Settings → Languages tab → check active languages (`de`, `ar`, `en`); system language (`de`) is shown but not editable
2. Language tabs appear above the canvas: **DE | AR | EN** (system language tab first)
3. Switch to **DE** tab → in the Properties panel (right), add property `{{SUBJECT}}` = `"Hallo Welt"`
4. Switch to **AR** tab → `{{SUBJECT}}` is listed but empty → enter `"مرحبا بالعالم"`
5. Toggle each property as **Global** (one value for all) or **Language-specific**
6. Export → choose "Export all languages (ZIP)" → receives `document-de.pdf`, `document-ar.pdf`, `document-en.pdf`

---

## Phase 6 — Data Model: Document-Level Language Configuration

### Frontend (TypeScript)

- [x] **Add `activeLanguages` to `PageSettings`**
  File: `ui-designer-v2/src/types.ts`
  The system language is read from `navigator.language` at runtime, never stored.
  ```typescript
  activeLanguages?: string[];    // user-selected active languages, e.g. ["de", "en", "ar"]
  ```

- [x] **Add `LocalizedProperty` interface**
  File: `ui-designer-v2/src/types.ts`
  ```typescript
  export interface LocalizedProperty {
    key: string;           // template variable name without {{ }}, e.g. "SUBJECT"
    global: boolean;       // true → globalValue applies to all languages
    globalValue: string;   // used when global = true, and as final fallback when language-specific
    localizedValues: Record<string, string>;  // { de: "Hallo Welt", ar: "مرحبا" }
  }
  ```

- [x] **Add `localizedProperties` to `PageSettings`**
  File: `ui-designer-v2/src/types.ts`
  ```typescript
  localizedProperties?: LocalizedProperty[];
  ```

- [x] **Add `currentPreviewLanguage` to editor store (ephemeral, not serialized)**
  File: `ui-designer-v2/src/store.ts`
  Drives which language tab is active in the editor. Initialised to `navigator.language` (or its base tag). Reset when active languages change.

- [x] **Update `DEFAULT_PAGE_SETTINGS`**
  File: `ui-designer-v2/src/store.ts`
  Add `activeLanguages: []`, `localizedProperties: []`.

### Backend (C#)

- [x] **Add `SystemLanguage` and `ActiveLanguages` to `DesignExportDto`**
  File: `src/Canvas.Core/Contracts/DesignExportDto.cs`
  `SystemLanguage` is sent by the client from `navigator.language`; the backend uses it as the fallback in property resolution.
  ```csharp
  public string? SystemLanguage { get; set; }
  public string[]? ActiveLanguages { get; set; }
  ```

- [x] **Create `LocalizedPropertyDto`**
  File: `src/Canvas.Core/Contracts/DesignExportDto.cs`
  ```csharp
  public class LocalizedPropertyDto {
      public string Key { get; set; } = "";
      public bool Global { get; set; }
      public string GlobalValue { get; set; } = "";
      public Dictionary<string, string> LocalizedValues { get; set; } = [];
  }
  ```

- [x] **Add `LocalizedProperties` to `DesignExportDto`**
  File: `src/Canvas.Core/Contracts/DesignExportDto.cs`
  ```csharp
  public LocalizedPropertyDto[]? LocalizedProperties { get; set; }
  ```

---

## Phase 7 — Backend: Property Resolution & Multi-Language Export

- [x] **Create `LocalizedPropertyResolver`**
  New file: `PXA.WebApi/Infrastructure/LocalizedPropertyResolver.cs`
  Resolves the effective value for each property given a target language and system language:
  - If `property.Global = true` → return `property.GlobalValue`
  - Else if `property.LocalizedValues[targetLanguage]` exists → return it
  - Else if `property.LocalizedValues[systemLanguage]` exists → fallback to system language value
  - Else → return `property.GlobalValue` (final fallback, never throws)

- [x] **Apply property values as data-binding payload in `DesignJsonMapper`**
  File: `PXA.WebApi/Infrastructure/DesignJsonMapper.cs`
  Before rendering, build a `Dictionary<string, string>` from resolved property values and inject into the expression evaluator context. This makes `{{SUBJECT}}` resolve to the language-specific value at render time.
  Signature: `MapToPdfDocument(design, fontLoader, targetLanguage)`.

- [x] **Add `language` query parameter to single-language export**
  File: `PXA.WebApi/Controllers/ExportController.cs`
  `POST /api/export?format=pdf&language=de` — renders the design with that language's property values.
  If omitted, uses `design.SystemLanguage` or renders without substitution.

- [x] **Add multi-language export endpoint**
  File: `PXA.WebApi/Controllers/ExportController.cs`
  `POST /api/export/multilanguage?format=pdf` — iterates over `design.ActiveLanguages`, renders one PDF per language, packs into a ZIP stream, returns `application/zip`.
  File naming: `{documentName}-{lang}.pdf` (e.g. `invoice-de.pdf`, `invoice-ar.pdf`).

- [x] **RTL document frame for RTL target languages**
  During export, when the target language is RTL (`PdfFontLoader.IsRtl(lang)`), default paragraph alignment to Right and text direction to RTL for elements that have no explicit `TextDirection` set.

---

## Phase 8 — UI: Language Tabs & Localized Properties Panel

- [x] **Active Languages in Document Settings dialog**
  File: `ui-designer-v2/src/components/Editor/SimpleCanvas.tsx` (Document Settings section)
  Add a "Languages" tab containing:
  - **System language** — read-only display of `navigator.language` (e.g. "System: Deutsch (de)"); shown as informational, not editable
  - **Active Languages** — checkboxes for each available language; checking adds to `pageSettings.activeLanguages`; unchecking removes it (with confirmation if localized values exist for that language)

- [x] **Language tab bar component**
  New file: `ui-designer-v2/src/components/Editor/LanguageTabBar.tsx`
  Rendered above the canvas when `pageSettings.activeLanguages.length > 1`.
  - One tab per active language (flag emoji + tag, e.g. "🇩🇪 DE"); system language tab shown first
  - Active tab highlighted; clicking calls `setCurrentPreviewLanguage(lang)` on the store
  - RTL languages show an "RTL" badge on the tab

- [x] **Canvas preview in language context**
  File: `ui-designer-v2/src/components/Editor/SimpleCanvas.tsx`
  When rendering element content for preview, resolve `{{KEY}}` placeholders using `currentPreviewLanguage`'s values from `localizedProperties`.
  Apply `dir="rtl"` wrapper to the entire canvas frame when `currentPreviewLanguage` is RTL.

- [x] **Localized Properties Panel**
  New file: `ui-designer-v2/src/components/Editor/LocalizedPropertiesPanel.tsx`
  Displayed in the right inspector panel when no element is selected (or as a dedicated drawer tab).

  Layout per property row:
  ```
  [ {{SUBJECT}} ]  [ Global ↔ Per-language toggle ]  [ value input for current tab ]  [ 🗑 ]
  ```
  - **Global mode**: single value input, same for all languages
  - **Per-language mode**: input shows value for the current language tab only; indicator dots (●○) show which other languages have a value set; missing languages show a warning icon
  - **"Add property" button**: opens a small inline form — key name (with `{{KEY}}` autocomplete from element content) + initial value
  - **Missing-value warning badge** per row when language-specific and one or more active languages have no value

- [x] **Property key autocomplete from element content**
  Scan all element `content` fields for `{{KEY}}` patterns and suggest them as autocomplete options when entering a property key.

- [x] **Update Export Modal for multi-language**
  File: `ui-designer-v2/src/components/Editor/ExportModal.tsx`
  When `pageSettings.activeLanguages.length > 1`:
  - "Export current language (`de`)" → `POST /api/export?format=pdf&language=de`
  - "Export all languages (ZIP)" → `POST /api/export/multilanguage?format=pdf`
  - Summary list showing which languages will be in the ZIP, with ⚠ for any properties missing a value in that language

---

## Phase 9 — Tests

- [x] **Unit test: `LocalizedPropertyResolver` resolution logic**
  - Global property → returns `globalValue` regardless of language
  - Language-specific, value present → returns correct value
  - Language-specific, value missing, system language fallback → returns system language value
  - Language-specific, no value anywhere → returns `globalValue`

- [x] **Integration test: single-language export with substitution**
  - Design with `{{SUBJECT}}` localized to `de = "Hallo"`, `ar = "مرحبا"`
  - Export `?language=de` → PDF contains German substitution
  - Export `?language=ar` → PDF contains Arabic substitution with Noto font

- [x] **Integration test: multi-language ZIP export**
  - Design with 2 active languages → ZIP returned with 2 entries
  - Each entry is a valid PDF (`%PDF` header)

- [x] **Unit test: property key scanner**
  - Elements with `{{SUBJECT}}` and `{{NAME}}` in content → scanner returns `["SUBJECT", "NAME"]`

- [x] **Unit test: system language detection**
  - `navigator.language = "de-DE"` → system language tag resolved to `"de"` (base tag used for lookup)

---

## Design Decisions

| Question | Decision |
|---|---|
| How is the default/fallback language determined? | Automatically from browser `navigator.language`. Not stored, not user-configurable. |
| Where are localized properties stored? | On `PageSettings.localizedProperties` — serialized with the design JSON |
| What if a language is removed from active languages? | Confirm with user; remove its entries from all `localizedValues` |
| Fallback chain for missing value | language value → system language value → `globalValue` → empty string |
| How does `{{SUBJECT}}` resolve in the expression engine? | `LocalizedPropertyResolver` builds a `Dictionary<string, string>` injected as top-level expression context before rendering |
| Single vs multi-language export API | Two calls: `?language=de` for single PDF, `/multilanguage` for ZIP |
| Can `activeLanguages` be empty? | Yes — empty means single-language mode (no tabs shown, no localization panel) |
| Does switching preview tab affect the saved design? | No — `currentPreviewLanguage` is ephemeral store state only |

---

---

# Phase 10 — Revised Property Model, Bug Fixes & Language-Scoped Export

**Status: COMPLETE** — Implemented 2026-05-25. 115 tests pass.

## Problem Analysis

### Bug: Values Are the Same for All Languages

**Root cause — terminology collision in the current model:**

The current `LocalizedProperty` has `global: boolean`:
- `global: true` → `globalValue` is used (same value for every language — like a constant)
- `global: false` → each language has its own value in `localizedValues`

**What actually happens in the UI:** When a user adds a new property via the "Add property" form, the value they type is stored in *both* `globalValue` *and* `localizedValues[activeLang]`. When the panel renders, properties with `global: false` correctly show the per-language `localizedValues` entry — but the toggle label "Global / Per lang" does not match the user's mental model and led to confusion about whether values were shared.

The deeper issue is that the term "Global" in the current code means "same constant for all languages", but the user uses "Global" to mean "this placeholder exists in all languages (each fills in its own value)." These are opposite meanings.

---

### Revised Mental Model — Two Property Scopes

The user's intent has two clearly distinct property types:

| Type | User's Term | Meaning |
|---|---|---|
| **Global Property** | "Global" | `{{KEY}}` placeholder exists in **all** language PDFs. Each language has its own value. If DE has `"Hallo"` and AR has `"مرحبا"`, both appear correctly in their respective PDFs. |
| **Own Property** | "Own" | `{{KEY}}` placeholder exists **only in one specific language**. The property is invisible to other languages — it does not appear in their PDFs and is not shown in their export JSON. |

**Key behavioral difference for Export Code / JSON:**
When the user switches to the DE tab and clicks "Export Code", the resulting JSON must only contain:
- Global properties with the DE value substituted
- Own properties whose `ownerLanguage === "de"`

When on the AR tab, Own properties that belong to DE must **not appear** in the AR JSON at all.

---

## Required Changes

### Data Model — Replace `global: boolean` with `scope: 'global' | 'own'`

**Old model (incorrect terminology):**
```typescript
interface LocalizedProperty {
  key: string;
  global: boolean;       // true = same for all (constant), false = per-language values
  globalValue: string;   // used when global = true, and as ultimate fallback
  localizedValues: Record<string, string>;
}
```

**New model:**
```typescript
interface LocalizedProperty {
  key: string;
  scope: 'global' | 'own';
  // scope === 'global': placeholder exists in all languages; each fills its own value
  // scope === 'own':    placeholder exists only in ownerLanguage; invisible to others
  ownerLanguage?: string;            // only set when scope === 'own'
  localizedValues: Record<string, string>;
  // for 'global': { de: "Hallo", en: "Hello", ar: "مرحبا" }
  // for 'own':    { "de": "Nur auf Deutsch" }  (single entry, the owner's value)
}
```

**C# backend (`LocalizedPropertyDto`):**
```csharp
public class LocalizedPropertyDto {
    public string Key { get; set; } = "";
    public string Scope { get; set; } = "global";  // "global" | "own"
    public string? OwnerLanguage { get; set; }      // only when Scope == "own"
    public Dictionary<string, string> LocalizedValues { get; set; } = [];
    // globalValue removed — no longer needed (fallback is now system language value)
}
```

---

## Phase 10 Checklist

### 10.1 — Frontend: Data Model

- [x] **Rename `global: boolean` → `scope: 'global' | 'own'` in `LocalizedProperty`**
  File: `ui-designer-v2/src/types.ts`
  Remove `globalValue`. Add `ownerLanguage?: string`.
  Migration: any existing property with `global: true` → `scope: 'global'`; `global: false` → `scope: 'global'` (was per-language, same concept).

- [x] **Update `upsertLocalizedProperty` in store**
  File: `ui-designer-v2/src/store.ts`
  Adapt to new shape. When adding an Own property, automatically set `ownerLanguage` to the current preview language.

- [x] **Update `DEFAULT_PAGE_SETTINGS`**
  File: `ui-designer-v2/src/store.ts`
  `localizedProperties: []` — no change needed, shape change is backward compatible via migration.

---

### 10.2 — Frontend: UI / LocalizedPropertiesPanel

- [x] **Replace "Global / Per lang" toggle with "Global / Own" toggle**
  File: `ui-designer-v2/src/components/Editor/LocalizedPropertiesPanel.tsx`
  - **Global** = placeholder in all languages, each fills its own value
  - **Own** = placeholder only for the current language tab (shows owner badge: `"DE only"`)

- [x] **Global property rows: show value input for current language tab only**
  Same as current behavior for `global: false` — one input per tab with coverage indicators for all other languages.
  Fallback for missing language: show system language value grayed-out as placeholder text.

- [x] **Own property rows: only visible when the current tab matches `ownerLanguage`**
  When the user switches to a language tab that does not own this property, the property row must NOT be shown.
  When it is shown (correct language tab), show a "Own — {LANG} only" badge instead of coverage indicators.

- [x] **"Add property" form: choose scope before saving**
  Add a two-button toggle in the add form: "Global" / "Own (this language)".
  Default: Global. When Own is selected, `ownerLanguage` is automatically set to `currentPreviewLanguage`.

- [x] **Remove `globalValue` from all UI paths**
  No single-value-for-all-languages input should exist. The old "Global" constant mode is gone.

---

### 10.3 — Frontend: Canvas Preview

- [x] **Fix `resolveContent` to correctly apply own vs global scoping**
  File: `ui-designer-v2/src/components/Editor/SimpleCanvas.tsx`
  When building the property map for the current preview language:
  - Include Global properties: value = `localizedValues[currentPreviewLanguage] ?? localizedValues[systemLanguage] ?? ""`
  - Include Own properties: only include when `ownerLanguage === currentPreviewLanguage`; value = `localizedValues[ownerLanguage]`
  - Exclude Own properties that belong to a different language

- [x] **"Export Code" / JSON preview reflects current language only**
  When the user views the raw JSON of the design (Export Code or debug view), the JSON payload sent to the backend must represent only the resolved properties for the active language tab. Own properties for other languages must be omitted.

---

### 10.4 — Backend: Data Model

- [x] **Update `LocalizedPropertyDto`**
  File: `src/Canvas.Core/Contracts/DesignExportDto.cs`
  Replace `bool Global` + `string GlobalValue` with:
  ```csharp
  public string Scope { get; set; } = "global";   // "global" | "own"
  public string? OwnerLanguage { get; set; }
  ```

---

### 10.5 — Backend: Resolution Logic

- [x] **Update `LocalizedPropertyResolver.Resolve()`**
  File: `PXA.WebApi/Infrastructure/LocalizedPropertyResolver.cs`
  New resolution rules:

  ```
  For each property:
    if scope == "own":
      if property.OwnerLanguage != targetLanguage → SKIP (do not include in result)
      else → value = localizedValues[ownerLanguage]
    if scope == "global":
      value = localizedValues[targetLanguage]
              ?? localizedValues[systemLanguage]
              ?? ""
  ```

  The resolved `Dictionary<string, string>` contains only properties that apply to the target language.

- [x] **Remove `globalValue` fallback from resolver**
  No longer needed — the fallback chain ends at system language value → empty string.

---

### 10.6 — Backend: Multi-Language Export

- [x] **Verify that `ExportController` multi-language ZIP correctly isolates per-language properties**
  File: `PXA.WebApi/Controllers/ExportController.cs`
  For each language in `ActiveLanguages`, the resolver must exclude Own properties belonging to other languages. The PDF for DE must not contain Arabic-only `{{KEY}}` placeholders as unresolved strings.

- [x] **Update `DesignJsonMapper.ApplyPropertySubstitutions()`**
  File: `PXA.WebApi/Infrastructure/DesignJsonMapper.cs`
  No structural change needed — it already calls `Resolve()` and substitutes. The fix is in the resolver itself.

---

### 10.7 — Tests

- [x] **Unit test: Own property excluded from other language export**
  Property `{ scope: "own", ownerLanguage: "de", localizedValues: { de: "Nur DE" } }`
  → `Resolve(props, "ar", "de")` does not contain the key
  → `Resolve(props, "de", "de")` returns `"Nur DE"`

- [x] **Unit test: Global property included in all languages with own value**
  Property `{ scope: "global", localizedValues: { de: "Hallo", ar: "مرحبا" } }`
  → `Resolve(props, "de", "de")["KEY"] == "Hallo"`
  → `Resolve(props, "ar", "de")["KEY"] == "مرحبا"`

- [x] **Unit test: Global property missing target language — falls back to system language**
  Property `{ scope: "global", localizedValues: { de: "Hallo" } }`, target = "fr", system = "de"
  → result = "Hallo"

- [x] **Unit test: Global property missing both languages — returns empty string**

- [x] **Integration test: Multi-language ZIP — each PDF only contains its language's Own properties**

---

---

## Phase 11 — Inspector UX + Element-Level Language Scope (2026-05-25)

### 11.1 — Properties Tab

- [x] **Move `LocalizedPropertiesPanel` to a dedicated inspector tab**
  File: `ui-designer-v2/src/components/Editor/SimpleCanvas.tsx`
  Added `'properties'` to `inspectorTab` type. Tab button appears only when `activeLanguages.length >= 1`. Shows property count badge. Removed panel from the `!selectedElement` inspector section.

- [x] **Fix second-property-add bug in `LocalizedPropertiesPanel`**
  File: `ui-designer-v2/src/components/Editor/LocalizedPropertiesPanel.tsx`
  Root cause: stale closure over `newKey`/`newValue` in `onKeyDown` handler. Fix: use `useRef` for add-form input values alongside display state. `addProperty` now reads `newKeyRef.current` / `newValueRef.current` — never stale.

### 11.2 — Element-Level Language Scope

- [x] **Add `elementLanguage?: string` to `SimpleElement`**
  File: `ui-designer-v2/src/types.ts`
  `undefined` = visible in all language tabs. BCP-47 tag = Own element for that language only.

- [x] **Add `ElementLanguage?: string` to `ElementDto`**
  File: `src/Canvas.Core/Contracts/DesignExportDto.cs`

- [x] **Auto-assign `elementLanguage` when adding elements in multi-lang mode**
  File: `ui-designer-v2/src/components/Editor/SimpleCanvas.tsx`
  New helper `addElementWithLangScope()`: if `activeLanguages.length >= 1` and `currentPreviewLanguage` is one of the active languages, new elements get `elementLanguage = currentPreviewLanguage`. Otherwise elements are "all languages" (no `elementLanguage`).

- [x] **Auto-create RTL mirror element for LTR Own elements**
  When the current language tab is LTR and RTL languages are active, a mirror element is auto-created for each active RTL language with:
  - `elementLanguage = rtlLang`
  - `textDirection = 'rtl'`
  - `x = pageWidth - base.x - base.width` (horizontally mirrored)

- [x] **Filter canvas rendering by `currentPreviewLanguage`**
  File: `ui-designer-v2/src/components/Editor/SimpleCanvas.tsx`
  Canvas renders only elements where `!el.elementLanguage || el.elementLanguage === currentPreviewLanguage`. Layers panel shows all elements with a language badge.

- [x] **Language Scope control in element inspector**
  "All languages" + one button per active language. "Create RTL mirror" button when element is LTR Own and RTL languages are active.

- [x] **Backend: filter elements by `ElementLanguage` in `DesignJsonMapper`**
  File: `PXA.WebApi/Infrastructure/DesignJsonMapper.cs`
  Both current-page elements and scoped elements are filtered: if `ElementLanguage` is set, only render when it matches the resolved target language (via `LocalizedPropertyResolver.NormalizeTag`).

- [x] **Make `NormalizeTag` public in `LocalizedPropertyResolver`**
  File: `PXA.WebApi/Infrastructure/LocalizedPropertyResolver.cs`
  Required for use in `DesignJsonMapper`.

- [x] **JSON export filters elements by target language**
  File: `ui-designer-v2/src/services/CodeGenerator.ts`
  When `hasLanguages && targetLanguage`, elements with a non-matching `elementLanguage` are excluded from the exported JSON.

- [x] **Language badge in Layers panel**
  Each element row shows a compact `DE` / `AR` badge when `elementLanguage` is set.

---

## Design Decisions (Updated)

| Question | Decision |
|---|---|
| What does "Global" property mean? | The `{{KEY}}` placeholder appears in **all** language PDFs; each language provides its own value. No single shared constant. |
| What does "Own" property mean? | The `{{KEY}}` placeholder exists **only** in the PDF for the language that owns it. Other languages are unaware of it. |
| Is there still a "same value for all languages" option? | **No.** That concept is removed. If all languages happen to have the same value, the user enters it identically in each tab. |
| What is the resolution fallback? | For Global: `localizedValues[target] → localizedValues[systemLang] → ""`. For Own: value or excluded. |
| What does "Export Code" show? | The JSON for the currently active language tab only — with all Own properties from other languages stripped out. |
| Backward compatibility for saved designs? | Old `global: true` maps to `scope: 'global'` (with globalValue as initial value for system language). Old `global: false` maps to `scope: 'global'`. |
| What does `elementLanguage` mean? | `undefined` = element visible in all language tabs and all exported PDFs. BCP-47 tag = element only shown on that language tab and only exported when target matches. |
| How are RTL mirrors created? | Automatically when adding any element on an LTR language tab, if RTL languages are active. Also via "Create RTL mirror" button in the element inspector. |
| How does backend filter Own elements? | `DesignJsonMapper` checks `ElementLanguage` against the resolved target language using `NormalizeTag()` — same normalisation as property resolution. |
