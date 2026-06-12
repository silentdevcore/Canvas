# Fonts Directory

This directory contains TrueType/OpenType font files used by the PDF export engine for multi-language text rendering.

## Required Noto Font Files

Download the following fonts from [fonts.google.com/noto](https://fonts.google.com/noto) and place them here:

| File | Script | Languages |
|---|---|---|
| `NotoSans-Regular.ttf` | Latin / Greek / Cyrillic | Default fallback, `en`, `de`, `fr`, `es`, `el`, `ru`, `uk`, `bg` |
| `NotoSansArabic-Regular.ttf` | Arabic | `ar`, `ur`, `fa` |
| `NotoSansHebrew-Regular.ttf` | Hebrew | `he`, `yi` |
| `NotoSansSC-Regular.otf` | Chinese Simplified | `zh`, `zh-CN`, `zh-TW` |
| `NotoSansJP-Regular.otf` | Japanese | `ja` |
| `NotoSansKR-Regular.otf` | Korean | `ko` |
| `NotoSansDevanagari-Regular.ttf` | Devanagari | `hi`, `mr`, `ne` |
| `NotoSansThai-Regular.ttf` | Thai | `th` |

## Graceful Degradation

Font files are optional. If a file is missing, the PDF engine falls back silently to the built-in Type1 fonts (Helvetica/Times/Courier). Non-Latin characters will not render correctly in that case, but no exception is thrown and the PDF is still valid.

## Configuration

The fonts directory path is configured in `Canvas.WebApi/appsettings.json`:

```json
"Pdf": {
  "FontsDirectory": "fonts"
}
```

Use an absolute path or a path relative to the running assembly. The default is the `fonts/` folder next to the executable.

## Known Limitations

- **Arabic cursive shaping** is not implemented — text is rendered as individual glyphs in visual order without cursive joining. Full shaping requires HarfBuzz.NET (future enhancement).
- **Font subsetting** is not implemented — the full font file is embedded in each PDF. Large CJK fonts (NotoSansSC, NotoSansJP, NotoSansKR) are 5–15 MB each and will increase PDF file size accordingly.
- **Vertical CJK text** is not supported.
