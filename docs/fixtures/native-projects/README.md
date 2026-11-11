# Native Project Reference Fixtures

> **Hard cut (2026-11-09):** Former-name aliases, legacy readers, dual writers and
> transition overlays described in this document were retired. Current builds read
> and write PVAD names only; retained evidence and external bindings are listed in the
> [legacy name retirement](../../../checklists/PVAD.Legacy-Name-Retirement.md) checklist. Statements below that describe
> compatibility behavior are kept as dated history.

These synthetic files contain no customer data, credentials, tenant identifiers, or
external resources:

- [compatibility entry `compat-bab92faade31`](../../reference/compatibility-identifiers.v1.json): one editable PDF page with one text element.
- [compatibility entry `compat-26397be11435`](../../reference/compatibility-identifiers.v1.json): one worksheet with text, numeric, and formula cells.
- `minimal.pvadp` and `minimal.pvads`: the corresponding canonical PVAD envelopes.

Regenerate both deterministic packages and `SHA256SUMS` from the repository root:

```bash
dotnet run --project tools/native-project-fixtures/PVAD.NativeProjectFixtures.csproj
```

The fixtures are compatibility examples, not exported PDF or XLSX documents. Tests open
them with the bounded project reader, verify exact canonical entry contents, metadata
and ordering, and check repeat-write byte determinism on the current runtime. Compressed
bytes may differ between .NET servicing releases without changing the contents. Do not
regenerate these original examples simply to update compression or make a test pass.
