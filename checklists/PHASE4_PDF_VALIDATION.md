# Phase 4 — PDF Infrastructure Isolation Validation

## Scope completed
- Moved PDF engine source compilation ownership (`PXA/Pdf/**`) into `src/Infrastructure/PVAD.Infrastructure.Pdf`.
- Removed unnecessary application-layer dependency from `PVAD.Infrastructure.Pdf`.
- Kept legacy `PXA` as compatibility/demo shell by referencing `PVAD.Infrastructure.Pdf`.

## Dependency isolation status
- `PVAD.Application` depends only on `PVAD.Core`.
- `PVAD.Infrastructure.Pdf` depends on `PVAD.Core` and now owns PDF implementation compilation.
- `PVADE` references `PVAD.Core` + `PVAD.Infrastructure.Pdf` and no longer compiles `Pdf/**` directly.

## Runtime validation performed
Commands executed successfully:
- `dotnet build`
- `dotnet run --project PXA/PXA.csproj`
- `dotnet run --project samples/PVAD.Demo/PVAD.Demo.csproj`

Observed in `PVADE` run diagnostics output:
- Links detected across pages (web links, page links, named destination links).
- TOC pages detected.
- Bookmarks/outlines and named destinations detected.
- Rotation, shapes, and transparency-related content paths exercised.
- Page box behavior exercised (crop box and page size/orientation diagnostics).

## Byte-compatibility note (documented deltas)
Byte-for-byte equality across runs is not guaranteed by default when dynamic metadata is used (for example `CreationDate`/`ModificationDate` set to `UtcNow` in demo flow).

For stable byte comparison, use fixed metadata/timestamps and stable input ordering before hashing generated output.

This satisfies: "Keep PDF output byte-compatible where feasible (or documented deltas)" by documenting known non-deterministic factors and the deterministic comparison approach.
