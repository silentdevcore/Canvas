# PVAD Schema And Client Contracts

## Canonical Schemas

Schema identities are stable identifiers, not a promise that the corresponding
public URL is already hosted. The offline mapping is
[schema-aliases.json](schema/schema-aliases.json). MCP packages bundle every schema
required by this registry, including both native envelope generations.

| Contract | Local schema |
| --- | --- |
| Designer document | [design-export.schema.json](schema/design-export.schema.json) |
| PVADS workbook | [pvad-workbook.schema.json](schema/pvad-workbook.schema.json) |
| Current native writer manifest | [pvad-project-manifest.schema.json](schema/pvad-project-manifest.schema.json) |
| Current and legacy native reader manifest | [pvad-project-manifest-reader.schema.json](schema/pvad-project-manifest-reader.schema.json) |
| Inert code-workspace component | [pvad-code-workspace.schema.json](schema/pvad-code-workspace.schema.json) |

The reader schema accepts four coherent identity tuples: PVADP, PVADS, legacy
PXAP and legacy PXAS. It rejects mixed magic/format/media identities. The historical
PXA manifest schema is retained separately, not redirected to a schema that rejects
its generation. Workbook and code-workspace aliases retain equivalent constraints.
Existing legacy archive fixtures and their hashes remain unchanged.

## Validation Boundaries

Schema checks are structural tooling checks, not archive acceptance or security
validation. The native reader additionally checks ZIP entry rules, bounded sizes,
checksums, supported versions/features, duplicate/conflicting version fields,
timestamp ordering and document content. Do not replace native import validation
with an AJV result. The current MCP AJV setup does not assert optional format
keywords such as date-time, URI or UUID; its warnings are not conformance evidence.
No remote schema fetch is needed by the bundled registry.

## Native API Downloads

The generated [OpenAPI snapshot](schema/openapi.json) describes:

- `POST /api/pvad/v1/designer/projects/pdf/export`
- `POST /api/pvad/v1/designer/projects/pdf/export-with-workspace`
- `POST /api/pvad/v1/designer/projects/spreadsheet/export`

Success responses are binary streams with `application/vnd.pvad.pvadp+zip` or
`application/vnd.pvad.pvads+zip`, plus Content-Disposition filenames. They are not
JSON or base64 responses. Multipart open/inspect/validate inputs and operation IDs
remain unchanged. Response schema corrections do not modify archive bytes, auth,
permissions, runtime limits or error responses.

## Implemented And Planned Clients

The shipped repository clients are Designer browser helpers and the MCP internal
API client. MCP migration discovery now calls `/api/pvad/migration/frameworks`,
retaining the existing array response. This is the canonical branded alias of the
compatibility discovery contract, not a switch to the different v1 catalog shape.
The neutral `/api/export/formats` path remains unchanged.

There are no implemented Java, Python, public TypeScript/JavaScript, C# REST, Go,
PHP or PowerShell SDK packages in this repository. Their roadmaps remain planning
documents, beginning with [the SDK roadmap](../checklists/PVAD.SDK-Roadmap.md).
Future clients must consume the canonical OpenAPI and schema registry; they are
not claimed as generated, renamed, tested or published by this rename stage.

## Verification

Registry tests exercise valid manifests for all four native identities and reject
mixed identities, unknown formats and invalid hashes. A distribution smoke test
starts the built MCP executable and resolves all packaged canonical/legacy schema
IDs. API tests verify binary response schemas and unchanged multipart contracts.
Archive tests remain responsible for real native reader/writer behavior.

Generated source, worker assembly identities and execution compatibility are covered
in [code generation compatibility](pvad-code-generation-compatibility.md).
