# Native Project Reference Fixtures

These synthetic files contain no customer data, credentials, tenant identifiers, or
external resources:

- `minimal.pxap`: one editable PDF page with one text element.
- `minimal.pxas`: one worksheet with text, numeric, and formula cells.
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
