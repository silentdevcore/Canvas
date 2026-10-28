# pxa-mcp

A [Model Context Protocol](https://modelcontextprotocol.io) server that lets AI agents generate and verify
Power Dox Automation / PXA documents. It exposes the **element catalog** (the single source of truth,
`pxa-designer/src/docs/elementCatalog.ts`), the **design JSON Schema**, the docs, and **validate/render**
tools — so an agent can query exact element properties and check its output instead of guessing.

The server exposes PXA resources through the `pxa://...` scheme.

## Tools

| Tool | Description |
| --- | --- |
| `list_elements` | All element types (optionally by category) with description + format support. |
| `get_element_schema` | Full docs for one element: properties, allowed values, defaults, example. |
| `get_example` | A ready-to-use example — `surface: "json"` (a `DesignExportDto`) or `"csharp"` (PXA-compatible PDF C#). |
| `search_docs` | Full-text search across `llms-full.txt` and the C# cookbook. |
| `validate_design` | Validate a `DesignExportDto` JSON string against the design schema. |
| `render_preview` | Validate then render the design to PDF via the backend; writes a temp file. |

## Resources

Primary resources:

`pxa://schema/design-export`, `pxa://schema/pxa-workbook`, `pxa://openapi`, `pxa://docs/llms-full`, `pxa://docs/cookbook`.

Use the `pxa://` scheme for all resources.

## Run

The validated runtime baseline is Node.js 22. Node declarations track that
major so compilation does not assume newer Node APIs than CI exercises. This
does not upgrade the runtime or change the upstream SDK's minimum requirement.
Use Node 22 for the compatibility check; passing on a newer runtime alone does
not establish compatibility with the baseline.

```bash
cd tools/PXA.Mcp
npm ci
npm run test:contracts
npm run typecheck
npm run test:smoke
# the render_preview tool needs the Power Dox Automation backend (default http://localhost:5086)
PXA_API_URL=http://localhost:5086 npm start     # stdio transport
```

The smoke test starts the server with the current Node executable and the
locally installed, lockfile-pinned `tsx` import hook. It does not download a
runner through `npx`. It tests the actual stdio protocol, schema defaults,
validation failures and resource discovery; it does not claim a backend PDF
rendering or authenticated deployment test.

### Compiler Maintenance

Type checking uses the lockfile-pinned TypeScript 7 native CLI. `tsx` still
executes the server; no compiler API, emitted application JavaScript or runtime
dependency changes. The existing strict, ES2022, ESNext/Bundler configuration
and explicit Node types remain unchanged. Do not apply this compiler upgrade
automatically to the Designer, where Jest/compiler-API consumers need separate
compatibility work.

`npm ci` must include optional dependencies so the matching native compiler is
installed. Contracts check all locked compiler-platform packages, the installed
platform package and actual CLI version. They compile valid Node/type-narrowing
code and require diagnostics for incorrect types, nulls, implicit `any` and an
unknown Node export. A nonzero diagnostic exit is required; old and new compilers
do not necessarily use the same numeric exit code.

Validation covers macOS ARM64 locally and Linux x64 in CI. Other native packages
are retained in the lockfile but are not claimed runtime-tested. Dependabot
continues compatible Node-type updates; major declarations are deferred until
the runtime baseline itself is reviewed and upgraded.

## Use from Claude Desktop / Claude Code

Add to your MCP config (e.g. Claude Desktop `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "pxa": {
      "command": "npx",
      "args": ["tsx", "src/index.ts"],
      "cwd": "/absolute/path/to/PXA/tools/PXA.Mcp",
      "env": { "PXA_API_URL": "http://localhost:5086" }
    }
  }
}
```

The element catalog is imported directly (run via `tsx`, which erases the type-only `ElementType` import),
so the server never drifts from the designer.
