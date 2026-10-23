# Multi-Language Support — UI Guide

The canvas designer supports per-element language tagging and text direction control. This enables authoring documents with Arabic, Hebrew, Japanese, Chinese, Korean, Hindi, Thai, Cyrillic, Greek, and other non-Latin scripts side-by-side with Latin text.

---

## Controls Location

Multi-language controls appear in the **Typography** section of the inspector panel on the right side, directly above the font size and color fields. They are visible whenever a text-type element is selected (`text`, `richtext`, `field`, `date`, `pagenumber`, etc.).

---

## Language Selector

A dropdown that assigns a BCP-47 language tag to the selected element.

**Available languages:**

| Option | Tag | Script | Direction |
|---|---|---|---|
| (none) | — | — | LTR |
| English | `en` | Latin | LTR |
| German | `de` | Latin | LTR |
| French | `fr` | Latin | LTR |
| Spanish | `es` | Latin | LTR |
| Italian | `it` | Latin | LTR |
| Portuguese | `pt` | Latin | LTR |
| Russian | `ru` | Cyrillic | LTR |
| Greek | `el` | Greek | LTR |
| Arabic | `ar` | Arabic | **RTL** (auto) |
| Hebrew | `he` | Hebrew | **RTL** (auto) |
| Persian | `fa` | Arabic | **RTL** (auto) |
| Chinese (Simplified) | `zh-CN` | Han | LTR |
| Chinese (Traditional) | `zh-TW` | Han | LTR |
| Japanese | `ja` | Han/Kana | LTR |
| Korean | `ko` | Hangul | LTR |
| Hindi | `hi` | Devanagari | LTR |
| Thai | `th` | Thai | LTR |

**Auto-RTL:** Selecting Arabic, Hebrew, or Persian automatically switches `textDirection` to `rtl`. Selecting any LTR language (or clearing the language) resets direction to `ltr`.

---

## Direction Toggle (LTR / RTL)

Two buttons — **LTR** and **RTL** — below the language selector. The active direction is highlighted.

- Use this to manually override the direction without changing the language tag (e.g., entering Arabic text with language set to `(none)`).
- The `dir` HTML attribute is applied to the canvas preview element, so the browser handles right-to-left text cursor, selection, and layout natively.

---

## Font Family

To render non-Latin scripts correctly in the browser preview, select a Noto font that matches the script. The font list includes:

| Font | Intended scripts |
|---|---|
| Noto Sans | Latin, Greek, Cyrillic |
| Noto Serif | Latin, Greek, Cyrillic (serif) |
| Noto Sans Arabic | Arabic |
| Noto Sans SC | Chinese Simplified |
| Noto Sans TC | Chinese Traditional |
| Noto Sans JP | Japanese |
| Noto Sans KR | Korean |
| Noto Sans Devanagari | Hindi, Marathi, Nepali |
| Noto Sans Thai | Thai |

Noto fonts must be loaded from Google Fonts (add `@import url('https://fonts.googleapis.com/css2?family=Noto+Sans+Arabic&...')` to the app's global CSS) or served locally for the canvas preview to render correctly.

> The PDF export engine embeds the Noto font file directly from the server's `fonts/` directory — the font does **not** need to be loaded by the browser for export to work.

---

## Canvas Preview Behavior

The canvas preview applies native browser RTL layout for right-to-left elements:

```tsx
<div
  dir={element.textDirection || 'ltr'}   // triggers native RTL layout
  lang={element.language || undefined}   // hints browser for hyphenation / rendering
  style={{ fontFamily: element.style?.fontFamily, ... }}
>
  {element.content}
</div>
```

- `dir="rtl"` makes the browser align text from right to left, places the cursor correctly, and renders Arabic/Hebrew glyphs in their correct visual form via the OS shaping engine.
- `lang` provides the language hint used by the browser for hyphenation, ligature selection, and accessibility tools.

> **Note:** The browser uses the OS or loaded web font for shaping (including Arabic cursive joining). The PDF export engine currently renders individual glyphs without cursive shaping — so Arabic text looks visually correct in the canvas preview but may appear as disconnected glyphs in the exported PDF until HarfBuzz shaping is integrated.

---

## Data Model

Language and direction are stored on the element alongside other properties:

```typescript
interface SimpleElement {
  // ... existing fields ...
  language?: string;           // BCP-47 tag — e.g. "ar", "zh-CN", "ja"
  textDirection?: 'ltr' | 'rtl';
}
```

These fields are serialized in the design JSON and sent to the backend export API as part of `ElementDto`:

```json
{
  "id": "e1",
  "type": "text",
  "content": "مرحبا بالعالم",
  "language": "ar",
  "textDirection": "rtl",
  "style": { "fontSize": 14, "fontFamily": "Noto Sans Arabic" }
}
```

---

## Typical Workflows

### Authoring an Arabic document

1. Add a **Text** element.
2. In the Typography panel, open the **Language** dropdown and select **Arabic (RTL)**.
3. Direction automatically switches to **RTL**.
4. Set **Font Family** to **Noto Sans Arabic**.
5. Type or paste Arabic text — the canvas preview renders right-to-left.
6. Export to PDF — the backend embeds `NotoSansArabic-Regular.ttf` and encodes the text as UTF-16BE.

### Mixed-language document (e.g., German invoice with Arabic customer name)

- German body text elements: language `(none)` or `de`, direction `LTR`, font `Arial` or `Roboto`.
- Arabic name element: language `ar`, direction `RTL`, font `Noto Sans Arabic`.
- Each element is independent — there is no document-level language setting.

### Overriding direction without a language tag

- Leave language as `(none)`.
- Click the **RTL** button to set direction manually.
- Useful for entering RTL text in an unsupported language or when no font embedding is needed.

---

## Export Notes

| Behavior | Detail |
|---|---|
| Language tag `(none)` | No `language` or `textDirection` field sent to API. Standard Type1 rendering. |
| Language set, font file present on server | Font embedded in PDF. UTF-16BE encoding. RTL reordered. |
| Language set, font file **absent** on server | Graceful fallback to Type1. PDF valid but non-Latin chars may not render. |
| `textDirection` not set | Defaults to `ltr` in PDF export. |

---

## Known Limitations

| Limitation | Impact |
|---|---|
| Arabic cursive shaping not in PDF export | Arabic text in exported PDF shows disconnected glyphs (no ligatures). Browser preview uses OS shaping so it looks correct there. |
| No inline BiDi (mixed direction within one element) | Each element is a single direction run. Mixing LTR and RTL within one text element is not supported. |
| Noto fonts must be available on the server | PDF export silently falls back to Latin-only Type1 if font file is missing. |
| Vertical CJK text not supported | Japanese/Chinese vertical layout is LTR only. |

---

---

# Document Localization System

Added 2026-05-25. This section covers the **document-level** multi-language feature: one template, multiple language versions. Each language version has its own property values substituted into `{{KEY}}` placeholders before export.

> The per-element language tagging above (Typography panel) controls *how text renders* (which font, which direction). This section controls *what text appears* in each language version.

---

## Setting Up Active Languages

1. Open **Document Settings** (gear icon in the toolbar).
2. Go to the **Languages** section.
3. Check the languages you want to activate (e.g. DE, EN, AR).
4. Your browser's language (`navigator.language`) is shown as the **system language** — it is used as a fallback and cannot be changed.
5. Language tabs appear above the canvas: **DE | EN | AR**.

---

## Property Scopes

Each localized property has a scope that controls which languages it applies to:

### Global Property (`scope: 'global'`)
- The `{{KEY}}` placeholder is present in **all** language versions.
- Each language has its own independent value.
- If a language has no value set, the system language's value is used as fallback.
- Example: `{{SUBJECT}}` → DE: "Betreff", EN: "Subject", AR: "موضوع"

### Own Property (`scope: 'own'`)
- The `{{KEY}}` placeholder exists **only** for the language that owns it.
- When exporting for another language, the property is completely absent — the `{{KEY}}` placeholder is not resolved.
- Example: `{{LEGAL_DE}}` owned by DE → appears only in the German PDF, not in EN or AR.

---

## Adding Properties

The **Localized Properties** panel appears in the right inspector when active languages are configured.

**Step 1 — Choose scope:**
- Click **Global (all languages)** to create a property that every language must fill.
- Click **Own (XX only)** to create a property visible only in the current language tab.

**Step 2 — Enter key and value:**
- Key: e.g. `SUBJECT` (typed without `{{ }}`).
- Value: the value for the current language tab.
- Press Enter or click `+` to save.

**Step 3 — Fill other languages (Global only):**
- Switch to each language tab and type the value for that language.
- Coverage indicators (green = has value, red = missing) show which languages still need a value.

---

## Canvas Preview

Switching language tabs updates the canvas in real time — `{{KEY}}` placeholders are replaced with the current tab's values. Own properties belonging to other tabs are not substituted (the placeholder is shown as-is).

---

## Export Code Panel

When you click **Export Code** in the toolbar:

- **JSON tab**: Shows the design JSON for the active language tab, including `targetLanguage`, `systemLanguage`, `activeLanguages`, and `localizedProperties` with `scope` and `ownerLanguage` fields.
- **C# Code tab**: `{{KEY}}` placeholders in element content are resolved to the current tab's values. Own properties for other languages are excluded.

---

## Exporting

### Single language
Click **Export** → **PDF** — exports the currently active language tab.

Or: **Export** → **PDF (DE)** / **PDF (EN)** etc. from the Export Modal to choose a specific language.

### All languages (ZIP)
Click **Export** → **Export all languages (ZIP)** in the Export Modal. Downloads a ZIP containing one PDF per active language: `{documentName}-de.pdf`, `{documentName}-en.pdf`, etc.

---

## JSON Structure

```json
{
  "pageSettings": {
    "systemLanguage": "de",
    "activeLanguages": ["de", "en", "ar"],
    "targetLanguage": "de",
    "localizedProperties": [
      {
        "key": "SUBJECT",
        "scope": "global",
        "localizedValues": { "de": "Betreff", "en": "Subject", "ar": "موضوع" }
      },
      {
        "key": "LEGAL_NOTE",
        "scope": "own",
        "ownerLanguage": "de",
        "localizedValues": { "de": "Nur für deutsche Empfänger." }
      }
    ]
  }
}
```
