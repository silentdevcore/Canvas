# Native Project Reference Fixtures

These synthetic files contain no customer data, credentials, tenant identifiers, or
external resources:

- `minimal.pxap`: one editable PDF page with one text element.
- `minimal.pxas`: one worksheet with text, numeric, and formula cells.

Regenerate both deterministic packages and `SHA256SUMS` from the repository root:

```bash
dotnet run --project tools/native-project-fixtures/PVAD.NativeProjectFixtures.csproj
```

The fixtures are compatibility examples, not exported PDF or XLSX documents. Tests open
them with the bounded project reader and compare their bytes with fresh canonical output.
