# PVADE Documentation

This folder holds the **generated C# API reference** (DocFX), schemas and
hand-written PVAD integration guides. PVADE is the platform brand; PVAD is the
technical identity, with PVADP and PVADS for PDF and Spreadsheet products.

The public documentation application is `websites/PVAD.Documentation`. Its
Editor and Code SDK navigation publishes the maintained guides and examples.
The Designer's Elements Reference is driven by
`pvad-designer/src/docs/elementCatalog.ts`; its tool organization comes from
`pvad-designer/src/data/elementRegistry.ts`. This directory's DocFX output is a
separate generated API reference, not the public application's source tree.
Operator mail guidance is not part of this public navigation or build; protected
administration guidance is served through the Admin documentation boundary.

## Build the C# API reference (DocFX)

XML doc comments are emitted automatically (`GenerateDocumentationFile` is enabled on the active
`PVAD.Pdf` and `PVAD.Core` projects). To produce the HTML reference:

```bash
# one-time: install the DocFX global tool
dotnet tool install -g docfx

# from the repo root
dotnet build src/Generation/PVAD.Pdf/PVAD.Pdf.csproj
docfx metadata docs/docfx.json   # reads the csprojs + XML → docs/api/*.yml
docfx build    docs/docfx.json   # → docs/_site (open docs/_site/index.html)

# or live-preview
docfx docs/docfx.json --serve    # http://localhost:8080
```

`docs/api/*.yml` and `docs/_site/` are generated build artifacts (git-ignored).

## Example Hosts

Example links use the selected PVADE domain `pvade.com` and proposed subdomains
such as `docs.pvade.com`. These illustrate target URLs, not verified live
deployments. Substitute your deployed host when running an example. Runtime
host defaults remain separate; see [hosting strategy](../websites/HOSTING.md).

## Contents

- `index.md` — API reference landing page.
- `csharp-cookbook.md` — task-oriented C# recipes.
- `docfx.json`, `toc.yml`, `api/index.md` — DocFX configuration and content.
