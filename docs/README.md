# Canvas documentation

This folder holds the **generated C# API reference** (DocFX) and hand-written guides. The visual-designer
element docs live in the app (the **Elements Reference** at `/docs`), driven by
`ui-designer-v2/src/docs/elementCatalog.ts`.

## Build the C# API reference (DocFX)

XML doc comments are emitted automatically (`GenerateDocumentationFile` is enabled on
`Canvas.Infrastructure.Pdf` and `Canvas.Core`). To produce the HTML reference:

```bash
# one-time: install the DocFX global tool
dotnet tool install -g docfx

# from the repo root
dotnet build src/Canvas.Infrastructure.Pdf/Canvas.Infrastructure.Pdf.csproj
docfx metadata docs/docfx.json   # reads the csprojs + XML → docs/api/*.yml
docfx build    docs/docfx.json   # → docs/_site (open docs/_site/index.html)

# or live-preview
docfx docs/docfx.json --serve    # http://localhost:8080
```

`docs/api/*.yml` and `docs/_site/` are generated build artifacts (git-ignored).

## Contents

- `index.md` — API reference landing page.
- `csharp-cookbook.md` — task-oriented C# recipes.
- `docfx.json`, `toc.yml`, `api/index.md` — DocFX configuration and content.
