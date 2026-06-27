# canvas-mcp

A [Model Context Protocol](https://modelcontextprotocol.io) server that lets AI agents generate and verify
Canvas documents. It exposes the **element catalog** (the single source of truth,
`ui-designer-v2/src/docs/elementCatalog.ts`), the **design JSON Schema**, the docs, and **validate/render**
tools — so an agent can query exact element properties and check its output instead of guessing.

## Tools

| Tool | Description |
| --- | --- |
| `list_elements` | All element types (optionally by category) with description + format support. |
| `get_element_schema` | Full docs for one element: properties, allowed values, defaults, example. |
| `get_example` | A ready-to-use example — `surface: "json"` (a `DesignExportDto`) or `"csharp"` (Canvas.Pdf). |
| `search_docs` | Full-text search across `llms-full.txt` and the C# cookbook. |
| `validate_design` | Validate a `DesignExportDto` JSON string against the design schema. |
| `render_preview` | Validate then render the design to PDF via the backend; writes a temp file. |

## Resources

`canvas://schema/design-export`, `canvas://openapi`, `canvas://docs/llms-full`, `canvas://docs/cookbook`.

## Run

```bash
cd tools/Canvas.Mcp
npm install
# the render_preview tool needs the Canvas backend (default http://localhost:5086)
CANVAS_API_URL=http://localhost:5086 npm start     # stdio transport

npx tsx smoke.ts        # smoke test (spawns the server, exercises the tools)
```

## Use from Claude Desktop / Claude Code

Add to your MCP config (e.g. Claude Desktop `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "canvas": {
      "command": "npx",
      "args": ["tsx", "src/index.ts"],
      "cwd": "/absolute/path/to/Canvas/tools/Canvas.Mcp",
      "env": { "CANVAS_API_URL": "http://localhost:5086" }
    }
  }
}
```

The element catalog is imported directly (run via `tsx`, which erases the type-only `ElementType` import),
so the server never drifts from the designer.
